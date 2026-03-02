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
        public ulong VertexDstOffset;
        public ulong VertexSize;
        public ulong IndexDstOffset;
        public ulong IndexSize;
    }

    private readonly VulkanDevice _device;
    public VulkanBuffer VertexBuffer { get; private set; }
    public VulkanBuffer IndexBuffer { get; private set; }

    private ulong _logicalVertexCapacity;
    private ulong _logicalIndexCapacity;

    private ulong _physicalVertexCapacity;
    private ulong _physicalIndexCapacity;

    private readonly Lock _allocLock = new();

    private readonly List<MeshFreeBlock> _freeVertexBlocks = [];
    private readonly List<MeshFreeBlock> _freeIndexBlocks = [];

    private readonly ConcurrentQueue<PendingUpload> _pendingUploads = new();

    private readonly List<VulkanBuffer>[] _staleBuffers;

    public DynamicMeshPool(VulkanDevice device, int maxFramesInFlight = 2, ulong initialVertexBytes = 128 * 1024 * 1024, ulong initialIndexBytes = 32 * 1024 * 1024)
    {
        _device = device;
        _logicalVertexCapacity = initialVertexBytes;
        _logicalIndexCapacity = initialIndexBytes;
        _physicalVertexCapacity = initialVertexBytes;
        _physicalIndexCapacity = initialIndexBytes;

        _staleBuffers = new List<VulkanBuffer>[maxFramesInFlight];
        for (int i = 0; i < maxFramesInFlight; i++) _staleBuffers[i] = [];

        var usage = BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.TransferSrcBit;
        VertexBuffer = new VulkanBuffer(_device, _physicalVertexCapacity, usage, MemoryPropertyFlags.DeviceLocalBit);
        IndexBuffer = new VulkanBuffer(_device, _physicalIndexCapacity, BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.DeviceLocalBit);

        _freeVertexBlocks.Add(new MeshFreeBlock { Offset = 0, Size = _logicalVertexCapacity });
        _freeIndexBlocks.Add(new MeshFreeBlock { Offset = 0, Size = _logicalIndexCapacity });
    }

    public MeshAllocation Allocate<T>(T[] vertices, uint[] indices) where T : unmanaged
    {
        ulong vertexSize = (ulong)(vertices.Length * sizeof(T));
        ulong indexSize = (ulong)(indices.Length * sizeof(uint));
        ulong vOffset, iOffset;

        lock (_allocLock)
        {
            vOffset = AllocateBlock(_freeVertexBlocks, vertexSize);
            if (vOffset == ulong.MaxValue)
            {
                ulong newCapacity = Math.Max(_logicalVertexCapacity * 2, _logicalVertexCapacity + vertexSize);
                AddAndMergeFreeBlock(_freeVertexBlocks, _logicalVertexCapacity, newCapacity - _logicalVertexCapacity);
                _logicalVertexCapacity = newCapacity;
                vOffset = AllocateBlock(_freeVertexBlocks, vertexSize);
            }

            iOffset = AllocateBlock(_freeIndexBlocks, indexSize);
            if (iOffset == ulong.MaxValue)
            {
                ulong newCapacity = Math.Max(_logicalIndexCapacity * 2, _logicalIndexCapacity + indexSize);
                AddAndMergeFreeBlock(_freeIndexBlocks, _logicalIndexCapacity, newCapacity - _logicalIndexCapacity);
                _logicalIndexCapacity = newCapacity;
                iOffset = AllocateBlock(_freeIndexBlocks, indexSize);
            }
        }

        _pendingUploads.Enqueue(new PendingUpload
        {
            Vertices = vertices,
            Indices = indices,
            VertexDstOffset = vOffset,
            VertexSize = vertexSize,
            IndexDstOffset = iOffset,
            IndexSize = indexSize
        });

        return new MeshAllocation((uint)indices.Length, (uint)(iOffset / sizeof(uint)), (int)(vOffset / (ulong)sizeof(T)), vOffset, vertexSize, iOffset, indexSize);
    }

    public void FlushUploads(CommandBuffer cmd, int currentFrame)
    {
        ulong targetVCap, targetICap;
        lock (_allocLock)
        {
            targetVCap = _logicalVertexCapacity;
            targetICap = _logicalIndexCapacity;
        }

        if (_physicalVertexCapacity < targetVCap)
        {
            var newBuffer = new VulkanBuffer(_device, targetVCap, BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.DeviceLocalBit);
            BufferCopy copyRegion = new() { SrcOffset = 0, DstOffset = 0, Size = _physicalVertexCapacity };
            _device.Vk.CmdCopyBuffer(cmd, VertexBuffer.Buffer, newBuffer.Buffer, 1, in copyRegion);
            _staleBuffers[currentFrame].Add(VertexBuffer);
            VertexBuffer = newBuffer;
            _physicalVertexCapacity = targetVCap;
        }

        if (_physicalIndexCapacity < targetICap)
        {
            var newBuffer = new VulkanBuffer(_device, targetICap, BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.DeviceLocalBit);
            BufferCopy copyRegion = new() { SrcOffset = 0, DstOffset = 0, Size = _physicalIndexCapacity };
            _device.Vk.CmdCopyBuffer(cmd, IndexBuffer.Buffer, newBuffer.Buffer, 1, in copyRegion);
            _staleBuffers[currentFrame].Add(IndexBuffer);
            IndexBuffer = newBuffer;
            _physicalIndexCapacity = targetICap;
        }

        if (_pendingUploads.IsEmpty) return;

        List<PendingUpload> uploads = [];
        ulong totalSize = 0;
        while (_pendingUploads.TryDequeue(out var upload))
        {
            uploads.Add(upload);
            totalSize += upload.VertexSize + upload.IndexSize;
        }

        if (totalSize == 0) return;

        var stagingBuffer = new VulkanBuffer(_device, totalSize, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        ulong currentOffset = 0;

        foreach (var upload in uploads)
        {
            GCHandle vHandle = GCHandle.Alloc(upload.Vertices, GCHandleType.Pinned);
            System.Buffer.MemoryCopy((void*)vHandle.AddrOfPinnedObject(), (byte*)stagingBuffer.MappedMemory + currentOffset, upload.VertexSize, upload.VertexSize);
            vHandle.Free();

            BufferCopy vCopy = new() { SrcOffset = currentOffset, DstOffset = upload.VertexDstOffset, Size = upload.VertexSize };
            _device.Vk.CmdCopyBuffer(cmd, stagingBuffer.Buffer, VertexBuffer.Buffer, 1, in vCopy);
            currentOffset += upload.VertexSize;

            GCHandle iHandle = GCHandle.Alloc(upload.Indices, GCHandleType.Pinned);
            System.Buffer.MemoryCopy((void*)iHandle.AddrOfPinnedObject(), (byte*)stagingBuffer.MappedMemory + currentOffset, upload.IndexSize, upload.IndexSize);
            iHandle.Free();

            BufferCopy iCopy = new() { SrcOffset = currentOffset, DstOffset = upload.IndexDstOffset, Size = upload.IndexSize };
            _device.Vk.CmdCopyBuffer(cmd, stagingBuffer.Buffer, IndexBuffer.Buffer, 1, in iCopy);
            currentOffset += upload.IndexSize;
        }

        _staleBuffers[currentFrame].Add(stagingBuffer);

        MemoryBarrier barrier = new()
        {
            SType = StructureType.MemoryBarrier,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = AccessFlags.VertexAttributeReadBit | AccessFlags.IndexReadBit
        };
        _device.Vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.VertexInputBit, 0, 1, in barrier, 0, null, 0, null);
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
        foreach (var buffer in _staleBuffers[currentFrame])
            buffer.Dispose();
        _staleBuffers[currentFrame].Clear();
    }

    public void Dispose()
    {
        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
        foreach (var list in _staleBuffers)
            foreach (var buf in list) buf.Dispose();
    }
}