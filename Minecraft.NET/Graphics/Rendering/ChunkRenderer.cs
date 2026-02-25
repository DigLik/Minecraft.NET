using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

using Minecraft.NET.Engine;
using Minecraft.NET.Graphics.Models;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Minecraft.NET.Graphics.Rendering;

public readonly record struct ChunkMeshGeometry(nint Buffer, uint VertexCount, uint IndexCount);

public sealed unsafe class ChunkRenderer : IChunkRenderer
{
    private readonly D3D12Context _d3d;

    // 256 КБ на один чанк с запасом. 2048 слотов = 512 МБ на один пул
    private const ulong SlotSize = 256 * 1024;
    private const int SlotsPerPool = 2048;

    private struct Pool
    {
        public ID3D12Resource* DefaultBuffer; // VRAM (Для максимальной скорости рендеринга)
        public ID3D12Resource* StagingBuffer; // RAM (Для мгновенной записи из CPU-потоков)
        public byte* MappedData;
        public ulong GpuAddress;
        public bool[] IsFlushed;              // Флаг готовности меша в видеопамяти
    }

    private readonly Pool[] _pools = new Pool[16]; // Лимит до 8 ГБ чанков
    private volatile int _activePools = 0;
    private readonly object _poolLock = new object();

    private readonly ConcurrentQueue<(int poolIndex, int slotIndex)> _freeSlots = new();

    private struct UploadRequest
    {
        public int PoolIndex;
        public int SlotIndex;
        public ulong Offset;
        public ulong Size;
    }

    private readonly ConcurrentQueue<UploadRequest> _pendingUploads = new();
    private readonly List<UploadRequest> _uploadBatch = new();
    private readonly ConcurrentQueue<(int, int, ulong)> _deferredFrees = new();

    private bool _isDisposed;

    public ChunkRenderer(D3D12Context d3d)
    {
        _d3d = d3d;
    }

    public void Initialize()
    {
        AllocatePool();
    }

    private void AllocatePool()
    {
        lock (_poolLock)
        {
            if (!_freeSlots.IsEmpty) return;

            int poolIndex = _activePools;
            if (poolIndex >= _pools.Length)
                throw new OutOfMemoryException("Maximum chunk memory pools reached!");

            var device = (ID3D12Device*)_d3d.Device.Handle;
            ulong totalSize = SlotSize * SlotsPerPool;

            var defaultHeapProps = new HeapProperties(HeapType.Default, CpuPageProperty.Unknown, MemoryPool.Unknown, 1, 1);
            var bufferDesc = new ResourceDesc
            {
                Dimension = ResourceDimension.Buffer, Width = totalSize, Height = 1, DepthOrArraySize = 1,
                MipLevels = 1, Format = Format.FormatUnknown, SampleDesc = new SampleDesc(1, 0),
                Layout = TextureLayout.LayoutRowMajor, Flags = ResourceFlags.None
            };

            ID3D12Resource* defaultBuffer;
            var uuid = SilkMarshal.GuidPtrOf<ID3D12Resource>();
            int hr = device->CreateCommittedResource(&defaultHeapProps, HeapFlags.None, &bufferDesc, ResourceStates.GenericRead, null, uuid, (void**)&defaultBuffer);
            if (hr < 0) throw new Exception("Failed to allocate VRAM chunk pool.");

            var uploadHeapProps = new HeapProperties(HeapType.Upload, CpuPageProperty.Unknown, MemoryPool.Unknown, 1, 1);
            ID3D12Resource* stagingBuffer;
            hr = device->CreateCommittedResource(&uploadHeapProps, HeapFlags.None, &bufferDesc, ResourceStates.GenericRead, null, uuid, (void**)&stagingBuffer);
            if (hr < 0) throw new Exception("Failed to allocate Staging chunk pool.");

            void* mapped;
            stagingBuffer->Map(0, null, &mapped);

            _pools[poolIndex] = new Pool
            {
                DefaultBuffer = defaultBuffer,
                StagingBuffer = stagingBuffer,
                MappedData = (byte*)mapped,
                GpuAddress = defaultBuffer->GetGPUVirtualAddress(),
                IsFlushed = new bool[SlotsPerPool]
            };

            Interlocked.Increment(ref _activePools);

            for (int i = 0; i < SlotsPerPool; i++)
            {
                _freeSlots.Enqueue((poolIndex, i));
            }
        }
    }

    public ChunkMeshGeometry UploadChunkMesh(MeshData meshData)
    {
        if (meshData.IndexCount == 0)
        {
            meshData.Dispose();
            return default;
        }

        uint vboSize = (uint)(meshData.VertexCount * ChunkVertex.Stride);
        uint eboSize = (uint)(meshData.IndexCount * sizeof(uint));
        uint vboAlignedSize = (vboSize + 255u) & ~255u;

        if (vboAlignedSize + eboSize > SlotSize)
        {
            meshData.Dispose();
            return default;
        }

        if (!_freeSlots.TryDequeue(out var slot))
        {
            AllocatePool();
            _freeSlots.TryDequeue(out slot);
        }

        var pool = _pools[slot.poolIndex];
        ulong offset = (ulong)slot.slotIndex * SlotSize;
        byte* dest = pool.MappedData + offset;

        // Сбрасываем флаг готовности для защиты от отрисовки мусора
        pool.IsFlushed[slot.slotIndex] = false;

        Unsafe.CopyBlock(dest, (void*)meshData.Vertices, vboSize);
        Unsafe.CopyBlock(dest + vboAlignedSize, meshData.Indices, eboSize);

        uint vCount = (uint)meshData.VertexCount;
        uint iCount = (uint)meshData.IndexCount;
        meshData.Dispose();

        _pendingUploads.Enqueue(new UploadRequest { PoolIndex = slot.poolIndex, SlotIndex = slot.slotIndex, Offset = offset, Size = vboAlignedSize + eboSize });

        nint packedBuffer = (nint)((slot.poolIndex << 16) | slot.slotIndex);
        return new ChunkMeshGeometry(packedBuffer, vCount, iCount);
    }

    public void FreeChunkMesh(ChunkMeshGeometry geometry)
    {
        if (geometry.IndexCount > 0)
        {
            int poolIndex = (int)geometry.Buffer >> 16;
            int slotIndex = (int)geometry.Buffer & 0xFFFF;
            ulong releaseFence = _d3d.FenceValues[_d3d.FrameIndex];
            _deferredFrees.Enqueue((poolIndex, slotIndex, releaseFence));
        }
    }

    public void FlushUploads(ID3D12GraphicsCommandList* cmdList)
    {
        ulong completedFence = ((ID3D12Fence*)_d3d.Fence.Handle)->GetCompletedValue();
        while (_deferredFrees.TryPeek(out var freeItem) && completedFence >= freeItem.Item3)
        {
            _deferredFrees.TryDequeue(out _);
            _freeSlots.Enqueue((freeItem.Item1, freeItem.Item2));
        }

        if (_pendingUploads.IsEmpty) return;

        int maxPoolIndex = -1;
        _uploadBatch.Clear();

        while (_pendingUploads.TryDequeue(out var req))
        {
            if (req.PoolIndex > maxPoolIndex) maxPoolIndex = req.PoolIndex;
            _uploadBatch.Add(req);
        }

        if (_uploadBatch.Count == 0) return;

        int transitionCount = maxPoolIndex + 1;
        bool* needsTransition = stackalloc bool[transitionCount];
        for (int i = 0; i < transitionCount; i++) needsTransition[i] = false;

        foreach (var req in _uploadBatch) needsTransition[req.PoolIndex] = true;

        int actualTransitionCount = 0;
        for (int i = 0; i < transitionCount; i++) if (needsTransition[i]) actualTransitionCount++;

        ResourceBarrier* barriers = stackalloc ResourceBarrier[actualTransitionCount];
        int bIdx = 0;
        for (int i = 0; i < transitionCount; i++)
        {
            if (needsTransition[i])
            {
                barriers[bIdx++] = new ResourceBarrier
                {
                    Type = ResourceBarrierType.Transition,
                    Flags = ResourceBarrierFlags.None,
                    Transition = new ResourceTransitionBarrier
                    {
                        PResource = _pools[i].DefaultBuffer,
                        StateBefore = ResourceStates.GenericRead,
                        StateAfter = ResourceStates.CopyDest,
                        Subresource = uint.MaxValue
                    }
                };
            }
        }

        cmdList->ResourceBarrier((uint)actualTransitionCount, barriers);

        foreach (var req in _uploadBatch)
        {
            var pool = _pools[req.PoolIndex];
            cmdList->CopyBufferRegion(pool.DefaultBuffer, req.Offset, pool.StagingBuffer, req.Offset, req.Size);

            // Меш скопирован в VRAM! Теперь его можно безопасно отрисовывать.
            pool.IsFlushed[req.SlotIndex] = true;
        }

        for (int i = 0; i < actualTransitionCount; i++)
        {
            barriers[i].Transition.StateBefore = ResourceStates.CopyDest;
            barriers[i].Transition.StateAfter = ResourceStates.GenericRead;
        }

        cmdList->ResourceBarrier((uint)actualTransitionCount, barriers);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawChunk(ChunkMeshGeometry geometry)
    {
        if (geometry.IndexCount == 0) return;

        ID3D12GraphicsCommandList* cmdList = (ID3D12GraphicsCommandList*)_d3d.CommandList.Handle;

        int poolIndex = (int)geometry.Buffer >> 16;
        int slotIndex = (int)geometry.Buffer & 0xFFFF;

        var pool = _pools[poolIndex];

        // Защита от состояния гонки! Если меш еще не доехал до видеопамяти, пропускаем 1 кадр.
        if (!pool.IsFlushed[slotIndex]) return;

        ulong offset = (ulong)slotIndex * SlotSize;
        ulong gpuAddress = pool.GpuAddress + offset;

        uint vboSize = geometry.VertexCount * ChunkVertex.Stride;
        uint vboAlignedSize = (vboSize + 255u) & ~255u;

        VertexBufferView vbv = new VertexBufferView
        {
            BufferLocation = gpuAddress,
            StrideInBytes = ChunkVertex.Stride,
            SizeInBytes = vboSize
        };

        IndexBufferView ibv = new IndexBufferView
        {
            BufferLocation = gpuAddress + vboAlignedSize,
            SizeInBytes = geometry.IndexCount * sizeof(uint),
            Format = Format.FormatR32Uint
        };

        cmdList->IASetVertexBuffers(0, 1, &vbv);
        cmdList->IASetIndexBuffer(&ibv);
        cmdList->DrawIndexedInstanced(geometry.IndexCount, 1, 0, 0, 0);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        for (int i = 0; i < _activePools; i++)
        {
            if (_pools[i].StagingBuffer != null)
            {
                _pools[i].StagingBuffer->Unmap(0, null);
                _pools[i].StagingBuffer->Release();
            }
            if (_pools[i].DefaultBuffer != null) _pools[i].DefaultBuffer->Release();
        }
        _activePools = 0;
    }
}