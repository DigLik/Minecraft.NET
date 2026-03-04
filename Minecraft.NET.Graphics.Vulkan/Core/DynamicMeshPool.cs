using System.Collections.Concurrent;
using System.Runtime.InteropServices;

using Silk.NET.Vulkan;

namespace Minecraft.NET.Graphics.Vulkan.Core;

public unsafe class DynamicMeshPool : IDisposable
{
    public struct MeshFreeBlock : IComparable<MeshFreeBlock>
    {
        public ulong Offset;
        public ulong Size;
        public readonly int CompareTo(MeshFreeBlock other) => Offset.CompareTo(other.Offset);
    }

    public class BufferChunk : IDisposable
    {
        public VulkanBuffer Buffer { get; }
        public List<MeshFreeBlock> FreeBlocks { get; }

        public BufferChunk(VulkanDevice device, ulong capacity, BufferUsageFlags usage)
        {
            Buffer = new VulkanBuffer(device, capacity, usage, MemoryPropertyFlags.DeviceLocalBit);
            FreeBlocks = [new MeshFreeBlock { Offset = 0, Size = capacity }];
        }

        public ulong Allocate(ulong size)
        {
            for (int i = 0; i < FreeBlocks.Count; i++)
            {
                var block = FreeBlocks[i];
                if (block.Size >= size)
                {
                    ulong offset = block.Offset;
                    if (block.Size == size) FreeBlocks.RemoveAt(i);
                    else { block.Offset += size; block.Size -= size; FreeBlocks[i] = block; }
                    return offset;
                }
            }
            return ulong.MaxValue;
        }

        public void Free(ulong offset, ulong size)
        {
            var newBlock = new MeshFreeBlock { Offset = offset, Size = size };
            int idx = FreeBlocks.BinarySearch(newBlock);
            if (idx < 0) idx = ~idx;
            FreeBlocks.Insert(idx, newBlock);

            int i = Math.Max(0, idx - 1);
            while (i < FreeBlocks.Count - 1)
            {
                if (FreeBlocks[i].Offset + FreeBlocks[i].Size == FreeBlocks[i + 1].Offset)
                {
                    FreeBlocks[i] = new MeshFreeBlock { Offset = FreeBlocks[i].Offset, Size = FreeBlocks[i].Size + FreeBlocks[i + 1].Size };
                    FreeBlocks.RemoveAt(i + 1);
                }
                else i++;
            }
        }

        public void Dispose() => Buffer.Dispose();
    }

    private class PendingUpload
    {
        public Array Vertices = null!;
        public Array Indices = null!;
        public MeshAllocation Allocation = null!;
    }

    private readonly VulkanDevice _device;
    private readonly Lock _allocLock = new();

    private readonly List<BufferChunk> _vertexChunks = [];
    private readonly List<BufferChunk> _indexChunks = [];
    private readonly List<BufferChunk> _blasChunks = [];

    private readonly ulong VertexChunkCapacity = 128UL * 1024 * 1024; // 128 MB
    private readonly ulong IndexChunkCapacity = 64UL * 1024 * 1024;   // 64 MB
    private readonly ulong BlasChunkCapacity = 128UL * 1024 * 1024;   // 128 MB

    private readonly BufferUsageFlags vUsage = BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;
    private readonly BufferUsageFlags iUsage = BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;
    private readonly BufferUsageFlags bUsage = BufferUsageFlags.AccelerationStructureStorageBitKhr | BufferUsageFlags.ShaderDeviceAddressBit;

    private readonly BlockingCollection<PendingUpload> _pendingUploads = new(new ConcurrentQueue<PendingUpload>());
    private readonly Thread _uploadThread;

    public DynamicMeshPool(VulkanDevice device)
    {
        _device = device;
        _uploadThread = new Thread(UploadLoop) { IsBackground = true, Name = "VulkanUploadThread" };
        _uploadThread.Start();
    }

    public MeshAllocation Allocate<T>(T[] vertices, uint[] indices) where T : unmanaged
    {
        ulong vertexSize = (ulong)(vertices.Length * sizeof(T));
        ulong indexSize = (ulong)(indices.Length * sizeof(uint));
        ulong vOffset = ulong.MaxValue, iOffset = ulong.MaxValue;
        BufferChunk? vChunk = null, iChunk = null;

        lock (_allocLock)
        {
            foreach (var chunk in _vertexChunks) { vOffset = chunk.Allocate(vertexSize); if (vOffset != ulong.MaxValue) { vChunk = chunk; break; } }
            if (vOffset == ulong.MaxValue) { vChunk = new BufferChunk(_device, VertexChunkCapacity, vUsage); vOffset = vChunk.Allocate(vertexSize); _vertexChunks.Add(vChunk); }

            foreach (var chunk in _indexChunks) { iOffset = chunk.Allocate(indexSize); if (iOffset != ulong.MaxValue) { iChunk = chunk; break; } }
            if (iOffset == ulong.MaxValue) { iChunk = new BufferChunk(_device, IndexChunkCapacity, iUsage); iOffset = iChunk.Allocate(indexSize); _indexChunks.Add(iChunk); }
        }

        var alloc = new MeshAllocation((uint)indices.Length, (uint)(iOffset / sizeof(uint)), (int)(vOffset / (ulong)sizeof(T)), vOffset, vertexSize, iOffset, indexSize)
        {
            VertexChunk = vChunk,
            IndexChunk = iChunk
        };

        _pendingUploads.Add(new PendingUpload { Vertices = vertices, Indices = indices, Allocation = alloc });
        return alloc;
    }

    private void UploadLoop()
    {
        CommandPoolCreateInfo poolInfo = new() { SType = StructureType.CommandPoolCreateInfo, Flags = CommandPoolCreateFlags.ResetCommandBufferBit, QueueFamilyIndex = _device.GraphicsFamilyIndex };
        _device.Vk.CreateCommandPool(_device.Device, in poolInfo, null, out CommandPool cmdPool);

        CommandBufferAllocateInfo allocInfo = new() { SType = StructureType.CommandBufferAllocateInfo, Level = CommandBufferLevel.Primary, CommandPool = cmdPool, CommandBufferCount = 1 };
        _device.Vk.AllocateCommandBuffers(_device.Device, in allocInfo, out CommandBuffer cmd);

        FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo };
        _device.Vk.CreateFence(_device.Device, in fenceInfo, null, out Fence fence);

        VulkanBuffer? stagingBuffer = null;
        ulong stagingCapacity = 0;
        VulkanBuffer? scratchBuffer = null;
        ulong scratchCapacity = 0;

        try
        {
            foreach (var firstUpload in _pendingUploads.GetConsumingEnumerable())
                ProcessBatch(firstUpload, cmd, fence, ref stagingBuffer, ref stagingCapacity, ref scratchBuffer, ref scratchCapacity);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Vulkan Fatal Error] UploadLoop crashed: {ex}");
        }
        finally
        {
            stagingBuffer?.Dispose();
            scratchBuffer?.Dispose();
            _device.Vk.DestroyFence(_device.Device, fence, null);
            _device.Vk.FreeCommandBuffers(_device.Device, cmdPool, 1, in cmd);
            _device.Vk.DestroyCommandPool(_device.Device, cmdPool, null);
        }
    }

    private void ProcessBatch(PendingUpload firstUpload, CommandBuffer cmd, Fence fence, ref VulkanBuffer? stagingBuffer, ref ulong stagingCapacity, ref VulkanBuffer? scratchBuffer, ref ulong scratchCapacity)
    {
        List<PendingUpload> uploads = [firstUpload];
        ulong totalSize = firstUpload.Allocation.VertexByteSize + firstUpload.Allocation.IndexByteSize;

        while (uploads.Count < 8 && _pendingUploads.TryTake(out var nextUpload))
        {
            uploads.Add(nextUpload);
            totalSize += nextUpload.Allocation.VertexByteSize + nextUpload.Allocation.IndexByteSize;
        }

        if (totalSize > stagingCapacity)
        {
            stagingBuffer?.Dispose();
            stagingCapacity = Math.Max(totalSize, stagingCapacity * 2);
            stagingCapacity = Math.Max(stagingCapacity, 16UL * 1024 * 1024);
            stagingBuffer = new VulkanBuffer(_device, stagingCapacity, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        }

        ulong currentOffset = 0;
        _device.Vk.ResetCommandBuffer(cmd, 0);

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
        _device.Vk.BeginCommandBuffer(cmd, in beginInfo);

        foreach (var upload in uploads)
        {
            var alloc = upload.Allocation;

            GCHandle vHandle = GCHandle.Alloc(upload.Vertices, GCHandleType.Pinned);
            System.Buffer.MemoryCopy((void*)vHandle.AddrOfPinnedObject(), (byte*)stagingBuffer!.MappedMemory + currentOffset, alloc.VertexByteSize, alloc.VertexByteSize);
            vHandle.Free();

            BufferCopy vCopy = new() { SrcOffset = currentOffset, DstOffset = alloc.VertexByteOffset, Size = alloc.VertexByteSize };
            _device.Vk.CmdCopyBuffer(cmd, stagingBuffer.Buffer, alloc.VertexChunk.Buffer.Buffer, 1, in vCopy);
            currentOffset += alloc.VertexByteSize;

            GCHandle iHandle = GCHandle.Alloc(upload.Indices, GCHandleType.Pinned);
            System.Buffer.MemoryCopy((void*)iHandle.AddrOfPinnedObject(), (byte*)stagingBuffer.MappedMemory + currentOffset, alloc.IndexByteSize, alloc.IndexByteSize);
            iHandle.Free();

            BufferCopy iCopy = new() { SrcOffset = currentOffset, DstOffset = alloc.IndexByteOffset, Size = alloc.IndexByteSize };
            _device.Vk.CmdCopyBuffer(cmd, stagingBuffer.Buffer, alloc.IndexChunk.Buffer.Buffer, 1, in iCopy);
            currentOffset += alloc.IndexByteSize;
        }

        MemoryBarrier transferBarrier = new() { SType = StructureType.MemoryBarrier, SrcAccessMask = AccessFlags.TransferWriteBit, DstAccessMask = AccessFlags.AccelerationStructureReadBitKhr | AccessFlags.ShaderReadBit };
        _device.Vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.AccelerationStructureBuildBitKhr | PipelineStageFlags.RayTracingShaderBitKhr, 0, 1, in transferBarrier, 0, null, 0, null);

        int uploadCount = uploads.Count;
        var geometries = new AccelerationStructureGeometryKHR[uploadCount];
        var buildInfos = new AccelerationStructureBuildGeometryInfoKHR[uploadCount];
        var buildRanges = new AccelerationStructureBuildRangeInfoKHR[uploadCount];
        var scratchAlignedSizes = new ulong[uploadCount];
        ulong totalScratchSize = 0;

        for (int i = 0; i < uploadCount; i++)
        {
            var alloc = uploads[i].Allocation;

            var triangles = new AccelerationStructureGeometryTrianglesDataKHR
            {
                SType = StructureType.AccelerationStructureGeometryTrianglesDataKhr,
                VertexFormat = Format.R32G32B32Sfloat,
                VertexData = new DeviceOrHostAddressConstKHR { DeviceAddress = alloc.VertexAddress + alloc.VertexByteOffset },
                VertexStride = 64,
                MaxVertex = (uint)(alloc.VertexByteSize / 64) - 1,
                IndexType = IndexType.Uint32,
                IndexData = new DeviceOrHostAddressConstKHR { DeviceAddress = alloc.IndexAddress + alloc.IndexByteOffset }
            };

            geometries[i] = new AccelerationStructureGeometryKHR
            {
                SType = StructureType.AccelerationStructureGeometryKhr, GeometryType = GeometryTypeKHR.TrianglesKhr,
                Geometry = new AccelerationStructureGeometryDataKHR { Triangles = triangles }, Flags = GeometryFlagsKHR.OpaqueBitKhr
            };

            var buildInfoSize = new AccelerationStructureBuildGeometryInfoKHR { SType = StructureType.AccelerationStructureBuildGeometryInfoKhr, Type = AccelerationStructureTypeKHR.BottomLevelKhr, Flags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr, GeometryCount = 1 };
            uint maxPrimitiveCount = alloc.IndexCount / 3;
            AccelerationStructureGeometryKHR tempGeom = geometries[i];
            buildInfoSize.PGeometries = &tempGeom;

            _device.KhrAccelerationStructure.GetAccelerationStructureBuildSizes(_device.Device, AccelerationStructureBuildTypeKHR.DeviceKhr, in buildInfoSize, &maxPrimitiveCount, out var buildSizes);

            ulong alignedBlasSize = (buildSizes.AccelerationStructureSize + 255) & ~255UL;
            ulong blasOffset = ulong.MaxValue;
            BufferChunk? bChunk = null;

            lock (_allocLock)
            {
                foreach (var chunk in _blasChunks) { blasOffset = chunk.Allocate(alignedBlasSize); if (blasOffset != ulong.MaxValue) { bChunk = chunk; break; } }
                if (blasOffset == ulong.MaxValue) { bChunk = new BufferChunk(_device, BlasChunkCapacity, bUsage); blasOffset = bChunk.Allocate(alignedBlasSize); _blasChunks.Add(bChunk); }
            }
            alloc.BlasByteOffset = blasOffset;
            alloc.BlasByteSize = alignedBlasSize;
            alloc.BlasChunk = bChunk!;

            var createInfo = new AccelerationStructureCreateInfoKHR { SType = StructureType.AccelerationStructureCreateInfoKhr, Buffer = bChunk!.Buffer.Buffer, Offset = blasOffset, Size = buildSizes.AccelerationStructureSize, Type = AccelerationStructureTypeKHR.BottomLevelKhr };
            _device.KhrAccelerationStructure.CreateAccelerationStructure(_device.Device, in createInfo, null, out alloc.Blas);

            var addressInfo = new AccelerationStructureDeviceAddressInfoKHR { SType = StructureType.AccelerationStructureDeviceAddressInfoKhr, AccelerationStructure = alloc.Blas };
            alloc.BlasDeviceAddress = _device.KhrAccelerationStructure.GetAccelerationStructureDeviceAddress(_device.Device, in addressInfo);

            ulong alignedScratch = (buildSizes.BuildScratchSize + 255) & ~255UL;
            scratchAlignedSizes[i] = alignedScratch;
            totalScratchSize += alignedScratch;
        }

        if (totalScratchSize > scratchCapacity)
        {
            scratchBuffer?.Dispose();
            scratchCapacity = Math.Max(totalScratchSize, scratchCapacity * 2);
            scratchCapacity = Math.Max(scratchCapacity, 8UL * 1024 * 1024);
            scratchBuffer = new VulkanBuffer(_device, scratchCapacity, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit, MemoryPropertyFlags.DeviceLocalBit);
        }

        if (totalScratchSize > 0)
        {
            fixed (AccelerationStructureGeometryKHR* pGeometries = geometries)
            fixed (AccelerationStructureBuildGeometryInfoKHR* pBuildInfos = buildInfos)
            fixed (AccelerationStructureBuildRangeInfoKHR* pBuildRanges = buildRanges)
            {
                ulong scratchOffset = 0;
                var ppBuildRanges = stackalloc AccelerationStructureBuildRangeInfoKHR*[uploadCount];

                for (int i = 0; i < uploadCount; i++)
                {
                    var alloc = uploads[i].Allocation;

                    buildInfos[i] = new AccelerationStructureBuildGeometryInfoKHR
                    {
                        SType = StructureType.AccelerationStructureBuildGeometryInfoKhr, Type = AccelerationStructureTypeKHR.BottomLevelKhr,
                        Flags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr, Mode = BuildAccelerationStructureModeKHR.BuildKhr,
                        DstAccelerationStructure = alloc.Blas, GeometryCount = 1, PGeometries = &pGeometries[i],
                        ScratchData = new DeviceOrHostAddressKHR { DeviceAddress = scratchBuffer!.DeviceAddress + scratchOffset }
                    };

                    buildRanges[i] = new AccelerationStructureBuildRangeInfoKHR { PrimitiveCount = alloc.IndexCount / 3, PrimitiveOffset = 0, FirstVertex = 0, TransformOffset = 0 };
                    ppBuildRanges[i] = &pBuildRanges[i];
                    scratchOffset += scratchAlignedSizes[i];
                }

                _device.KhrAccelerationStructure.CmdBuildAccelerationStructures(cmd, (uint)uploadCount, pBuildInfos, ppBuildRanges);
            }

            var buildBarrier = new MemoryBarrier { SType = StructureType.MemoryBarrier, SrcAccessMask = AccessFlags.AccelerationStructureWriteBitKhr, DstAccessMask = AccessFlags.AccelerationStructureReadBitKhr | AccessFlags.AccelerationStructureWriteBitKhr };
            _device.Vk.CmdPipelineBarrier(cmd, PipelineStageFlags.AccelerationStructureBuildBitKhr, PipelineStageFlags.AccelerationStructureBuildBitKhr, 0, 1, in buildBarrier, 0, null, 0, null);
        }

        _device.Vk.EndCommandBuffer(cmd);
        _device.Vk.ResetFences(_device.Device, 1, in fence);
        SubmitInfo submitInfo = new() { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &cmd };

        lock (_device.QueueLock) { _device.Vk.QueueSubmit(_device.GraphicsQueue, 1, in submitInfo, fence); }
        _device.Vk.WaitForFences(_device.Device, 1, in fence, Vk.True, ulong.MaxValue);

        foreach (var upload in uploads) upload.Allocation.IsReady = true;
    }

    public void Free(MeshAllocation allocation)
    {
        lock (_allocLock)
        {
            allocation.VertexChunk?.Free(allocation.VertexByteOffset, allocation.VertexByteSize);
            allocation.IndexChunk?.Free(allocation.IndexByteOffset, allocation.IndexByteSize);
            allocation.BlasChunk?.Free(allocation.BlasByteOffset, allocation.BlasByteSize);
        }
    }

    public void Dispose()
    {
        _pendingUploads.CompleteAdding();
        _uploadThread.Join(TimeSpan.FromSeconds(1));
        foreach (var chunk in _vertexChunks) chunk.Dispose();
        foreach (var chunk in _indexChunks) chunk.Dispose();
        foreach (var chunk in _blasChunks) chunk.Dispose();
    }
}