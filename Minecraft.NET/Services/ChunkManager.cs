using Minecraft.NET.Abstractions;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.World;
using System.Collections.Concurrent;

namespace Minecraft.NET.Services;

public class ChunkManager(
    IPlayerStateProvider playerState,
    IWorldStorage storage,
    ChunkMeshRequestHandler meshRequestHandler
) : IChunkManager, IChunkProvider
{
    private readonly ConcurrentDictionary<Vector2D<int>, ChunkColumn> _chunks = new();

    private static readonly Vector2D<int>[] NeighborOffsets =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
    ];

    public void OnLoad()
    {
    }

    public void OnUpdate(double deltaTime)
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
        List<Vector2D<int>> chunksToRemove;
        lock (_chunks)
        {
            chunksToRemove = [.. _chunks.Keys.Where(pos =>
                (pos - playerChunkPos).LengthSquared > (RenderDistance + 2) * (RenderDistance + 2)
            )];
        }

        if (chunksToRemove.Count > 0)
        {
            foreach (var posToRemove in chunksToRemove)
            {
                if (_chunks.TryRemove(posToRemove, out var removedChunk))
                {
                    removedChunk.Dispose();
                }

                if (removedChunk != null)
                {
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

    public void OnClose() { }
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