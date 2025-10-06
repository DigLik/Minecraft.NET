using Minecraft.NET.Core.World;

namespace Minecraft.NET.Core.Abstractions;

public interface IWorldManager
{
    ChunkColumn? GetChunkColumn(Vector2 position);
    Task<ChunkColumn?> RequestChunkColumnAsync(Vector2 position);
    void UnloadChunkColumn(Vector2 position);
}