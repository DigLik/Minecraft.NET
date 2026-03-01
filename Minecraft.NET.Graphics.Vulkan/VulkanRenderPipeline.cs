using System.Numerics;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Game.World.Meshing;
using Minecraft.NET.Graphics.Vulkan.Core;
using Minecraft.NET.Utils.Math;

using Silk.NET.Vulkan;

using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Minecraft.NET.Graphics.Vulkan;

public unsafe class VulkanRenderPipeline : IRenderPipeline
{
    private const int MaxFramesInFlight = 3;
    private int _currentFrame = 0;

    private readonly VulkanDevice _device;
    private VulkanSwapchain _swapchain;
    private VulkanPipeline _pipeline;

    private CommandPool _commandPool;
    private readonly CommandBuffer[] _commandBuffers = new CommandBuffer[MaxFramesInFlight];

    private readonly Semaphore[] _imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
    private readonly Semaphore[] _renderFinishedSemaphores = new Semaphore[MaxFramesInFlight];
    private readonly Fence[] _inFlightFences = new Fence[MaxFramesInFlight];

    private VulkanBuffer _vertexBuffer = null!;
    private VulkanBuffer _indexBuffer = null!;

    private readonly VulkanBuffer[] _cameraBuffers = new VulkanBuffer[MaxFramesInFlight];
    private DescriptorPool _descriptorPool;
    private readonly DescriptorSet[] _descriptorSets = new DescriptorSet[MaxFramesInFlight];

    private Vector2<int> _framebufferSize;
    private bool _framebufferResized = false;
    private double _totalTime = 0.0;

    public VulkanRenderPipeline(IWindow window)
    {
        _framebufferSize = window.FramebufferSize;
        _device = new VulkanDevice(window.Handle);
        _swapchain = new VulkanSwapchain(_device, _framebufferSize);
        _pipeline = new VulkanPipeline(_device, _swapchain);

        CreateCommandPool();
        CreateCommandBuffers();
        CreateSyncObjects();
        CreateDescriptorPoolAndSets();
        CreateTestMesh();
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

    private void CreateDescriptorPoolAndSets()
    {
        DescriptorPoolSize poolSize = new() { Type = DescriptorType.UniformBuffer, DescriptorCount = MaxFramesInFlight };
        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 1, PPoolSizes = &poolSize, MaxSets = MaxFramesInFlight
        };
        _device.Vk.CreateDescriptorPool(_device.Device, in poolInfo, null, out _descriptorPool);

        var layouts = stackalloc DescriptorSetLayout[MaxFramesInFlight];
        for (int i = 0; i < MaxFramesInFlight; i++) layouts[i] = _pipeline.DescriptorSetLayout;

        DescriptorSetAllocateInfo allocInfo = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool, DescriptorSetCount = MaxFramesInFlight, PSetLayouts = layouts
        };

        fixed (DescriptorSet* pSets = _descriptorSets)
            _device.Vk.AllocateDescriptorSets(_device.Device, in allocInfo, pSets);

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _cameraBuffers[i] = new VulkanBuffer(_device, (ulong)sizeof(Matrix4x4), BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

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

    private void CreateTestMesh()
    {
        ChunkVertex[] vertices = [
            new(new Vector3<float>( 0.0f, -0.5f, 0.0f), new Vector2<float>(0.5f, 0.0f), 0, 1.0f),
        new(new Vector3<float>( 0.5f,  0.5f, 0.0f), new Vector2<float>(1.0f, 1.0f), 0, 0.5f),
        new(new Vector3<float>(-0.5f,  0.5f, 0.0f), new Vector2<float>(0.0f, 1.0f), 0, 0.2f)
        ];
        uint[] indices = [0, 1, 2];

        _vertexBuffer = new VulkanBuffer(_device, (ulong)(sizeof(ChunkVertex) * vertices.Length), BufferUsageFlags.VertexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        _vertexBuffer.UpdateData(vertices);

        _indexBuffer = new VulkanBuffer(_device, (ulong)(sizeof(uint) * indices.Length), BufferUsageFlags.IndexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        _indexBuffer.UpdateData(indices);
    }

    private void RecreateSwapchain()
    {
        _device.Vk.DeviceWaitIdle(_device.Device);
        _swapchain.Dispose();
        _pipeline.Dispose();

        _device.Vk.DestroyDescriptorPool(_device.Device, _descriptorPool, null);
        foreach (var cb in _cameraBuffers) cb.Dispose();

        _swapchain = new VulkanSwapchain(_device, _framebufferSize);
        _pipeline = new VulkanPipeline(_device, _swapchain);

        CreateDescriptorPoolAndSets();
    }

    public void OnRender(double deltaTime)
    {
        // 1. Ждем ТОЛЬКО тот кадр, который собираемся рендерить (CPU уходит в работу, пока GPU доделывает другие)
        _device.Vk.WaitForFences(_device.Device, 1, ref _inFlightFences[_currentFrame], Vk.True, ulong.MaxValue);

        if (_framebufferSize.X == 0 || _framebufferSize.Y == 0) return;

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

        _totalTime += deltaTime;
        float xOffset = MathF.Sin((float)_totalTime * 3.0f) * 0.5f;
        Matrix4x4 viewProj = Matrix4x4.CreateTranslation(xOffset, 0f, 0f);
        _cameraBuffers[_currentFrame].UpdateData([viewProj]);

        _device.Vk.ResetFences(_device.Device, 1, ref _inFlightFences[_currentFrame]);

        CommandBuffer cmd = _commandBuffers[_currentFrame];
        _device.Vk.ResetCommandBuffer(cmd, 0);

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        _device.Vk.BeginCommandBuffer(cmd, in beginInfo);

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
        _device.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeline.GraphicsPipeline);

        var vertexBuffers = stackalloc[] { _vertexBuffer.Buffer };
        var offsets = stackalloc[] { 0ul };
        _device.Vk.CmdBindVertexBuffers(cmd, 0, 1, vertexBuffers, offsets);
        _device.Vk.CmdBindIndexBuffer(cmd, _indexBuffer.Buffer, 0, IndexType.Uint32);

        var descriptorSet = _descriptorSets[_currentFrame];
        _device.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _pipeline.PipelineLayout, 0, 1, &descriptorSet, 0, null);

        Vector3<float> chunkOffset = new Vector3<float>(0, 0, 0);
        _device.Vk.CmdPushConstants(cmd, _pipeline.PipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)sizeof(Vector3<float>), &chunkOffset);

        _device.Vk.CmdDrawIndexed(cmd, 3, 1, 0, 0, 0);
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

        _device.Vk.QueueSubmit(_device.GraphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]);

        var swapchains = stackalloc[] { _swapchain.Swapchain };
        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1, PWaitSemaphores = signalSemaphores,
            SwapchainCount = 1, PSwapchains = swapchains, PImageIndices = &imageIndex
        };

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

        _indexBuffer.Dispose();
        _vertexBuffer.Dispose();

        _device.Vk.DestroyDescriptorPool(_device.Device, _descriptorPool, null);
        foreach (var cb in _cameraBuffers) cb.Dispose();

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _device.Vk.DestroySemaphore(_device.Device, _imageAvailableSemaphores[i], null);
            _device.Vk.DestroySemaphore(_device.Device, _renderFinishedSemaphores[i], null);
            _device.Vk.DestroyFence(_device.Device, _inFlightFences[i], null);
        }

        _device.Vk.DestroyCommandPool(_device.Device, _commandPool, null);
        _pipeline.Dispose();
        _swapchain.Dispose();
        _device.Dispose();
    }
}