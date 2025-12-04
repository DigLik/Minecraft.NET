using Minecraft.NET.Character;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using Minecraft.NET.Graphics.Rendering;
using System.Collections.Concurrent;

namespace Minecraft.NET.Services;

public delegate void ChunkMeshRequestHandler(ChunkColumn column, int sectionY);

public class ChunkManager(Player playerState, WorldStorage storage) : IDisposable
{
    private Action<ChunkMeshGeometry>? _meshFreeHandler = null;
    private ChunkMeshRequestHandler? _meshRequestHandler = null;

    private readonly ConcurrentDictionary<Vector2D<int>, ChunkColumn> _chunks = new();
    private readonly List<Vector2D<int>> _chunksToRemove = [];

    private readonly ConcurrentStack<ChunkColumn> _chunkPool = new();

    private Vector2D<int> _lastPlayerChunkPos = new(int.MaxValue, int.MaxValue);
    private static readonly Vector2D<int>[] NeighborOffsets =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
    ];

    public void SetHandlers(ChunkMeshRequestHandler meshRequestHandler, Action<ChunkMeshGeometry> meshFreeHandler)
    {
        _meshRequestHandler = meshRequestHandler;
        _meshFreeHandler = meshFreeHandler;
        foreach (var column in _chunks.Values) column.OnFreeMeshGeometry = _meshFreeHandler;
    }

    public void OnUpdate(double _)
    {
        if (_meshRequestHandler is null) return;
        var playerChunkPos = new Vector2D<int>(
            (int)Math.Floor(playerState.Position.X) >> ChunkShift,
            (int)Math.Floor(playerState.Position.Z) >> ChunkShift
        );
        if (playerChunkPos == _lastPlayerChunkPos) return;

        UnloadFarChunks(playerChunkPos);
        LoadCloseChunks(playerChunkPos);
        _lastPlayerChunkPos = playerChunkPos;
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
                    if (!_chunkPool.TryPop(out var newChunk))
                        newChunk = new ChunkColumn();

                    newChunk.Reset(targetPos);
                    newChunk.OnFreeMeshGeometry = _meshFreeHandler;

                    if (_chunks.TryAdd(targetPos, newChunk))
                        Task.Run(() => GenerateChunkData(newChunk));
                }
            }
    }

    private void UnloadFarChunks(Vector2D<int> playerChunkPos)
    {
        _chunksToRemove.Clear();
        var unloadDistSq = (RenderDistance + 2) * (RenderDistance + 2);

        foreach (var chunkPair in _chunks)
        {
            if ((chunkPair.Key - playerChunkPos).LengthSquared > unloadDistSq)
                _chunksToRemove.Add(chunkPair.Key);
        }

        if (_chunksToRemove.Count > 0)
        {
            foreach (var posToRemove in _chunksToRemove)
            {
                if (_chunks.TryRemove(posToRemove, out var removedChunk))
                {
                    removedChunk.Reset(Vector2D<int>.Zero);
                    _chunkPool.Push(removedChunk);
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
            MarkSectionForRemeshing(column, y);
        foreach (var offset in NeighborOffsets)
            if (_chunks.TryGetValue(column.Position + offset, out var neighbor) && neighbor.IsGenerated)
                for (int y = 0; y < WorldHeightInChunks; y++)
                    MarkSectionForRemeshing(neighbor, y);
    }

    public void MarkSectionForRemeshing(ChunkColumn column, int sectionY)
    {
        if (_meshRequestHandler is null) return;
        if (column.SectionStates[sectionY] == ChunkSectionState.Rendered || column.SectionStates[sectionY] == ChunkSectionState.Empty)
            column.SectionStates[sectionY] = ChunkSectionState.AwaitingMesh;
        if (column.SectionStates[sectionY] == ChunkSectionState.AwaitingMesh)
            _meshRequestHandler(column, sectionY);
    }

    public ChunkColumn? GetColumn(Vector2D<int> position)
    {
        _chunks.TryGetValue(position, out var chunk);
        return chunk;
    }

    public ConcurrentDictionary<Vector2D<int>, ChunkColumn> GetLoadedChunks() => _chunks;
    public int GetLoadedChunkCount() => _chunks.Count;
    public int GetMeshedSectionCount() => _chunks.Values.Sum(c => c.MeshGeometries.Count(m => m != null));

    public void Dispose()
    {
        lock (_chunks)
        {
            foreach (var chunk in _chunks.Values) chunk.Dispose();
            foreach (var chunk in _chunkPool) chunk.Dispose();
            _chunks.Clear();
            _chunkPool.Clear();
        }
        GC.SuppressFinalize(this);
    }
}