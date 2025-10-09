using Minecraft.NET.Core.Chunks;

namespace Minecraft.NET.Abstractions;

public delegate void ChunkMeshRequestHandler(ChunkColumn column, int sectionY);

public interface IChunkManager : ILifecycleHandler, IUpdatable
{
    ChunkColumn? GetColumn(Vector2D<int> position);
    IReadOnlyCollection<ChunkColumn> GetLoadedChunks();
    int GetLoadedChunkCount();
    int GetMeshedSectionCount();
    void MarkSectionForRemeshing(ChunkColumn column, int sectionY);
}