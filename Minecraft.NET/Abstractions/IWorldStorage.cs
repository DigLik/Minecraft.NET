using Minecraft.NET.Core.Chunks;

namespace Minecraft.NET.Abstractions;

public interface IWorldStorage : ILifecycleHandler
{
    void ApplyModificationsToChunk(ChunkColumn column);
    void RecordModification(Vector2D<int> chunkPos, int x, int y, int z, BlockId blockId);
}