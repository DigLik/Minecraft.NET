using System.Collections.Concurrent;
using System.Runtime.InteropServices;

using Silk.NET.Vulkan;

namespace Minecraft.NET.Graphics.Vulkan.Core;

public unsafe class DynamicMeshPool : IDisposable
{
    private struct MeshFreeBlock : IComparable<MeshFreeBlock>
    {
        public ulong Offset;
        public ulong Size;
        public readonly int CompareTo(MeshFreeBlock other) => Offset.CompareTo(other.Offset);
    }

    private class PendingUpload
    {
        public Array Vertices = null!;
        public Array Indices = null!;
        public MeshAllocation Allocation = null!;
    }

    private readonly VulkanDevice _device;
    public VulkanBuffer VertexBuffer { get; private set; }
    public VulkanBuffer IndexBuffer { get; private set; }

    private readonly Lock _allocLock = new();

    private readonly List<MeshFreeBlock> _freeVertexBlocks = [];
    private readonly List<MeshFreeBlock> _freeIndexBlocks = [];

    private readonly ConcurrentQueue<PendingUpload> _pendingUploads = new();
    private readonly List<VulkanBuffer>[] _staleBuffers;

    public DynamicMeshPool(VulkanDevice device, int maxFramesInFlight = 2)
    {
        _device = device;
        _staleBuffers = new List<VulkanBuffer>[maxFramesInFlight];
        for (int i = 0; i < maxFramesInFlight; i++) _staleBuffers[i] = [];

        ulong vCap = 3072UL * 1024 * 1024;
        ulong iCap = 1024UL * 1024 * 1024;

        var usage = BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;
        var iUsage = BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;

        VertexBuffer = new VulkanBuffer(_device, vCap, usage, MemoryPropertyFlags.DeviceLocalBit);
        IndexBuffer = new VulkanBuffer(_device, iCap, iUsage, MemoryPropertyFlags.DeviceLocalBit);

        _freeVertexBlocks.Add(new MeshFreeBlock { Offset = 0, Size = vCap });
        _freeIndexBlocks.Add(new MeshFreeBlock { Offset = 0, Size = iCap });
    }

    public MeshAllocation Allocate<T>(T[] vertices, uint[] indices) where T : unmanaged
    {
        ulong vertexSize = (ulong)(vertices.Length * sizeof(T));
        ulong indexSize = (ulong)(indices.Length * sizeof(uint));
        ulong vOffset, iOffset;

        lock (_allocLock)
        {
            vOffset = AllocateBlock(_freeVertexBlocks, vertexSize);
            iOffset = AllocateBlock(_freeIndexBlocks, indexSize);
            if (vOffset == ulong.MaxValue || iOffset == ulong.MaxValue) throw new Exception("Mesh Pool Out of Memory");
        }

        var alloc = new MeshAllocation((uint)indices.Length, (uint)(iOffset / sizeof(uint)), (int)(vOffset / (ulong)sizeof(T)), vOffset, vertexSize, iOffset, indexSize);

        _pendingUploads.Enqueue(new PendingUpload { Vertices = vertices, Indices = indices, Allocation = alloc });

        return alloc;
    }

    public void FlushUploads(CommandBuffer cmd, int currentFrame)
    {
        if (_pendingUploads.IsEmpty) return;

        List<PendingUpload> uploads = [];
        ulong totalSize = 0;
        while (_pendingUploads.TryDequeue(out var upload))
        {
            uploads.Add(upload);
            totalSize += upload.Allocation.VertexByteSize + upload.Allocation.IndexByteSize;
        }

        if (totalSize == 0) return;

        var stagingBuffer = new VulkanBuffer(_device, totalSize, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        ulong currentOffset = 0;

        foreach (var upload in uploads)
        {
            var alloc = upload.Allocation;
            GCHandle vHandle = GCHandle.Alloc(upload.Vertices, GCHandleType.Pinned);
            System.Buffer.MemoryCopy((void*)vHandle.AddrOfPinnedObject(), (byte*)stagingBuffer.MappedMemory + currentOffset, alloc.VertexByteSize, alloc.VertexByteSize);
            vHandle.Free();

            BufferCopy vCopy = new() { SrcOffset = currentOffset, DstOffset = alloc.VertexByteOffset, Size = alloc.VertexByteSize };
            _device.Vk.CmdCopyBuffer(cmd, stagingBuffer.Buffer, VertexBuffer.Buffer, 1, in vCopy);
            currentOffset += alloc.VertexByteSize;

            GCHandle iHandle = GCHandle.Alloc(upload.Indices, GCHandleType.Pinned);
            System.Buffer.MemoryCopy((void*)iHandle.AddrOfPinnedObject(), (byte*)stagingBuffer.MappedMemory + currentOffset, alloc.IndexByteSize, alloc.IndexByteSize);
            iHandle.Free();

            BufferCopy iCopy = new() { SrcOffset = currentOffset, DstOffset = alloc.IndexByteOffset, Size = alloc.IndexByteSize };
            _device.Vk.CmdCopyBuffer(cmd, stagingBuffer.Buffer, IndexBuffer.Buffer, 1, in iCopy);
            currentOffset += alloc.IndexByteSize;
        }

        _staleBuffers[currentFrame].Add(stagingBuffer);

        MemoryBarrier transferBarrier = new()
        {
            SType = StructureType.MemoryBarrier,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = AccessFlags.AccelerationStructureReadBitKhr | AccessFlags.ShaderReadBit
        };
        _device.Vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.AccelerationStructureBuildBitKhr | PipelineStageFlags.RayTracingShaderBitKhr, 0, 1, in transferBarrier, 0, null, 0, null);

        ulong maxScratchSize = 0;
        foreach (var upload in uploads)
        {
            var alloc = upload.Allocation;
            var triangles = new AccelerationStructureGeometryTrianglesDataKHR
            {
                SType = StructureType.AccelerationStructureGeometryTrianglesDataKhr,
                VertexFormat = Format.R32G32B32Sfloat,
                VertexData = new DeviceOrHostAddressConstKHR { DeviceAddress = VertexBuffer.DeviceAddress + alloc.VertexByteOffset },
                VertexStride = 64,
                MaxVertex = (uint)(alloc.VertexByteSize / 64) - 1,
                IndexType = IndexType.Uint32,
                IndexData = new DeviceOrHostAddressConstKHR { DeviceAddress = IndexBuffer.DeviceAddress + alloc.IndexByteOffset }
            };

            var geometry = new AccelerationStructureGeometryKHR
            {
                SType = StructureType.AccelerationStructureGeometryKhr,
                GeometryType = GeometryTypeKHR.TrianglesKhr,
                Geometry = new AccelerationStructureGeometryDataKHR { Triangles = triangles },
                Flags = GeometryFlagsKHR.OpaqueBitKhr
            };

            var buildInfo = new AccelerationStructureBuildGeometryInfoKHR
            {
                SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
                Type = AccelerationStructureTypeKHR.BottomLevelKhr,
                Flags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr,
                GeometryCount = 1,
                PGeometries = &geometry
            };

            uint maxPrimitiveCount = alloc.IndexCount / 3;
            _device.KhrAccelerationStructure.GetAccelerationStructureBuildSizes(_device.Device, AccelerationStructureBuildTypeKHR.DeviceKhr, in buildInfo, &maxPrimitiveCount, out var buildSizes);

            maxScratchSize = Math.Max(maxScratchSize, buildSizes.BuildScratchSize);

            alloc.BlasBuffer = new VulkanBuffer(_device, buildSizes.AccelerationStructureSize, BufferUsageFlags.AccelerationStructureStorageBitKhr | BufferUsageFlags.ShaderDeviceAddressBit, MemoryPropertyFlags.DeviceLocalBit);

            var createInfo = new AccelerationStructureCreateInfoKHR
            {
                SType = StructureType.AccelerationStructureCreateInfoKhr,
                Buffer = alloc.BlasBuffer.Buffer,
                Size = buildSizes.AccelerationStructureSize,
                Type = AccelerationStructureTypeKHR.BottomLevelKhr
            };
            _device.KhrAccelerationStructure.CreateAccelerationStructure(_device.Device, in createInfo, null, out alloc.Blas);

            var addressInfo = new AccelerationStructureDeviceAddressInfoKHR { SType = StructureType.AccelerationStructureDeviceAddressInfoKhr, AccelerationStructure = alloc.Blas };
            alloc.BlasDeviceAddress = _device.KhrAccelerationStructure.GetAccelerationStructureDeviceAddress(_device.Device, in addressInfo);
        }

        var scratchBuffer = new VulkanBuffer(_device, maxScratchSize, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit, MemoryPropertyFlags.DeviceLocalBit);

        foreach (var upload in uploads)
        {
            var alloc = upload.Allocation;
            var triangles = new AccelerationStructureGeometryTrianglesDataKHR
            {
                SType = StructureType.AccelerationStructureGeometryTrianglesDataKhr,
                VertexFormat = Format.R32G32B32Sfloat,
                VertexData = new DeviceOrHostAddressConstKHR { DeviceAddress = VertexBuffer.DeviceAddress + alloc.VertexByteOffset },
                VertexStride = 64,
                MaxVertex = (uint)(alloc.VertexByteSize / 64) - 1,
                IndexType = IndexType.Uint32,
                IndexData = new DeviceOrHostAddressConstKHR { DeviceAddress = IndexBuffer.DeviceAddress + alloc.IndexByteOffset }
            };

            var geometry = new AccelerationStructureGeometryKHR
            {
                SType = StructureType.AccelerationStructureGeometryKhr,
                GeometryType = GeometryTypeKHR.TrianglesKhr,
                Geometry = new AccelerationStructureGeometryDataKHR { Triangles = triangles },
                Flags = GeometryFlagsKHR.OpaqueBitKhr
            };

            var buildInfo = new AccelerationStructureBuildGeometryInfoKHR
            {
                SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
                Type = AccelerationStructureTypeKHR.BottomLevelKhr,
                Flags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr,
                Mode = BuildAccelerationStructureModeKHR.BuildKhr,
                DstAccelerationStructure = alloc.Blas,
                GeometryCount = 1,
                PGeometries = &geometry,
                ScratchData = new DeviceOrHostAddressKHR { DeviceAddress = scratchBuffer.DeviceAddress }
            };

            var buildRange = new AccelerationStructureBuildRangeInfoKHR { PrimitiveCount = alloc.IndexCount / 3, PrimitiveOffset = 0, FirstVertex = 0, TransformOffset = 0 };
            var pBuildRange = &buildRange;

            _device.KhrAccelerationStructure.CmdBuildAccelerationStructures(cmd, 1, in buildInfo, &pBuildRange);

            var buildBarrier = new MemoryBarrier { SType = StructureType.MemoryBarrier, SrcAccessMask = AccessFlags.AccelerationStructureWriteBitKhr, DstAccessMask = AccessFlags.AccelerationStructureReadBitKhr | AccessFlags.AccelerationStructureWriteBitKhr };
            _device.Vk.CmdPipelineBarrier(cmd, PipelineStageFlags.AccelerationStructureBuildBitKhr, PipelineStageFlags.AccelerationStructureBuildBitKhr, 0, 1, in buildBarrier, 0, null, 0, null);
        }

        _staleBuffers[currentFrame].Add(scratchBuffer);
    }

    public void Free(MeshAllocation allocation)
    {
        lock (_allocLock)
        {
            AddAndMergeFreeBlock(_freeVertexBlocks, allocation.VertexByteOffset, allocation.VertexByteSize);
            AddAndMergeFreeBlock(_freeIndexBlocks, allocation.IndexByteOffset, allocation.IndexByteSize);
        }
    }

    private static ulong AllocateBlock(List<MeshFreeBlock> blocks, ulong size)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block.Size >= size)
            {
                ulong allocatedOffset = block.Offset;
                if (block.Size == size) blocks.RemoveAt(i);
                else { block.Offset += size; block.Size -= size; blocks[i] = block; }
                return allocatedOffset;
            }
        }
        return ulong.MaxValue;
    }

    private static void AddAndMergeFreeBlock(List<MeshFreeBlock> blocks, ulong offset, ulong size)
    {
        var newBlock = new MeshFreeBlock { Offset = offset, Size = size };
        int idx = blocks.BinarySearch(newBlock);
        if (idx < 0) idx = ~idx;
        blocks.Insert(idx, newBlock);

        int i = Math.Max(0, idx - 1);
        while (i < blocks.Count - 1)
        {
            if (blocks[i].Offset + blocks[i].Size == blocks[i + 1].Offset)
            {
                blocks[i] = new MeshFreeBlock { Offset = blocks[i].Offset, Size = blocks[i].Size + blocks[i + 1].Size };
                blocks.RemoveAt(i + 1);
            }
            else i++;
        }
    }

    public void CleanupResources(int currentFrame)
    {
        foreach (var buffer in _staleBuffers[currentFrame]) buffer.Dispose();
        _staleBuffers[currentFrame].Clear();
    }

    public void Dispose()
    {
        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
        foreach (var list in _staleBuffers) foreach (var buf in list) buf.Dispose();
    }
}