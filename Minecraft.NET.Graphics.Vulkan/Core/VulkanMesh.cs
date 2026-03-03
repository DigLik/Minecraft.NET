using Silk.NET.Vulkan;

using Minecraft.NET.Engine.Abstractions.Graphics;

namespace Minecraft.NET.Graphics.Vulkan.Core;

public unsafe class VulkanMesh : IMesh
{
    private readonly VulkanDevice _device;
    public VulkanBuffer VertexBuffer;
    public VulkanBuffer IndexBuffer;

    public uint IndexCount { get; private set; }

    public bool IsReady => true;

    public VulkanMesh(VulkanDevice device, void* vertices, ulong vertexSize, void* indices, uint indexCount)
    {
        _device = device;
        IndexCount = indexCount;

        VertexBuffer = new VulkanBuffer(_device, vertexSize, BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit, MemoryPropertyFlags.DeviceLocalBit);
        IndexBuffer = new VulkanBuffer(_device, indexCount * sizeof(uint), BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit, MemoryPropertyFlags.DeviceLocalBit);

        using VulkanBuffer stagingVertex = new(_device, vertexSize, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        using VulkanBuffer stagingIndex = new(_device, indexCount * sizeof(uint), BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* vData;
        _device.Vk.MapMemory(_device.Device, stagingVertex.Memory, 0, vertexSize, 0, &vData);
        System.Buffer.MemoryCopy(vertices, vData, vertexSize, vertexSize);
        _device.Vk.UnmapMemory(_device.Device, stagingVertex.Memory);

        void* iData;
        ulong indexSize = indexCount * sizeof(uint);
        _device.Vk.MapMemory(_device.Device, stagingIndex.Memory, 0, indexSize, 0, &iData);
        System.Buffer.MemoryCopy(indices, iData, indexSize, indexSize);
        _device.Vk.UnmapMemory(_device.Device, stagingIndex.Memory);

        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.TransientBit,
            QueueFamilyIndex = _device.GraphicsFamilyIndex
        };

        _device.Vk.CreateCommandPool(_device.Device, in poolInfo, null, out CommandPool cmdPool);

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = cmdPool,
            CommandBufferCount = 1
        };

        _device.Vk.AllocateCommandBuffers(_device.Device, in allocInfo, out CommandBuffer cmd);

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
        _device.Vk.BeginCommandBuffer(cmd, in beginInfo);

        BufferCopy vCopy = new() { Size = vertexSize };
        _device.Vk.CmdCopyBuffer(cmd, stagingVertex.Buffer, VertexBuffer.Buffer, 1, in vCopy);

        BufferCopy iCopy = new() { Size = indexSize };
        _device.Vk.CmdCopyBuffer(cmd, stagingIndex.Buffer, IndexBuffer.Buffer, 1, in iCopy);

        _device.Vk.EndCommandBuffer(cmd);

        FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo };
        _device.Vk.CreateFence(_device.Device, in fenceInfo, null, out Fence fence);

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd
        };

        lock (_device.QueueLock)
            _device.Vk.QueueSubmit(_device.GraphicsQueue, 1, in submitInfo, fence);

        _device.Vk.WaitForFences(_device.Device, 1, in fence, Vk.True, ulong.MaxValue);

        _device.Vk.DestroyFence(_device.Device, fence, null);
        _device.Vk.DestroyCommandPool(_device.Device, cmdPool, null);
    }

    public void Dispose()
    {
        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
    }
}