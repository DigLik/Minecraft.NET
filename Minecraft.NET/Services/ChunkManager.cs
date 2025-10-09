using Minecraft.NET.Character;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using System.Collections.Concurrent;

namespace Minecraft.NET.Services;

public delegate void ChunkMeshRequestHandler(ChunkColumn column, int sectionY);

public class ChunkManager(
    Player playerState,
    WorldStorage storage,
    ChunkMeshRequestHandler meshRequestHandler
) : IDisposable
{
    private readonly ConcurrentDictionary<Vector2D<int>, ChunkColumn> _chunks = new();
    private readonly List<Vector2D<int>> _chunksToRemove = [];

    private static readonly Vector2D<int>[] NeighborOffsets =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
    ];

    public void OnUpdate(double _)
    {
        var playerChunkPos = new Vector2D<int>(
            (int)Math.Floor(playerState.Position.X / ChunkSize),
            (int)Math.Floor(playerState.Position.Z / ChunkSize)
        );

        UnloadFarChunks(playerChunkPos);
        LoadCloseChunks(playerChunkPos);
    }

    private void LoadCloseChunks(Vector2D<int> playerChunkPos)
    {
        for (int x = -RenderDistance; x <= RenderDistance; x++)
            for (int z = -RenderDistance; z <= RenderDistance; z++)
            {
                var offset = new Vector2D<int>(x, z);
                var targetPos = playerChunkPos + offset;

                var distSq = offset.LengthSquared;
                if (distSq > RenderDistance * RenderDistance) continue;

                if (!_chunks.ContainsKey(targetPos))
                {
                    var newChunk = new ChunkColumn(targetPos);
                    if (_chunks.TryAdd(targetPos, newChunk))
                    {
                        Task.Run(() => GenerateChunkData(newChunk));
                    }
                }
            }
    }

    private void UnloadFarChunks(Vector2D<int> playerChunkPos)
    {
        _chunksToRemove.Clear();
        var unloadDistSq = (RenderDistance + 2) * (RenderDistance + 2);

        foreach (var key in _chunks.Keys)
        {
            if ((key - playerChunkPos).LengthSquared > unloadDistSq)
            {
                _chunksToRemove.Add(key);
            }
        }

        if (_chunksToRemove.Count > 0)
        {
            foreach (var posToRemove in _chunksToRemove)
            {
                if (_chunks.TryRemove(posToRemove, out var removedChunk))
                {
                    removedChunk.Dispose();

                    // Оповещаем соседей о необходимости перерисовки
                    foreach (var offset in NeighborOffsets)
                    {
                        if (_chunks.TryGetValue(posToRemove + offset, out var neighbor) && neighbor.IsGenerated)
                        {
                            for (int y = 0; y < WorldHeightInChunks; y++)
                            {
                                MarkSectionForRemeshing(neighbor, y);
                            }
                        }
                    }
                }
            }
        }
    }

    private void GenerateChunkData(ChunkColumn column)
    {
        WorldGenerator.Generate(column);
        storage.ApplyModificationsToChunk(column);
        column.IsGenerated = true;

        for (int y = 0; y < WorldHeightInChunks; y++)
        {
            MarkSectionForRemeshing(column, y);
        }

        foreach (var offset in NeighborOffsets)
            if (_chunks.TryGetValue(column.Position + offset, out var neighbor) && neighbor.IsGenerated)
                for (int y = 0; y < WorldHeightInChunks; y++)
                    MarkSectionForRemeshing(neighbor, y);
    }

    public void MarkSectionForRemeshing(ChunkColumn column, int sectionY)
    {
        if (column.SectionStates[sectionY] == ChunkSectionState.Rendered || column.SectionStates[sectionY] == ChunkSectionState.Empty)
        {
            column.SectionStates[sectionY] = ChunkSectionState.AwaitingMesh;
        }

        if (column.SectionStates[sectionY] == ChunkSectionState.AwaitingMesh)
        {
            meshRequestHandler(column, sectionY);
        }
    }

    public ChunkColumn? GetColumn(Vector2D<int> position)
    {
        _chunks.TryGetValue(position, out var chunk);
        return chunk;
    }

    public IReadOnlyCollection<ChunkColumn> GetLoadedChunks() => (IReadOnlyCollection<ChunkColumn>)_chunks.Values;

    public int GetLoadedChunkCount() => _chunks.Count;

    public int GetMeshedSectionCount() => _chunks.Values.Sum(c => c.Meshes.Count(m => m != null));

    public void Dispose()
    {
        lock (_chunks)
        {
            foreach (var chunk in _chunks.Values)
            {
                chunk.Dispose();
            }
            _chunks.Clear();
        }

        GC.SuppressFinalize(this);
    }
}