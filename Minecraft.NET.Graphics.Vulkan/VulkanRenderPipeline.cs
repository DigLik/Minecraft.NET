using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Graphics.Vulkan.Core;
using Minecraft.NET.Utils.Collections;
using Minecraft.NET.Utils.Math;

using Silk.NET.Vulkan;
using Streamline;

using Semaphore = Silk.NET.Vulkan.Semaphore;
using Result = Silk.NET.Vulkan.Result;
using SlResult = Streamline.Result;
using SlBoolean = Streamline.Boolean;

namespace Minecraft.NET.Graphics.Vulkan;

public unsafe class VulkanRenderPipeline : IRenderPipeline
{
    private struct DrawCall
    {
        public IMesh Mesh;
        public Vector3 Position;
    }

    public struct InstanceData
    {
        public uint VertexOffset;
        public uint IndexOffset;
        public uint OpaqueIndexCount;
        public uint Pad2;
        public ulong VertexAddress;
        public ulong IndexAddress;
    }

    private const int MaxFramesInFlight = 3;
    private int _currentFrame = 0;

    private readonly VulkanDevice _device;
    private VulkanSwapchain _swapchain;
    private VulkanRayTracingPipeline? _pipeline;

    private CommandPool _commandPool;
    private readonly CommandBuffer[] _commandBuffers = new CommandBuffer[MaxFramesInFlight];

    private readonly Semaphore[] _imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
    private readonly Semaphore[] _renderFinishedSemaphores = new Semaphore[8];
    private readonly Fence[] _inFlightFences = new Fence[MaxFramesInFlight];

    private readonly VulkanBuffer[] _cameraBuffers = new VulkanBuffer[MaxFramesInFlight];
    private DescriptorPool _descriptorPool;
    private readonly DescriptorSet[] _descriptorSets = new DescriptorSet[MaxFramesInFlight];

    private Vector2Int _framebufferSize;
    private bool _framebufferResized = false;

    // Output target image (high-res)
    private Image _storageImage;
    private DeviceMemory _storageImageMemory;
    private ImageView _storageImageView;

    // DLSS / G-buffer resources
    private bool _useDLSS_RR = false;
    private bool _useReflex = false;
    private bool _useLatewarp = false;
    private ViewportHandle _slViewport;
    private Vector2Int _renderSize;
    private uint _slFrameIndex = 0;
    private float _currentJitterX = 0f;
    private float _currentJitterY = 0f;

    private FrameToken* _currentFrameToken = null;
    private FrameToken* _prevFrameToken = null;
    private Matrix4x4 _prevWorldToView = Matrix4x4.Identity;
    private Matrix4x4 _prevViewToClip = Matrix4x4.Identity;
    private Matrix4x4 _currentPredictedView = Matrix4x4.Identity;
    private Matrix4x4 _currentPredictedProj = Matrix4x4.Identity;
    private bool _hasPredictedCamera = false;

    private Image _renderStorageImage;
    private DeviceMemory _renderStorageImageMemory;
    private ImageView _renderStorageImageView;

    private Image _renderAccumImage;
    private DeviceMemory _renderAccumImageMemory;
    private ImageView _renderAccumImageView;

    private Image _noisyColorImage;
    private DeviceMemory _noisyColorImageMemory;
    private ImageView _noisyColorImageView;

    private Image _normalRoughnessImage;
    private DeviceMemory _normalRoughnessImageMemory;
    private ImageView _normalRoughnessImageView;

    private Image _albedoImage;
    private DeviceMemory _albedoImageMemory;
    private ImageView _albedoImageView;

    private Image _specularAlbedoImage;
    private DeviceMemory _specularAlbedoImageMemory;
    private ImageView _specularAlbedoImageView;

    private Image _motionVectorsImage;
    private DeviceMemory _motionVectorsImageMemory;
    private ImageView _motionVectorsImageView;

    private Image _depthImage;
    private DeviceMemory _depthImageMemory;
    private ImageView _depthImageView;

    private Image _specularMotionVectorsImage;
    private DeviceMemory _specularMotionVectorsImageMemory;
    private ImageView _specularMotionVectorsImageView;

    private VulkanBuffer? _materialBuffer;
    private Matrix4x4 _lastViewProj;
    private Vector3 _lastLocalPos;
    private uint _frameCount = 1;
    private uint _seed = 0;

    private DrawCall[] _drawCalls = new DrawCall[32768];
    private int _drawCallCount = 0;
    private ITextureArray? _currentTextureArray;

    private DynamicMeshPool _meshPool = null!;
    private readonly ConcurrentQueue<IMesh> _pendingMeshesToDispose = new();
    private readonly List<IMesh>[] _meshesToDispose = new List<IMesh>[MaxFramesInFlight];

    private readonly VulkanBuffer[] _tlasBuffers = new VulkanBuffer[MaxFramesInFlight];
    private readonly AccelerationStructureKHR[] _tlasHandles = new AccelerationStructureKHR[MaxFramesInFlight];
    private readonly VulkanBuffer[] _instancesBuffers = new VulkanBuffer[MaxFramesInFlight];
    private readonly VulkanBuffer[] _instanceDataBuffers = new VulkanBuffer[MaxFramesInFlight];
    private readonly VulkanBuffer[] _tlasScratchBuffers = new VulkanBuffer[MaxFramesInFlight];

    private readonly int[] _tlasCapacities = new int[MaxFramesInFlight];
    private readonly int[] _tlasInstanceCounts = new int[MaxFramesInFlight];
    private readonly ulong[] _tlasScratchCapacities = new ulong[MaxFramesInFlight];
    private readonly bool[] _tlasNeedsRebuild = new bool[MaxFramesInFlight];

    public VulkanRenderPipeline(IWindow window)
    {

        // 2. Now create Vulkan device and swapchain
        _framebufferSize = window.FramebufferSize;
        _device = new VulkanDevice(window.Handle);
        _swapchain = new VulkanSwapchain(_device, _framebufferSize);

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _meshesToDispose[i] = [];
            _tlasInstanceCounts[i] = -1;
            _tlasNeedsRebuild[i] = true;
        }

        CreateCommandPool();
        CreateCommandBuffers();
        CreateSyncObjects();

        // 3. Set Vulkan Info and query feature support (since Device is now created)
        try
        {
            var vkInfo = VulkanInfo.Create();
            vkInfo.Device = (void*)_device.Device.Handle;
            vkInfo.Instance = (void*)_device.Instance.Handle;
            vkInfo.PhysicalDevice = (void*)_device.PhysicalDevice.Handle;
            vkInfo.ComputeQueueIndex = 0;
            vkInfo.ComputeQueueFamily = _device.GraphicsFamilyIndex;
            vkInfo.GraphicsQueueIndex = 0;
            vkInfo.GraphicsQueueFamily = _device.GraphicsFamilyIndex;
            vkInfo.OpticalFlowQueueIndex = 0;
            vkInfo.OpticalFlowQueueFamily = _device.GraphicsFamilyIndex;
            vkInfo.UseNativeOpticalFlowMode = 0;

            int setVkRes = StreamlineAPI.slSetVulkanInfo(&vkInfo);
            if (setVkRes == (int)SlResult.eOk)
            {
                var adapterInfo = new AdapterInfo((void*)_device.PhysicalDevice.Handle);
                
                // Check DLSS RR
                int supRes = StreamlineAPI.slIsFeatureSupported((uint)Feature.kFeatureDLSS_RR, &adapterInfo);
                if (supRes == (int)SlResult.eOk)
                {
                    _useDLSS_RR = true;
                    StreamlineAPI.LoadDLSSDFunctions();

                    int supDlss = StreamlineAPI.slIsFeatureSupported((uint)Feature.kFeatureDLSS, &adapterInfo);
                    if (supDlss == (int)SlResult.eOk)
                    {
                        StreamlineAPI.LoadDLSSFunctions();
                    }

                    _slViewport = new ViewportHandle(1);

                    // Set standard DLSS options first
                    if (StreamlineAPI.slDLSSSetOptions != null)
                    {
                        var dlssOptions = DLSSOptions.Create();
                        dlssOptions.Mode = DLSSMode.eMaxQuality;
                        dlssOptions.QualityPreset = DLSSPreset.ePresetK; // Preset K requested by user
                        dlssOptions.OutputWidth = (uint)_framebufferSize.X;
                        dlssOptions.OutputHeight = (uint)_framebufferSize.Y;
                        dlssOptions.ColorBuffersHDR = SlBoolean.eTrue;
                        var vp = _slViewport;
                        StreamlineAPI.slDLSSSetOptions(&vp, &dlssOptions);
                    }

                    var dlssdOptions = DLSSDOptions.Create();
                    dlssdOptions.Mode = DLSSMode.eMaxQuality; // Changed to eMaxQuality
                    dlssdOptions.QualityPreset = DLSSDPreset.ePresetE; // Latest transformer model for RR (avoids crash)
                    dlssdOptions.OutputWidth = (uint)_framebufferSize.X;
                    dlssdOptions.OutputHeight = (uint)_framebufferSize.Y;
                    dlssdOptions.ColorBuffersHDR = SlBoolean.eTrue;
                    
                    var dlssdSettings = DLSSDOptimalSettings.Create();
                    if (StreamlineAPI.slDLSSDGetOptimalSettings(&dlssdOptions, &dlssdSettings) == (int)SlResult.eOk)
                    {
                        _renderSize = new Vector2Int((int)dlssdSettings.OptimalRenderWidth, (int)dlssdSettings.OptimalRenderHeight);
                        Console.WriteLine($"[Streamline] DLSS RR (Quality) initialized successfully. Output: {_framebufferSize.X}x{_framebufferSize.Y}, Render size: {_renderSize.X}x{_renderSize.Y}");
                    }
                }
                else
                {
                    Console.WriteLine($"[Streamline] DLSS RR is not supported on this GPU: {(SlResult)supRes}");
                }

                // Check Reflex
                int supReflex = StreamlineAPI.slIsFeatureSupported((uint)Feature.kFeatureReflex, &adapterInfo);
                if (supReflex == (int)SlResult.eOk)
                {
                    _useReflex = true;
                    StreamlineAPI.LoadReflexFunctions();

                    var reflexOpt = ReflexOptions.Create();
                    reflexOpt.Mode = (uint)ReflexMode.eLowLatencyWithBoost;
                    reflexOpt.UseMarkersToOptimize = 0;
                    int setReflexRes = StreamlineAPI.slReflexSetOptions(&reflexOpt);
                    Console.WriteLine($"[Streamline] Reflex 2 initialized. Mode: LowLatencyWithBoost, status: {(SlResult)setReflexRes}");
                }
                else
                {
                    Console.WriteLine($"[Streamline] Reflex is not supported: {(SlResult)supReflex}");
                }

                // Check PCL
                int supPCL = StreamlineAPI.slIsFeatureSupported((uint)Feature.kFeaturePCL, &adapterInfo);
                if (supPCL == (int)SlResult.eOk)
                {
                    StreamlineAPI.LoadPCLFunctions();
                    Console.WriteLine($"[Streamline] PCL Stats initialized successfully.");
                }
                else
                {
                    Console.WriteLine($"[Streamline] PCL Stats is not supported: {(SlResult)supPCL}");
                }

                // Check Late Warp
                int supLatewarp = StreamlineAPI.slIsFeatureSupported((uint)Feature.kFeatureLatewarp, &adapterInfo);
                if (supLatewarp == (int)SlResult.eOk)
                {
                    _useLatewarp = true;
                    Console.WriteLine($"[Streamline] Late Warp (Frame Warp) supported and initialized.");
                }
                else
                {
                    Console.WriteLine($"[Streamline] Late Warp (Frame Warp) is not supported: {(SlResult)supLatewarp}");
                }
            }
            else
            {
                Console.WriteLine($"[Streamline] Failed to set Vulkan info: {(SlResult)setVkRes}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Streamline] Error during Vulkan setup: {ex.Message}");
        }

        if (!_useDLSS_RR)
        {
            _renderSize = _framebufferSize;
        }
    }

    public void Initialize(VertexElement[] layout, uint stride)
    {
        _pipeline = new VulkanRayTracingPipeline(_device);
        CreateStorageImage();
        CreateDescriptorPoolAndSets();
        _meshPool = new DynamicMeshPool(_device);
    }

    private void CreateImageHelper(uint width, uint height, Format format, ImageUsageFlags usage, out Image image, out DeviceMemory memory, out ImageView imageView)
    {
        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo, ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D(width, height, 1),
            MipLevels = 1, ArrayLayers = 1, Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal, Usage = usage,
            InitialLayout = ImageLayout.Undefined
        };

        if (_device.Vk.CreateImage(_device.Device, in imageInfo, null, out image) != Result.Success)
            throw new Exception($"Failed to create image with format {format}!");

        _device.Vk.GetImageMemoryRequirements(_device.Device, image, out var memReqs);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memReqs.Size,
            MemoryTypeIndex = _device.FindMemoryType(memReqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };

        if (_device.Vk.AllocateMemory(_device.Device, in allocInfo, null, out memory) != Result.Success)
            throw new Exception("Failed to allocate image memory!");

        _device.Vk.BindImageMemory(_device.Device, image, memory, 0);

        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo, Image = image, ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        if (_device.Vk.CreateImageView(_device.Device, in viewInfo, null, out imageView) != Result.Success)
            throw new Exception("Failed to create image view!");
    }

    private void DestroyImageHelper(Image image, DeviceMemory memory, ImageView view)
    {
        if (view.Handle != 0) _device.Vk.DestroyImageView(_device.Device, view, null);
        if (image.Handle != 0) _device.Vk.DestroyImage(_device.Device, image, null);
        if (memory.Handle != 0) _device.Vk.FreeMemory(_device.Device, memory, null);
    }

    private void CreateStorageImage()
    {
        // 1. Create main output target image (high-res)
        CreateImageHelper((uint)_framebufferSize.X, (uint)_framebufferSize.Y, Format.R16G16B16A16Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _storageImage, out _storageImageMemory, out _storageImageView);

        // 2. Create render storage image (low-res)
        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R16G16B16A16Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _renderStorageImage, out _renderStorageImageMemory, out _renderStorageImageView);

        // 3. Create render accumulation image (low-res)
        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R32G32B32A32Sfloat, 
            ImageUsageFlags.StorageBit, 
            out _renderAccumImage, out _renderAccumImageMemory, out _renderAccumImageView);

        // 4. Create G-buffers at render resolution
        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R16G16B16A16Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _noisyColorImage, out _noisyColorImageMemory, out _noisyColorImageView);

        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R16G16B16A16Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _normalRoughnessImage, out _normalRoughnessImageMemory, out _normalRoughnessImageView);

        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R8G8B8A8Unorm, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _albedoImage, out _albedoImageMemory, out _albedoImageView);

        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R8G8B8A8Unorm, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _specularAlbedoImage, out _specularAlbedoImageMemory, out _specularAlbedoImageView);

        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R16G16Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _motionVectorsImage, out _motionVectorsImageMemory, out _motionVectorsImageView);

        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R32Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _depthImage, out _depthImageMemory, out _depthImageView);

        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R16G16Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _specularMotionVectorsImage, out _specularMotionVectorsImageMemory, out _specularMotionVectorsImageView);
    }

    public IMesh CreateMesh<T>(NativeList<T> vertices, NativeList<ushort> indices, uint opaqueIndexCount = 0) where T : unmanaged
        => _meshPool.Allocate(vertices, indices, opaqueIndexCount);

    public void DeleteMesh(IMesh mesh) => _pendingMeshesToDispose.Enqueue(mesh);

    public ITextureArray CreateTextureArray(int width, int height, byte[][] pixels) => new VulkanTextureArray(_device, width, height, pixels);
    public void BindTextureArray(ITextureArray textureArray) => _currentTextureArray = textureArray;

    public void BindMaterials(MaterialData[] materials)
    {
        ulong size = (ulong)(materials.Length * sizeof(MaterialData));
        if (_materialBuffer == null || _materialBuffer.Size < size)
        {
            _materialBuffer?.Dispose();
            _materialBuffer = new VulkanBuffer(_device, size, BufferUsageFlags.StorageBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        }
        var span = new Span<MaterialData>(_materialBuffer.MappedMemory, materials.Length);
        materials.CopyTo(span);
    }

    public void SubmitDraw(IMesh mesh, Vector3 position)
    {
        if (_drawCallCount >= _drawCalls.Length)
            Array.Resize(ref _drawCalls, _drawCalls.Length * 2);
        _drawCalls[_drawCallCount++] = new DrawCall { Mesh = mesh, Position = position };
    }

    public void ClearDraws() => _drawCallCount = 0;

    private void CreateCommandPool()
    {
        CommandPoolCreateInfo poolInfo = new() { SType = StructureType.CommandPoolCreateInfo, Flags = CommandPoolCreateFlags.ResetCommandBufferBit, QueueFamilyIndex = _device.GraphicsFamilyIndex };
        _device.Vk.CreateCommandPool(_device.Device, in poolInfo, null, out _commandPool);
    }

    private void CreateCommandBuffers()
    {
        CommandBufferAllocateInfo allocInfo = new() { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _commandPool, Level = CommandBufferLevel.Primary, CommandBufferCount = MaxFramesInFlight };
        fixed (CommandBuffer* pCmds = _commandBuffers) _device.Vk.AllocateCommandBuffers(_device.Device, in allocInfo, pCmds);
    }

    private void CreateSyncObjects()
    {
        SemaphoreCreateInfo semaphoreInfo = new() { SType = StructureType.SemaphoreCreateInfo };
        FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _device.Vk.CreateSemaphore(_device.Device, in semaphoreInfo, null, out _imageAvailableSemaphores[i]);
            _device.Vk.CreateFence(_device.Device, in fenceInfo, null, out _inFlightFences[i]);
        }

        for (int i = 0; i < 8; i++)
        {
            _device.Vk.CreateSemaphore(_device.Device, in semaphoreInfo, null, out _renderFinishedSemaphores[i]);
        }
    }

    private void CreateDescriptorPoolAndSets()
    {
        DescriptorPoolSize[] poolSizes = [
            new() { Type = DescriptorType.AccelerationStructureKhr, DescriptorCount = MaxFramesInFlight },
            new() { Type = DescriptorType.StorageImage, DescriptorCount = MaxFramesInFlight * 12 },
            new() { Type = DescriptorType.UniformBuffer, DescriptorCount = MaxFramesInFlight },
            new() { Type = DescriptorType.CombinedImageSampler, DescriptorCount = MaxFramesInFlight },
            new() { Type = DescriptorType.StorageBuffer, DescriptorCount = MaxFramesInFlight * 2 }
        ];

        fixed (DescriptorPoolSize* pPoolSizes = poolSizes)
        {
            DescriptorPoolCreateInfo poolInfo = new() { SType = StructureType.DescriptorPoolCreateInfo, PoolSizeCount = 5, PPoolSizes = pPoolSizes, MaxSets = MaxFramesInFlight };
            _device.Vk.CreateDescriptorPool(_device.Device, in poolInfo, null, out _descriptorPool);
        }

        var layouts = stackalloc DescriptorSetLayout[MaxFramesInFlight];
        for (int i = 0; i < MaxFramesInFlight; i++) layouts[i] = _pipeline!.DescriptorSetLayout;

        DescriptorSetAllocateInfo allocInfo = new() { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _descriptorPool, DescriptorSetCount = MaxFramesInFlight, PSetLayouts = layouts };
        fixed (DescriptorSet* pSets = _descriptorSets) _device.Vk.AllocateDescriptorSets(_device.Device, in allocInfo, pSets);

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _cameraBuffers[i] = new VulkanBuffer(_device, (ulong)sizeof(CameraData), BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

            DescriptorBufferInfo bufferInfo = new() { Buffer = _cameraBuffers[i].Buffer, Offset = 0, Range = (ulong)sizeof(CameraData) };
            WriteDescriptorSet descriptorWrite = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[i], DstBinding = 2, DstArrayElement = 0, DescriptorType = DescriptorType.UniformBuffer, DescriptorCount = 1, PBufferInfo = &bufferInfo };

            _device.Vk.UpdateDescriptorSets(_device.Device, 1, &descriptorWrite, 0, null);
        }
    }

    private void RecreateSwapchain()
    {
        _device.Vk.DeviceWaitIdle(_device.Device);

        DestroyImageHelper(_storageImage, _storageImageMemory, _storageImageView);
        DestroyImageHelper(_renderStorageImage, _renderStorageImageMemory, _renderStorageImageView);
        DestroyImageHelper(_renderAccumImage, _renderAccumImageMemory, _renderAccumImageView);
        DestroyImageHelper(_noisyColorImage, _noisyColorImageMemory, _noisyColorImageView);
        DestroyImageHelper(_normalRoughnessImage, _normalRoughnessImageMemory, _normalRoughnessImageView);
        DestroyImageHelper(_albedoImage, _albedoImageMemory, _albedoImageView);
        DestroyImageHelper(_specularAlbedoImage, _specularAlbedoImageMemory, _specularAlbedoImageView);
        DestroyImageHelper(_motionVectorsImage, _motionVectorsImageMemory, _motionVectorsImageView);
        DestroyImageHelper(_depthImage, _depthImageMemory, _depthImageView);
        DestroyImageHelper(_specularMotionVectorsImage, _specularMotionVectorsImageMemory, _specularMotionVectorsImageView);

        _swapchain.Dispose();
        _swapchain = new VulkanSwapchain(_device, _framebufferSize);

        if (_useDLSS_RR)
        {
            if (StreamlineAPI.slDLSSSetOptions != null)
            {
                var dlssOptions = DLSSOptions.Create();
                dlssOptions.Mode = DLSSMode.eMaxQuality;
                dlssOptions.QualityPreset = DLSSPreset.ePresetK;
                dlssOptions.OutputWidth = (uint)_framebufferSize.X;
                dlssOptions.OutputHeight = (uint)_framebufferSize.Y;
                dlssOptions.ColorBuffersHDR = SlBoolean.eTrue;
                var vp = _slViewport;
                StreamlineAPI.slDLSSSetOptions(&vp, &dlssOptions);
            }

            var dlssdOptions = DLSSDOptions.Create();
            dlssdOptions.Mode = DLSSMode.eMaxQuality; // Changed to eMaxQuality
            dlssdOptions.QualityPreset = DLSSDPreset.ePresetE; // Latest transformer model for RR (avoids crash)
            dlssdOptions.OutputWidth = (uint)_framebufferSize.X;
            dlssdOptions.OutputHeight = (uint)_framebufferSize.Y;
            dlssdOptions.ColorBuffersHDR = SlBoolean.eTrue;
            
            var dlssdSettings = DLSSDOptimalSettings.Create();
            if (StreamlineAPI.slDLSSDGetOptimalSettings(&dlssdOptions, &dlssdSettings) == (int)SlResult.eOk)
            {
                _renderSize = new Vector2Int((int)dlssdSettings.OptimalRenderWidth, (int)dlssdSettings.OptimalRenderHeight);
            }
        }
        else
        {
            _renderSize = _framebufferSize;
        }

        CreateStorageImage();
    }

    public void RenderFrame(CameraData cameraData)
    {
        if (_pipeline == null) throw new Exception("Pipeline is not initialized.");

        if (cameraData.ViewProjection != _lastViewProj || cameraData.LocalPosition != _lastLocalPos)
        {
            _frameCount = 1;
            _lastViewProj = cameraData.ViewProjection;
            _lastLocalPos = cameraData.LocalPosition;
        }
        else
        {
            _frameCount++;
        }

        _seed = unchecked(_seed + 1664525 * _frameCount + 1013904223);
        cameraData.FrameCount = _frameCount;
        cameraData.Seed = _seed;

        UpdateJitter();
        cameraData.JitterX = _currentJitterX;
        cameraData.JitterY = _currentJitterY;

        _device.Vk.WaitForFences(_device.Device, 1, ref _inFlightFences[_currentFrame], Vk.True, ulong.MaxValue);

        foreach (var mesh in _meshesToDispose[_currentFrame])
        {
            var alloc = (MeshAllocation)mesh;
            _device.KhrAccelerationStructure.DestroyAccelerationStructure(_device.Device, alloc.Blas, null);
            _meshPool.Free(alloc);
            mesh.Dispose();
        }
        _meshesToDispose[_currentFrame].Clear();

        int pendingDisposeCount = _pendingMeshesToDispose.Count;
        for (int i = 0; i < pendingDisposeCount; i++)
        {
            if (_pendingMeshesToDispose.TryDequeue(out var mesh))
            {
                if (mesh.IsReady) _meshesToDispose[_currentFrame].Add(mesh);
                else _pendingMeshesToDispose.Enqueue(mesh);
            }
        }

        if (_framebufferSize.X == 0 || _framebufferSize.Y == 0)
        {
            _currentFrameToken = null;
            _prevFrameToken = null;
            return;
        }

        if (_framebufferResized)
        {
            _framebufferResized = false;
            RecreateSwapchain();
            return;
        }

        uint imageIndex;
        var result = _device.KhrSwapchain.AcquireNextImage(_device.Device, _swapchain.Swapchain, ulong.MaxValue, _imageAvailableSemaphores[_currentFrame], default, &imageIndex);

        if (result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            return;
        }

        if (_currentFrameToken == null)
        {
            if (_useDLSS_RR || _useReflex)
            {
                FrameToken* frameToken = null;
                _slFrameIndex++;
                uint localFrameIndex = _slFrameIndex;
                StreamlineAPI.slGetNewFrameToken(&frameToken, &localFrameIndex);
                _slFrameIndex = localFrameIndex;
                _currentFrameToken = frameToken;

                if (StreamlineAPI.slPCLSetMarker != null)
                {
                    StreamlineAPI.slPCLSetMarker(PCLMarker.eSimulationStart, _currentFrameToken);
                    StreamlineAPI.slPCLSetMarker(PCLMarker.eSimulationEnd, _currentFrameToken);
                }
            }
        }

        var originalView = Matrix4x4.CreateLookAt(cameraData.LocalPosition, cameraData.LocalPosition + cameraData.CameraFwd, cameraData.CameraUp);

        float aspect = _framebufferSize.X / (float)Math.Max(1, _framebufferSize.Y);
        var originalProj = Matrix4x4.CreatePerspectiveFieldOfView(float.Pi / 2.5f, aspect, 0.1f, 3000f);
        originalProj.M22 *= -1;

        Matrix4x4 view, proj, viewInverse;
        if (_useReflex && _hasPredictedCamera)
        {
            view = _currentPredictedView;
            proj = _currentPredictedProj;
            Matrix4x4.Invert(view, out viewInverse);
        }
        else
        {
            view = originalView;
            proj = originalProj;
            Matrix4x4.Invert(view, out viewInverse);
        }

        if (_useReflex && _currentFrameToken != null)
        {
            var viewport = _slViewport;
            var reflexCam = ReflexCameraData.Create();
            reflexCam.WorldToViewMatrix = originalView;
            reflexCam.ViewToClipMatrix = originalProj;
            reflexCam.PrevRenderedWorldToViewMatrix = _prevWorldToView;
            reflexCam.PrevRenderedViewToClipMatrix = _prevViewToClip;

            // Save for next frame
            _prevWorldToView = originalView;
            _prevViewToClip = originalProj;

            int setCamRes = StreamlineAPI.slReflexSetCameraData(&viewport, _currentFrameToken, &reflexCam);
            if (setCamRes != (int)SlResult.eOk)
            {
                Console.WriteLine($"[Streamline] slReflexSetCameraData failed: {(SlResult)setCamRes}");
            }
        }

        _cameraBuffers[_currentFrame].UpdateData(in cameraData);
        _device.Vk.ResetFences(_device.Device, 1, ref _inFlightFences[_currentFrame]);

        CommandBuffer cmd = _commandBuffers[_currentFrame];
        _device.Vk.ResetCommandBuffer(cmd, 0);

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        _device.Vk.BeginCommandBuffer(cmd, in beginInfo);

        if (StreamlineAPI.slPCLSetMarker != null && _currentFrameToken != null)
        {
            StreamlineAPI.slPCLSetMarker(PCLMarker.eRenderSubmitStart, _currentFrameToken);
        }

        if (_drawCallCount > 0)
        {
            bool needsRebuild = _tlasNeedsRebuild[_currentFrame] || _drawCallCount != _tlasInstanceCounts[_currentFrame];
            int requiredCapacity = Math.Max(128, _drawCallCount);

            if (_tlasCapacities[_currentFrame] < requiredCapacity)
            {
                requiredCapacity = Math.Max(_tlasCapacities[_currentFrame] * 2, requiredCapacity);

                _instancesBuffers[_currentFrame]?.Dispose();
                _instanceDataBuffers[_currentFrame]?.Dispose();

                _instancesBuffers[_currentFrame] = new VulkanBuffer(_device, (ulong)(requiredCapacity * sizeof(AccelerationStructureInstanceKHR)), BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
                _instanceDataBuffers[_currentFrame] = new VulkanBuffer(_device, (ulong)(requiredCapacity * sizeof(InstanceData)), BufferUsageFlags.StorageBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

                _tlasCapacities[_currentFrame] = requiredCapacity;
                needsRebuild = true;
            }

            var instSpan = new Span<AccelerationStructureInstanceKHR>(_instancesBuffers[_currentFrame].MappedMemory, _drawCallCount);
            var dataSpan = new Span<InstanceData>(_instanceDataBuffers[_currentFrame].MappedMemory, _drawCallCount);

            for (int i = 0; i < _drawCallCount; i++)
            {
                var alloc = (MeshAllocation)_drawCalls[i].Mesh;
                var pos = _drawCalls[i].Position;

                var tf = new TransformMatrixKHR();
                tf.Matrix[0] = 1; tf.Matrix[1] = 0; tf.Matrix[2] = 0; tf.Matrix[3] = pos.X;
                tf.Matrix[4] = 0; tf.Matrix[5] = 1; tf.Matrix[6] = 0; tf.Matrix[7] = pos.Y;
                tf.Matrix[8] = 0; tf.Matrix[9] = 0; tf.Matrix[10] = 1; tf.Matrix[11] = pos.Z;

                instSpan[i] = new AccelerationStructureInstanceKHR
                {
                    Transform = tf,
                    InstanceCustomIndex = (uint)i,
                    Mask = 0xFF,
                    InstanceShaderBindingTableRecordOffset = 0,
                    Flags = GeometryInstanceFlagsKHR.None,
                    AccelerationStructureReference = alloc.BlasDeviceAddress
                };

                dataSpan[i] = new InstanceData
                {
                    VertexOffset = (uint)alloc.VertexOffset,
                    IndexOffset = alloc.FirstIndex,
                    OpaqueIndexCount = alloc.OpaqueIndexCount,
                    VertexAddress = alloc.VertexAddress,
                    IndexAddress = alloc.IndexAddress
                };
            }

            var instancesData = new AccelerationStructureGeometryInstancesDataKHR { SType = StructureType.AccelerationStructureGeometryInstancesDataKhr, ArrayOfPointers = Vk.False, Data = new DeviceOrHostAddressConstKHR { DeviceAddress = _instancesBuffers[_currentFrame].DeviceAddress } };
            var geometry = new AccelerationStructureGeometryKHR { SType = StructureType.AccelerationStructureGeometryKhr, GeometryType = GeometryTypeKHR.InstancesKhr, Geometry = new AccelerationStructureGeometryDataKHR { Instances = instancesData } };

            var buildFlags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr | BuildAccelerationStructureFlagsKHR.AllowUpdateBitKhr;

            var buildInfoSize = new AccelerationStructureBuildGeometryInfoKHR
            {
                SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
                Type = AccelerationStructureTypeKHR.TopLevelKhr,
                Flags = buildFlags,
                GeometryCount = 1,
                PGeometries = &geometry
            };

            uint maxInstanceCount = (uint)_drawCallCount;
            _device.KhrAccelerationStructure.GetAccelerationStructureBuildSizes(_device.Device, AccelerationStructureBuildTypeKHR.DeviceKhr, in buildInfoSize, &maxInstanceCount, out var buildSizes);

            ulong requiredScratchSize = needsRebuild ? buildSizes.BuildScratchSize : buildSizes.UpdateScratchSize;

            if (_tlasScratchCapacities[_currentFrame] < requiredScratchSize)
            {
                _tlasScratchBuffers[_currentFrame]?.Dispose();
                ulong newCap = Math.Max(requiredScratchSize, _tlasScratchCapacities[_currentFrame] * 2);
                newCap = Math.Max(newCap, 1024 * 1024);
                _tlasScratchBuffers[_currentFrame] = new VulkanBuffer(_device, newCap, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit, MemoryPropertyFlags.DeviceLocalBit);
                _tlasScratchCapacities[_currentFrame] = newCap;
            }

            if (needsRebuild)
            {
                if (_tlasHandles[_currentFrame].Handle != 0)
                    _device.KhrAccelerationStructure.DestroyAccelerationStructure(_device.Device, _tlasHandles[_currentFrame], null);

                _tlasBuffers[_currentFrame]?.Dispose();
                _tlasBuffers[_currentFrame] = new VulkanBuffer(_device, buildSizes.AccelerationStructureSize, BufferUsageFlags.AccelerationStructureStorageBitKhr, MemoryPropertyFlags.DeviceLocalBit);

                var createInfo = new AccelerationStructureCreateInfoKHR { SType = StructureType.AccelerationStructureCreateInfoKhr, Buffer = _tlasBuffers[_currentFrame].Buffer, Size = buildSizes.AccelerationStructureSize, Type = AccelerationStructureTypeKHR.TopLevelKhr };
                _device.KhrAccelerationStructure.CreateAccelerationStructure(_device.Device, in createInfo, null, out _tlasHandles[_currentFrame]);
            }

            var buildInfo = new AccelerationStructureBuildGeometryInfoKHR
            {
                SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
                Type = AccelerationStructureTypeKHR.TopLevelKhr,
                Flags = buildFlags,
                GeometryCount = 1,
                PGeometries = &geometry,
                Mode = needsRebuild ? BuildAccelerationStructureModeKHR.BuildKhr : BuildAccelerationStructureModeKHR.UpdateKhr,
                SrcAccelerationStructure = needsRebuild ? default : _tlasHandles[_currentFrame],
                DstAccelerationStructure = _tlasHandles[_currentFrame],
                ScratchData = new DeviceOrHostAddressKHR { DeviceAddress = _tlasScratchBuffers[_currentFrame].DeviceAddress }
            };

            var buildRange = new AccelerationStructureBuildRangeInfoKHR { PrimitiveCount = (uint)_drawCallCount, PrimitiveOffset = 0, FirstVertex = 0, TransformOffset = 0 };
            var pBuildRange = &buildRange;

            _device.KhrAccelerationStructure.CmdBuildAccelerationStructures(cmd, 1, in buildInfo, &pBuildRange);

            _tlasInstanceCounts[_currentFrame] = _drawCallCount;
            _tlasNeedsRebuild[_currentFrame] = false;

            var buildBarrier = new MemoryBarrier2 { SType = StructureType.MemoryBarrier2, SrcStageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr, SrcAccessMask = AccessFlags2.AccelerationStructureWriteBitKhr, DstStageMask = PipelineStageFlags2.RayTracingShaderBitKhr, DstAccessMask = AccessFlags2.AccelerationStructureReadBitKhr };
            var depInfo1 = new DependencyInfo { SType = StructureType.DependencyInfo, MemoryBarrierCount = 1, PMemoryBarriers = &buildBarrier };
            _device.Vk.CmdPipelineBarrier2(cmd, in depInfo1);

            AccelerationStructureKHR tlasHandleForWrite = _tlasHandles[_currentFrame];

            WriteDescriptorSetAccelerationStructureKHR descriptorAS = new() { SType = StructureType.WriteDescriptorSetAccelerationStructureKhr, AccelerationStructureCount = 1, PAccelerationStructures = &tlasHandleForWrite };
            WriteDescriptorSet writeAS = new() { SType = StructureType.WriteDescriptorSet, PNext = &descriptorAS, DstSet = _descriptorSets[_currentFrame], DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.AccelerationStructureKhr };

            DescriptorImageInfo storageImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _renderStorageImageView };
            WriteDescriptorSet writeStorageImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 1, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &storageImageInfo };

            DescriptorImageInfo accumImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _renderAccumImageView };
            WriteDescriptorSet writeAccumImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 5, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &accumImageInfo };

            DescriptorBufferInfo instanceDataInfo = new() { Buffer = _instanceDataBuffers[_currentFrame].Buffer, Offset = 0, Range = Vk.WholeSize };
            WriteDescriptorSet writeInstanceData = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 4, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &instanceDataInfo };

            DescriptorImageInfo noisyImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _noisyColorImageView };
            WriteDescriptorSet writeNoisyImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 7, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &noisyImageInfo };

            DescriptorImageInfo normalRoughnessImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _normalRoughnessImageView };
            WriteDescriptorSet writeNormalRoughnessImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 8, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &normalRoughnessImageInfo };

            DescriptorImageInfo albedoImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _albedoImageView };
            WriteDescriptorSet writeAlbedoImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 9, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &albedoImageInfo };

            DescriptorImageInfo specAlbedoImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _specularAlbedoImageView };
            WriteDescriptorSet writeSpecAlbedoImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 10, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &specAlbedoImageInfo };

            DescriptorImageInfo mvecImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _motionVectorsImageView };
            WriteDescriptorSet writeMvecImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 11, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &mvecImageInfo };

            DescriptorImageInfo depthImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _depthImageView };
            WriteDescriptorSet writeDepthImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 12, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &depthImageInfo };

            DescriptorImageInfo specularMotionVectorsImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _specularMotionVectorsImageView };
            WriteDescriptorSet writeSpecularMotionVectorsImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 13, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &specularMotionVectorsImageInfo };

            var writes = stackalloc WriteDescriptorSet[11] { writeAS, writeStorageImage, writeAccumImage, writeInstanceData, writeNoisyImage, writeNormalRoughnessImage, writeAlbedoImage, writeSpecAlbedoImage, writeMvecImage, writeDepthImage, writeSpecularMotionVectorsImage };
            _device.Vk.UpdateDescriptorSets(_device.Device, 11, writes, 0, null);

            if (_materialBuffer != null)
            {
                DescriptorBufferInfo matBufferInfo = new() { Buffer = _materialBuffer.Buffer, Offset = 0, Range = Vk.WholeSize };
                WriteDescriptorSet writeMatData = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 6, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &matBufferInfo };
                _device.Vk.UpdateDescriptorSets(_device.Device, 1, &writeMatData, 0, null);
            }

            if (_currentTextureArray is VulkanTextureArray vkTexArray)
            {
                DescriptorImageInfo texArrayInfo = new() { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = vkTexArray.ImageView, Sampler = vkTexArray.Sampler };
                WriteDescriptorSet writeTex = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 3, DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &texArrayInfo };
                _device.Vk.UpdateDescriptorSets(_device.Device, 1, &writeTex, 0, null);
            }

            TransitionImageLayout(cmd, _renderStorageImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
            TransitionImageLayout(cmd, _renderAccumImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit | AccessFlags2.ShaderReadBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
            TransitionImageLayout(cmd, _noisyColorImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
            TransitionImageLayout(cmd, _normalRoughnessImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
            TransitionImageLayout(cmd, _albedoImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
            TransitionImageLayout(cmd, _specularAlbedoImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
            TransitionImageLayout(cmd, _motionVectorsImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
            TransitionImageLayout(cmd, _depthImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
            TransitionImageLayout(cmd, _specularMotionVectorsImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);

            _device.Vk.CmdBindPipeline(cmd, PipelineBindPoint.RayTracingKhr, _pipeline.Pipeline);
            var descSet = _descriptorSets[_currentFrame];
            _device.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.RayTracingKhr, _pipeline.PipelineLayout, 0, 1, &descSet, 0, null);

            var sbtProps = _pipeline.SbtProps;
            var raygenRegion = new StridedDeviceAddressRegionKHR { DeviceAddress = _pipeline.SbtBuffer.DeviceAddress, Stride = sbtProps.RegionAligned, Size = sbtProps.RegionAligned };
            var missRegion = new StridedDeviceAddressRegionKHR { DeviceAddress = _pipeline.SbtBuffer.DeviceAddress + sbtProps.RegionAligned, Stride = sbtProps.RegionAligned, Size = sbtProps.RegionAligned };
            var hitRegion = new StridedDeviceAddressRegionKHR { DeviceAddress = _pipeline.SbtBuffer.DeviceAddress + 2 * sbtProps.RegionAligned, Stride = sbtProps.RegionAligned, Size = sbtProps.RegionAligned };
            var callRegion = new StridedDeviceAddressRegionKHR { };

            _device.KhrRayTracingPipeline.CmdTraceRays(cmd, &raygenRegion, &missRegion, &hitRegion, &callRegion, (uint)_renderSize.X, (uint)_renderSize.Y, 1);

            if (_useDLSS_RR)
            {
                // Streamline layout transitions are handled by Streamline, but output image _storageImage needs to be in General
                TransitionImageLayout(cmd, _storageImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);

                FrameToken* frameToken = _currentFrameToken;

                var viewport = _slViewport;



                // Set Constants
                var consts = Constants.Create();
                consts.CameraViewToClip = proj;
                Matrix4x4.Invert(proj, out consts.ClipToCameraView);
                consts.ClipToPrevClip = cameraData.InverseViewProjection * cameraData.PrevViewProjection;
                Matrix4x4.Invert(cameraData.PrevViewProjection, out var invPrevViewProj);
                consts.PrevClipToClip = invPrevViewProj * cameraData.ViewProjection;

                consts.CameraPos = cameraData.LocalPosition;
                consts.CameraUp = cameraData.CameraUp;
                consts.CameraRight = cameraData.CameraRight;
                consts.CameraFwd = cameraData.CameraFwd;
                consts.CameraNear = 0.1f;
                consts.CameraFar = 3000.0f;
                consts.CameraFOV = float.Pi / 2.5f;
                consts.CameraAspectRatio = _framebufferSize.X / (float)Math.Max(1, _framebufferSize.Y);
                consts.JitterOffset = new Vector2(-_currentJitterX, -_currentJitterY);
                consts.MvecScale = new Vector2(0.5f, 0.5f);
                consts.DepthInverted = SlBoolean.eFalse;
                consts.CameraMotionIncluded = SlBoolean.eTrue;
                consts.MotionVectors3D = SlBoolean.eFalse;
                consts.Reset = (cameraData.FrameCount == 1) ? SlBoolean.eTrue : SlBoolean.eFalse;

                StreamlineAPI.slSetConstants(&consts, frameToken, &viewport);

                // Set Options
                if (StreamlineAPI.slDLSSSetOptions != null)
                {
                    var dlssOpt = DLSSOptions.Create();
                    dlssOpt.Mode = DLSSMode.eMaxQuality;
                    dlssOpt.QualityPreset = DLSSPreset.ePresetK; // Preset K requested by user
                    dlssOpt.OutputWidth = (uint)_framebufferSize.X;
                    dlssOpt.OutputHeight = (uint)_framebufferSize.Y;
                    dlssOpt.ColorBuffersHDR = SlBoolean.eTrue;
                    StreamlineAPI.slDLSSSetOptions(&viewport, &dlssOpt);
                }

                var opt = DLSSDOptions.Create();
                opt.Mode = DLSSMode.eMaxQuality; // Changed to eMaxQuality
                opt.QualityPreset = DLSSDPreset.ePresetE; // Latest transformer model for RR (avoids crash)
                opt.OutputWidth = (uint)_framebufferSize.X;
                opt.OutputHeight = (uint)_framebufferSize.Y;
                opt.NormalRoughnessMode = DLSSDNormalRoughnessMode.ePacked;
                opt.WorldToCameraView = view;
                opt.CameraViewToWorld = viewInverse;
                opt.ColorBuffersHDR = SlBoolean.eTrue;

                StreamlineAPI.slDLSSDSetOptions(&viewport, &opt);

                // Setup resource tags
                var extentIn = new Extent((uint)_renderSize.X, (uint)_renderSize.Y);
                var extentOut = new Extent((uint)_framebufferSize.X, (uint)_framebufferSize.Y);

                uint gBufferUsage = (uint)(ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit);
                uint outUsage = (uint)(ImageUsageFlags.StorageBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit);

                var resNoisy = new Resource(ResourceType.eTex2d, (void*)_noisyColorImage.Handle, (void*)_noisyColorImageMemory.Handle, (void*)_noisyColorImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R16G16B16A16Sfloat, gBufferUsage);
                var tagNoisy = new ResourceTag(&resNoisy, BufferType.kBufferTypeScalingInputColor, ResourceLifecycle.eValidUntilEvaluate, extentIn);

                var resAlbedo = new Resource(ResourceType.eTex2d, (void*)_albedoImage.Handle, (void*)_albedoImageMemory.Handle, (void*)_albedoImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R8G8B8A8Unorm, gBufferUsage);
                var tagAlbedo = new ResourceTag(&resAlbedo, BufferType.kBufferTypeAlbedo, ResourceLifecycle.eValidUntilEvaluate, extentIn);

                var resSpecAlbedo = new Resource(ResourceType.eTex2d, (void*)_specularAlbedoImage.Handle, (void*)_specularAlbedoImageMemory.Handle, (void*)_specularAlbedoImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R8G8B8A8Unorm, gBufferUsage);
                var tagSpecAlbedo = new ResourceTag(&resSpecAlbedo, BufferType.kBufferTypeSpecularAlbedo, ResourceLifecycle.eValidUntilEvaluate, extentIn);

                var resNormalRough = new Resource(ResourceType.eTex2d, (void*)_normalRoughnessImage.Handle, (void*)_normalRoughnessImageMemory.Handle, (void*)_normalRoughnessImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R16G16B16A16Sfloat, gBufferUsage);
                var tagNormalRough = new ResourceTag(&resNormalRough, BufferType.kBufferTypeNormalRoughness, ResourceLifecycle.eValidUntilEvaluate, extentIn);

                var resMvec = new Resource(ResourceType.eTex2d, (void*)_motionVectorsImage.Handle, (void*)_motionVectorsImageMemory.Handle, (void*)_motionVectorsImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R16G16Sfloat, gBufferUsage);
                var tagMvec = new ResourceTag(&resMvec, BufferType.kBufferTypeMotionVectors, ResourceLifecycle.eValidUntilEvaluate, extentIn);

                var resDepth = new Resource(ResourceType.eTex2d, (void*)_depthImage.Handle, (void*)_depthImageMemory.Handle, (void*)_depthImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R32Sfloat, gBufferUsage);
                var tagDepth = new ResourceTag(&resDepth, BufferType.kBufferTypeLinearDepth, ResourceLifecycle.eValidUntilEvaluate, extentIn);

                var resOut = new Resource(ResourceType.eTex2d, (void*)_storageImage.Handle, (void*)_storageImageMemory.Handle, (void*)_storageImageView.Handle, (uint)ImageLayout.General, (uint)_framebufferSize.X, (uint)_framebufferSize.Y, (uint)Format.R16G16B16A16Sfloat, outUsage);
                var tagOut = new ResourceTag(&resOut, BufferType.kBufferTypeScalingOutputColor, ResourceLifecycle.eValidUntilEvaluate, extentOut);

                var resSpecularMvec = new Resource(ResourceType.eTex2d, (void*)_specularMotionVectorsImage.Handle, (void*)_specularMotionVectorsImageMemory.Handle, (void*)_specularMotionVectorsImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R16G16Sfloat, gBufferUsage);
                var tagSpecularMvec = new ResourceTag(&resSpecularMvec, BufferType.kBufferTypeSpecularMotionVectors, ResourceLifecycle.eValidUntilEvaluate, extentIn);

                // For Late Warp, we also tag the backbuffer
                var resBackBuffer = new Resource(ResourceType.eTex2d, (void*)_swapchain.Images[imageIndex].Handle, null, (void*)_swapchain.ImageViews[imageIndex].Handle, (uint)ImageLayout.TransferDstOptimal, (uint)_framebufferSize.X, (uint)_framebufferSize.Y, (uint)_swapchain.ImageFormat, (uint)(ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit));
                var tagBackBuffer = new ResourceTag(&resBackBuffer, BufferType.kBufferTypeBackbuffer, ResourceLifecycle.eValidUntilEvaluate, extentOut);

                int numTags = _useLatewarp ? 9 : 8;
                ResourceTag* pTags = stackalloc ResourceTag[numTags];
                pTags[0] = tagNoisy;
                pTags[1] = tagAlbedo;
                pTags[2] = tagSpecAlbedo;
                pTags[3] = tagNormalRough;
                pTags[4] = tagMvec;
                pTags[5] = tagDepth;
                pTags[6] = tagOut;
                pTags[7] = tagSpecularMvec;
                if (_useLatewarp)
                {
                    pTags[8] = tagBackBuffer;
                }

                StreamlineAPI.slSetTagForFrame(frameToken, &viewport, pTags, (uint)numTags, (void*)cmd.Handle);

                void* inputViewport = &viewport;
                int evalRes = StreamlineAPI.slEvaluateFeature((uint)Feature.kFeatureDLSS_RR, frameToken, &inputViewport, 1, (void*)cmd.Handle);
                if (evalRes != (int)SlResult.eOk)
                {
                    Console.WriteLine($"[Streamline] Evaluate failed: {(SlResult)evalRes}");
                }

                TransitionImageLayout(cmd, _storageImage, ImageLayout.General, ImageLayout.TransferSrcOptimal, AccessFlags2.ShaderWriteBit, AccessFlags2.TransferReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.TransferBit);
            }
            else
            {
                // Fallback copy
                TransitionImageLayout(cmd, _renderStorageImage, ImageLayout.General, ImageLayout.TransferSrcOptimal, AccessFlags2.ShaderWriteBit, AccessFlags2.TransferReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.TransferBit);
                TransitionImageLayout(cmd, _storageImage, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, AccessFlags2.None, AccessFlags2.TransferWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.TransferBit);

                ImageCopy2 copy = new() { SType = StructureType.ImageCopy2, SrcSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1), DstSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1), Extent = new Extent3D((uint)_framebufferSize.X, (uint)_framebufferSize.Y, 1) };
                CopyImageInfo2 copyInfo = new() { SType = StructureType.CopyImageInfo2, SrcImage = _renderStorageImage, SrcImageLayout = ImageLayout.TransferSrcOptimal, DstImage = _storageImage, DstImageLayout = ImageLayout.TransferDstOptimal, RegionCount = 1, PRegions = &copy };
                _device.Vk.CmdCopyImage2(cmd, in copyInfo);

                TransitionImageLayout(cmd, _storageImage, ImageLayout.TransferDstOptimal, ImageLayout.TransferSrcOptimal, AccessFlags2.TransferWriteBit, AccessFlags2.TransferReadBit, PipelineStageFlags2.TransferBit, PipelineStageFlags2.TransferBit);
            }
        }

        TransitionImageLayout(cmd, _swapchain.Images[imageIndex], ImageLayout.Undefined, ImageLayout.TransferDstOptimal, AccessFlags2.None, AccessFlags2.TransferWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.TransferBit);

        if (_drawCallCount > 0)
        {
            ImageBlit blit = new()
            {
                SrcSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                DstSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1)
            };
            blit.SrcOffsets[0] = new Offset3D(0, 0, 0);
            blit.SrcOffsets[1] = new Offset3D((int)_framebufferSize.X, (int)_framebufferSize.Y, 1);
            blit.DstOffsets[0] = new Offset3D(0, 0, 0);
            blit.DstOffsets[1] = new Offset3D((int)_framebufferSize.X, (int)_framebufferSize.Y, 1);

            _device.Vk.CmdBlitImage(cmd, _storageImage, ImageLayout.TransferSrcOptimal, _swapchain.Images[imageIndex], ImageLayout.TransferDstOptimal, 1, &blit, Filter.Linear);
        }

        if (_useLatewarp && _useDLSS_RR)
        {
            // Transition backbuffer to General layout for Late Warp evaluation
            TransitionImageLayout(cmd, _swapchain.Images[imageIndex], ImageLayout.TransferDstOptimal, ImageLayout.General, AccessFlags2.TransferWriteBit, AccessFlags2.ShaderWriteBit | AccessFlags2.ShaderReadBit, PipelineStageFlags2.TransferBit, PipelineStageFlags2.RayTracingShaderBitKhr);

            var viewport = _slViewport;
            void* inputViewport = &viewport;
            int evalLatewarpRes = StreamlineAPI.slEvaluateFeature((uint)Feature.kFeatureLatewarp, _currentFrameToken, &inputViewport, 1, (void*)cmd.Handle);
            if (evalLatewarpRes != (int)SlResult.eOk)
            {
                Console.WriteLine($"[Streamline] Late Warp Evaluate failed: {(SlResult)evalLatewarpRes}");
            }

            // Transition from General to PresentSrcKhr
            TransitionImageLayout(cmd, _swapchain.Images[imageIndex], ImageLayout.General, ImageLayout.PresentSrcKhr, AccessFlags2.ShaderWriteBit | AccessFlags2.ShaderReadBit, AccessFlags2.None, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.BottomOfPipeBit);
        }
        else
        {
            TransitionImageLayout(cmd, _swapchain.Images[imageIndex], ImageLayout.TransferDstOptimal, ImageLayout.PresentSrcKhr, AccessFlags2.TransferWriteBit, AccessFlags2.None, PipelineStageFlags2.TransferBit, PipelineStageFlags2.BottomOfPipeBit);
        }

        _device.Vk.EndCommandBuffer(cmd);

        var waitInfo = new SemaphoreSubmitInfo { SType = StructureType.SemaphoreSubmitInfo, Semaphore = _imageAvailableSemaphores[_currentFrame], StageMask = PipelineStageFlags2.ColorAttachmentOutputBit };
        var signalInfo = new SemaphoreSubmitInfo { SType = StructureType.SemaphoreSubmitInfo, Semaphore = _renderFinishedSemaphores[imageIndex], StageMask = PipelineStageFlags2.AllCommandsBit };
        var cmdInfo = new CommandBufferSubmitInfo { SType = StructureType.CommandBufferSubmitInfo, CommandBuffer = cmd };

        var submitInfo = new SubmitInfo2 { SType = StructureType.SubmitInfo2, WaitSemaphoreInfoCount = 1, PWaitSemaphoreInfos = &waitInfo, CommandBufferInfoCount = 1, PCommandBufferInfos = &cmdInfo, SignalSemaphoreInfoCount = 1, PSignalSemaphoreInfos = &signalInfo };

        if (StreamlineAPI.slPCLSetMarker != null && _currentFrameToken != null)
        {
            StreamlineAPI.slPCLSetMarker(PCLMarker.eRenderSubmitEnd, _currentFrameToken);
        }

        lock (_device.QueueLock) _device.Vk.QueueSubmit2(_device.GraphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]);

        var swapchains = stackalloc[] { _swapchain.Swapchain };
        PresentInfoKHR presentInfo = new() { SType = StructureType.PresentInfoKhr, WaitSemaphoreCount = 1, PWaitSemaphores = (Semaphore*)Unsafe.AsPointer(ref _renderFinishedSemaphores[imageIndex]), SwapchainCount = 1, PSwapchains = swapchains, PImageIndices = &imageIndex };

        if (StreamlineAPI.slPCLSetMarker != null && _currentFrameToken != null)
        {
            StreamlineAPI.slPCLSetMarker(PCLMarker.ePresentStart, _currentFrameToken);
        }

        if (_useDLSS_RR)
        {
            lock (_device.QueueLock) result = (Result)StreamlineAPI.vkQueuePresentKHR((void*)_device.PresentQueue.Handle, &presentInfo);
        }
        else
        {
            lock (_device.QueueLock) result = _device.KhrSwapchain.QueuePresent(_device.PresentQueue, in presentInfo);
        }

        if (StreamlineAPI.slPCLSetMarker != null && _currentFrameToken != null)
        {
            StreamlineAPI.slPCLSetMarker(PCLMarker.ePresentEnd, _currentFrameToken);
        }

        if (result == Result.ErrorDeviceLost) throw new Exception("Критическая ошибка: Vulkan Device Lost (видеокарта перестала отвечать)!");
        if (result is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr) _framebufferResized = true;

        _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
        _prevFrameToken = _currentFrameToken;
        _currentFrameToken = null;
    }

    private void TransitionImageLayout(CommandBuffer cmd, Image image, ImageLayout oldLayout, ImageLayout newLayout, AccessFlags2 srcAccess, AccessFlags2 dstAccess, PipelineStageFlags2 srcStage, PipelineStageFlags2 dstStage)
    {
        var barrier = new ImageMemoryBarrier2 { SType = StructureType.ImageMemoryBarrier2, OldLayout = oldLayout, NewLayout = newLayout, SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored, Image = image, SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1), SrcAccessMask = srcAccess, DstAccessMask = dstAccess, SrcStageMask = srcStage, DstStageMask = dstStage };
        var depInfo = new DependencyInfo { SType = StructureType.DependencyInfo, ImageMemoryBarrierCount = 1, PImageMemoryBarriers = &barrier };
        _device.Vk.CmdPipelineBarrier2(cmd, in depInfo);
    }

    public void OnFramebufferResize(Vector2Int newSize)
    {
        _framebufferSize = newSize;
        _framebufferResized = true;
    }

    public void Dispose()
    {
        _device.Vk.DeviceWaitIdle(_device.Device);

        _materialBuffer?.Dispose();

        foreach (var list in _meshesToDispose)
            foreach (var mesh in list)
            {
                var a = (MeshAllocation)mesh;
                _device.KhrAccelerationStructure.DestroyAccelerationStructure(_device.Device, a.Blas, null);
                mesh.Dispose();
            }

        while (_pendingMeshesToDispose.TryDequeue(out var mesh))
        {
            var a = (MeshAllocation)mesh;
            _device.KhrAccelerationStructure.DestroyAccelerationStructure(_device.Device, a.Blas, null);
            mesh.Dispose();
        }

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _instancesBuffers[i]?.Dispose();
            _instanceDataBuffers[i]?.Dispose();
            _tlasScratchBuffers[i]?.Dispose();
            _tlasBuffers[i]?.Dispose();
            if (_tlasHandles[i].Handle != 0) _device.KhrAccelerationStructure.DestroyAccelerationStructure(_device.Device, _tlasHandles[i], null);
        }

        if (_descriptorPool.Handle != 0) _device.Vk.DestroyDescriptorPool(_device.Device, _descriptorPool, null);

        foreach (var cb in _cameraBuffers) cb?.Dispose();

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _device.Vk.DestroySemaphore(_device.Device, _imageAvailableSemaphores[i], null);
            _device.Vk.DestroyFence(_device.Device, _inFlightFences[i], null);
        }

        for (int i = 0; i < 8; i++)
        {
            _device.Vk.DestroySemaphore(_device.Device, _renderFinishedSemaphores[i], null);
        }

        _meshPool?.Dispose();

        DestroyImageHelper(_storageImage, _storageImageMemory, _storageImageView);
        DestroyImageHelper(_renderStorageImage, _renderStorageImageMemory, _renderStorageImageView);
        DestroyImageHelper(_renderAccumImage, _renderAccumImageMemory, _renderAccumImageView);
        DestroyImageHelper(_noisyColorImage, _noisyColorImageMemory, _noisyColorImageView);
        DestroyImageHelper(_normalRoughnessImage, _normalRoughnessImageMemory, _normalRoughnessImageView);
        DestroyImageHelper(_albedoImage, _albedoImageMemory, _albedoImageView);
        DestroyImageHelper(_specularAlbedoImage, _specularAlbedoImageMemory, _specularAlbedoImageView);
        DestroyImageHelper(_motionVectorsImage, _motionVectorsImageMemory, _motionVectorsImageView);
        DestroyImageHelper(_depthImage, _depthImageMemory, _depthImageView);
        DestroyImageHelper(_specularMotionVectorsImage, _specularMotionVectorsImageMemory, _specularMotionVectorsImageView);

        _device.Vk.DestroyCommandPool(_device.Device, _commandPool, null);
        _pipeline?.Dispose();
        _swapchain.Dispose();

        if (_useDLSS_RR)
        {
            try
            {
                StreamlineAPI.slShutdown();
            }
            catch {}
        }

        _device.Dispose();
    }

    private static float Halton(int index, int @base)
    {
        float result = 0f;
        float f = 1f / @base;
        int i = index;
        while (i > 0)
        {
            result += f * (i % @base);
            i /= @base;
            f /= @base;
        }
        return result;
    }

    private void UpdateJitter()
    {
        if (_useDLSS_RR)
        {
            int haltonIndex = (int)(_slFrameIndex % 16) + 1;
            _currentJitterX = Halton(haltonIndex, 2) - 0.5f;
            _currentJitterY = Halton(haltonIndex, 3) - 0.5f;
        }
        else
        {
            _currentJitterX = 0f;
            _currentJitterY = 0f;
        }
    }

    public void StartFrame()
    {
        _hasPredictedCamera = false;
        if (_currentFrameToken != null) return;

        if (_useDLSS_RR || _useReflex)
        {
            FrameToken* frameToken = null;
            _slFrameIndex++;
            uint localFrameIndex = _slFrameIndex;
            StreamlineAPI.slGetNewFrameToken(&frameToken, &localFrameIndex);
            _slFrameIndex = localFrameIndex;
            _currentFrameToken = frameToken;
        }

        if (_useReflex && _currentFrameToken != null)
        {
            StreamlineAPI.slReflexSleep(_currentFrameToken);
        }
    }

    public bool GetPredictedCamera(out Matrix4x4 view, out Matrix4x4 proj)
    {
        view = Matrix4x4.Identity;
        proj = Matrix4x4.Identity;
        _hasPredictedCamera = false;
        if (!_useReflex || !_useLatewarp || _prevFrameToken == null) return false;

        var predicted = ReflexPredictedCameraData.Create();
        var viewport = _slViewport;
        int res = StreamlineAPI.slReflexGetPredictedCameraData(&viewport, _prevFrameToken, &predicted);
        if (res == (int)SlResult.eOk)
        {
            view = predicted.PredictedWorldToViewMatrix;
            proj = predicted.PredictedViewToClipMatrix;
            _currentPredictedView = view;
            _currentPredictedProj = proj;
            _hasPredictedCamera = true;
            return true;
        }
        return false;
    }

    public void SetSimulationStart()
    {
        if (StreamlineAPI.slPCLSetMarker != null && _currentFrameToken != null)
        {
            StreamlineAPI.slPCLSetMarker(PCLMarker.eSimulationStart, _currentFrameToken);
        }
    }

    public void SetSimulationEnd()
    {
        if (StreamlineAPI.slPCLSetMarker != null && _currentFrameToken != null)
        {
            StreamlineAPI.slPCLSetMarker(PCLMarker.eSimulationEnd, _currentFrameToken);
        }
    }

}