using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Minecraft.NET.Utils.Collections;

using Silk.NET.Vulkan;

using Semaphore = Silk.NET.Vulkan.Semaphore;

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

        public BufferChunk(VulkanDevice device, ulong capacity, BufferUsageFlags usage, bool isShared = false)
        {
            Buffer = new VulkanBuffer(device, capacity, usage, MemoryPropertyFlags.DeviceLocalBit, isShared);
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

    private struct PendingUpload
    {
        public void* Vertices;
        public int VertexByteSize;
        public int VertexCount;
        public int VertexStride;

        public void* Indices;
        public int IndexByteSize;
        public int IndexCount;
        public uint OpaqueIndexCount;
        public MeshAllocation Allocation;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong AlignUp(ulong size, ulong alignment) => (size + alignment - 1) & ~(alignment - 1);

    private readonly VulkanDevice _device;
    private readonly Lock _allocLock = new();

    private readonly List<BufferChunk> _vertexChunks = [];
    private readonly List<BufferChunk> _indexChunks = [];
    private readonly List<BufferChunk> _blasChunks = [];

    private readonly ulong VertexChunkCapacity = 128UL * 1024 * 1024;
    private readonly ulong IndexChunkCapacity = 64UL * 1024 * 1024;
    private readonly ulong BlasChunkCapacity = 128UL * 1024 * 1024;

    private readonly BufferUsageFlags vUsage = BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;
    private readonly BufferUsageFlags iUsage = BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;
    private readonly BufferUsageFlags bUsage = BufferUsageFlags.AccelerationStructureStorageBitKhr | BufferUsageFlags.ShaderDeviceAddressBit;

    private readonly BlockingCollection<PendingUpload> _pendingUploads = new(new ConcurrentQueue<PendingUpload>());
    private readonly Thread _uploadThread;

    private const int MaxInFlight = 3;
    private const int MaxBatchUploads = 256;

    private CommandPool _cmdPool;
    private readonly CommandBuffer[] _cmds = new CommandBuffer[MaxInFlight];

    private CommandPool _transferCmdPool;
    private readonly CommandBuffer[] _transferCmds = new CommandBuffer[MaxInFlight];
    private Semaphore _transferCompleteSemaphore;
    private Fence _uploadFence;

    private QueryPool _queryPool;

    private readonly VulkanBuffer?[] _stagingBuffers = new VulkanBuffer?[MaxInFlight];
    private readonly ulong[] _stagingCapacities = new ulong[MaxInFlight];

    private readonly VulkanBuffer?[] _scratchBuffers = new VulkanBuffer?[MaxInFlight];
    private readonly ulong[] _scratchCapacities = new ulong[MaxInFlight];

    private Semaphore _timelineSemaphore;
    private ulong _submitCounter = 0;

    public DynamicMeshPool(VulkanDevice device)
    {
        _device = device;

        SemaphoreTypeCreateInfo timelineInfo = new() { SType = StructureType.SemaphoreTypeCreateInfo, SemaphoreType = SemaphoreType.Timeline, InitialValue = 0 };
        SemaphoreCreateInfo semInfo = new() { SType = StructureType.SemaphoreCreateInfo, PNext = &timelineInfo };
        _device.Vk.CreateSemaphore(_device.Device, in semInfo, null, out _timelineSemaphore);

        SemaphoreCreateInfo binSemInfo = new() { SType = StructureType.SemaphoreCreateInfo };
        _device.Vk.CreateSemaphore(_device.Device, in binSemInfo, null, out _transferCompleteSemaphore);

        FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo };
        _device.Vk.CreateFence(_device.Device, in fenceInfo, null, out _uploadFence);

        CommandPoolCreateInfo poolInfo = new() { SType = StructureType.CommandPoolCreateInfo, Flags = CommandPoolCreateFlags.ResetCommandBufferBit, QueueFamilyIndex = _device.GraphicsFamilyIndex };
        _device.Vk.CreateCommandPool(_device.Device, in poolInfo, null, out _cmdPool);

        CommandBufferAllocateInfo allocInfo = new() { SType = StructureType.CommandBufferAllocateInfo, Level = CommandBufferLevel.Primary, CommandPool = _cmdPool, CommandBufferCount = MaxInFlight };
        fixed (CommandBuffer* pCmds = _cmds) _device.Vk.AllocateCommandBuffers(_device.Device, in allocInfo, pCmds);

        CommandPoolCreateInfo transferPoolInfo = new() { SType = StructureType.CommandPoolCreateInfo, Flags = CommandPoolCreateFlags.ResetCommandBufferBit, QueueFamilyIndex = _device.TransferFamilyIndex };
        _device.Vk.CreateCommandPool(_device.Device, in transferPoolInfo, null, out _transferCmdPool);

        CommandBufferAllocateInfo transferAllocInfo = new() { SType = StructureType.CommandBufferAllocateInfo, Level = CommandBufferLevel.Primary, CommandPool = _transferCmdPool, CommandBufferCount = MaxInFlight };
        fixed (CommandBuffer* pCmds = _transferCmds) _device.Vk.AllocateCommandBuffers(_device.Device, in transferAllocInfo, pCmds);

        QueryPoolCreateInfo queryPoolInfo = new() { SType = StructureType.QueryPoolCreateInfo, QueryType = QueryType.AccelerationStructureCompactedSizeKhr, QueryCount = MaxBatchUploads };
        _device.Vk.CreateQueryPool(_device.Device, in queryPoolInfo, null, out _queryPool);

        _uploadThread = new Thread(UploadLoop) { IsBackground = true, Name = "VulkanUploadThread" };
        _uploadThread.Start();
    }

    public MeshAllocation Allocate<T>(NativeList<T> vertices, NativeList<ushort> indices, uint opaqueIndexCount) where T : unmanaged
    {
        ulong exactVertexSize = (ulong)(vertices.Count * sizeof(T));
        ulong exactIndexSize = (ulong)(indices.Count * sizeof(ushort));

        ulong alignedVertexSize = AlignUp(exactVertexSize, 256);
        ulong alignedIndexSize = AlignUp(exactIndexSize, 256);

        ulong vOffset = ulong.MaxValue, iOffset = ulong.MaxValue;
        BufferChunk? vChunk = null, iChunk = null;

        lock (_allocLock)
        {
            foreach (var chunk in _vertexChunks)
            {
                vOffset = chunk.Allocate(alignedVertexSize);
                if (vOffset != ulong.MaxValue) { vChunk = chunk; break; }
            }
            if (vOffset == ulong.MaxValue)
            {
                vChunk = new BufferChunk(_device, VertexChunkCapacity, vUsage, isShared: true);
                vOffset = vChunk.Allocate(alignedVertexSize); _vertexChunks.Add(vChunk);
            }

            foreach (var chunk in _indexChunks)
            {
                iOffset = chunk.Allocate(alignedIndexSize);
                if (iOffset != ulong.MaxValue) { iChunk = chunk; break; }
            }
            if (iOffset == ulong.MaxValue)
            {
                iChunk = new BufferChunk(_device, IndexChunkCapacity, iUsage, isShared: true);
                iOffset = iChunk.Allocate(alignedIndexSize); _indexChunks.Add(iChunk);
            }
        }

        var alloc = new MeshAllocation(this, (uint)indices.Count, (uint)(iOffset / sizeof(ushort)), (int)(vOffset / (ulong)sizeof(T)), opaqueIndexCount, vOffset, alignedVertexSize, iOffset, alignedIndexSize)
        {
            VertexChunk = vChunk!,
            IndexChunk = iChunk!
        };

        _pendingUploads.Add(new PendingUpload
        {
            Vertices = vertices.Data,
            VertexByteSize = (int)exactVertexSize,
            VertexCount = vertices.Count,
            VertexStride = sizeof(T),
            Indices = indices.Data,
            IndexByteSize = (int)exactIndexSize,
            IndexCount = indices.Count,
            OpaqueIndexCount = opaqueIndexCount,
            Allocation = alloc
        });

        return alloc;
    }

    public ulong GetCompletedValue()
    {
        _device.Vk.GetSemaphoreCounterValue(_device.Device, _timelineSemaphore, out ulong value);
        return value;
    }

    private void UploadLoop()
    {
        try
        {
            foreach (var firstUpload in _pendingUploads.GetConsumingEnumerable())
            {
                int frameIndex = (int)(_submitCounter % MaxInFlight);
                ProcessBatch(firstUpload, frameIndex);
                _submitCounter++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Vulkan Fatal Error] UploadLoop crashed: {ex}");
        }
    }

    private void ProcessBatch(PendingUpload firstUpload, int frameIndex)
    {
        const ulong MaxBatchBytes = 32UL * 1024 * 1024;

        PendingUpload[] uploads = new PendingUpload[MaxBatchUploads];
        uploads[0] = firstUpload;
        int uploadCount = 1;

        ulong totalSize = AlignUp((ulong)firstUpload.VertexByteSize, 256) + AlignUp((ulong)firstUpload.IndexByteSize, 256);

        while (uploadCount < MaxBatchUploads && totalSize < MaxBatchBytes)
        {
            if (_pendingUploads.TryTake(out var nextUpload))
            {
                uploads[uploadCount++] = nextUpload;
                totalSize += AlignUp((ulong)nextUpload.VertexByteSize, 256) + AlignUp((ulong)nextUpload.IndexByteSize, 256);
            }
            else break;
        }

        if (totalSize > _stagingCapacities[frameIndex])
        {
            _stagingBuffers[frameIndex]?.Dispose();
            _stagingCapacities[frameIndex] = Math.Max(totalSize, _stagingCapacities[frameIndex] * 2);
            _stagingCapacities[frameIndex] = Math.Max(_stagingCapacities[frameIndex], 32UL * 1024 * 1024);
            _stagingBuffers[frameIndex] = new VulkanBuffer(_device, _stagingCapacities[frameIndex], BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        }

        var stagingBuffer = _stagingBuffers[frameIndex]!;
        var transferCmd = _transferCmds[frameIndex];
        var graphicsCmd = _cmds[frameIndex];

        ulong currentOffset = 0;

        _device.Vk.ResetCommandBuffer(transferCmd, 0);
        CommandBufferBeginInfo beginTransferInfo = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
        _device.Vk.BeginCommandBuffer(transferCmd, in beginTransferInfo);

        foreach (ref readonly var upload in uploads.AsSpan(0, uploadCount))
        {
            var alloc = upload.Allocation;

            currentOffset = AlignUp(currentOffset, 256);
            System.Buffer.MemoryCopy(upload.Vertices, (byte*)stagingBuffer.MappedMemory + currentOffset, upload.VertexByteSize, upload.VertexByteSize);
            BufferCopy2 vCopy = new() { SType = StructureType.BufferCopy2, SrcOffset = currentOffset, DstOffset = alloc.VertexByteOffset, Size = (ulong)upload.VertexByteSize };
            CopyBufferInfo2 vCopyInfo = new() { SType = StructureType.CopyBufferInfo2, SrcBuffer = stagingBuffer.Buffer, DstBuffer = alloc.VertexChunk.Buffer.Buffer, RegionCount = 1, PRegions = &vCopy };
            _device.Vk.CmdCopyBuffer2(transferCmd, in vCopyInfo);
            currentOffset += (ulong)upload.VertexByteSize;

            currentOffset = AlignUp(currentOffset, 256);
            System.Buffer.MemoryCopy(upload.Indices, (byte*)stagingBuffer.MappedMemory + currentOffset, upload.IndexByteSize, upload.IndexByteSize);
            BufferCopy2 iCopy = new() { SType = StructureType.BufferCopy2, SrcOffset = currentOffset, DstOffset = alloc.IndexByteOffset, Size = (ulong)upload.IndexByteSize };
            CopyBufferInfo2 iCopyInfo = new() { SType = StructureType.CopyBufferInfo2, SrcBuffer = stagingBuffer.Buffer, DstBuffer = alloc.IndexChunk.Buffer.Buffer, RegionCount = 1, PRegions = &iCopy };
            _device.Vk.CmdCopyBuffer2(transferCmd, in iCopyInfo);
            currentOffset += (ulong)upload.IndexByteSize;

            NativeMemory.Free(upload.Vertices);
            NativeMemory.Free(upload.Indices);
        }

        _device.Vk.EndCommandBuffer(transferCmd);

        _device.Vk.ResetCommandBuffer(graphicsCmd, 0);
        CommandBufferBeginInfo beginGraphicsInfo = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
        _device.Vk.BeginCommandBuffer(graphicsCmd, in beginGraphicsInfo);

        var transferBarrier = new MemoryBarrier2
        {
            SType = StructureType.MemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.TransferBit,
            SrcAccessMask = AccessFlags2.TransferWriteBit,
            DstStageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr,
            DstAccessMask = AccessFlags2.AccelerationStructureReadBitKhr
        };
        var depInfo1 = new DependencyInfo { SType = StructureType.DependencyInfo, MemoryBarrierCount = 1, PMemoryBarriers = &transferBarrier };
        _device.Vk.CmdPipelineBarrier2(graphicsCmd, in depInfo1);

        int totalGeometries = 0;
        for (int i = 0; i < uploadCount; i++)
        {
            if (uploads[i].OpaqueIndexCount > 0) totalGeometries++;
            if (uploads[i].IndexCount > uploads[i].OpaqueIndexCount) totalGeometries++;
        }

        var geometries = stackalloc AccelerationStructureGeometryKHR[totalGeometries];
        var buildInfos = stackalloc AccelerationStructureBuildGeometryInfoKHR[uploadCount];
        var buildRanges = stackalloc AccelerationStructureBuildRangeInfoKHR[totalGeometries];
        var scratchAlignedSizes = stackalloc ulong[uploadCount];
        var uncompactedHandles = stackalloc AccelerationStructureKHR[uploadCount];
        ulong totalScratchSize = 0;

        int geomIdx = 0;

        for (int i = 0; i < uploadCount; i++)
        {
            var upload = uploads[i];
            var alloc = upload.Allocation;
            int startGeomIdx = geomIdx;

            if (upload.OpaqueIndexCount > 0)
            {
                var triangles = new AccelerationStructureGeometryTrianglesDataKHR
                {
                    SType = StructureType.AccelerationStructureGeometryTrianglesDataKhr,
                    VertexFormat = Format.R32G32B32Sfloat,
                    VertexData = new DeviceOrHostAddressConstKHR { DeviceAddress = alloc.VertexAddress + alloc.VertexByteOffset },
                    VertexStride = (ulong)upload.VertexStride,
                    MaxVertex = (uint)(alloc.VertexByteSize / (ulong)upload.VertexStride) - 1,
                    IndexType = IndexType.Uint16,
                    IndexData = new DeviceOrHostAddressConstKHR { DeviceAddress = alloc.IndexAddress + alloc.IndexByteOffset }
                };

                geometries[geomIdx] = new AccelerationStructureGeometryKHR
                {
                    SType = StructureType.AccelerationStructureGeometryKhr,
                    GeometryType = GeometryTypeKHR.TrianglesKhr,
                    Geometry = new AccelerationStructureGeometryDataKHR { Triangles = triangles },
                    Flags = GeometryFlagsKHR.OpaqueBitKhr
                };
                buildRanges[geomIdx] = new AccelerationStructureBuildRangeInfoKHR { PrimitiveCount = upload.OpaqueIndexCount / 3, PrimitiveOffset = 0, FirstVertex = 0, TransformOffset = 0 };
                geomIdx++;
            }

            if (upload.IndexCount > upload.OpaqueIndexCount)
            {
                uint transparentCount = (uint)upload.IndexCount - upload.OpaqueIndexCount;
                var triangles = new AccelerationStructureGeometryTrianglesDataKHR
                {
                    SType = StructureType.AccelerationStructureGeometryTrianglesDataKhr,
                    VertexFormat = Format.R32G32B32Sfloat,
                    VertexData = new DeviceOrHostAddressConstKHR { DeviceAddress = alloc.VertexAddress + alloc.VertexByteOffset },
                    VertexStride = (ulong)upload.VertexStride,
                    MaxVertex = (uint)(alloc.VertexByteSize / (ulong)upload.VertexStride) - 1,
                    IndexType = IndexType.Uint16,
                    IndexData = new DeviceOrHostAddressConstKHR { DeviceAddress = alloc.IndexAddress + alloc.IndexByteOffset + (upload.OpaqueIndexCount * sizeof(ushort)) }
                };

                geometries[geomIdx] = new AccelerationStructureGeometryKHR
                {
                    SType = StructureType.AccelerationStructureGeometryKhr,
                    GeometryType = GeometryTypeKHR.TrianglesKhr,
                    Geometry = new AccelerationStructureGeometryDataKHR { Triangles = triangles },
                    Flags = GeometryFlagsKHR.None
                };
                buildRanges[geomIdx] = new AccelerationStructureBuildRangeInfoKHR { PrimitiveCount = transparentCount / 3, PrimitiveOffset = 0, FirstVertex = 0, TransformOffset = 0 };
                geomIdx++;
            }

            var buildInfoSize = new AccelerationStructureBuildGeometryInfoKHR
            {
                SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
                Type = AccelerationStructureTypeKHR.BottomLevelKhr,
                Flags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr | BuildAccelerationStructureFlagsKHR.AllowCompactionBitKhr,
                GeometryCount = (uint)(geomIdx - startGeomIdx),
                PGeometries = &geometries[startGeomIdx]
            };

            uint* maxPrimitiveCounts = stackalloc uint[(geomIdx - startGeomIdx)];
            for (int j = 0; j < (geomIdx - startGeomIdx); j++)
                maxPrimitiveCounts[j] = buildRanges[startGeomIdx + j].PrimitiveCount;

            _device.KhrAccelerationStructure.GetAccelerationStructureBuildSizes(_device.Device, AccelerationStructureBuildTypeKHR.DeviceKhr, in buildInfoSize, maxPrimitiveCounts, out var buildSizes);

            ulong alignedBlasSize = (buildSizes.AccelerationStructureSize + 255) & ~255UL;
            ulong blasOffset = ulong.MaxValue;
            BufferChunk? bChunk = null;

            lock (_allocLock)
            {
                foreach (var chunk in _blasChunks)
                {
                    blasOffset = chunk.Allocate(alignedBlasSize);
                    if (blasOffset != ulong.MaxValue) { bChunk = chunk; break; }
                }
                if (blasOffset == ulong.MaxValue)
                {
                    bChunk = new BufferChunk(_device, BlasChunkCapacity, bUsage, isShared: true);
                    blasOffset = bChunk.Allocate(alignedBlasSize); _blasChunks.Add(bChunk);
                }
            }
            alloc.BlasByteOffset = blasOffset;
            alloc.BlasByteSize = alignedBlasSize;
            alloc.BlasChunk = bChunk!;

            var createInfo = new AccelerationStructureCreateInfoKHR { SType = StructureType.AccelerationStructureCreateInfoKhr, Buffer = bChunk!.Buffer.Buffer, Offset = blasOffset, Size = buildSizes.AccelerationStructureSize, Type = AccelerationStructureTypeKHR.BottomLevelKhr };
            _device.KhrAccelerationStructure.CreateAccelerationStructure(_device.Device, in createInfo, null, out uncompactedHandles[i]);

            ulong alignedScratch = (buildSizes.BuildScratchSize + 255) & ~255UL;
            scratchAlignedSizes[i] = alignedScratch;
            totalScratchSize += alignedScratch;
        }

        if (totalScratchSize > _scratchCapacities[frameIndex])
        {
            _scratchBuffers[frameIndex]?.Dispose();
            _scratchCapacities[frameIndex] = Math.Max(totalScratchSize, _scratchCapacities[frameIndex] * 2);
            _scratchCapacities[frameIndex] = Math.Max(_scratchCapacities[frameIndex], 8UL * 1024 * 1024);
            _scratchBuffers[frameIndex] = new VulkanBuffer(_device, _scratchCapacities[frameIndex], BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit, MemoryPropertyFlags.DeviceLocalBit);
        }

        if (totalScratchSize > 0)
        {
            ulong scratchOffset = 0;
            var ppBuildRanges = stackalloc AccelerationStructureBuildRangeInfoKHR*[uploadCount];

            int currentGeom = 0;
            for (int i = 0; i < uploadCount; i++)
            {
                uint gCount = 0;
                if (uploads[i].OpaqueIndexCount > 0) gCount++;
                if (uploads[i].IndexCount > uploads[i].OpaqueIndexCount) gCount++;

                buildInfos[i] = new AccelerationStructureBuildGeometryInfoKHR
                {
                    SType = StructureType.AccelerationStructureBuildGeometryInfoKhr, Type = AccelerationStructureTypeKHR.BottomLevelKhr,
                    Flags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr | BuildAccelerationStructureFlagsKHR.AllowCompactionBitKhr, Mode = BuildAccelerationStructureModeKHR.BuildKhr,
                    DstAccelerationStructure = uncompactedHandles[i], GeometryCount = gCount, PGeometries = &geometries[currentGeom],
                    ScratchData = new DeviceOrHostAddressKHR { DeviceAddress = _scratchBuffers[frameIndex]!.DeviceAddress + scratchOffset }
                };

                ppBuildRanges[i] = &buildRanges[currentGeom];
                currentGeom += (int)gCount;
                scratchOffset += scratchAlignedSizes[i];
            }

            _device.Vk.CmdResetQueryPool(graphicsCmd, _queryPool, 0, (uint)uploadCount);
            _device.KhrAccelerationStructure.CmdBuildAccelerationStructures(graphicsCmd, (uint)uploadCount, buildInfos, ppBuildRanges);

            var buildBarrier = new MemoryBarrier2
            {
                SType = StructureType.MemoryBarrier2,
                SrcStageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr,
                SrcAccessMask = AccessFlags2.AccelerationStructureWriteBitKhr,
                DstStageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr,
                DstAccessMask = AccessFlags2.AccelerationStructureReadBitKhr
            };
            var depInfo2 = new DependencyInfo { SType = StructureType.DependencyInfo, MemoryBarrierCount = 1, PMemoryBarriers = &buildBarrier };
            _device.Vk.CmdPipelineBarrier2(graphicsCmd, in depInfo2);

            _device.KhrAccelerationStructure.CmdWriteAccelerationStructuresProperties(graphicsCmd, (uint)uploadCount, uncompactedHandles, QueryType.AccelerationStructureCompactedSizeKhr, _queryPool, 0);
        }

        _device.Vk.EndCommandBuffer(graphicsCmd);

        var transferSignalInfo = new SemaphoreSubmitInfo { SType = StructureType.SemaphoreSubmitInfo, Semaphore = _transferCompleteSemaphore, StageMask = PipelineStageFlags2.TransferBit };
        var transferCmdInfo = new CommandBufferSubmitInfo { SType = StructureType.CommandBufferSubmitInfo, CommandBuffer = transferCmd };
        var transferSubmit = new SubmitInfo2 { SType = StructureType.SubmitInfo2, CommandBufferInfoCount = 1, PCommandBufferInfos = &transferCmdInfo, SignalSemaphoreInfoCount = 1, PSignalSemaphoreInfos = &transferSignalInfo };

        lock (_device.TransferQueueLock)
            _device.Vk.QueueSubmit2(_device.TransferQueue, 1, in transferSubmit, default);

        var graphicsWaitInfo = new SemaphoreSubmitInfo { SType = StructureType.SemaphoreSubmitInfo, Semaphore = _transferCompleteSemaphore, StageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr };
        var graphicsCmdInfo = new CommandBufferSubmitInfo { SType = StructureType.CommandBufferSubmitInfo, CommandBuffer = graphicsCmd };
        var graphicsSubmit = new SubmitInfo2 { SType = StructureType.SubmitInfo2, WaitSemaphoreInfoCount = 1, PWaitSemaphoreInfos = &graphicsWaitInfo, CommandBufferInfoCount = 1, PCommandBufferInfos = &graphicsCmdInfo };

        _device.Vk.ResetFences(_device.Device, 1, in _uploadFence);

        lock (_device.QueueLock)
            _device.Vk.QueueSubmit2(_device.GraphicsQueue, 1, in graphicsSubmit, _uploadFence);

        _device.Vk.WaitForFences(_device.Device, 1, in _uploadFence, Vk.True, ulong.MaxValue);

        ulong* compactedSizes = stackalloc ulong[uploadCount];
        _device.Vk.GetQueryPoolResults(_device.Device, _queryPool, 0, (uint)uploadCount, (nuint)(uploadCount * sizeof(ulong)), compactedSizes, (ulong)sizeof(ulong), QueryResultFlags.ResultWaitBit | QueryResultFlags.Result64Bit);

        _device.Vk.ResetCommandBuffer(graphicsCmd, 0);
        _device.Vk.BeginCommandBuffer(graphicsCmd, in beginGraphicsInfo);

        AccelerationStructureKHR[] compactedHandles = new AccelerationStructureKHR[uploadCount];
        ulong[] oldOffsets = new ulong[uploadCount];
        ulong[] oldSizes = new ulong[uploadCount];
        BufferChunk[] oldChunks = new BufferChunk[uploadCount];

        for (int i = 0; i < uploadCount; i++)
        {
            var alloc = uploads[i].Allocation;
            oldOffsets[i] = alloc.BlasByteOffset;
            oldSizes[i] = alloc.BlasByteSize;
            oldChunks[i] = alloc.BlasChunk;

            ulong compactedSize = AlignUp(compactedSizes[i], 256);
            ulong blasOffset = ulong.MaxValue;
            BufferChunk? bChunk = null;

            lock (_allocLock)
            {
                foreach (var chunk in _blasChunks)
                {
                    blasOffset = chunk.Allocate(compactedSize);
                    if (blasOffset != ulong.MaxValue) { bChunk = chunk; break; }
                }
                if (blasOffset == ulong.MaxValue)
                {
                    bChunk = new BufferChunk(_device, BlasChunkCapacity, bUsage, isShared: true);
                    blasOffset = bChunk.Allocate(compactedSize); _blasChunks.Add(bChunk);
                }
            }

            alloc.BlasByteOffset = blasOffset;
            alloc.BlasByteSize = compactedSize;
            alloc.BlasChunk = bChunk!;

            var createInfo = new AccelerationStructureCreateInfoKHR { SType = StructureType.AccelerationStructureCreateInfoKhr, Buffer = bChunk!.Buffer.Buffer, Offset = blasOffset, Size = compactedSize, Type = AccelerationStructureTypeKHR.BottomLevelKhr };
            _device.KhrAccelerationStructure.CreateAccelerationStructure(_device.Device, in createInfo, null, out compactedHandles[i]);

            var copyInfo = new CopyAccelerationStructureInfoKHR { SType = StructureType.CopyAccelerationStructureInfoKhr, Src = uncompactedHandles[i], Dst = compactedHandles[i], Mode = CopyAccelerationStructureModeKHR.CompactKhr };
            _device.KhrAccelerationStructure.CmdCopyAccelerationStructure(graphicsCmd, in copyInfo);

            var addressInfo = new AccelerationStructureDeviceAddressInfoKHR { SType = StructureType.AccelerationStructureDeviceAddressInfoKhr, AccelerationStructure = compactedHandles[i] };
            alloc.BlasDeviceAddress = _device.KhrAccelerationStructure.GetAccelerationStructureDeviceAddress(_device.Device, in addressInfo);
            alloc.Blas = compactedHandles[i];
        }

        _device.Vk.EndCommandBuffer(graphicsCmd);

        ulong signalValue = _submitCounter + 1;
        var graphicsSignalInfo = new SemaphoreSubmitInfo { SType = StructureType.SemaphoreSubmitInfo, Semaphore = _timelineSemaphore, Value = signalValue, StageMask = PipelineStageFlags2.AllCommandsBit };
        var graphicsSubmit2 = new SubmitInfo2 { SType = StructureType.SubmitInfo2, CommandBufferInfoCount = 1, PCommandBufferInfos = &graphicsCmdInfo, SignalSemaphoreInfoCount = 1, PSignalSemaphoreInfos = &graphicsSignalInfo };

        _device.Vk.ResetFences(_device.Device, 1, in _uploadFence);

        lock (_device.QueueLock)
            _device.Vk.QueueSubmit2(_device.GraphicsQueue, 1, in graphicsSubmit2, _uploadFence);

        _device.Vk.WaitForFences(_device.Device, 1, in _uploadFence, Vk.True, ulong.MaxValue);

        for (int i = 0; i < uploadCount; i++)
        {
            _device.KhrAccelerationStructure.DestroyAccelerationStructure(_device.Device, uncompactedHandles[i], null);
            lock (_allocLock) oldChunks[i].Free(oldOffsets[i], oldSizes[i]);
            uploads[i].Allocation.ReadySyncValue = signalValue;
        }
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

        _device.Vk.DestroyQueryPool(_device.Device, _queryPool, null);
        _device.Vk.DestroyFence(_device.Device, _uploadFence, null);

        _device.Vk.DestroySemaphore(_device.Device, _timelineSemaphore, null);
        _device.Vk.DestroySemaphore(_device.Device, _transferCompleteSemaphore, null);
        _device.Vk.DestroyCommandPool(_device.Device, _cmdPool, null);
        _device.Vk.DestroyCommandPool(_device.Device, _transferCmdPool, null);

        for (int i = 0; i < MaxInFlight; i++)
        {
            _stagingBuffers[i]?.Dispose();
            _scratchBuffers[i]?.Dispose();
        }

        foreach (var chunk in _vertexChunks) chunk.Dispose();
        foreach (var chunk in _indexChunks) chunk.Dispose();
        foreach (var chunk in _blasChunks) chunk.Dispose();
    }
}