using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Graphics.Vulkan.Core;
using Minecraft.NET.Utils.Collections;
using Minecraft.NET.Utils.Math;

using Silk.NET.Vulkan;

using Semaphore = Silk.NET.Vulkan.Semaphore;

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
        public uint Pad1, Pad2;
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
    private readonly Semaphore[] _renderFinishedSemaphores = new Semaphore[MaxFramesInFlight];
    private readonly Fence[] _inFlightFences = new Fence[MaxFramesInFlight];

    private readonly VulkanBuffer[] _cameraBuffers = new VulkanBuffer[MaxFramesInFlight];
    private DescriptorPool _descriptorPool;
    private readonly DescriptorSet[] _descriptorSets = new DescriptorSet[MaxFramesInFlight];

    private Vector2Int _framebufferSize;
    private bool _framebufferResized = false;

    private Image _storageImage;
    private DeviceMemory _storageImageMemory;
    private ImageView _storageImageView;

    private Image _accumulationImage;
    private DeviceMemory _accumulationImageMemory;
    private ImageView _accumulationImageView;
    
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

    public VulkanRenderPipeline(IWindow window)
    {
        _framebufferSize = window.FramebufferSize;
        _device = new VulkanDevice(window.Handle);
        _swapchain = new VulkanSwapchain(_device, _framebufferSize);

        for (int i = 0; i < MaxFramesInFlight; i++) _meshesToDispose[i] = [];

        CreateCommandPool();
        CreateCommandBuffers();
        CreateSyncObjects();
    }

    public void Initialize(VertexElement[] layout, uint stride)
    {
        _pipeline = new VulkanRayTracingPipeline(_device);
        CreateStorageImage();
        CreateDescriptorPoolAndSets();
        _meshPool = new DynamicMeshPool(_device);
    }

    private void CreateStorageImage()
    {
        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo, ImageType = ImageType.Type2D,
            Format = Format.R8G8B8A8Unorm,
            Extent = new Extent3D((uint)_framebufferSize.X, (uint)_framebufferSize.Y, 1),
            MipLevels = 1, ArrayLayers = 1, Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal, Usage = ImageUsageFlags.StorageBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit,
            InitialLayout = ImageLayout.Undefined
        };

        _device.Vk.CreateImage(_device.Device, in imageInfo, null, out _storageImage);
        _device.Vk.GetImageMemoryRequirements(_device.Device, _storageImage, out var memReqs);

        MemoryAllocateInfo allocInfo = new() { SType = StructureType.MemoryAllocateInfo, AllocationSize = memReqs.Size, MemoryTypeIndex = _device.FindMemoryType(memReqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit) };
        _device.Vk.AllocateMemory(_device.Device, in allocInfo, null, out _storageImageMemory);
        _device.Vk.BindImageMemory(_device.Device, _storageImage, _storageImageMemory, 0);

        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo, Image = _storageImage, ViewType = ImageViewType.Type2D,
            Format = Format.R8G8B8A8Unorm,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _device.Vk.CreateImageView(_device.Device, in viewInfo, null, out _storageImageView);

        ImageCreateInfo accumInfo = imageInfo;
        accumInfo.Format = Format.R32G32B32A32Sfloat;
        accumInfo.Usage = ImageUsageFlags.StorageBit;
        _device.Vk.CreateImage(_device.Device, in accumInfo, null, out _accumulationImage);
        _device.Vk.GetImageMemoryRequirements(_device.Device, _accumulationImage, out memReqs);

        allocInfo.AllocationSize = memReqs.Size;
        allocInfo.MemoryTypeIndex = _device.FindMemoryType(memReqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit);
        _device.Vk.AllocateMemory(_device.Device, in allocInfo, null, out _accumulationImageMemory);
        _device.Vk.BindImageMemory(_device.Device, _accumulationImage, _accumulationImageMemory, 0);

        ImageViewCreateInfo accumViewInfo = viewInfo;
        accumViewInfo.Image = _accumulationImage;
        accumViewInfo.Format = Format.R32G32B32A32Sfloat;
        _device.Vk.CreateImageView(_device.Device, in accumViewInfo, null, out _accumulationImageView);
    }

    public IMesh CreateMesh<T>(NativeList<T> vertices, NativeList<uint> indices) where T : unmanaged
        => _meshPool.Allocate(vertices, indices);

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
            _device.Vk.CreateSemaphore(_device.Device, in semaphoreInfo, null, out _renderFinishedSemaphores[i]);
            _device.Vk.CreateFence(_device.Device, in fenceInfo, null, out _inFlightFences[i]);
        }
    }

    private void CreateDescriptorPoolAndSets()
    {
        DescriptorPoolSize[] poolSizes = [
            new() { Type = DescriptorType.AccelerationStructureKhr, DescriptorCount = MaxFramesInFlight },
            new() { Type = DescriptorType.StorageImage, DescriptorCount = MaxFramesInFlight * 2 },
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

        _device.Vk.DestroyImageView(_device.Device, _storageImageView, null);
        _device.Vk.DestroyImage(_device.Device, _storageImage, null);
        _device.Vk.FreeMemory(_device.Device, _storageImageMemory, null);

        _device.Vk.DestroyImageView(_device.Device, _accumulationImageView, null);
        _device.Vk.DestroyImage(_device.Device, _accumulationImage, null);
        _device.Vk.FreeMemory(_device.Device, _accumulationImageMemory, null);

        _swapchain.Dispose();
        _swapchain = new VulkanSwapchain(_device, _framebufferSize);

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

        if (_framebufferSize.X == 0 || _framebufferSize.Y == 0) return;

        if (_framebufferResized) { _framebufferResized = false; RecreateSwapchain(); return; }

        uint imageIndex;
        var result = _device.KhrSwapchain.AcquireNextImage(_device.Device, _swapchain.Swapchain, ulong.MaxValue, _imageAvailableSemaphores[_currentFrame], default, &imageIndex);

        if (result == Result.ErrorOutOfDateKhr) { RecreateSwapchain(); return; }

        _cameraBuffers[_currentFrame].UpdateData(in cameraData);
        _device.Vk.ResetFences(_device.Device, 1, ref _inFlightFences[_currentFrame]);

        CommandBuffer cmd = _commandBuffers[_currentFrame];
        _device.Vk.ResetCommandBuffer(cmd, 0);

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        _device.Vk.BeginCommandBuffer(cmd, in beginInfo);

        if (_drawCallCount > 0)
        {
            int requiredCapacity = Math.Max(128, _drawCallCount);

            if (_tlasCapacities[_currentFrame] < _drawCallCount)
            {
                requiredCapacity = Math.Max(_tlasCapacities[_currentFrame] * 2, requiredCapacity);

                _instancesBuffers[_currentFrame]?.Dispose();
                _instanceDataBuffers[_currentFrame]?.Dispose();
                _tlasScratchBuffers[_currentFrame]?.Dispose();
                _tlasBuffers[_currentFrame]?.Dispose();

                if (_tlasHandles[_currentFrame].Handle != 0) _device.KhrAccelerationStructure.DestroyAccelerationStructure(_device.Device, _tlasHandles[_currentFrame], null);

                _instancesBuffers[_currentFrame] = new VulkanBuffer(_device, (ulong)(requiredCapacity * sizeof(AccelerationStructureInstanceKHR)), BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
                _instanceDataBuffers[_currentFrame] = new VulkanBuffer(_device, (ulong)(requiredCapacity * sizeof(InstanceData)), BufferUsageFlags.StorageBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

                var instancesDataInfo = new AccelerationStructureGeometryInstancesDataKHR { SType = StructureType.AccelerationStructureGeometryInstancesDataKhr, ArrayOfPointers = Vk.False, Data = new DeviceOrHostAddressConstKHR { DeviceAddress = _instancesBuffers[_currentFrame].DeviceAddress } };
                var geometryInfo = new AccelerationStructureGeometryKHR { SType = StructureType.AccelerationStructureGeometryKhr, GeometryType = GeometryTypeKHR.InstancesKhr, Geometry = new AccelerationStructureGeometryDataKHR { Instances = instancesDataInfo } };
                var buildInfoSize = new AccelerationStructureBuildGeometryInfoKHR { SType = StructureType.AccelerationStructureBuildGeometryInfoKhr, Type = AccelerationStructureTypeKHR.TopLevelKhr, Flags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr, GeometryCount = 1, PGeometries = &geometryInfo };

                uint maxInstanceCount = (uint)requiredCapacity;
                _device.KhrAccelerationStructure.GetAccelerationStructureBuildSizes(_device.Device, AccelerationStructureBuildTypeKHR.DeviceKhr, in buildInfoSize, &maxInstanceCount, out var buildSizes);

                _tlasBuffers[_currentFrame] = new VulkanBuffer(_device, buildSizes.AccelerationStructureSize, BufferUsageFlags.AccelerationStructureStorageBitKhr, MemoryPropertyFlags.DeviceLocalBit);
                var createInfo = new AccelerationStructureCreateInfoKHR { SType = StructureType.AccelerationStructureCreateInfoKhr, Buffer = _tlasBuffers[_currentFrame].Buffer, Size = buildSizes.AccelerationStructureSize, Type = AccelerationStructureTypeKHR.TopLevelKhr };
                _device.KhrAccelerationStructure.CreateAccelerationStructure(_device.Device, in createInfo, null, out _tlasHandles[_currentFrame]);

                _tlasScratchBuffers[_currentFrame] = new VulkanBuffer(_device, buildSizes.BuildScratchSize, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit, MemoryPropertyFlags.DeviceLocalBit);
                _tlasCapacities[_currentFrame] = requiredCapacity;
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
                    VertexAddress = alloc.VertexAddress,
                    IndexAddress = alloc.IndexAddress
                };
            }

            var instancesData = new AccelerationStructureGeometryInstancesDataKHR { SType = StructureType.AccelerationStructureGeometryInstancesDataKhr, ArrayOfPointers = Vk.False, Data = new DeviceOrHostAddressConstKHR { DeviceAddress = _instancesBuffers[_currentFrame].DeviceAddress } };
            var geometry = new AccelerationStructureGeometryKHR { SType = StructureType.AccelerationStructureGeometryKhr, GeometryType = GeometryTypeKHR.InstancesKhr, Geometry = new AccelerationStructureGeometryDataKHR { Instances = instancesData } };

            var buildInfo = new AccelerationStructureBuildGeometryInfoKHR { SType = StructureType.AccelerationStructureBuildGeometryInfoKhr, Type = AccelerationStructureTypeKHR.TopLevelKhr, Flags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr, GeometryCount = 1, PGeometries = &geometry, Mode = BuildAccelerationStructureModeKHR.BuildKhr, DstAccelerationStructure = _tlasHandles[_currentFrame], ScratchData = new DeviceOrHostAddressKHR { DeviceAddress = _tlasScratchBuffers[_currentFrame].DeviceAddress } };
            var buildRange = new AccelerationStructureBuildRangeInfoKHR { PrimitiveCount = (uint)_drawCallCount, PrimitiveOffset = 0, FirstVertex = 0, TransformOffset = 0 };
            var pBuildRange = &buildRange;

            _device.KhrAccelerationStructure.CmdBuildAccelerationStructures(cmd, 1, in buildInfo, &pBuildRange);

            var buildBarrier = new MemoryBarrier2 { SType = StructureType.MemoryBarrier2, SrcStageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr, SrcAccessMask = AccessFlags2.AccelerationStructureWriteBitKhr, DstStageMask = PipelineStageFlags2.RayTracingShaderBitKhr, DstAccessMask = AccessFlags2.AccelerationStructureReadBitKhr };
            var depInfo1 = new DependencyInfo { SType = StructureType.DependencyInfo, MemoryBarrierCount = 1, PMemoryBarriers = &buildBarrier };
            _device.Vk.CmdPipelineBarrier2(cmd, in depInfo1);

            AccelerationStructureKHR tlasHandleForWrite = _tlasHandles[_currentFrame];

            WriteDescriptorSetAccelerationStructureKHR descriptorAS = new() { SType = StructureType.WriteDescriptorSetAccelerationStructureKhr, AccelerationStructureCount = 1, PAccelerationStructures = &tlasHandleForWrite };
            WriteDescriptorSet writeAS = new() { SType = StructureType.WriteDescriptorSet, PNext = &descriptorAS, DstSet = _descriptorSets[_currentFrame], DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.AccelerationStructureKhr };

            DescriptorImageInfo storageImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _storageImageView };
            WriteDescriptorSet writeStorageImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 1, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &storageImageInfo };

            DescriptorImageInfo accumImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _accumulationImageView };
            WriteDescriptorSet writeAccumImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 5, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &accumImageInfo };

            DescriptorBufferInfo instanceDataInfo = new() { Buffer = _instanceDataBuffers[_currentFrame].Buffer, Offset = 0, Range = Vk.WholeSize };
            WriteDescriptorSet writeInstanceData = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 4, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &instanceDataInfo };

            var writes = stackalloc WriteDescriptorSet[4] { writeAS, writeStorageImage, writeAccumImage, writeInstanceData };
            _device.Vk.UpdateDescriptorSets(_device.Device, 4, writes, 0, null);

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

            TransitionImageLayout(cmd, _storageImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
            
            if (_frameCount == 1)
                TransitionImageLayout(cmd, _accumulationImage, ImageLayout.Undefined, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit | AccessFlags2.ShaderReadBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);
            else
                TransitionImageLayout(cmd, _accumulationImage, ImageLayout.General, ImageLayout.General, AccessFlags2.None, AccessFlags2.ShaderWriteBit | AccessFlags2.ShaderReadBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.RayTracingShaderBitKhr);

            _device.Vk.CmdBindPipeline(cmd, PipelineBindPoint.RayTracingKhr, _pipeline.Pipeline);
            var descSet = _descriptorSets[_currentFrame];
            _device.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.RayTracingKhr, _pipeline.PipelineLayout, 0, 1, &descSet, 0, null);

            var sbtProps = _pipeline.SbtProps;
            var raygenRegion = new StridedDeviceAddressRegionKHR { DeviceAddress = _pipeline.SbtBuffer.DeviceAddress, Stride = sbtProps.RegionAligned, Size = sbtProps.RegionAligned };
            var missRegion = new StridedDeviceAddressRegionKHR { DeviceAddress = _pipeline.SbtBuffer.DeviceAddress + sbtProps.RegionAligned, Stride = sbtProps.RegionAligned, Size = sbtProps.RegionAligned };
            var hitRegion = new StridedDeviceAddressRegionKHR { DeviceAddress = _pipeline.SbtBuffer.DeviceAddress + 2 * sbtProps.RegionAligned, Stride = sbtProps.RegionAligned, Size = sbtProps.RegionAligned };
            var callRegion = new StridedDeviceAddressRegionKHR { };

            _device.KhrRayTracingPipeline.CmdTraceRays(cmd, &raygenRegion, &missRegion, &hitRegion, &callRegion, (uint)_framebufferSize.X, (uint)_framebufferSize.Y, 1);

            TransitionImageLayout(cmd, _storageImage, ImageLayout.General, ImageLayout.TransferSrcOptimal, AccessFlags2.ShaderWriteBit, AccessFlags2.TransferReadBit, PipelineStageFlags2.RayTracingShaderBitKhr, PipelineStageFlags2.TransferBit);
        }

        TransitionImageLayout(cmd, _swapchain.Images[imageIndex], ImageLayout.Undefined, ImageLayout.TransferDstOptimal, AccessFlags2.None, AccessFlags2.TransferWriteBit, PipelineStageFlags2.TopOfPipeBit, PipelineStageFlags2.TransferBit);

        if (_drawCallCount > 0)
        {
            ImageCopy2 copy = new() { SType = StructureType.ImageCopy2, SrcSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1), DstSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1), Extent = new Extent3D((uint)_framebufferSize.X, (uint)_framebufferSize.Y, 1) };
            CopyImageInfo2 copyInfo = new() { SType = StructureType.CopyImageInfo2, SrcImage = _storageImage, SrcImageLayout = ImageLayout.TransferSrcOptimal, DstImage = _swapchain.Images[imageIndex], DstImageLayout = ImageLayout.TransferDstOptimal, RegionCount = 1, PRegions = &copy };
            _device.Vk.CmdCopyImage2(cmd, in copyInfo);
        }

        TransitionImageLayout(cmd, _swapchain.Images[imageIndex], ImageLayout.TransferDstOptimal, ImageLayout.PresentSrcKhr, AccessFlags2.TransferWriteBit, AccessFlags2.None, PipelineStageFlags2.TransferBit, PipelineStageFlags2.BottomOfPipeBit);

        _device.Vk.EndCommandBuffer(cmd);

        var waitInfo = new SemaphoreSubmitInfo { SType = StructureType.SemaphoreSubmitInfo, Semaphore = _imageAvailableSemaphores[_currentFrame], StageMask = PipelineStageFlags2.ColorAttachmentOutputBit };
        var signalInfo = new SemaphoreSubmitInfo { SType = StructureType.SemaphoreSubmitInfo, Semaphore = _renderFinishedSemaphores[_currentFrame], StageMask = PipelineStageFlags2.AllCommandsBit };
        var cmdInfo = new CommandBufferSubmitInfo { SType = StructureType.CommandBufferSubmitInfo, CommandBuffer = cmd };

        var submitInfo = new SubmitInfo2 { SType = StructureType.SubmitInfo2, WaitSemaphoreInfoCount = 1, PWaitSemaphoreInfos = &waitInfo, CommandBufferInfoCount = 1, PCommandBufferInfos = &cmdInfo, SignalSemaphoreInfoCount = 1, PSignalSemaphoreInfos = &signalInfo };

        lock (_device.QueueLock) _device.Vk.QueueSubmit2(_device.GraphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]);

        var swapchains = stackalloc[] { _swapchain.Swapchain };
        PresentInfoKHR presentInfo = new() { SType = StructureType.PresentInfoKhr, WaitSemaphoreCount = 1, PWaitSemaphores = (Semaphore*)Unsafe.AsPointer(ref _renderFinishedSemaphores[_currentFrame]), SwapchainCount = 1, PSwapchains = swapchains, PImageIndices = &imageIndex };

        lock (_device.QueueLock) result = _device.KhrSwapchain.QueuePresent(_device.PresentQueue, in presentInfo);

        if (result == Result.ErrorDeviceLost) throw new Exception("Критическая ошибка: Vulkan Device Lost (видеокарта перестала отвечать)!");
        if (result is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr) _framebufferResized = true;

        _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
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
            _device.Vk.DestroySemaphore(_device.Device, _renderFinishedSemaphores[i], null);
            _device.Vk.DestroyFence(_device.Device, _inFlightFences[i], null);
        }

        _meshPool?.Dispose();

        _device.Vk.DestroyImageView(_device.Device, _storageImageView, null);
        _device.Vk.DestroyImage(_device.Device, _storageImage, null);
        _device.Vk.FreeMemory(_device.Device, _storageImageMemory, null);

        _device.Vk.DestroyCommandPool(_device.Device, _commandPool, null);
        _pipeline?.Dispose();
        _swapchain.Dispose();
        _device.Dispose();
    }
}