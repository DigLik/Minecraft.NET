using Minecraft.NET.Core.Chunks;

namespace Minecraft.NET.Abstractions;

public interface IChunkMesherService : ILifecycleHandler, IUpdatable
{
    void SetDependencies(IWorld world, IChunkManager chunkManager);
    void QueueForMeshing(ChunkColumn column, int sectionY);
}