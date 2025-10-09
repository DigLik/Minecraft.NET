using Minecraft.NET.Graphics.Rendering;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Core.Chunks;

public enum ChunkSectionState : byte { Empty, AwaitingMesh, Meshing, Rendered }

public unsafe sealed class ChunkColumn : IDisposable
{
    public readonly Vector2D<int> Position;
    public BlockId* Blocks { get; private set; }

    public ChunkMeshGeometry?[] MeshGeometries { get; }
    public ChunkSectionState[] SectionStates { get; }

    public volatile bool IsGenerated;

    private volatile bool _isDisposed;

    public ChunkColumn(Vector2D<int> position)
    {
        Position = position;

        MeshGeometries = new ChunkMeshGeometry?[WorldHeightInChunks];
        SectionStates = new ChunkSectionState[WorldHeightInChunks];
        Array.Fill(SectionStates, ChunkSectionState.Empty);

        Blocks = (BlockId*)NativeMemory.Alloc(ChunkSize * WorldHeightInBlocks * ChunkSize, sizeof(BlockId));
        NativeMemory.Clear(Blocks, ChunkSize * WorldHeightInBlocks * ChunkSize * sizeof(BlockId));
    }

    public static int GetIndex(int x, int y, int z) => x + z * ChunkSize + y * ChunkSize * ChunkSize;

    public void SetBlock(int x, int y, int z, BlockId id)
    {
        if (_isDisposed) return;
        if (x < 0 || x >= ChunkSize || y < 0 || y >= WorldHeightInBlocks || z < 0 || z >= ChunkSize) return;
        Blocks[GetIndex(x, y, z)] = id;
    }

    public BlockId GetBlock(int x, int y, int z)
    {
        if (_isDisposed) return BlockId.Air;
        if (x < 0 || x >= ChunkSize || y < 0 || y >= WorldHeightInBlocks || z < 0 || z >= ChunkSize) return BlockId.Air;
        return Blocks[GetIndex(x, y, z)];
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (Blocks != null)
        {
            NativeMemory.Free(Blocks);
            Blocks = null;
        }

        GC.SuppressFinalize(this);
    }

    ~ChunkColumn() => Dispose();
}