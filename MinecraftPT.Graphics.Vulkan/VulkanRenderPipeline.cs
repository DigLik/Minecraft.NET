using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

using MinecraftPT.Engine.Abstractions;
using MinecraftPT.Engine.Abstractions.Graphics;
using MinecraftPT.Graphics.Vulkan.Core;
using MinecraftPT.Utils.Collections;
using MinecraftPT.Utils.Math;

using Silk.NET.Vulkan;
using Streamline;

using Semaphore = Silk.NET.Vulkan.Semaphore;
using Result = Silk.NET.Vulkan.Result;
using SlResult = Streamline.Result;
using SlBoolean = Streamline.Boolean;

namespace MinecraftPT.Graphics.Vulkan;

public unsafe partial class VulkanRenderPipeline : IRenderPipeline
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
    private readonly ILogger<VulkanRenderPipeline> _logger;

    private CommandPool _commandPool;
    private readonly CommandBuffer[] _commandBuffers = new CommandBuffer[MaxFramesInFlight];

    private readonly Semaphore[] _imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
    private Semaphore[] _renderFinishedSemaphores = [];
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
    private bool _useDLSS_SR = false;
    private bool _useDLSS_RR = false;
    private bool _useReflex = false;
    private ViewportHandle _slViewport;
    private Vector2Int _renderSize;
    private uint _slFrameIndex = 0;
    private float _currentJitterX = 0f;
    private float _currentJitterY = 0f;

    private FrameToken* _currentFrameToken = null;
    private FrameToken* _prevFrameToken = null;
    private Matrix4x4 _prevWorldToView = Matrix4x4.Identity;
    private Matrix4x4 _prevViewToClip = Matrix4x4.Identity;


    private Image _renderStorageImage;
    private DeviceMemory _renderStorageImageMemory;
    private ImageView _renderStorageImageView;



    private Image _noisyColorImage;
    private DeviceMemory _noisyColorImageMemory;
    private ImageView _noisyColorImageView;

    private Image _normalImage;
    private DeviceMemory _normalImageMemory;
    private ImageView _normalImageView;

    private Image _roughnessImage;
    private DeviceMemory _roughnessImageMemory;
    private ImageView _roughnessImageView;

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

    private Image _linearDepthImage;
    private DeviceMemory _linearDepthImageMemory;
    private ImageView _linearDepthImageView;

    private Image _colorBeforeTransparencyImage;
    private DeviceMemory _colorBeforeTransparencyImageMemory;
    private ImageView _colorBeforeTransparencyImageView;

    private Image _specularMotionVectorsImage;
    private DeviceMemory _specularMotionVectorsImageMemory;
    private ImageView _specularMotionVectorsImageView;

    private Image _exposureImage;
    private DeviceMemory _exposureImageMemory;
    private ImageView _exposureImageView;

    private Image _biasColorImage;
    private DeviceMemory _biasColorImageMemory;
    private ImageView _biasColorImageView;

    private Image _specularHitDistanceImage;
    private DeviceMemory _specularHitDistanceImageMemory;
    private ImageView _specularHitDistanceImageView;

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

    [LoggerMessage(EventId = 10, Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "[Streamline] DLSS SR is not supported: {Result}")]
    private partial void LogDlssSrNotSupported(SlResult result);

    [LoggerMessage(EventId = 11, Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "[Streamline] DLSS RR is not supported: {Result}")]
    private partial void LogDlssRrNotSupported(SlResult result);

    [LoggerMessage(EventId = 12, Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "[Streamline] DLSS SR (Quality) initialized successfully. Output: {OutX}x{OutY}, Render size: {RenderX}x{RenderY}")]
    private partial void LogDlssSrSuccess(int outX, int outY, int renderX, int renderY);

    [LoggerMessage(EventId = 13, Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "[Streamline] DLSS RR (Quality) initialized successfully. Output: {OutX}x{OutY}, Render size: {RenderX}x{RenderY}")]
    private partial void LogDlssRrSuccess(int outX, int outY, int renderX, int renderY);

    [LoggerMessage(EventId = 14, Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "[Streamline] Reflex 2 initialized. Mode: LowLatencyWithBoost, status: {Result}")]
    private partial void LogReflexSuccess(SlResult result);

    [LoggerMessage(EventId = 15, Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "[Streamline] Reflex is not supported: {Result}")]
    private partial void LogReflexNotSupported(SlResult result);

    [LoggerMessage(EventId = 16, Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "[Streamline] PCL Stats initialized successfully.")]
    private partial void LogPclSuccess();

    [LoggerMessage(EventId = 17, Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "[Streamline] PCL Stats is not supported: {Result}")]
    private partial void LogPclNotSupported(SlResult result);

    [LoggerMessage(EventId = 18, Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "[Streamline] Failed to set Vulkan info: {Result}")]
    private partial void LogFailedSetVulkanInfo(SlResult result);

    [LoggerMessage(EventId = 19, Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "[Streamline] Error during Vulkan setup: {Error}")]
    private partial void LogVulkanSetupError(string error);

    [LoggerMessage(EventId = 20, Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "[Streamline] slReflexSetCameraData failed: {Result}")]
    private partial void LogReflexCameraDataError(SlResult result);

    [LoggerMessage(EventId = 21, Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "[Streamline] DLSS RR Evaluate failed: {Result}")]
    private partial void LogDlssRrEvalError(int result);

    [LoggerMessage(EventId = 22, Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "[Streamline] DLSS SR Evaluate failed: {Result}")]
    private partial void LogDlssSrEvalError(int result);

    public VulkanRenderPipeline(IWindow window, ILogger<VulkanRenderPipeline> logger)
    {
        _logger = logger;

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

        InitializeStreamline();
    }

    private void InitializeStreamline()
    {
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
                
                // Check DLSS SR
                int supDlss = StreamlineAPI.slIsFeatureSupported((uint)Feature.kFeatureDLSS, &adapterInfo);
                if (supDlss == (int)SlResult.eOk)
                {
                    _useDLSS_SR = true;
                    StreamlineAPI.LoadDLSSFunctions();
                }
                else
                {
                    LogDlssSrNotSupported((SlResult)supDlss);
                }

                // Check DLSS RR
                int supRes = StreamlineAPI.slIsFeatureSupported((uint)Feature.kFeatureDLSS_RR, &adapterInfo);
                if (supRes == (int)SlResult.eOk)
                {
                    _useDLSS_RR = true;
                    StreamlineAPI.LoadDLSSDFunctions();
                }
                else
                {
                    LogDlssRrNotSupported((SlResult)supRes);
                }

                if (_useDLSS_SR || _useDLSS_RR)
                {
                    _slViewport = new ViewportHandle(1);
                }

                // Initialize DLSS SR options/settings if supported
                if (_useDLSS_SR)
                {
                    if (StreamlineAPI.slDLSSSetOptions != null)
                    {
                        var dlssOptions = DLSSOptions.Create();
                        dlssOptions.Mode = DLSSMode.eBalanced;
                        dlssOptions.QualityPreset = DLSSPreset.ePresetK; // Preset K requested by user
                        dlssOptions.OutputWidth = (uint)_framebufferSize.X;
                        dlssOptions.OutputHeight = (uint)_framebufferSize.Y;
                        dlssOptions.ColorBuffersHDR = SlBoolean.eTrue;
                        dlssOptions.UseAutoExposure = SlBoolean.eTrue; // Enable auto-exposure for SR
                        var vp = _slViewport;
                        StreamlineAPI.slDLSSSetOptions(&vp, &dlssOptions);

                        var dlssSettings = DLSSOptimalSettings.Create();
                        if (StreamlineAPI.slDLSSGetOptimalSettings(&dlssOptions, &dlssSettings) == (int)SlResult.eOk)
                        {
                            if (!_useDLSS_RR)
                            {
                                _renderSize = new Vector2Int((int)dlssSettings.OptimalRenderWidth, (int)dlssSettings.OptimalRenderHeight);
                                LogDlssSrSuccess(_framebufferSize.X, _framebufferSize.Y, _renderSize.X, _renderSize.Y);
                            }
                        }
                    }
                }

                // Initialize DLSS RR options/settings if supported
                if (_useDLSS_RR)
                {
                    var dlssdOptions = DLSSDOptions.Create();
                    dlssdOptions.Mode = DLSSMode.eMaxQuality;
                    dlssdOptions.QualityPreset = DLSSDPreset.ePresetE; // Latest transformer model for RR (avoids crash)
                    dlssdOptions.OutputWidth = (uint)_framebufferSize.X;
                    dlssdOptions.OutputHeight = (uint)_framebufferSize.Y;
                    dlssdOptions.ColorBuffersHDR = SlBoolean.eTrue;
                    dlssdOptions.NormalRoughnessMode = DLSSDNormalRoughnessMode.eUnpacked;
                    
                    var dlssdSettings = DLSSDOptimalSettings.Create();
                    if (StreamlineAPI.slDLSSDGetOptimalSettings(&dlssdOptions, &dlssdSettings) == (int)SlResult.eOk)
                    {
                        _renderSize = new Vector2Int((int)dlssdSettings.OptimalRenderWidth, (int)dlssdSettings.OptimalRenderHeight);
                        LogDlssRrSuccess(_framebufferSize.X, _framebufferSize.Y, _renderSize.X, _renderSize.Y);
                    }
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
                    LogReflexSuccess((SlResult)setReflexRes);
                }
                else
                {
                    LogReflexNotSupported((SlResult)supReflex);
                }

                // Check PCL
                int supPCL = StreamlineAPI.slIsFeatureSupported((uint)Feature.kFeaturePCL, &adapterInfo);
                if (supPCL == (int)SlResult.eOk)
                {
                    StreamlineAPI.LoadPCLFunctions();
                    LogPclSuccess();
                }
                else
                {
                    LogPclNotSupported((SlResult)supPCL);
                }
            }
            else
            {
                LogFailedSetVulkanInfo((SlResult)setVkRes);
            }
        }
        catch (Exception ex)
        {
            LogVulkanSetupError(ex.Message);
        }

        if (!_useDLSS_RR && !_useDLSS_SR)
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



        // 4. Create G-buffers at render resolution
        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R16G16B16A16Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _noisyColorImage, out _noisyColorImageMemory, out _noisyColorImageView);

        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R16G16B16A16Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _normalImage, out _normalImageMemory, out _normalImageView);

        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R16Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _roughnessImage, out _roughnessImageMemory, out _roughnessImageView);

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

        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R32Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _linearDepthImage, out _linearDepthImageMemory, out _linearDepthImageView);

        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R16G16B16A16Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _colorBeforeTransparencyImage, out _colorBeforeTransparencyImageMemory, out _colorBeforeTransparencyImageView);

        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R16G16Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _specularMotionVectorsImage, out _specularMotionVectorsImageMemory, out _specularMotionVectorsImageView);

        CreateImageHelper(1, 1, Format.R32Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _exposureImage, out _exposureImageMemory, out _exposureImageView);

        CreateImageHelper(1, 1, Format.R32Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _biasColorImage, out _biasColorImageMemory, out _biasColorImageView);

        CreateImageHelper((uint)_renderSize.X, (uint)_renderSize.Y, Format.R16Sfloat, 
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit, 
            out _specularHitDistanceImage, out _specularHitDistanceImageMemory, out _specularHitDistanceImageView);
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

        _renderFinishedSemaphores = new Semaphore[_swapchain.Images.Length];
        for (int i = 0; i < _renderFinishedSemaphores.Length; i++)
        {
            _device.Vk.CreateSemaphore(_device.Device, in semaphoreInfo, null, out _renderFinishedSemaphores[i]);
        }
    }

    private void CreateDescriptorPoolAndSets()
    {
        DescriptorPoolSize[] poolSizes = [
            new() { Type = DescriptorType.AccelerationStructureKhr, DescriptorCount = MaxFramesInFlight },
            new() { Type = DescriptorType.StorageImage, DescriptorCount = MaxFramesInFlight * 14 },
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

        foreach (var sem in _renderFinishedSemaphores)
        {
            if (sem.Handle != 0) _device.Vk.DestroySemaphore(_device.Device, sem, null);
        }

        DestroyImageHelper(_storageImage, _storageImageMemory, _storageImageView);
        DestroyImageHelper(_renderStorageImage, _renderStorageImageMemory, _renderStorageImageView);

        DestroyImageHelper(_noisyColorImage, _noisyColorImageMemory, _noisyColorImageView);
        DestroyImageHelper(_normalImage, _normalImageMemory, _normalImageView);
        DestroyImageHelper(_roughnessImage, _roughnessImageMemory, _roughnessImageView);
        DestroyImageHelper(_albedoImage, _albedoImageMemory, _albedoImageView);
        DestroyImageHelper(_specularAlbedoImage, _specularAlbedoImageMemory, _specularAlbedoImageView);
        DestroyImageHelper(_motionVectorsImage, _motionVectorsImageMemory, _motionVectorsImageView);
        DestroyImageHelper(_depthImage, _depthImageMemory, _depthImageView);
        DestroyImageHelper(_linearDepthImage, _linearDepthImageMemory, _linearDepthImageView);
        DestroyImageHelper(_colorBeforeTransparencyImage, _colorBeforeTransparencyImageMemory, _colorBeforeTransparencyImageView);
        DestroyImageHelper(_specularMotionVectorsImage, _specularMotionVectorsImageMemory, _specularMotionVectorsImageView);
        DestroyImageHelper(_exposureImage, _exposureImageMemory, _exposureImageView);
        DestroyImageHelper(_biasColorImage, _biasColorImageMemory, _biasColorImageView);
        DestroyImageHelper(_specularHitDistanceImage, _specularHitDistanceImageMemory, _specularHitDistanceImageView);

        _swapchain.Dispose();
        _swapchain = new VulkanSwapchain(_device, _framebufferSize);

        if (_useDLSS_RR)
        {
            var dlssdOptions = DLSSDOptions.Create();
            dlssdOptions.Mode = DLSSMode.eMaxQuality;
            dlssdOptions.QualityPreset = DLSSDPreset.ePresetE;
            dlssdOptions.OutputWidth = (uint)_framebufferSize.X;
            dlssdOptions.OutputHeight = (uint)_framebufferSize.Y;
            dlssdOptions.ColorBuffersHDR = SlBoolean.eTrue;
            dlssdOptions.NormalRoughnessMode = DLSSDNormalRoughnessMode.eUnpacked;
            
            var dlssdSettings = DLSSDOptimalSettings.Create();
            if (StreamlineAPI.slDLSSDGetOptimalSettings(&dlssdOptions, &dlssdSettings) == (int)SlResult.eOk)
            {
                _renderSize = new Vector2Int((int)dlssdSettings.OptimalRenderWidth, (int)dlssdSettings.OptimalRenderHeight);
            }
        }
        else if (_useDLSS_SR)
        {
            if (StreamlineAPI.slDLSSSetOptions != null)
            {
                var dlssOptions = DLSSOptions.Create();
                dlssOptions.Mode = DLSSMode.eBalanced;
                dlssOptions.QualityPreset = DLSSPreset.ePresetK;
                dlssOptions.OutputWidth = (uint)_framebufferSize.X;
                dlssOptions.OutputHeight = (uint)_framebufferSize.Y;
                dlssOptions.ColorBuffersHDR = SlBoolean.eTrue;
                dlssOptions.UseAutoExposure = SlBoolean.eTrue; // Enable auto-exposure for SR
                var vp = _slViewport;
                StreamlineAPI.slDLSSSetOptions(&vp, &dlssOptions);

                var dlssSettings = DLSSOptimalSettings.Create();
                if (StreamlineAPI.slDLSSGetOptimalSettings(&dlssOptions, &dlssSettings) == (int)SlResult.eOk)
                {
                    _renderSize = new Vector2Int((int)dlssSettings.OptimalRenderWidth, (int)dlssSettings.OptimalRenderHeight);
                }
            }
        }
        else
        {
            _renderSize = _framebufferSize;
        }

        SemaphoreCreateInfo semaphoreInfo = new() { SType = StructureType.SemaphoreCreateInfo };
        _renderFinishedSemaphores = new Semaphore[_swapchain.Images.Length];
        for (int i = 0; i < _renderFinishedSemaphores.Length; i++)
        {
            _device.Vk.CreateSemaphore(_device.Device, in semaphoreInfo, null, out _renderFinishedSemaphores[i]);
        }

        CreateStorageImage();
    }

    public void RenderFrame(CameraData cameraData)
    {
        if (_pipeline == null) throw new Exception("Pipeline is not initialized.");

        StartFrame();
        SetSimulationStart();

        UpdateCameraBuffer(ref cameraData);

        // WaitForFences and mesh disposal
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

        SetSimulationEnd();

        uint imageIndex;
        var result = _device.KhrSwapchain.AcquireNextImage(_device.Device, _swapchain.Swapchain, ulong.MaxValue, _imageAvailableSemaphores[_currentFrame], default, &imageIndex);

        if (result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            return;
        }

        UpdateStreamlineFrameTokenAndReflex(in cameraData);

        _device.Vk.ResetFences(_device.Device, 1, ref _inFlightFences[_currentFrame]);

        CommandBuffer cmd = _commandBuffers[_currentFrame];
        _device.Vk.ResetCommandBuffer(cmd, 0);

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        _device.Vk.BeginCommandBuffer(cmd, in beginInfo);

        RecordCommandBuffer(cmd, imageIndex, in cameraData);

        SubmitAndPresent(cmd, imageIndex);
    }

    private void UpdateCameraBuffer(ref CameraData cameraData)
    {
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

        _cameraBuffers[_currentFrame].UpdateData(in cameraData);
    }

    private void UpdateStreamlineFrameTokenAndReflex(in CameraData cameraData)
    {
        if (_currentFrameToken == null)
        {
            if (_useDLSS_RR || _useDLSS_SR || _useReflex)
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

        if (_useReflex && _currentFrameToken != null)
        {
            var originalView = Matrix4x4.CreateLookAt(cameraData.LocalPosition, cameraData.LocalPosition + cameraData.CameraFwd, cameraData.CameraUp);

            float aspect = _framebufferSize.X / (float)Math.Max(1, _framebufferSize.Y);
            var originalProj = Matrix4x4.CreatePerspectiveFieldOfView(float.Pi / 2.5f, aspect, 0.1f, 3000f);
            originalProj.M33 = -originalProj.M33 - 1.0f;
            originalProj.M43 = -originalProj.M43;
            originalProj.M22 *= -1;

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
                LogReflexCameraDataError((SlResult)setCamRes);
            }
        }
    }

    private void BuildTLAS(CommandBuffer cmd)
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
    }

    private void UpdateDescriptors()
    {
        AccelerationStructureKHR tlasHandleForWrite = _tlasHandles[_currentFrame];

        WriteDescriptorSetAccelerationStructureKHR descriptorAS = new() { SType = StructureType.WriteDescriptorSetAccelerationStructureKhr, AccelerationStructureCount = 1, PAccelerationStructures = &tlasHandleForWrite };
        WriteDescriptorSet writeAS = new() { SType = StructureType.WriteDescriptorSet, PNext = &descriptorAS, DstSet = _descriptorSets[_currentFrame], DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.AccelerationStructureKhr };

        DescriptorImageInfo storageImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _renderStorageImageView };
        WriteDescriptorSet writeStorageImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 1, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &storageImageInfo };

        DescriptorBufferInfo instanceDataInfo = new() { Buffer = _instanceDataBuffers[_currentFrame].Buffer, Offset = 0, Range = Vk.WholeSize };
        WriteDescriptorSet writeInstanceData = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 4, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &instanceDataInfo };

        DescriptorImageInfo noisyImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _noisyColorImageView };
        WriteDescriptorSet writeNoisyImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 6, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &noisyImageInfo };

        DescriptorImageInfo normalImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _normalImageView };
        WriteDescriptorSet writeNormalImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 7, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &normalImageInfo };

        DescriptorImageInfo roughnessImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _roughnessImageView };
        WriteDescriptorSet writeRoughnessImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 8, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &roughnessImageInfo };

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

        DescriptorImageInfo specHitDistanceImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _specularHitDistanceImageView };
        WriteDescriptorSet writeSpecHitDistanceImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 14, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &specHitDistanceImageInfo };

        DescriptorImageInfo linearDepthImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _linearDepthImageView };
        WriteDescriptorSet writeLinearDepthImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 15, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &linearDepthImageInfo };

        DescriptorImageInfo colorBeforeTransInfo = new() { ImageLayout = ImageLayout.General, ImageView = _colorBeforeTransparencyImageView };
        WriteDescriptorSet writeColorBeforeTrans = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 16, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &colorBeforeTransInfo };

        var writes = stackalloc WriteDescriptorSet[14] { writeAS, writeStorageImage, writeInstanceData, writeNoisyImage, writeNormalImage, writeAlbedoImage, writeSpecAlbedoImage, writeMvecImage, writeDepthImage, writeSpecularMotionVectorsImage, writeRoughnessImage, writeSpecHitDistanceImage, writeLinearDepthImage, writeColorBeforeTrans };
        _device.Vk.UpdateDescriptorSets(_device.Device, 14, writes, 0, null);

        if (_materialBuffer != null)
        {
            DescriptorBufferInfo matBufferInfo = new() { Buffer = _materialBuffer.Buffer, Offset = 0, Range = Vk.WholeSize };
            WriteDescriptorSet writeMatData = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 5, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &matBufferInfo };
            _device.Vk.UpdateDescriptorSets(_device.Device, 1, &writeMatData, 0, null);
        }

        if (_currentTextureArray is VulkanTextureArray vkTexArray)
        {
            DescriptorImageInfo texArrayInfo = new() { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = vkTexArray.ImageView, Sampler = vkTexArray.Sampler };
            WriteDescriptorSet writeTex = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 3, DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &texArrayInfo };
            _device.Vk.UpdateDescriptorSets(_device.Device, 1, &writeTex, 0, null);
        }
    }

    private void RecordCommandBuffer(CommandBuffer cmd, uint imageIndex, in CameraData cameraData)
    {
        if (StreamlineAPI.slPCLSetMarker != null && _currentFrameToken != null)
        {
            StreamlineAPI.slPCLSetMarker(PCLMarker.eRenderSubmitStart, _currentFrameToken);
        }

        if (_drawCallCount == 0)
        {
            TransitionImageLayout(cmd, _swapchain.Images[imageIndex], ImageLayout.Undefined, ImageLayout.PresentSrcKhr, AccessFlags2.None, AccessFlags2.None, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.BottomOfPipeBit);
            return;
        }

        BuildTLAS(cmd);
        UpdateDescriptors();

        TransitionImageLayout(cmd, _renderStorageImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);

        TransitionImageLayout(cmd, _noisyColorImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
        TransitionImageLayout(cmd, _normalImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
        TransitionImageLayout(cmd, _roughnessImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
        TransitionImageLayout(cmd, _albedoImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
        TransitionImageLayout(cmd, _specularAlbedoImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
        TransitionImageLayout(cmd, _motionVectorsImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
        TransitionImageLayout(cmd, _depthImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
        TransitionImageLayout(cmd, _linearDepthImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
        TransitionImageLayout(cmd, _colorBeforeTransparencyImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
        TransitionImageLayout(cmd, _specularMotionVectorsImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
        TransitionImageLayout(cmd, _specularHitDistanceImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);

        TransitionImageLayout(cmd, _exposureImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.TransferWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.TransferBit);
        ClearColorImage(cmd, _exposureImage, 1.0f, 1.0f, 1.0f, 1.0f);
        TransitionImageLayout(cmd, _exposureImage, ImageLayout.General, ImageLayout.General, AccessFlags2.TransferWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.TransferBit, PipelineStageFlags2.ComputeShaderBit);

        TransitionImageLayout(cmd, _biasColorImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.TransferWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.TransferBit);
        ClearColorImage(cmd, _biasColorImage, 1.0f, 1.0f, 1.0f, 1.0f);
        TransitionImageLayout(cmd, _biasColorImage, ImageLayout.General, ImageLayout.General, AccessFlags2.TransferWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.TransferBit, PipelineStageFlags2.ComputeShaderBit);

        _device.Vk.CmdBindPipeline(cmd, PipelineBindPoint.RayTracingKhr, _pipeline!.Pipeline);
        var descSet = _descriptorSets[_currentFrame];
        _device.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.RayTracingKhr, _pipeline.PipelineLayout, 0, 1, &descSet, 0, null);

        var sbtProps = _pipeline.SbtProps;
        var raygenRegion = new StridedDeviceAddressRegionKHR { DeviceAddress = _pipeline.SbtBuffer.DeviceAddress, Stride = sbtProps.RegionAligned, Size = sbtProps.RegionAligned };
        var missRegion = new StridedDeviceAddressRegionKHR { DeviceAddress = _pipeline.SbtBuffer.DeviceAddress + sbtProps.RegionAligned, Stride = sbtProps.RegionAligned, Size = sbtProps.RegionAligned };
        var hitRegion = new StridedDeviceAddressRegionKHR { DeviceAddress = _pipeline.SbtBuffer.DeviceAddress + 2 * sbtProps.RegionAligned, Stride = sbtProps.RegionAligned, Size = sbtProps.RegionAligned };
        var callRegion = new StridedDeviceAddressRegionKHR { };

        _device.KhrRayTracingPipeline.CmdTraceRays(cmd, &raygenRegion, &missRegion, &hitRegion, &callRegion, (uint)_renderSize.X, (uint)_renderSize.Y, 1);

        if (_useDLSS_RR || _useDLSS_SR)
        {
            // Синхронизация записи G-буферов из Ray Tracing для последующего чтения в Compute (DLSS)
            TransitionImageLayout(cmd, _noisyColorImage, ImageLayout.General, ImageLayout.General, AccessFlags2.ShaderWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.ComputeShaderBit);
            TransitionImageLayout(cmd, _normalImage, ImageLayout.General, ImageLayout.General, AccessFlags2.ShaderWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.ComputeShaderBit);
            TransitionImageLayout(cmd, _roughnessImage, ImageLayout.General, ImageLayout.General, AccessFlags2.ShaderWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.ComputeShaderBit);
            TransitionImageLayout(cmd, _albedoImage, ImageLayout.General, ImageLayout.General, AccessFlags2.ShaderWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.ComputeShaderBit);
            TransitionImageLayout(cmd, _specularAlbedoImage, ImageLayout.General, ImageLayout.General, AccessFlags2.ShaderWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.ComputeShaderBit);
            TransitionImageLayout(cmd, _motionVectorsImage, ImageLayout.General, ImageLayout.General, AccessFlags2.ShaderWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.ComputeShaderBit);
            TransitionImageLayout(cmd, _depthImage, ImageLayout.General, ImageLayout.General, AccessFlags2.ShaderWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.ComputeShaderBit);
            TransitionImageLayout(cmd, _linearDepthImage, ImageLayout.General, ImageLayout.General, AccessFlags2.ShaderWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.ComputeShaderBit);
            TransitionImageLayout(cmd, _colorBeforeTransparencyImage, ImageLayout.General, ImageLayout.General, AccessFlags2.ShaderWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.ComputeShaderBit);
            TransitionImageLayout(cmd, _specularMotionVectorsImage, ImageLayout.General, ImageLayout.General, AccessFlags2.ShaderWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.ComputeShaderBit);
            TransitionImageLayout(cmd, _specularHitDistanceImage, ImageLayout.General, ImageLayout.General, AccessFlags2.ShaderWriteBit, AccessFlags2.ShaderReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.ComputeShaderBit);

            EvaluateStreamlineFeatures(cmd, in cameraData);
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

        TransitionImageLayout(cmd, _swapchain.Images[imageIndex], ImageLayout.Undefined, ImageLayout.TransferDstOptimal, AccessFlags2.None, AccessFlags2.TransferWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.TransferBit);

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

        TransitionImageLayout(cmd, _swapchain.Images[imageIndex], ImageLayout.TransferDstOptimal, ImageLayout.PresentSrcKhr, AccessFlags2.TransferWriteBit, AccessFlags2.None, PipelineStageFlags2.TransferBit, PipelineStageFlags2.BottomOfPipeBit);
    }

    private void EvaluateStreamlineFeatures(CommandBuffer cmd, in CameraData cameraData)
    {
        // Streamline layout transitions are handled by Streamline, but output image _storageImage needs to be in General
        TransitionImageLayout(cmd, _storageImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.ComputeShaderBit);

        FrameToken* frameToken = _currentFrameToken;
        var viewport = _slViewport;

        var originalView = Matrix4x4.CreateLookAt(cameraData.LocalPosition, cameraData.LocalPosition + cameraData.CameraFwd, cameraData.CameraUp);
        float aspect = _framebufferSize.X / (float)Math.Max(1, _framebufferSize.Y);
        var originalProj = Matrix4x4.CreatePerspectiveFieldOfView(float.Pi / 2.5f, aspect, 0.1f, 3000f);
        originalProj.M33 = -originalProj.M33 - 1.0f;
        originalProj.M43 = -originalProj.M43;
        originalProj.M22 *= -1;

        var view = originalView;
        var proj = originalProj;
        Matrix4x4.Invert(view, out var viewInverse);

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
        consts.DepthInverted = SlBoolean.eTrue;
        consts.CameraMotionIncluded = SlBoolean.eTrue;
        consts.MotionVectors3D = SlBoolean.eFalse;
        consts.Reset = (cameraData.FrameCount == 1) ? SlBoolean.eTrue : SlBoolean.eFalse;

        StreamlineAPI.slSetConstants(&consts, frameToken, &viewport);

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

        var resNormal = new Resource(ResourceType.eTex2d, (void*)_normalImage.Handle, (void*)_normalImageMemory.Handle, (void*)_normalImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R16G16B16A16Sfloat, gBufferUsage);
        var tagNormals = new ResourceTag(&resNormal, BufferType.kBufferTypeNormals, ResourceLifecycle.eValidUntilEvaluate, extentIn);

        var resRoughness = new Resource(ResourceType.eTex2d, (void*)_roughnessImage.Handle, (void*)_roughnessImageMemory.Handle, (void*)_roughnessImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R16Sfloat, gBufferUsage);
        var tagRoughness = new ResourceTag(&resRoughness, BufferType.kBufferTypeRoughness, ResourceLifecycle.eValidUntilEvaluate, extentIn);

        var resMvec = new Resource(ResourceType.eTex2d, (void*)_motionVectorsImage.Handle, (void*)_motionVectorsImageMemory.Handle, (void*)_motionVectorsImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R16G16Sfloat, gBufferUsage);
        var tagMvec = new ResourceTag(&resMvec, BufferType.kBufferTypeMotionVectors, ResourceLifecycle.eValidUntilEvaluate, extentIn);

        var resDepth = new Resource(ResourceType.eTex2d, (void*)_depthImage.Handle, (void*)_depthImageMemory.Handle, (void*)_depthImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R32Sfloat, gBufferUsage);
        var tagDepthStandard = new ResourceTag(&resDepth, BufferType.kBufferTypeDepth, ResourceLifecycle.eValidUntilEvaluate, extentIn);

        var resLinearDepth = new Resource(ResourceType.eTex2d, (void*)_linearDepthImage.Handle, (void*)_linearDepthImageMemory.Handle, (void*)_linearDepthImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R32Sfloat, gBufferUsage);
        var tagDepthLinear = new ResourceTag(&resLinearDepth, BufferType.kBufferTypeLinearDepth, ResourceLifecycle.eValidUntilEvaluate, extentIn);

        var resColorBeforeTrans = new Resource(ResourceType.eTex2d, (void*)_colorBeforeTransparencyImage.Handle, (void*)_colorBeforeTransparencyImageMemory.Handle, (void*)_colorBeforeTransparencyImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R16G16B16A16Sfloat, gBufferUsage);
        var tagColorBeforeTrans = new ResourceTag(&resColorBeforeTrans, BufferType.kBufferTypeColorBeforeTransparency, ResourceLifecycle.eValidUntilEvaluate, extentIn);

        var resOut = new Resource(ResourceType.eTex2d, (void*)_storageImage.Handle, (void*)_storageImageMemory.Handle, (void*)_storageImageView.Handle, (uint)ImageLayout.General, (uint)_framebufferSize.X, (uint)_framebufferSize.Y, (uint)Format.R16G16B16A16Sfloat, outUsage);
        var tagOut = new ResourceTag(&resOut, BufferType.kBufferTypeScalingOutputColor, ResourceLifecycle.eValidUntilEvaluate, extentOut);

        var resSpecularMvec = new Resource(ResourceType.eTex2d, (void*)_specularMotionVectorsImage.Handle, (void*)_specularMotionVectorsImageMemory.Handle, (void*)_specularMotionVectorsImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R16G16Sfloat, gBufferUsage);
        var tagSpecularMvec = new ResourceTag(&resSpecularMvec, BufferType.kBufferTypeSpecularMotionVectors, ResourceLifecycle.eValidUntilEvaluate, extentIn);

        var tagColor = new ResourceTag(&resNoisy, BufferType.kBufferTypeHUDLessColor, ResourceLifecycle.eValidUntilEvaluate, extentIn);
        var tagBaseColor = new ResourceTag(&resAlbedo, BufferType.kBufferTypeOpaqueColor, ResourceLifecycle.eValidUntilEvaluate, extentIn);

        var extentExposure = new Extent(1, 1);
        var resExposure = new Resource(ResourceType.eTex2d, (void*)_exposureImage.Handle, (void*)_exposureImageMemory.Handle, (void*)_exposureImageView.Handle, (uint)ImageLayout.General, 1, 1, (uint)Format.R32Sfloat, gBufferUsage);
        var tagExposure = new ResourceTag(&resExposure, BufferType.kBufferTypeExposure, ResourceLifecycle.eValidUntilEvaluate, extentExposure);

        var resBiasColor = new Resource(ResourceType.eTex2d, (void*)_biasColorImage.Handle, (void*)_biasColorImageMemory.Handle, (void*)_biasColorImageView.Handle, (uint)ImageLayout.General, 1, 1, (uint)Format.R32Sfloat, gBufferUsage);
        var tagBiasColor = new ResourceTag(&resBiasColor, BufferType.kBufferTypeBiasCurrentColorHint, ResourceLifecycle.eValidUntilEvaluate, extentExposure);

        var resSpecularHitDistance = new Resource(ResourceType.eTex2d, (void*)_specularHitDistanceImage.Handle, (void*)_specularHitDistanceImageMemory.Handle, (void*)_specularHitDistanceImageView.Handle, (uint)ImageLayout.General, (uint)_renderSize.X, (uint)_renderSize.Y, (uint)Format.R16Sfloat, gBufferUsage);
        var tagSpecularHitDistance = new ResourceTag(&resSpecularHitDistance, BufferType.kBufferTypeSpecularHitDistance, ResourceLifecycle.eValidUntilEvaluate, extentIn);

        if (_useDLSS_RR)
        {
            // Update options for RR
            var opt = DLSSDOptions.Create();
            opt.Mode = DLSSMode.eMaxQuality;
            opt.QualityPreset = DLSSDPreset.ePresetE;
            opt.OutputWidth = (uint)_framebufferSize.X;
            opt.OutputHeight = (uint)_framebufferSize.Y;
            opt.NormalRoughnessMode = DLSSDNormalRoughnessMode.eUnpacked;
            opt.WorldToCameraView = view;
            opt.CameraViewToWorld = viewInverse;
            opt.ColorBuffersHDR = SlBoolean.eTrue;

            StreamlineAPI.slDLSSDSetOptions(&viewport, &opt);

            ResourceTag* pTags = stackalloc ResourceTag[16];
            pTags[0] = tagNoisy;
            pTags[1] = tagColor;
            pTags[2] = tagAlbedo;
            pTags[3] = tagSpecAlbedo;
            pTags[4] = tagNormals;
            pTags[5] = tagRoughness;
            pTags[6] = tagMvec;
            pTags[7] = tagDepthLinear;
            pTags[8] = tagDepthStandard;
            pTags[9] = tagOut;
            pTags[10] = tagSpecularMvec;
            pTags[11] = tagExposure;
            pTags[12] = tagBiasColor;
            pTags[13] = tagBaseColor;
            pTags[14] = tagSpecularHitDistance;
            pTags[15] = tagColorBeforeTrans;

            StreamlineAPI.slSetTagForFrame(frameToken, &viewport, pTags, 16, (void*)cmd.Handle);

            void* inputViewport = &viewport;
            int evalRes = StreamlineAPI.slEvaluateFeature((uint)Feature.kFeatureDLSS_RR, frameToken, &inputViewport, 1, (void*)cmd.Handle);
            if (evalRes != (int)SlResult.eOk)
            {
                LogDlssRrEvalError(evalRes);
            }
        }
        else if (_useDLSS_SR)
        {
            // Update options for SR
            var dlssOpt = DLSSOptions.Create();
            dlssOpt.Mode = DLSSMode.eMaxQuality;
            dlssOpt.QualityPreset = DLSSPreset.ePresetK;
            dlssOpt.OutputWidth = (uint)_framebufferSize.X;
            dlssOpt.OutputHeight = (uint)_framebufferSize.Y;
            dlssOpt.ColorBuffersHDR = SlBoolean.eTrue;
            dlssOpt.UseAutoExposure = SlBoolean.eTrue;

            StreamlineAPI.slDLSSSetOptions(&viewport, &dlssOpt);

            ResourceTag* pTags = stackalloc ResourceTag[10];
            pTags[0] = tagNoisy;
            pTags[1] = tagColor;
            pTags[2] = tagMvec;
            pTags[3] = tagDepthStandard;
            pTags[4] = tagOut;
            pTags[5] = tagExposure;
            pTags[6] = tagBiasColor;
            pTags[7] = tagSpecularMvec;
            pTags[8] = tagSpecularHitDistance;
            pTags[9] = tagColorBeforeTrans;

            StreamlineAPI.slSetTagForFrame(frameToken, &viewport, pTags, 10, (void*)cmd.Handle);

            void* inputViewport = &viewport;
            int evalRes = StreamlineAPI.slEvaluateFeature((uint)Feature.kFeatureDLSS, frameToken, &inputViewport, 1, (void*)cmd.Handle);
            if (evalRes != (int)SlResult.eOk)
            {
                LogDlssSrEvalError(evalRes);
            }
        }

        TransitionImageLayout(cmd, _storageImage, ImageLayout.General, ImageLayout.TransferSrcOptimal, AccessFlags2.ShaderWriteBit, AccessFlags2.TransferReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.TransferBit);
    }

    private void SubmitAndPresent(CommandBuffer cmd, uint imageIndex)
    {
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

        Result result;
        if (_useDLSS_RR || _useDLSS_SR)
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

    private void ClearColorImage(CommandBuffer cmd, Image image, float r, float g, float b, float a)
    {
        var clearColor = new ClearColorValue();
        unsafe
        {
            float* pFloat = (float*)&clearColor;
            pFloat[0] = r;
            pFloat[1] = g;
            pFloat[2] = b;
            pFloat[3] = a;
        }
        var range = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1);
        _device.Vk.CmdClearColorImage(cmd, image, ImageLayout.General, in clearColor, 1, in range);
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

        foreach (var sem in _renderFinishedSemaphores)
        {
            if (sem.Handle != 0) _device.Vk.DestroySemaphore(_device.Device, sem, null);
        }

        _meshPool?.Dispose();

        DestroyImageHelper(_storageImage, _storageImageMemory, _storageImageView);
        DestroyImageHelper(_renderStorageImage, _renderStorageImageMemory, _renderStorageImageView);

        DestroyImageHelper(_noisyColorImage, _noisyColorImageMemory, _noisyColorImageView);
        DestroyImageHelper(_normalImage, _normalImageMemory, _normalImageView);
        DestroyImageHelper(_roughnessImage, _roughnessImageMemory, _roughnessImageView);
        DestroyImageHelper(_albedoImage, _albedoImageMemory, _albedoImageView);
        DestroyImageHelper(_specularAlbedoImage, _specularAlbedoImageMemory, _specularAlbedoImageView);
        DestroyImageHelper(_motionVectorsImage, _motionVectorsImageMemory, _motionVectorsImageView);
        DestroyImageHelper(_depthImage, _depthImageMemory, _depthImageView);
        DestroyImageHelper(_linearDepthImage, _linearDepthImageMemory, _linearDepthImageView);
        DestroyImageHelper(_colorBeforeTransparencyImage, _colorBeforeTransparencyImageMemory, _colorBeforeTransparencyImageView);
        DestroyImageHelper(_specularMotionVectorsImage, _specularMotionVectorsImageMemory, _specularMotionVectorsImageView);
        DestroyImageHelper(_exposureImage, _exposureImageMemory, _exposureImageView);
        DestroyImageHelper(_biasColorImage, _biasColorImageMemory, _biasColorImageView);
        DestroyImageHelper(_specularHitDistanceImage, _specularHitDistanceImageMemory, _specularHitDistanceImageView);

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
        if (_currentFrameToken != null) return;

        if (_useDLSS_RR || _useDLSS_SR || _useReflex)
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