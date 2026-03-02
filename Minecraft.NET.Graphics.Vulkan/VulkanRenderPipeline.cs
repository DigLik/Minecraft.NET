using System.Collections.Concurrent;
using System.Numerics;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct DrawIndexedIndirectCommand
    {
        public uint IndexCount;
        public uint InstanceCount;
        public uint FirstIndex;
        public int VertexOffset;
        public uint FirstInstance;
    }

    private const int MaxFramesInFlight = 2;
    private int _currentFrame = 0;

    private readonly VulkanDevice _device;
    private VulkanSwapchain _swapchain;
    private VulkanPipeline? _pipeline;

    private CommandPool _commandPool;
    private readonly CommandBuffer[] _commandBuffers = new CommandBuffer[MaxFramesInFlight];

    private readonly Semaphore[] _imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
    private readonly Semaphore[] _renderFinishedSemaphores = new Semaphore[MaxFramesInFlight];
    private readonly Fence[] _inFlightFences = new Fence[MaxFramesInFlight];

    private readonly VulkanBuffer[] _cameraBuffers = new VulkanBuffer[MaxFramesInFlight];
    private DescriptorPool _descriptorPool;
    private readonly DescriptorSet[] _descriptorSets = new DescriptorSet[MaxFramesInFlight];

    private readonly VulkanBuffer[] _indirectBuffers = new VulkanBuffer[MaxFramesInFlight];
    private readonly VulkanBuffer[] _instanceBuffers = new VulkanBuffer[MaxFramesInFlight];
    private int _maxDraws = 0;

    private Vector2<int> _framebufferSize;
    private bool _framebufferResized = false;

    private readonly List<DrawCall> _drawCalls = [];
    private ITextureArray? _currentTextureArray;

    private VertexElement[]? _currentLayout;
    private uint _currentStride;

    private DynamicMeshPool _meshPool = null!;

    private readonly ConcurrentQueue<IMesh> _pendingMeshesToDispose = new();
    private readonly List<IMesh>[] _meshesToDispose = new List<IMesh>[MaxFramesInFlight];

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
        _currentLayout = layout;
        _currentStride = stride;
        _pipeline = new VulkanPipeline(_device, _swapchain, layout, stride);
        CreateDescriptorPoolAndSets();

        _meshPool = new DynamicMeshPool(_device, MaxFramesInFlight, 128 * 1024 * 1024, 32 * 1024 * 1024);
    }

    public IMesh CreateMesh<T>(T[] vertices, uint[] indices) where T : unmanaged
        => _meshPool.Allocate(vertices, indices);

    public void DeleteMesh(IMesh mesh)
        => _pendingMeshesToDispose.Enqueue(mesh);

    public ITextureArray CreateTextureArray(int width, int height, byte[][] pixels)
        => new VulkanTextureArray(_device, width, height, pixels);

    public void BindTextureArray(ITextureArray textureArray)
        => _currentTextureArray = textureArray;

    public void SubmitDraw(IMesh mesh, Vector3<float> position)
        => _drawCalls.Add(new DrawCall { Mesh = mesh, Position = position });

    public void ClearDraws()
        => _drawCalls.Clear();

    private void CreateCommandPool()
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = _device.GraphicsFamilyIndex
        };

        _device.Vk.CreateCommandPool(_device.Device, in poolInfo, null, out _commandPool);
    }

    private void CreateCommandBuffers()
    {
        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = MaxFramesInFlight
        };

        fixed (CommandBuffer* pCmds = _commandBuffers)
            _device.Vk.AllocateCommandBuffers(_device.Device, in allocInfo, pCmds);
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
        DescriptorPoolSize[] poolSizes =
        [
            new DescriptorPoolSize() { Type = DescriptorType.UniformBuffer, DescriptorCount = MaxFramesInFlight },
            new DescriptorPoolSize() { Type = DescriptorType.CombinedImageSampler, DescriptorCount = MaxFramesInFlight }
        ];

        fixed (DescriptorPoolSize* pPoolSizes = poolSizes)
        {
            DescriptorPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = 2,
                PPoolSizes = pPoolSizes,
                MaxSets = MaxFramesInFlight
            };

            _device.Vk.CreateDescriptorPool(_device.Device, in poolInfo, null, out _descriptorPool);
        }

        var layouts = stackalloc DescriptorSetLayout[MaxFramesInFlight];
        for (int i = 0; i < MaxFramesInFlight; i++) layouts[i] = _pipeline!.DescriptorSetLayout;

        DescriptorSetAllocateInfo allocInfo = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool, DescriptorSetCount = MaxFramesInFlight, PSetLayouts = layouts
        };

        fixed (DescriptorSet* pSets = _descriptorSets)
            _device.Vk.AllocateDescriptorSets(_device.Device, in allocInfo, pSets);

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _cameraBuffers[i] = new VulkanBuffer(
                _device, (ulong)sizeof(Matrix4x4), BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

            DescriptorBufferInfo bufferInfo = new() { Buffer = _cameraBuffers[i].Buffer, Offset = 0, Range = (ulong)sizeof(Matrix4x4) };

            WriteDescriptorSet descriptorWrite = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _descriptorSets[i], DstBinding = 0, DstArrayElement = 0,
                DescriptorType = DescriptorType.UniformBuffer, DescriptorCount = 1, PBufferInfo = &bufferInfo
            };

            _device.Vk.UpdateDescriptorSets(_device.Device, 1, &descriptorWrite, 0, null);
        }
    }

    private void RecreateSwapchain()
    {
        _device.Vk.DeviceWaitIdle(_device.Device);

        _swapchain.Dispose();
        _pipeline?.Dispose();

        if (_descriptorPool.Handle != 0)
        {
            _device.Vk.DestroyDescriptorPool(_device.Device, _descriptorPool, null);
            _descriptorPool.Handle = 0;
        }

        foreach (var cb in _cameraBuffers) cb?.Dispose();

        _swapchain = new VulkanSwapchain(_device, _framebufferSize);
        if (_currentLayout != null)
        {
            _pipeline = new VulkanPipeline(_device, _swapchain, _currentLayout, _currentStride);
            CreateDescriptorPoolAndSets();
        }
    }

    public void RenderFrame(Matrix4x4 viewProjection)
    {
        if (_pipeline == null) throw new Exception("Pipeline is not initialized.");

        _device.Vk.WaitForFences(_device.Device, 1, ref _inFlightFences[_currentFrame], Vk.True, ulong.MaxValue);

        _meshPool.CleanupResources(_currentFrame);

        foreach (var mesh in _meshesToDispose[_currentFrame])
        {
            _meshPool.Free((MeshAllocation)mesh);
            mesh.Dispose();
        }
        _meshesToDispose[_currentFrame].Clear();

        while (_pendingMeshesToDispose.TryDequeue(out var mesh))
            _meshesToDispose[_currentFrame].Add(mesh);

        if (_framebufferSize.X == 0 || _framebufferSize.Y == 0) return;

        if (_framebufferResized)
        {
            _framebufferResized = false;
            RecreateSwapchain();
            return;
        }

        uint imageIndex;
        var result = _device.KhrSwapchain.AcquireNextImage(
            _device.Device, _swapchain.Swapchain, ulong.MaxValue, _imageAvailableSemaphores[_currentFrame], default, &imageIndex);

        if (result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            return;
        }

        _cameraBuffers[_currentFrame].UpdateData([viewProjection]);

        if (_currentTextureArray is VulkanTextureArray vkTexArray)
        {
            DescriptorImageInfo imageInfo = new()
            {
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = vkTexArray.ImageView,
                Sampler = vkTexArray.Sampler
            };

            WriteDescriptorSet descriptorWrite = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _descriptorSets[_currentFrame],
                DstBinding = 1,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                PImageInfo = &imageInfo
            };

            _device.Vk.UpdateDescriptorSets(_device.Device, 1, &descriptorWrite, 0, null);
        }

        int drawCount = _drawCalls.Count;
        if (drawCount > _maxDraws)
        {
            _device.Vk.DeviceWaitIdle(_device.Device);
            _maxDraws = (int)(drawCount * 1.5f) + 100;

            for (int i = 0; i < MaxFramesInFlight; i++)
            {
                _indirectBuffers[i]?.Dispose();
                _instanceBuffers[i]?.Dispose();
                _indirectBuffers[i] = new VulkanBuffer(_device, (ulong)(_maxDraws * sizeof(DrawIndexedIndirectCommand)), BufferUsageFlags.IndirectBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
                _instanceBuffers[i] = new VulkanBuffer(_device, (ulong)(_maxDraws * sizeof(Vector3<float>)), BufferUsageFlags.VertexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            }
        }

        if (drawCount > 0)
        {
            var indirectSpan = new Span<DrawIndexedIndirectCommand>(_indirectBuffers[_currentFrame].MappedMemory, drawCount);
            var instanceSpan = new Span<Vector3<float>>(_instanceBuffers[_currentFrame].MappedMemory, drawCount);

            for (int i = 0; i < drawCount; i++)
            {
                var draw = _drawCalls[i];
                var alloc = (MeshAllocation)draw.Mesh;

                indirectSpan[i] = new DrawIndexedIndirectCommand
                {
                    IndexCount = alloc.IndexCount,
                    InstanceCount = 1,
                    FirstIndex = alloc.FirstIndex,
                    VertexOffset = alloc.VertexOffset,
                    FirstInstance = (uint)i
                };
                instanceSpan[i] = draw.Position;
            }
        }

        _device.Vk.ResetFences(_device.Device, 1, ref _inFlightFences[_currentFrame]);

        CommandBuffer cmd = _commandBuffers[_currentFrame];
        _device.Vk.ResetCommandBuffer(cmd, 0);

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        _device.Vk.BeginCommandBuffer(cmd, in beginInfo);

        _meshPool.FlushUploads(cmd, _currentFrame);

        var clearValues = stackalloc ClearValue[2];
        clearValues[0].Color = new ClearColorValue(0.4f, 0.6f, 0.9f, 1.0f);
        clearValues[1].DepthStencil = new ClearDepthStencilValue(1.0f, 0);

        RenderPassBeginInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _swapchain.RenderPass,
            Framebuffer = _swapchain.Framebuffers[imageIndex],
            RenderArea = new Rect2D(new Offset2D(0, 0), _swapchain.Extent),
            ClearValueCount = 2,
            PClearValues = clearValues
        };

        _device.Vk.CmdBeginRenderPass(cmd, in renderPassInfo, SubpassContents.Inline);

        if (drawCount > 0)
        {
            _device.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeline.GraphicsPipeline);

            var descriptorSet = _descriptorSets[_currentFrame];
            _device.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _pipeline.PipelineLayout, 0, 1, &descriptorSet, 0, null);

            var vertexBuffers = stackalloc Buffer[2] { _meshPool.VertexBuffer.Buffer, _instanceBuffers[_currentFrame].Buffer };
            var offsets = stackalloc ulong[2] { 0, 0 };

            _device.Vk.CmdBindVertexBuffers(cmd, 0, 2, vertexBuffers, offsets);
            _device.Vk.CmdBindIndexBuffer(cmd, _meshPool.IndexBuffer.Buffer, 0, IndexType.Uint32);

            _device.Vk.CmdDrawIndexedIndirect(cmd, _indirectBuffers[_currentFrame].Buffer, 0, (uint)drawCount, (uint)sizeof(DrawIndexedIndirectCommand));
        }

        _device.Vk.CmdEndRenderPass(cmd);
        _device.Vk.EndCommandBuffer(cmd);

        var waitSemaphores = stackalloc[] { _imageAvailableSemaphores[_currentFrame] };
        var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };

        var signalSemaphores = stackalloc[] { _renderFinishedSemaphores[_currentFrame] };
        var commandBuffers = stackalloc[] { cmd };

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1, PWaitSemaphores = waitSemaphores, PWaitDstStageMask = waitStages,
            CommandBufferCount = 1, PCommandBuffers = commandBuffers,
            SignalSemaphoreCount = 1, PSignalSemaphores = signalSemaphores
        };

        lock (_device.QueueLock)
            _device.Vk.QueueSubmit(_device.GraphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]);

        var swapchains = stackalloc[] { _swapchain.Swapchain };
        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1, PWaitSemaphores = signalSemaphores,
            SwapchainCount = 1, PSwapchains = swapchains, PImageIndices = &imageIndex
        };

        lock (_device.QueueLock)
            result = _device.KhrSwapchain.QueuePresent(_device.PresentQueue, in presentInfo);

        if (result is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr)
            _framebufferResized = true;

        _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
    }

    public void OnFramebufferResize(Vector2<int> newSize)
    {
        _framebufferSize = newSize;
        _framebufferResized = true;
    }

    public void Dispose()
    {
        _device.Vk.DeviceWaitIdle(_device.Device);

        foreach (var list in _meshesToDispose)
            foreach (var mesh in list) mesh.Dispose();

        while (_pendingMeshesToDispose.TryDequeue(out var mesh)) mesh.Dispose();

        if (_descriptorPool.Handle != 0)
            _device.Vk.DestroyDescriptorPool(_device.Device, _descriptorPool, null);

        foreach (var cb in _cameraBuffers) cb?.Dispose();

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _indirectBuffers[i]?.Dispose();
            _instanceBuffers[i]?.Dispose();

            _device.Vk.DestroySemaphore(_device.Device, _imageAvailableSemaphores[i], null);
            _device.Vk.DestroySemaphore(_device.Device, _renderFinishedSemaphores[i], null);
            _device.Vk.DestroyFence(_device.Device, _inFlightFences[i], null);
        }

        _meshPool?.Dispose();

        _device.Vk.DestroyCommandPool(_device.Device, _commandPool, null);
        _pipeline?.Dispose();
        _swapchain.Dispose();
        _device.Dispose();
    }
}