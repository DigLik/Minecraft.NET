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

    private struct PendingUpload
    {
        public void* Vertices;
        public int VertexByteSize;
        public int VertexCount;
        public int VertexStride;

        public void* Indices;
        public int IndexByteSize;
        public int IndexCount;

        public MeshAllocation Allocation;
    }

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
    private CommandPool _cmdPool;
    private readonly CommandBuffer[] _cmds = new CommandBuffer[MaxInFlight];

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

        CommandPoolCreateInfo poolInfo = new() { SType = StructureType.CommandPoolCreateInfo, Flags = CommandPoolCreateFlags.ResetCommandBufferBit, QueueFamilyIndex = _device.GraphicsFamilyIndex };
        _device.Vk.CreateCommandPool(_device.Device, in poolInfo, null, out _cmdPool);

        CommandBufferAllocateInfo allocInfo = new() { SType = StructureType.CommandBufferAllocateInfo, Level = CommandBufferLevel.Primary, CommandPool = _cmdPool, CommandBufferCount = MaxInFlight };
        fixed (CommandBuffer* pCmds = _cmds) _device.Vk.AllocateCommandBuffers(_device.Device, in allocInfo, pCmds);

        _uploadThread = new Thread(UploadLoop) { IsBackground = true, Name = "VulkanUploadThread" };
        _uploadThread.Start();
    }

    public MeshAllocation Allocate<T>(NativeList<T> vertices, NativeList<uint> indices) where T : unmanaged
    {
        ulong vertexSize = (ulong)(vertices.Count * sizeof(T));
        ulong indexSize = (ulong)(indices.Count * sizeof(uint));

        ulong vOffset = ulong.MaxValue, iOffset = ulong.MaxValue;
        BufferChunk? vChunk = null, iChunk = null;

        lock (_allocLock)
        {
            foreach (var chunk in _vertexChunks)
            {
                vOffset = chunk.Allocate(vertexSize);
                if (vOffset != ulong.MaxValue) { vChunk = chunk; break; }
            }
            if (vOffset == ulong.MaxValue)
            {
                vChunk = new BufferChunk(_device, VertexChunkCapacity, vUsage);
                vOffset = vChunk.Allocate(vertexSize); _vertexChunks.Add(vChunk);
            }

            foreach (var chunk in _indexChunks)
            {
                iOffset = chunk.Allocate(indexSize);
                if (iOffset != ulong.MaxValue) { iChunk = chunk; break; }
            }
            if (iOffset == ulong.MaxValue)
            {
                iChunk = new BufferChunk(_device, IndexChunkCapacity, iUsage);
                iOffset = iChunk.Allocate(indexSize); _indexChunks.Add(iChunk);
            }
        }

        var alloc = new MeshAllocation(this, (uint)indices.Count, (uint)(iOffset / sizeof(uint)), (int)(vOffset / (ulong)sizeof(T)), vOffset, vertexSize, iOffset, indexSize)
        {
            VertexChunk = vChunk!,
            IndexChunk = iChunk!
        };

        _pendingUploads.Add(new PendingUpload
        {
            Vertices = vertices.Data,
            VertexByteSize = (int)vertexSize,
            VertexCount = vertices.Count,
            VertexStride = sizeof(T),
            Indices = indices.Data,
            IndexByteSize = (int)indexSize,
            IndexCount = indices.Count,
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
                ulong waitValue = _submitCounter >= MaxInFlight ? _submitCounter - MaxInFlight + 1 : 0;
                if (waitValue > 0)
                {
                    SemaphoreWaitInfo waitInfo = new() { SType = StructureType.SemaphoreWaitInfo, SemaphoreCount = 1, PSemaphores = (Semaphore*)Unsafe.AsPointer(ref _timelineSemaphore), PValues = &waitValue };
                    _device.Vk.WaitSemaphores(_device.Device, in waitInfo, ulong.MaxValue);
                }

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
        PendingUpload[] uploads = new PendingUpload[8];
        uploads[0] = firstUpload;
        int uploadCount = 1;

        ulong totalSize = (ulong)firstUpload.VertexByteSize + (ulong)firstUpload.IndexByteSize;

        while (uploadCount < 8 && _pendingUploads.TryTake(out var nextUpload))
        {
            uploads[uploadCount++] = nextUpload;
            totalSize += (ulong)nextUpload.VertexByteSize + (ulong)nextUpload.IndexByteSize;
        }

        if (totalSize > _stagingCapacities[frameIndex])
        {
            _stagingBuffers[frameIndex]?.Dispose();
            _stagingCapacities[frameIndex] = Math.Max(totalSize, _stagingCapacities[frameIndex] * 2);
            _stagingCapacities[frameIndex] = Math.Max(_stagingCapacities[frameIndex], 16UL * 1024 * 1024);
            _stagingBuffers[frameIndex] = new VulkanBuffer(_device, _stagingCapacities[frameIndex], BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        }

        var stagingBuffer = _stagingBuffers[frameIndex]!;
        var cmd = _cmds[frameIndex];

        ulong currentOffset = 0;
        _device.Vk.ResetCommandBuffer(cmd, 0);

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
        _device.Vk.BeginCommandBuffer(cmd, in beginInfo);

        foreach (ref readonly var upload in uploads.AsSpan(0, uploadCount))
        {
            var alloc = upload.Allocation;

            System.Buffer.MemoryCopy(upload.Vertices, (byte*)stagingBuffer.MappedMemory + currentOffset, alloc.VertexByteSize, alloc.VertexByteSize);
            BufferCopy2 vCopy = new() { SType = StructureType.BufferCopy2, SrcOffset = currentOffset, DstOffset = alloc.VertexByteOffset, Size = alloc.VertexByteSize };
            CopyBufferInfo2 vCopyInfo = new() { SType = StructureType.CopyBufferInfo2, SrcBuffer = stagingBuffer.Buffer, DstBuffer = alloc.VertexChunk.Buffer.Buffer, RegionCount = 1, PRegions = &vCopy };
            _device.Vk.CmdCopyBuffer2(cmd, in vCopyInfo);
            currentOffset += alloc.VertexByteSize;

            System.Buffer.MemoryCopy(upload.Indices, (byte*)stagingBuffer.MappedMemory + currentOffset, alloc.IndexByteSize, alloc.IndexByteSize);
            BufferCopy2 iCopy = new() { SType = StructureType.BufferCopy2, SrcOffset = currentOffset, DstOffset = alloc.IndexByteOffset, Size = alloc.IndexByteSize };
            CopyBufferInfo2 iCopyInfo = new() { SType = StructureType.CopyBufferInfo2, SrcBuffer = stagingBuffer.Buffer, DstBuffer = alloc.IndexChunk.Buffer.Buffer, RegionCount = 1, PRegions = &iCopy };
            _device.Vk.CmdCopyBuffer2(cmd, in iCopyInfo);
            currentOffset += alloc.IndexByteSize;

            NativeMemory.Free(upload.Vertices);
            NativeMemory.Free(upload.Indices);
        }

        var transferBarrier = new MemoryBarrier2
        {
            SType = StructureType.MemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.TransferBit,
            SrcAccessMask = AccessFlags2.TransferWriteBit,
            DstStageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr | PipelineStageFlags2.RayTracingShaderBitKhr,
            DstAccessMask = AccessFlags2.AccelerationStructureReadBitKhr | AccessFlags2.ShaderReadBit
        };
        var depInfo1 = new DependencyInfo { SType = StructureType.DependencyInfo, MemoryBarrierCount = 1, PMemoryBarriers = &transferBarrier };
        _device.Vk.CmdPipelineBarrier2(cmd, in depInfo1);

        var geometries = stackalloc AccelerationStructureGeometryKHR[uploadCount];
        var buildInfos = stackalloc AccelerationStructureBuildGeometryInfoKHR[uploadCount];
        var buildRanges = stackalloc AccelerationStructureBuildRangeInfoKHR[uploadCount];
        var scratchAlignedSizes = stackalloc ulong[uploadCount];
        ulong totalScratchSize = 0;

        for (int i = 0; i < uploadCount; i++)
        {
            var upload = uploads[i];
            var alloc = upload.Allocation;

            var triangles = new AccelerationStructureGeometryTrianglesDataKHR
            {
                SType = StructureType.AccelerationStructureGeometryTrianglesDataKhr,
                VertexFormat = Format.R32G32B32Sfloat,
                VertexData = new DeviceOrHostAddressConstKHR { DeviceAddress = alloc.VertexAddress + alloc.VertexByteOffset },
                VertexStride = (ulong)upload.VertexStride,
                MaxVertex = (uint)(alloc.VertexByteSize / (ulong)upload.VertexStride) - 1,
                IndexType = IndexType.Uint32,
                IndexData = new DeviceOrHostAddressConstKHR { DeviceAddress = alloc.IndexAddress + alloc.IndexByteOffset }
            };

            geometries[i] = new AccelerationStructureGeometryKHR
            {
                SType = StructureType.AccelerationStructureGeometryKhr,
                GeometryType = GeometryTypeKHR.TrianglesKhr,
                Geometry = new AccelerationStructureGeometryDataKHR { Triangles = triangles },
                Flags = GeometryFlagsKHR.None
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
                foreach (var chunk in _blasChunks)
                {
                    blasOffset = chunk.Allocate(alignedBlasSize);
                    if (blasOffset != ulong.MaxValue) { bChunk = chunk; break; }
                }
                if (blasOffset == ulong.MaxValue)
                {
                    bChunk = new BufferChunk(_device, BlasChunkCapacity, bUsage);
                    blasOffset = bChunk.Allocate(alignedBlasSize); _blasChunks.Add(bChunk);
                }
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

            for (int i = 0; i < uploadCount; i++)
            {
                var alloc = uploads[i].Allocation;

                buildInfos[i] = new AccelerationStructureBuildGeometryInfoKHR
                {
                    SType = StructureType.AccelerationStructureBuildGeometryInfoKhr, Type = AccelerationStructureTypeKHR.BottomLevelKhr,
                    Flags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr, Mode = BuildAccelerationStructureModeKHR.BuildKhr,
                    DstAccelerationStructure = alloc.Blas, GeometryCount = 1, PGeometries = &geometries[i],
                    ScratchData = new DeviceOrHostAddressKHR { DeviceAddress = _scratchBuffers[frameIndex]!.DeviceAddress + scratchOffset }
                };

                buildRanges[i] = new AccelerationStructureBuildRangeInfoKHR { PrimitiveCount = alloc.IndexCount / 3, PrimitiveOffset = 0, FirstVertex = 0, TransformOffset = 0 };
                ppBuildRanges[i] = &buildRanges[i];
                scratchOffset += scratchAlignedSizes[i];
            }

            _device.KhrAccelerationStructure.CmdBuildAccelerationStructures(cmd, (uint)uploadCount, buildInfos, ppBuildRanges);

            var buildBarrier = new MemoryBarrier2
            {
                SType = StructureType.MemoryBarrier2,
                SrcStageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr,
                SrcAccessMask = AccessFlags2.AccelerationStructureWriteBitKhr,
                DstStageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr,
                DstAccessMask = AccessFlags2.AccelerationStructureReadBitKhr | AccessFlags2.AccelerationStructureWriteBitKhr
            };
            var depInfo2 = new DependencyInfo { SType = StructureType.DependencyInfo, MemoryBarrierCount = 1, PMemoryBarriers = &buildBarrier };
            _device.Vk.CmdPipelineBarrier2(cmd, in depInfo2);
        }

        _device.Vk.EndCommandBuffer(cmd);

        ulong signalValue = _submitCounter + 1;
        var signalInfo = new SemaphoreSubmitInfo { SType = StructureType.SemaphoreSubmitInfo, Semaphore = _timelineSemaphore, Value = signalValue, StageMask = PipelineStageFlags2.AllCommandsBit };
        var cmdInfo = new CommandBufferSubmitInfo { SType = StructureType.CommandBufferSubmitInfo, CommandBuffer = cmd };
        var submitInfo = new SubmitInfo2 { SType = StructureType.SubmitInfo2, CommandBufferInfoCount = 1, PCommandBufferInfos = &cmdInfo, SignalSemaphoreInfoCount = 1, PSignalSemaphoreInfos = &signalInfo };

        lock (_device.QueueLock)
        {
            _device.Vk.QueueSubmit2(_device.GraphicsQueue, 1, in submitInfo, default);
        }

        for (int i = 0; i < uploadCount; i++) uploads[i].Allocation.ReadySyncValue = signalValue;
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

        _device.Vk.DestroySemaphore(_device.Device, _timelineSemaphore, null);
        _device.Vk.DestroyCommandPool(_device.Device, _cmdPool, null);

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