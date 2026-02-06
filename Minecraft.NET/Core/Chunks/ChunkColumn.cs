using Minecraft.NET.Graphics.Rendering;
using System.Runtime.CompilerServices;

namespace Minecraft.NET.Core.Chunks;

public enum ChunkSectionState : byte { Empty, AwaitingMesh, Meshing, Rendered }

public unsafe sealed class ChunkColumn : IDisposable
{
    public readonly Lock StateLock = new();
    public Action<ChunkMeshGeometry>? OnFreeMeshGeometry;
    public Vector2D<int> Position;
    public readonly ChunkSection[] Sections;
    public ChunkMeshGeometry[] MeshGeometries { get; }
    public ChunkSectionState[] SectionStates { get; }
    public volatile bool IsGenerated;
    public ushort ActiveMask;
    public int Version;

    private volatile bool _isDisposed;

    public ChunkColumn()
    {
        MeshGeometries = new ChunkMeshGeometry[WorldHeightInChunks];
        SectionStates = new ChunkSectionState[WorldHeightInChunks];
        Sections = new ChunkSection[WorldHeightInChunks];
    }

    public void Reset(Vector2D<int> newPosition)
    {
        lock (StateLock)
        {
            Position = newPosition;
            IsGenerated = false;
            // [NEW] Обновляем версию при сбросе
            Version++;

            Array.Clear(SectionStates);

            if (OnFreeMeshGeometry != null)
                while (ActiveMask != 0)
                {
                    int i = BitOperations.TrailingZeroCount(ActiveMask);
                    if (MeshGeometries[i].IndexCount > 0)
                    {
                        OnFreeMeshGeometry(MeshGeometries[i]);
                        MeshGeometries[i] = default;
                    }

                    ActiveMask &= (ushort)~(1 << i);
                }

            ActiveMask = 0;
            for (int i = 0; i < Sections.Length; i++)
                Sections[i].Reset();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(int x, int y, int z, BlockId id)
    {
        lock (StateLock)
        {
            if (y < 0 || y >= WorldHeightInBlocks)
                return;
            int sectionIndex = y >> ChunkShift;
            int localY = y & ChunkMask;

            Sections[sectionIndex].SetBlock(x, localY, z, id);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillSection(int sectionY, BlockId id)
    {
        if (sectionY < 0 || sectionY >= WorldHeightInChunks)
            return;
        lock (StateLock)
            Sections[sectionY].Fill(id);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BlockId GetBlock(int x, int y, int z)
    {
        if (y < 0 || y >= WorldHeightInBlocks)
            return BlockId.Air;
        return Sections[y >> ChunkShift].GetBlock(x, y & ChunkMask, z);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        Reset(Vector2D<int>.Zero);
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}