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
    public VulkanBuffer BlasBuffer { get; private set; }

    private readonly Lock _allocLock = new();

    private readonly List<MeshFreeBlock> _freeVertexBlocks = [];
    private readonly List<MeshFreeBlock> _freeIndexBlocks = [];
    private readonly List<MeshFreeBlock> _freeBlasBlocks = [];

    private readonly BlockingCollection<PendingUpload> _pendingUploads = new(new ConcurrentQueue<PendingUpload>());

    private readonly Thread _uploadThread;

    public DynamicMeshPool(VulkanDevice device)
    {
        _device = device;

        ulong vCap = 3072UL * 1024 * 1024; // 3GB
        ulong iCap = 1024UL * 1024 * 1024; // 1GB
        ulong bCap = 1024UL * 1024 * 1024; // 1GB

        var usage = BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;
        var iUsage = BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;
        var bUsage = BufferUsageFlags.AccelerationStructureStorageBitKhr | BufferUsageFlags.ShaderDeviceAddressBit;

        VertexBuffer = new VulkanBuffer(_device, vCap, usage, MemoryPropertyFlags.DeviceLocalBit);
        IndexBuffer = new VulkanBuffer(_device, iCap, iUsage, MemoryPropertyFlags.DeviceLocalBit);
        BlasBuffer = new VulkanBuffer(_device, bCap, bUsage, MemoryPropertyFlags.DeviceLocalBit);

        _freeVertexBlocks.Add(new MeshFreeBlock { Offset = 0, Size = vCap });
        _freeIndexBlocks.Add(new MeshFreeBlock { Offset = 0, Size = iCap });
        _freeBlasBlocks.Add(new MeshFreeBlock { Offset = 0, Size = bCap });

        _uploadThread = new Thread(UploadLoop) { IsBackground = true, Name = "VulkanUploadThread" };
        _uploadThread.Start();
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

                MemoryBarrier transferBarrier = new()
                {
                    SType = StructureType.MemoryBarrier, SrcAccessMask = AccessFlags.TransferWriteBit, DstAccessMask = AccessFlags.AccelerationStructureReadBitKhr | AccessFlags.ShaderReadBit
                };
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
                        VertexData = new DeviceOrHostAddressConstKHR { DeviceAddress = VertexBuffer.DeviceAddress + alloc.VertexByteOffset },
                        VertexStride = 64,
                        MaxVertex = (uint)(alloc.VertexByteSize / 64) - 1,
                        IndexType = IndexType.Uint32,
                        IndexData = new DeviceOrHostAddressConstKHR { DeviceAddress = IndexBuffer.DeviceAddress + alloc.IndexByteOffset }
                    };

                    geometries[i] = new AccelerationStructureGeometryKHR
                    {
                        SType = StructureType.AccelerationStructureGeometryKhr, GeometryType = GeometryTypeKHR.TrianglesKhr,
                        Geometry = new AccelerationStructureGeometryDataKHR { Triangles = triangles }, Flags = GeometryFlagsKHR.OpaqueBitKhr
                    };

                    var buildInfoSize = new AccelerationStructureBuildGeometryInfoKHR
                    {
                        SType = StructureType.AccelerationStructureBuildGeometryInfoKhr, Type = AccelerationStructureTypeKHR.BottomLevelKhr,
                        Flags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr, GeometryCount = 1
                    };

                    uint maxPrimitiveCount = alloc.IndexCount / 3;
                    AccelerationStructureGeometryKHR tempGeom = geometries[i];
                    buildInfoSize.PGeometries = &tempGeom;

                    _device.KhrAccelerationStructure.GetAccelerationStructureBuildSizes(_device.Device, AccelerationStructureBuildTypeKHR.DeviceKhr, in buildInfoSize, &maxPrimitiveCount, out var buildSizes);

                    ulong alignedBlasSize = (buildSizes.AccelerationStructureSize + 255) & ~255UL;
                    ulong blasOffset;
                    lock (_allocLock)
                    {
                        blasOffset = AllocateBlock(_freeBlasBlocks, alignedBlasSize);
                        if (blasOffset == ulong.MaxValue) throw new Exception("BLAS Pool Out of Memory");
                    }
                    alloc.BlasByteOffset = blasOffset;
                    alloc.BlasByteSize = alignedBlasSize;

                    var createInfo = new AccelerationStructureCreateInfoKHR
                    {
                        SType = StructureType.AccelerationStructureCreateInfoKhr,
                        Buffer = BlasBuffer.Buffer,
                        Offset = blasOffset,
                        Size = buildSizes.AccelerationStructureSize,
                        Type = AccelerationStructureTypeKHR.BottomLevelKhr
                    };
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
        }
        catch (Exception) { }
        finally
        {
            stagingBuffer?.Dispose();
            scratchBuffer?.Dispose();
            _device.Vk.DestroyFence(_device.Device, fence, null);
            _device.Vk.FreeCommandBuffers(_device.Device, cmdPool, 1, in cmd);
            _device.Vk.DestroyCommandPool(_device.Device, cmdPool, null);
        }
    }

    public void Free(MeshAllocation allocation)
    {
        lock (_allocLock)
        {
            AddAndMergeFreeBlock(_freeVertexBlocks, allocation.VertexByteOffset, allocation.VertexByteSize);
            AddAndMergeFreeBlock(_freeIndexBlocks, allocation.IndexByteOffset, allocation.IndexByteSize);
            if (allocation.BlasByteSize > 0)
                AddAndMergeFreeBlock(_freeBlasBlocks, allocation.BlasByteOffset, allocation.BlasByteSize);
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

    public void Dispose()
    {
        _pendingUploads.CompleteAdding();
        _uploadThread.Join();

        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
        BlasBuffer.Dispose();
    }
}