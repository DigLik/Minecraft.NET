using Minecraft.NET.Graphics;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Core;

public enum ChunkState : byte { Empty, AwaitingGeneration, Generating, AwaitingMesh, Meshing, Rendered }

public unsafe sealed class ChunkSection : IDisposable
{
    public readonly Vector3 Position;
    public BlockId* Blocks { get; private set; }
    public ChunkState State { get; set; }
    public Mesh? Mesh { get; set; }

    private bool _isDisposed;

    public ChunkSection(Vector3 position)
    {
        Position = position;
        State = ChunkState.Empty;
        Blocks = (BlockId*)NativeMemory.Alloc(BlocksInChunk, sizeof(BlockId));
        NativeMemory.Clear(Blocks, BlocksInChunk * sizeof(BlockId));
    }

    public void SetBlock(int x, int y, int z, BlockId id)
    {
        if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkSize || z < 0 || z >= ChunkSize) return;
        Blocks[x + y * ChunkSize + z * ChunkSize * ChunkSize] = id;
    }

    public BlockId GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkSize || z < 0 || z >= ChunkSize) return BlockId.Air;
        return Blocks[x + y * ChunkSize + z * ChunkSize * ChunkSize];
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        if (Blocks != null)
        {
            NativeMemory.Free(Blocks);
            Blocks = null;
        }
        Mesh?.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    ~ChunkSection() => Dispose();
}