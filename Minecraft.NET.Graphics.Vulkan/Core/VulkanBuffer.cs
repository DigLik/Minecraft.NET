using System.Runtime.CompilerServices;

using Silk.NET.Vulkan;

namespace Minecraft.NET.Graphics.Vulkan.Core;

public unsafe class VulkanBuffer : IDisposable
{
    private readonly VulkanDevice _device;
    public Silk.NET.Vulkan.Buffer Buffer;
    public DeviceMemory Memory;

    public void* MappedMemory { get; private set; }

    public VulkanBuffer(VulkanDevice device, ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties)
    {
        _device = device;

        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };
        _device.Vk.CreateBuffer(_device.Device, in bufferInfo, null, out Buffer);

        _device.Vk.GetBufferMemoryRequirements(_device.Device, Buffer, out var memRequirements);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = _device.FindMemoryType(memRequirements.MemoryTypeBits, properties)
        };
        _device.Vk.AllocateMemory(_device.Device, in allocInfo, null, out Memory);
        _device.Vk.BindBufferMemory(_device.Device, Buffer, Memory, 0);

        if ((properties & MemoryPropertyFlags.HostVisibleBit) != 0)
        {
            void* mapped;
            _device.Vk.MapMemory(_device.Device, Memory, 0, size, 0, &mapped);
            MappedMemory = mapped;
        }
    }

    public void UpdateData<T>(T[] data) where T : unmanaged
    {
        ulong size = (ulong)(data.Length * sizeof(T));

        if (MappedMemory != null)
            fixed (void* pData = data)
                System.Buffer.MemoryCopy(pData, MappedMemory, size, size);
    }

    public void Dispose()
    {
        if (MappedMemory != null)
            _device.Vk.UnmapMemory(_device.Device, Memory);

        _device.Vk.DestroyBuffer(_device.Device, Buffer, null);
        _device.Vk.FreeMemory(_device.Device, Memory, null);
    }
}