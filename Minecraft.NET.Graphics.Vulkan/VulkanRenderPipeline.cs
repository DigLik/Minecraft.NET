using System.Collections.Concurrent;
using System.Runtime.InteropServices;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Graphics.Vulkan.Core;
using Minecraft.NET.Utils.Math;

using Silk.NET.Vulkan;

using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Minecraft.NET.Graphics.Vulkan;

public unsafe class VulkanRenderPipeline : IRenderPipeline
{
    private struct DrawCall
    {
        public IMesh Mesh;
        public Vector3<float> Position;
    }

    public struct InstanceData
    {
        public uint VertexOffset;
        public uint IndexOffset;
        public uint Pad1, Pad2;
    }

    private const int MaxFramesInFlight = 2;
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

    private Vector2<int> _framebufferSize;
    private bool _framebufferResized = false;

    private Image _storageImage;
    private DeviceMemory _storageImageMemory;
    private ImageView _storageImageView;

    private readonly List<DrawCall> _drawCalls = [];
    private ITextureArray? _currentTextureArray;

    private DynamicMeshPool _meshPool = null!;

    private readonly ConcurrentQueue<IMesh> _pendingMeshesToDispose = new();
    private readonly List<IMesh>[] _meshesToDispose = new List<IMesh>[MaxFramesInFlight];
    private readonly List<IDisposable>[] _frameResources = new List<IDisposable>[MaxFramesInFlight];
    private readonly List<AccelerationStructureKHR>[] _frameAS = new List<AccelerationStructureKHR>[MaxFramesInFlight];

    public VulkanRenderPipeline(IWindow window)
    {
        _framebufferSize = window.FramebufferSize;
        _device = new VulkanDevice(window.Handle);
        _swapchain = new VulkanSwapchain(_device, _framebufferSize);

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _meshesToDispose[i] = [];
            _frameResources[i] = [];
            _frameAS[i] = [];
        }

        CreateCommandPool();
        CreateCommandBuffers();
        CreateSyncObjects();
    }

    public void Initialize(VertexElement[] layout, uint stride)
    {
        _pipeline = new VulkanRayTracingPipeline(_device);
        CreateStorageImage();
        CreateDescriptorPoolAndSets();
        _meshPool = new DynamicMeshPool(_device, MaxFramesInFlight);
    }

    private void CreateStorageImage()
    {
        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo, ImageType = ImageType.Type2D, Format = Format.B8G8R8A8Unorm,
            Extent = new Extent3D((uint)_framebufferSize.X, (uint)_framebufferSize.Y, 1),
            MipLevels = 1, ArrayLayers = 1, Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal, Usage = ImageUsageFlags.StorageBit | ImageUsageFlags.TransferSrcBit,
            InitialLayout = ImageLayout.Undefined
        };
        _device.Vk.CreateImage(_device.Device, in imageInfo, null, out _storageImage);

        _device.Vk.GetImageMemoryRequirements(_device.Device, _storageImage, out var memReqs);
        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo, AllocationSize = memReqs.Size,
            MemoryTypeIndex = _device.FindMemoryType(memReqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };
        _device.Vk.AllocateMemory(_device.Device, in allocInfo, null, out _storageImageMemory);
        _device.Vk.BindImageMemory(_device.Device, _storageImage, _storageImageMemory, 0);

        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo, Image = _storageImage, ViewType = ImageViewType.Type2D, Format = Format.B8G8R8A8Unorm,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _device.Vk.CreateImageView(_device.Device, in viewInfo, null, out _storageImageView);
    }

    public IMesh CreateMesh<T>(T[] vertices, uint[] indices) where T : unmanaged => _meshPool.Allocate(vertices, indices);
    public void DeleteMesh(IMesh mesh) => _pendingMeshesToDispose.Enqueue(mesh);
    public ITextureArray CreateTextureArray(int width, int height, byte[][] pixels) => new VulkanTextureArray(_device, width, height, pixels);
    public void BindTextureArray(ITextureArray textureArray) => _currentTextureArray = textureArray;
    public void SubmitDraw(IMesh mesh, Vector3<float> position) => _drawCalls.Add(new DrawCall { Mesh = mesh, Position = position });
    public void ClearDraws() => _drawCalls.Clear();

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
            new() { Type = DescriptorType.StorageImage, DescriptorCount = MaxFramesInFlight },
            new() { Type = DescriptorType.UniformBuffer, DescriptorCount = MaxFramesInFlight },
            new() { Type = DescriptorType.CombinedImageSampler, DescriptorCount = MaxFramesInFlight },
            new() { Type = DescriptorType.StorageBuffer, DescriptorCount = MaxFramesInFlight * 3 }
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
        _swapchain.Dispose();

        _swapchain = new VulkanSwapchain(_device, _framebufferSize);
        CreateStorageImage();
    }

    public void RenderFrame(CameraData cameraData)
    {
        if (_pipeline == null) throw new Exception("Pipeline is not initialized.");

        _device.Vk.WaitForFences(_device.Device, 1, ref _inFlightFences[_currentFrame], Vk.True, ulong.MaxValue);

        foreach (var res in _frameResources[_currentFrame]) res.Dispose();
        _frameResources[_currentFrame].Clear();

        foreach (var asHandle in _frameAS[_currentFrame]) _device.KhrAccelerationStructure.DestroyAccelerationStructure(_device.Device, asHandle, null);
        _frameAS[_currentFrame].Clear();

        _meshPool.CleanupResources(_currentFrame);
        foreach (var mesh in _meshesToDispose[_currentFrame])
        {
            var alloc = (MeshAllocation)mesh;
            _device.KhrAccelerationStructure.DestroyAccelerationStructure(_device.Device, alloc.Blas, null);
            alloc.BlasBuffer?.Dispose();
            _meshPool.Free(alloc);
            mesh.Dispose();
        }
        _meshesToDispose[_currentFrame].Clear();
        while (_pendingMeshesToDispose.TryDequeue(out var mesh)) _meshesToDispose[_currentFrame].Add(mesh);

        if (_framebufferSize.X == 0 || _framebufferSize.Y == 0) return;
        if (_framebufferResized) { _framebufferResized = false; RecreateSwapchain(); return; }

        uint imageIndex;
        var result = _device.KhrSwapchain.AcquireNextImage(_device.Device, _swapchain.Swapchain, ulong.MaxValue, _imageAvailableSemaphores[_currentFrame], default, &imageIndex);
        if (result == Result.ErrorOutOfDateKhr) { RecreateSwapchain(); return; }

        _cameraBuffers[_currentFrame].UpdateData([cameraData]);

        _device.Vk.ResetFences(_device.Device, 1, ref _inFlightFences[_currentFrame]);
        CommandBuffer cmd = _commandBuffers[_currentFrame];
        _device.Vk.ResetCommandBuffer(cmd, 0);

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        _device.Vk.BeginCommandBuffer(cmd, in beginInfo);

        _meshPool.FlushUploads(cmd, _currentFrame);

        AccelerationStructureKHR tlasHandle = default;
        if (_drawCalls.Count > 0)
        {
            var instancesBuffer = new VulkanBuffer(_device, (ulong)(_drawCalls.Count * sizeof(AccelerationStructureInstanceKHR)), BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            var instanceDataBuffer = new VulkanBuffer(_device, (ulong)(_drawCalls.Count * sizeof(InstanceData)), BufferUsageFlags.StorageBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            _frameResources[_currentFrame].Add(instancesBuffer);
            _frameResources[_currentFrame].Add(instanceDataBuffer);

            var instSpan = new Span<AccelerationStructureInstanceKHR>(instancesBuffer.MappedMemory, _drawCalls.Count);
            var dataSpan = new Span<InstanceData>(instanceDataBuffer.MappedMemory, _drawCalls.Count);

            for (int i = 0; i < _drawCalls.Count; i++)
            {
                var alloc = (MeshAllocation)_drawCalls[i].Mesh;
                var pos = _drawCalls[i].Position;
                var tf = new TransformMatrixKHR();
                tf.Matrix[0] = 1; tf.Matrix[1] = 0; tf.Matrix[2] = 0; tf.Matrix[3] = pos.X;
                tf.Matrix[4] = 0; tf.Matrix[5] = 1; tf.Matrix[6] = 0; tf.Matrix[7] = pos.Y;
                tf.Matrix[8] = 0; tf.Matrix[9] = 0; tf.Matrix[10] = 1; tf.Matrix[11] = pos.Z;

                instSpan[i] = new AccelerationStructureInstanceKHR
                {
                    Transform = tf, InstanceCustomIndex = (uint)i, Mask = 0xFF, InstanceShaderBindingTableRecordOffset = 0,
                    Flags = GeometryInstanceFlagsKHR.ForceOpaqueBitKhr, AccelerationStructureReference = alloc.BlasDeviceAddress
                };
                dataSpan[i] = new InstanceData { VertexOffset = (uint)alloc.VertexOffset, IndexOffset = alloc.FirstIndex };
            }

            var instancesData = new AccelerationStructureGeometryInstancesDataKHR
            {
                SType = StructureType.AccelerationStructureGeometryInstancesDataKhr, ArrayOfPointers = Vk.False,
                Data = new DeviceOrHostAddressConstKHR { DeviceAddress = instancesBuffer.DeviceAddress }
            };

            var geometry = new AccelerationStructureGeometryKHR
            {
                SType = StructureType.AccelerationStructureGeometryKhr, GeometryType = GeometryTypeKHR.InstancesKhr,
                Geometry = new AccelerationStructureGeometryDataKHR { Instances = instancesData }
            };

            var buildInfo = new AccelerationStructureBuildGeometryInfoKHR
            {
                SType = StructureType.AccelerationStructureBuildGeometryInfoKhr, Type = AccelerationStructureTypeKHR.TopLevelKhr,
                Flags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr, GeometryCount = 1, PGeometries = &geometry
            };

            uint instanceCount = (uint)_drawCalls.Count;
            _device.KhrAccelerationStructure.GetAccelerationStructureBuildSizes(_device.Device, AccelerationStructureBuildTypeKHR.DeviceKhr, in buildInfo, &instanceCount, out var buildSizes);

            var tlasBuffer = new VulkanBuffer(_device, buildSizes.AccelerationStructureSize, BufferUsageFlags.AccelerationStructureStorageBitKhr, MemoryPropertyFlags.DeviceLocalBit);
            _frameResources[_currentFrame].Add(tlasBuffer);

            var createInfo = new AccelerationStructureCreateInfoKHR
            {
                SType = StructureType.AccelerationStructureCreateInfoKhr, Buffer = tlasBuffer.Buffer, Size = buildSizes.AccelerationStructureSize, Type = AccelerationStructureTypeKHR.TopLevelKhr
            };
            _device.KhrAccelerationStructure.CreateAccelerationStructure(_device.Device, in createInfo, null, out tlasHandle);
            _frameAS[_currentFrame].Add(tlasHandle);

            var scratchBuffer = new VulkanBuffer(_device, buildSizes.BuildScratchSize, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit, MemoryPropertyFlags.DeviceLocalBit);
            _frameResources[_currentFrame].Add(scratchBuffer);

            buildInfo.Mode = BuildAccelerationStructureModeKHR.BuildKhr;
            buildInfo.DstAccelerationStructure = tlasHandle;
            buildInfo.ScratchData = new DeviceOrHostAddressKHR { DeviceAddress = scratchBuffer.DeviceAddress };

            var buildRange = new AccelerationStructureBuildRangeInfoKHR { PrimitiveCount = instanceCount, PrimitiveOffset = 0, FirstVertex = 0, TransformOffset = 0 };
            var pBuildRange = &buildRange;
            _device.KhrAccelerationStructure.CmdBuildAccelerationStructures(cmd, 1, in buildInfo, &pBuildRange);

            var buildBarrier = new MemoryBarrier { SType = StructureType.MemoryBarrier, SrcAccessMask = AccessFlags.AccelerationStructureWriteBitKhr, DstAccessMask = AccessFlags.AccelerationStructureReadBitKhr };
            _device.Vk.CmdPipelineBarrier(cmd, PipelineStageFlags.AccelerationStructureBuildBitKhr, PipelineStageFlags.RayTracingShaderBitKhr, 0, 1, in buildBarrier, 0, null, 0, null);

            WriteDescriptorSetAccelerationStructureKHR descriptorAS = new() { SType = StructureType.WriteDescriptorSetAccelerationStructureKhr, AccelerationStructureCount = 1, PAccelerationStructures = &tlasHandle };
            WriteDescriptorSet writeAS = new() { SType = StructureType.WriteDescriptorSet, PNext = &descriptorAS, DstSet = _descriptorSets[_currentFrame], DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.AccelerationStructureKhr };

            DescriptorImageInfo storageImageInfo = new() { ImageLayout = ImageLayout.General, ImageView = _storageImageView };
            WriteDescriptorSet writeStorageImage = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 1, DescriptorCount = 1, DescriptorType = DescriptorType.StorageImage, PImageInfo = &storageImageInfo };

            DescriptorBufferInfo vertexBufferInfo = new() { Buffer = _meshPool.VertexBuffer.Buffer, Offset = 0, Range = Vk.WholeSize };
            WriteDescriptorSet writeVertices = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 4, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &vertexBufferInfo };

            DescriptorBufferInfo indexBufferInfo = new() { Buffer = _meshPool.IndexBuffer.Buffer, Offset = 0, Range = Vk.WholeSize };
            WriteDescriptorSet writeIndices = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 5, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &indexBufferInfo };

            DescriptorBufferInfo instanceDataInfo = new() { Buffer = instanceDataBuffer.Buffer, Offset = 0, Range = Vk.WholeSize };
            WriteDescriptorSet writeInstanceData = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 6, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &instanceDataInfo };

            var writes = stackalloc WriteDescriptorSet[5] { writeAS, writeStorageImage, writeVertices, writeIndices, writeInstanceData };
            _device.Vk.UpdateDescriptorSets(_device.Device, 5, writes, 0, null);

            if (_currentTextureArray is VulkanTextureArray vkTexArray)
            {
                DescriptorImageInfo texArrayInfo = new() { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = vkTexArray.ImageView, Sampler = vkTexArray.Sampler };
                WriteDescriptorSet writeTex = new() { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[_currentFrame], DstBinding = 3, DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &texArrayInfo };
                _device.Vk.UpdateDescriptorSets(_device.Device, 1, &writeTex, 0, null);
            }

            TransitionImageLayout(cmd, _storageImage, ImageLayout.Undefined, ImageLayout.General, 0, AccessFlags.ShaderWriteBit, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.RayTracingShaderBitKhr);

            _device.Vk.CmdBindPipeline(cmd, PipelineBindPoint.RayTracingKhr, _pipeline.Pipeline);
            var descSet = _descriptorSets[_currentFrame];
            _device.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.RayTracingKhr, _pipeline.PipelineLayout, 0, 1, &descSet, 0, null);

            var sbtProps = _pipeline.SbtProps;
            var raygenRegion = new StridedDeviceAddressRegionKHR { DeviceAddress = _pipeline.SbtBuffer.DeviceAddress, Stride = sbtProps.RegionAligned, Size = sbtProps.RegionAligned };
            var missRegion = new StridedDeviceAddressRegionKHR { DeviceAddress = _pipeline.SbtBuffer.DeviceAddress + sbtProps.RegionAligned, Stride = sbtProps.RegionAligned, Size = sbtProps.RegionAligned };
            var hitRegion = new StridedDeviceAddressRegionKHR { DeviceAddress = _pipeline.SbtBuffer.DeviceAddress + 2 * sbtProps.RegionAligned, Stride = sbtProps.RegionAligned, Size = sbtProps.RegionAligned };
            var callRegion = new StridedDeviceAddressRegionKHR { };

            _device.KhrRayTracingPipeline.CmdTraceRays(cmd, &raygenRegion, &missRegion, &hitRegion, &callRegion, (uint)_framebufferSize.X, (uint)_framebufferSize.Y, 1);

            TransitionImageLayout(cmd, _storageImage, ImageLayout.General, ImageLayout.TransferSrcOptimal, AccessFlags.ShaderWriteBit, AccessFlags.TransferReadBit, PipelineStageFlags.RayTracingShaderBitKhr, PipelineStageFlags.TransferBit);
        }

        TransitionImageLayout(cmd, _swapchain.Images[imageIndex], ImageLayout.Undefined, ImageLayout.TransferDstOptimal, 0, AccessFlags.TransferWriteBit, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit);

        if (_drawCalls.Count > 0)
        {
            ImageCopy copy = new()
            {
                SrcSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                DstSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                Extent = new Extent3D((uint)_framebufferSize.X, (uint)_framebufferSize.Y, 1)
            };
            _device.Vk.CmdCopyImage(cmd, _storageImage, ImageLayout.TransferSrcOptimal, _swapchain.Images[imageIndex], ImageLayout.TransferDstOptimal, 1, &copy);
        }

        TransitionImageLayout(cmd, _swapchain.Images[imageIndex], ImageLayout.TransferDstOptimal, ImageLayout.PresentSrcKhr, AccessFlags.TransferWriteBit, 0, PipelineStageFlags.TransferBit, PipelineStageFlags.BottomOfPipeBit);

        _device.Vk.EndCommandBuffer(cmd);

        var waitSemaphores = stackalloc[] { _imageAvailableSemaphores[_currentFrame] };
        var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
        var signalSemaphores = stackalloc[] { _renderFinishedSemaphores[_currentFrame] };
        var commandBuffers = stackalloc[] { cmd };

        SubmitInfo submitInfo = new() { SType = StructureType.SubmitInfo, WaitSemaphoreCount = 1, PWaitSemaphores = waitSemaphores, PWaitDstStageMask = waitStages, CommandBufferCount = 1, PCommandBuffers = commandBuffers, SignalSemaphoreCount = 1, PSignalSemaphores = signalSemaphores };
        lock (_device.QueueLock) _device.Vk.QueueSubmit(_device.GraphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]);

        var swapchains = stackalloc[] { _swapchain.Swapchain };
        PresentInfoKHR presentInfo = new() { SType = StructureType.PresentInfoKhr, WaitSemaphoreCount = 1, PWaitSemaphores = signalSemaphores, SwapchainCount = 1, PSwapchains = swapchains, PImageIndices = &imageIndex };
        lock (_device.QueueLock) result = _device.KhrSwapchain.QueuePresent(_device.PresentQueue, in presentInfo);

        if (result is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr) _framebufferResized = true;
        _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
    }

    private void TransitionImageLayout(CommandBuffer cmd, Image image, ImageLayout oldLayout, ImageLayout newLayout, AccessFlags srcAccess, AccessFlags dstAccess, PipelineStageFlags srcStage, PipelineStageFlags dstStage)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier, OldLayout = oldLayout, NewLayout = newLayout, SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image, SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1), SrcAccessMask = srcAccess, DstAccessMask = dstAccess
        };
        _device.Vk.CmdPipelineBarrier(cmd, srcStage, dstStage, 0, 0, null, 0, null, 1, &barrier);
    }

    public void OnFramebufferResize(Vector2<int> newSize) { _framebufferSize = newSize; _framebufferResized = true; }

    public void Dispose()
    {
        _device.Vk.DeviceWaitIdle(_device.Device);

        foreach (var list in _meshesToDispose) foreach (var mesh in list) { var a = (MeshAllocation)mesh; _device.KhrAccelerationStructure.DestroyAccelerationStructure(_device.Device, a.Blas, null); a.BlasBuffer?.Dispose(); mesh.Dispose(); }
        while (_pendingMeshesToDispose.TryDequeue(out var mesh)) { var a = (MeshAllocation)mesh; _device.KhrAccelerationStructure.DestroyAccelerationStructure(_device.Device, a.Blas, null); a.BlasBuffer?.Dispose(); mesh.Dispose(); }
        foreach (var list in _frameResources) foreach (var res in list) res.Dispose();
        foreach (var list in _frameAS) foreach (var handle in list) _device.KhrAccelerationStructure.DestroyAccelerationStructure(_device.Device, handle, null);

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