using Minecraft.NET.Character;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using Minecraft.NET.Graphics.Rendering;
using System.Collections.Concurrent;

namespace Minecraft.NET.Services;

public delegate void ChunkMeshRequestHandler(ChunkColumn column, int sectionY);

public class ChunkManager(Player playerState, WorldStorage storage, IWorldGenerator generator) : IDisposable
{
    private Action<ChunkMeshGeometry>? _meshFreeHandler = null;
    private ChunkMeshRequestHandler? _meshRequestHandler = null;

    private readonly ConcurrentDictionary<Vector2D<int>, ChunkColumn> _chunks = new();

    private readonly List<ChunkColumn> _renderChunksList = new(4096);
    private bool _isRenderListDirty = false;
    private readonly Lock _listLock = new();

    private readonly List<Vector2D<int>> _chunksToRemove = new(256);
    private readonly ConcurrentStack<ChunkColumn> _chunkPool = new();

    private readonly Vector2D<int>[] _sortedChunkOffsets = GenerateSortedOffsets();

    private Vector2D<int> _lastPlayerChunkPos = new(int.MaxValue, int.MaxValue);

    private static readonly Vector2D<int>[] NeighborOffsets =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
    ];

    public void SetHandlers(ChunkMeshRequestHandler meshRequestHandler, Action<ChunkMeshGeometry> meshFreeHandler)
    {
        _meshRequestHandler = meshRequestHandler;
        _meshFreeHandler = meshFreeHandler;
        foreach (var column in _chunks.Values)
            column.OnFreeMeshGeometry = _meshFreeHandler;
    }

    public void OnUpdate(double _)
    {
        if (_meshRequestHandler is null)
            return;

        var playerChunkPos = new Vector2D<int>(
            (int)Math.Floor(playerState.Position.X) >> ChunkShift,
            (int)Math.Floor(playerState.Position.Z) >> ChunkShift
        );

        if (playerChunkPos != _lastPlayerChunkPos)
        {
            UnloadFarChunks(playerChunkPos);
            LoadCloseChunks(playerChunkPos);
            _lastPlayerChunkPos = playerChunkPos;
        }

        if (_isRenderListDirty)
        {
            lock (_listLock)
            {
                if (_isRenderListDirty)
                {
                    _renderChunksList.Clear();
                    foreach (var kvp in _chunks)
                        _renderChunksList.Add(kvp.Value);
                    _isRenderListDirty = false;
                }
            }
        }
    }

    public IReadOnlyList<ChunkColumn> GetRenderChunks() => _renderChunksList;

    private static Vector2D<int>[] GenerateSortedOffsets()
    {
        var offsets = new List<Vector2D<int>>();
        int rd = RenderDistance;
        int rdSq = rd * rd;

        for (int x = -rd; x <= rd; x++)
            for (int z = -rd; z <= rd; z++)
                if (x * x + z * z <= rdSq)
                    offsets.Add(new Vector2D<int>(x, z));

        return [.. offsets.OrderBy(v => v.X * v.X + v.Y * v.Y)];
    }

    private void LoadCloseChunks(Vector2D<int> playerChunkPos)
    {
        bool addedAny = false;
        foreach (var offset in _sortedChunkOffsets)
        {
            var targetPos = playerChunkPos + offset;

            if (!_chunks.ContainsKey(targetPos))
            {
                if (!_chunkPool.TryPop(out var newChunk))
                    newChunk = new ChunkColumn();

                newChunk.Reset(targetPos);
                newChunk.OnFreeMeshGeometry = _meshFreeHandler;

                if (_chunks.TryAdd(targetPos, newChunk))
                {
                    ThreadPool.QueueUserWorkItem(
                        static state => state.Manager.GenerateChunkData(state.Chunk),
                        (Manager: this, Chunk: newChunk),
                        preferLocal: false
                    );
                    addedAny = true;
                }
            }
        }

        if (addedAny)
            _isRenderListDirty = true;
    }

    private void UnloadFarChunks(Vector2D<int> playerChunkPos)
    {
        _chunksToRemove.Clear();
        int unloadDist = RenderDistance + 2;
        int unloadDistSq = unloadDist * unloadDist;

        foreach (var kvp in _chunks)
        {
            var pos = kvp.Key;
            int dx = pos.X - playerChunkPos.X;
            int dy = pos.Y - playerChunkPos.Y;

            if (dx * dx + dy * dy > unloadDistSq)
                _chunksToRemove.Add(pos);
        }

        if (_chunksToRemove.Count > 0)
        {
            int count = _chunksToRemove.Count;
            for (int i = 0; i < count; i++)
            {
                if (_chunks.TryRemove(_chunksToRemove[i], out var removedChunk))
                {
                    removedChunk.Reset(Vector2D<int>.Zero);
                    _chunkPool.Push(removedChunk);
                }
            }

            _isRenderListDirty = true;
        }
    }

    private void GenerateChunkData(ChunkColumn column)
    {
        generator.Generate(column);

        int sectionLen = column.Sections.Length;
        for (int i = 0; i < sectionLen; i++)
            column.Sections[i].Optimize();

        storage.ApplyModificationsToChunk(column);
        column.IsGenerated = true;

        for (int y = 0; y < WorldHeightInChunks; y++)
            MarkSectionForRemeshing(column, y);

        for (int i = 0; i < NeighborOffsets.Length; i++)
            if (_chunks.TryGetValue(column.Position + NeighborOffsets[i], out var neighbor) && neighbor.IsGenerated)
                for (int y = 0; y < WorldHeightInChunks; y++)
                    MarkSectionForRemeshing(neighbor, y);
    }

    public void MarkSectionForRemeshing(ChunkColumn column, int sectionY)
    {
        if (_meshRequestHandler is null)
            return;

        var state = column.SectionStates[sectionY];

        if (state is ChunkSectionState.Rendered or ChunkSectionState.Empty)
            column.SectionStates[sectionY] = ChunkSectionState.AwaitingMesh;

        if (column.SectionStates[sectionY] == ChunkSectionState.AwaitingMesh)
            _meshRequestHandler(column, sectionY);
    }

    public ChunkColumn? GetColumn(Vector2D<int> position)
    {
        _ = _chunks.TryGetValue(position, out var chunk);
        return chunk;
    }

    public int GetLoadedChunkCount() => _chunks.Count;

    public int GetMeshedSectionCount()
    {
        int total = 0;
        foreach (var chunk in _chunks.Values)
        {
            var meshes = chunk.MeshGeometries;
            for (int i = 0; i < WorldHeightInChunks; i++)
                if (meshes[i].IndexCount > 0)
                    total++;
        }

        return total;
    }

    public long GetTotalPolygonCount()
    {
        long totalIndices = 0;
        foreach (var chunk in _chunks.Values)
        {
            var meshes = chunk.MeshGeometries;
            for (int i = 0; i < WorldHeightInChunks; i++)
            {
                totalIndices += meshes[i].IndexCount;
            }
        }

        return totalIndices / 3;
    }

    public void Dispose()
    {
        foreach (var chunk in _chunks.Values)
            chunk.Dispose();
        foreach (var chunk in _chunkPool)
            chunk.Dispose();
        _chunks.Clear();
        _chunkPool.Clear();
        GC.SuppressFinalize(this);
    }
}