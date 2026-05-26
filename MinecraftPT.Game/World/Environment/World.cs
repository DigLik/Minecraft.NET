using System.Runtime.CompilerServices;

using MinecraftPT.Game.World.Blocks;
using MinecraftPT.Utils.Math;

namespace MinecraftPT.Game.World.Environment;

public sealed class World : IAsyncDisposable
{
    private readonly WorldStorage _storage;
    public ChunkManager Chunks { get; }

    public World(WorldStorage storage, IWorldGenerator generator)
    {
        _storage = storage;
        Chunks = new ChunkManager(_storage, generator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(Vector3Int position, BlockId id)
        => Chunks.SetBlock(position, id);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BlockId GetBlock(Vector3Int blockPos)
        => Chunks.GetBlock(blockPos);

    public async ValueTask DisposeAsync()
    {
        Chunks.Dispose();
        await _storage.DisposeAsync();
    }
}