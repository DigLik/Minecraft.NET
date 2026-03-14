using Silk.NET.Vulkan;

namespace Minecraft.NET.Graphics.Vulkan.Core;

public unsafe class VulkanBuffer : IDisposable
{
    private readonly VulkanDevice _device;

    public Silk.NET.Vulkan.Buffer Buffer;
    public DeviceMemory Memory;

    public void* MappedMemory { get; private set; }
    public ulong DeviceAddress { get; private set; }

    public VulkanBuffer(VulkanDevice device, ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, bool isShared = false)
    {
        _device = device;

        uint[] queueFamilies = [_device.GraphicsFamilyIndex, _device.TransferFamilyIndex];
        bool useConcurrent = isShared && _device.GraphicsFamilyIndex != _device.TransferFamilyIndex;

        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = useConcurrent ? SharingMode.Concurrent : SharingMode.Exclusive,
            QueueFamilyIndexCount = useConcurrent ? 2u : 0u,
            PQueueFamilyIndices = useConcurrent ? (uint*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref queueFamilies[0]) : null
        };

        _device.Vk.CreateBuffer(_device.Device, in bufferInfo, null, out Buffer);

        _device.Vk.GetBufferMemoryRequirements(_device.Device, Buffer, out var memRequirements);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = _device.FindMemoryType(memRequirements.MemoryTypeBits, properties)
        };

        if (usage.HasFlag(BufferUsageFlags.ShaderDeviceAddressBit))
        {
            var allocFlagsInfo = new MemoryAllocateFlagsInfo { SType = StructureType.MemoryAllocateFlagsInfo, Flags = MemoryAllocateFlags.DeviceAddressBit };
            allocInfo.PNext = &allocFlagsInfo;
        }

        var result = _device.Vk.AllocateMemory(_device.Device, in allocInfo, null, out Memory);

        if (result != Result.Success)
            throw new Exception($"Failed to allocate VRAM ({size} bytes): {result}");

        _device.Vk.BindBufferMemory(_device.Device, Buffer, Memory, 0);

        if ((properties & MemoryPropertyFlags.HostVisibleBit) != 0)
        {
            void* mapped;
            result = _device.Vk.MapMemory(_device.Device, Memory, 0, size, 0, &mapped);
            if (result != Result.Success)
                throw new Exception($"Failed to map VRAM: {result}");
            MappedMemory = mapped;
        }

        if (usage.HasFlag(BufferUsageFlags.ShaderDeviceAddressBit))
        {
            BufferDeviceAddressInfo info = new() { SType = StructureType.BufferDeviceAddressInfo, Buffer = Buffer };
            DeviceAddress = _device.Vk.GetBufferDeviceAddress(_device.Device, in info);
        }
    }

    public void UpdateData<T>(T[] data) where T : unmanaged
    {
        ulong size = (ulong)(data.Length * sizeof(T));
        if (MappedMemory != null)
            fixed (void* pData = data)
                System.Buffer.MemoryCopy(pData, MappedMemory, size, size);
    }

    public void UpdateData<T>(in T data) where T : unmanaged
    {
        ulong size = (ulong)sizeof(T);
        if (MappedMemory != null)
        {
            fixed (T* pData = &data)
                System.Buffer.MemoryCopy(pData, MappedMemory, size, size);
        }
    }

    public void Dispose()
    {
        if (MappedMemory != null)
            _device.Vk.UnmapMemory(_device.Device, Memory);

        _device.Vk.DestroyBuffer(_device.Device, Buffer, null);
        _device.Vk.FreeMemory(_device.Device, Memory, null);
    }
}