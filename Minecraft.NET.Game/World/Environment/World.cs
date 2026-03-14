using System.Runtime.CompilerServices;

using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Environment;

public sealed class World : IAsyncDisposable
{
    private readonly WorldStorage _storage;
    public ChunkManager Chunks { get; }

    public World(WorldStorage storage, IWorldGenerator generator)
    {
        _storage = storage;
        Chunks = new ChunkManager(_storage, generator);
    }

    public static Task InitializeAsync()
    {
        BlockRegistry.Initialize();
        return Task.CompletedTask;
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