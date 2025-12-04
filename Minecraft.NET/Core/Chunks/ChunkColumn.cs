using Minecraft.NET.Graphics.Rendering;
using System.Runtime.CompilerServices;

namespace Minecraft.NET.Core.Chunks;

public enum ChunkSectionState : byte { Empty, AwaitingMesh, Meshing, Rendered }

public unsafe sealed class ChunkColumn : IDisposable
{
    public readonly ReaderWriterLockSlim DataLock = new(LockRecursionPolicy.NoRecursion);
    public Action<ChunkMeshGeometry>? OnFreeMeshGeometry;
    public Vector2D<int> Position;
    public readonly ChunkSection[] Sections;
    public ChunkMeshGeometry?[] MeshGeometries { get; }
    public ChunkSectionState[] SectionStates { get; }
    public volatile bool IsGenerated;
    private volatile bool _isDisposed;

    public ChunkColumn()
    {
        MeshGeometries = new ChunkMeshGeometry?[WorldHeightInChunks];
        SectionStates = new ChunkSectionState[WorldHeightInChunks];
        Sections = new ChunkSection[WorldHeightInChunks];
    }

    public void Reset(Vector2D<int> newPosition)
    {
        Position = newPosition;
        IsGenerated = false;
        Array.Clear(SectionStates);

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

        for (int i = 0; i < Sections.Length; i++)
        {
            Sections[i].Reset();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(int x, int y, int z, BlockId id)
    {
        DataLock.EnterWriteLock();
        try
        {
            if (y < 0 || y >= WorldHeightInBlocks) return;

            int sectionIndex = y >> ChunkShift;
            int localY = y & ChunkMask;

            Sections[sectionIndex].SetBlock(x, localY, z, id);
        }
        finally
        {
            DataLock.ExitWriteLock();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillSection(int sectionY, BlockId id)
    {
        if (sectionY < 0 || sectionY >= WorldHeightInChunks) return;
        Sections[sectionY].Fill(id);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BlockId GetBlock(int x, int y, int z)
    {
        DataLock.EnterReadLock();
        try
        {
            if (y < 0 || y >= WorldHeightInBlocks) return BlockId.Air;
            int sectionIndex = y >> ChunkShift;
            int localY = y & ChunkMask;
            return Sections[sectionIndex].GetBlock(x, localY, z);
        }
        finally
        {
            DataLock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        Reset(Vector2D<int>.Zero);
        DataLock.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}