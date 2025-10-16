using Minecraft.NET.Graphics.Rendering;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Core.Chunks;

public enum ChunkSectionState : byte { Empty, AwaitingMesh, Meshing, Rendered }

public unsafe sealed class ChunkColumn : IDisposable
{
    public readonly ReaderWriterLockSlim DataLock = new(LockRecursionPolicy.NoRecursion);

    public Action<ChunkMeshGeometry>? OnFreeMeshGeometry;

    public readonly Vector2D<int> Position;
    public BlockId* Blocks { get; private set; }

    public ChunkMeshGeometry?[] MeshGeometries { get; }
    public ChunkSectionState[] SectionStates { get; }

    public volatile bool IsGenerated;
    private volatile bool _isDisposed;

    private const int FullChunkSizeInBytes = ChunkSize * WorldHeightInBlocks * ChunkSize * sizeof(BlockId);

    public ChunkColumn(Vector2D<int> position)
    {
        Position = position;

        MeshGeometries = new ChunkMeshGeometry?[WorldHeightInChunks];
        SectionStates = new ChunkSectionState[WorldHeightInChunks];
        Array.Fill(SectionStates, ChunkSectionState.Empty);

        Blocks = (BlockId*)NativeMemory.Alloc(ChunkSize * WorldHeightInBlocks * ChunkSize, sizeof(BlockId));
        NativeMemory.Clear(Blocks, ChunkSize * WorldHeightInBlocks * ChunkSize * sizeof(BlockId));
    }

    public bool TryCopyBlocks(Span<BlockId> buffer)
    {
        if (buffer.Length * sizeof(BlockId) < FullChunkSizeInBytes)
            return false; // Буфер слишком мал

        DataLock.EnterReadLock();
        try
        {
            if (_isDisposed || Blocks == null)
                return false;

            var sourceSpan = new Span<BlockId>(Blocks, ChunkSize * WorldHeightInBlocks * ChunkSize);
            sourceSpan.CopyTo(buffer);
            return true;
        }
        finally
        {
            DataLock.ExitReadLock();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndex(int x, int y, int z) => x + (z << ChunkShift) + (y << (ChunkShift * 2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(int x, int y, int z, BlockId id)
    {
        DataLock.EnterWriteLock();
        try
        {
            if (_isDisposed || y < 0 || y >= WorldHeightInBlocks) return;
            Blocks[GetIndex(x, y, z)] = id;
        }
        finally
        {
            DataLock.ExitWriteLock();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BlockId GetBlock(int x, int y, int z)
    {
        DataLock.EnterReadLock();
        try
        {
            if (_isDisposed || y < 0 || y >= WorldHeightInBlocks || Blocks == null) return BlockId.Air;
            return Blocks[GetIndex(x, y, z)];
        }
        finally
        {
            DataLock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        DataLock.EnterWriteLock();
        try
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (OnFreeMeshGeometry != null)
            {
                for (int i = 0; i < MeshGeometries.Length; i++)
                {
                    if (MeshGeometries[i].HasValue)
                    {
                        OnFreeMeshGeometry(MeshGeometries[i]!.Value);
                        MeshGeometries[i] = null;
                    }
                }
            }

            if (Blocks != null)
            {
                NativeMemory.Free(Blocks);
                Blocks = null;
            }
        }
        finally
        {
            DataLock.ExitWriteLock();
        }

        DataLock.Dispose();
        GC.SuppressFinalize(this);
    }

    ~ChunkColumn() => Dispose();
}