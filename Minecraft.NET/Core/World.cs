using Minecraft.NET.Graphics;
using System.Collections.Concurrent;

namespace Minecraft.NET.Core;

public sealed class World : IDisposable
{
    private readonly ConcurrentDictionary<Vector3, ChunkSection> _chunks = new();
    private readonly WorldGenerator _generator = new();
    private readonly HashSet<ChunkSection> _renderableChunks = [];

    private readonly ConcurrentQueue<(ChunkSection chunk, MeshData meshData, bool isNewChunk)> _generatedMeshes = new();
    private readonly ConcurrentQueue<ChunkSection> _chunksToMesh = new();

    private readonly Lock _chunkLock = new();
    private Task _mesherTask = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;

    private static readonly Vector3[] NeighborOffsets =
    [
        new(1,0,0), new(-1,0,0), new(0,1,0), new(0,-1,0), new(0,0,1), new(0,0,-1)
    ];

    public void Initialize()
    {
        BlockRegistry.Initialize();

        _cancellationTokenSource = new CancellationTokenSource();
        _mesherTask = Task.Run(() => MesherLoop(_cancellationTokenSource.Token));
    }

    private void MesherLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_chunksToMesh.TryDequeue(out var chunkToMesh))
            {
                if (_chunks.ContainsKey(chunkToMesh.Position))
                    RemeshChunk(chunkToMesh);
            }
            else
            {
                try { Task.Delay(10, token).Wait(token); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private bool AreNeighborsGenerated(Vector3 chunkPos)
    {
        foreach (var offset in NeighborOffsets)
        {
            var neighborPos = chunkPos + offset;

            if (neighborPos.Y < -VerticalChunkOffset || neighborPos.Y >= VerticalChunkOffset)
                continue;

            if (!_chunks.TryGetValue(neighborPos, out var neighbor) || neighbor.State < ChunkState.AwaitingMesh)
                return false;
        }
        return true;
    }
    private void TryQueueChunkForMeshing(ChunkSection chunk)
    {
        if (chunk.State == ChunkState.AwaitingMesh && AreNeighborsGenerated(chunk.Position))
        {
            chunk.State = ChunkState.Meshing;
            _chunksToMesh.Enqueue(chunk);
        }
    }

    public void Update(Vector3 playerPosition)
    {
        var playerChunkPos = new Vector3(
            MathF.Floor(playerPosition.X / ChunkSize),
            MathF.Floor(playerPosition.Y / ChunkSize),
            MathF.Floor(playerPosition.Z / ChunkSize)
        );

        lock (_chunkLock)
        {
            var chunksToRemove = new List<Vector3>();
            foreach (var pos in _chunks.Keys)
                if (Vector3.Distance(pos, playerChunkPos) > RenderDistance + 2)
                    chunksToRemove.Add(pos);

            if (chunksToRemove.Count > 0)
            {
                foreach (var posToRemove in chunksToRemove)
                {
                    if (_chunks.TryRemove(posToRemove, out var removedChunk))
                    {
                        foreach (var offset in NeighborOffsets)
                        {
                            if (_chunks.TryGetValue(posToRemove + offset, out var neighbor) && neighbor.State == ChunkState.Rendered)
                            {
                                neighbor.State = ChunkState.AwaitingMesh;
                                TryQueueChunkForMeshing(neighbor);
                            }
                        }

                        _renderableChunks.Remove(removedChunk);
                        removedChunk.Dispose();
                    }
                }
            }

            for (int x = -RenderDistance; x <= RenderDistance; x++)
                for (int y = -RenderDistance; y <= RenderDistance; y++)
                    for (int z = -RenderDistance; z <= RenderDistance; z++)
                    {
                        var targetPos = playerChunkPos + new Vector3(x, y, z);
                        if (targetPos.Y < -VerticalChunkOffset || targetPos.Y >= VerticalChunkOffset) continue;
                        if (Vector3.Distance(targetPos, playerChunkPos) > RenderDistance) continue;

                        if (!_chunks.ContainsKey(targetPos))
                        {
                            var newChunk = new ChunkSection(targetPos);
                            if (_chunks.TryAdd(targetPos, newChunk))
                            {
                                newChunk.State = ChunkState.AwaitingGeneration;
                                Task.Run(() => GenerateChunkData(newChunk));
                            }
                        }
                    }
        }
    }

    private void GenerateChunkData(ChunkSection chunk)
    {
        chunk.State = ChunkState.Generating;
        _generator.Generate(chunk);

        lock (_chunkLock)
        {
            chunk.State = ChunkState.AwaitingMesh;

            TryQueueChunkForMeshing(chunk);

            foreach (var offset in NeighborOffsets)
                if (_chunks.TryGetValue(chunk.Position + offset, out var neighbor))
                    TryQueueChunkForMeshing(neighbor);
        }
    }

    private void RemeshChunk(ChunkSection chunk)
    {
        var meshData = ChunkMesher.GenerateMesh(chunk, this);
        bool isNewChunk;
        lock (_chunkLock) isNewChunk = !_renderableChunks.Contains(chunk);
        _generatedMeshes.Enqueue((chunk, meshData, isNewChunk));
    }

    public void BreakBlock(Vector3 cameraPos, Vector3 cameraDir)
    {
        var result = Raycast(cameraPos, cameraDir, 6.0f);
        if (result.HasValue)
        {
            SetBlock(result.Value, BlockId.Air);
        }
    }

    private Vector3? Raycast(Vector3 origin, Vector3 direction, float maxDistance)
    {
        Vector3 pos = origin;
        Vector3 step = Vector3.Normalize(direction) * 0.05f;

        for (float dist = 0; dist < maxDistance; dist += 0.05f)
        {
            pos += step;
            var blockPos = new Vector3(MathF.Floor(pos.X), MathF.Floor(pos.Y), MathF.Floor(pos.Z));

            var blockId = GetBlock(blockPos);
            if (blockId != BlockId.Air)
                return blockPos;
        }

        return null;
    }

    public BlockId GetBlock(Vector3 worldPosition)
    {
        var chunkPos = new Vector3(
            MathF.Floor(worldPosition.X / ChunkSize),
            MathF.Floor(worldPosition.Y / ChunkSize),
            MathF.Floor(worldPosition.Z / ChunkSize)
        );

        if (!_chunks.TryGetValue(chunkPos, out var chunk))
            return BlockId.Air;

        int localX = (int)(worldPosition.X - chunkPos.X * ChunkSize);
        int localY = (int)(worldPosition.Y - chunkPos.Y * ChunkSize);
        int localZ = (int)(worldPosition.Z - chunkPos.Z * ChunkSize);

        return chunk.GetBlock(localX, localY, localZ);
    }

    public void SetBlock(Vector3 worldPosition, BlockId id)
    {
        var chunkPos = new Vector3(
            MathF.Floor(worldPosition.X / ChunkSize),
            MathF.Floor(worldPosition.Y / ChunkSize),
            MathF.Floor(worldPosition.Z / ChunkSize)
        );

        if (!_chunks.TryGetValue(chunkPos, out var chunk))
            return;

        int localX = (int)(worldPosition.X - chunkPos.X * ChunkSize);
        int localY = (int)(worldPosition.Y - chunkPos.Y * ChunkSize);
        int localZ = (int)(worldPosition.Z - chunkPos.Z * ChunkSize);

        chunk.SetBlock(localX, localY, localZ, id);

        lock (_chunkLock)
        {
            if (chunk.State == ChunkState.Rendered)
            {
                chunk.State = ChunkState.AwaitingMesh;
                TryQueueChunkForMeshing(chunk);
            }

            if (localX == 0 && _chunks.TryGetValue(chunkPos - Vector3.UnitX, out var neighborXN))
                if (neighborXN.State == ChunkState.Rendered) { neighborXN.State = ChunkState.AwaitingMesh; TryQueueChunkForMeshing(neighborXN); }
            if (localX == ChunkSize - 1 && _chunks.TryGetValue(chunkPos + Vector3.UnitX, out var neighborXP))
                if (neighborXP.State == ChunkState.Rendered) { neighborXP.State = ChunkState.AwaitingMesh; TryQueueChunkForMeshing(neighborXP); }

            if (localY == 0 && _chunks.TryGetValue(chunkPos - Vector3.UnitY, out var neighborYN))
                if (neighborYN.State == ChunkState.Rendered) { neighborYN.State = ChunkState.AwaitingMesh; TryQueueChunkForMeshing(neighborYN); }
            if (localY == ChunkSize - 1 && _chunks.TryGetValue(chunkPos + Vector3.UnitY, out var neighborYP))
                if (neighborYP.State == ChunkState.Rendered) { neighborYP.State = ChunkState.AwaitingMesh; TryQueueChunkForMeshing(neighborYP); }

            if (localZ == 0 && _chunks.TryGetValue(chunkPos - Vector3.UnitZ, out var neighborZN))
                if (neighborZN.State == ChunkState.Rendered) { neighborZN.State = ChunkState.AwaitingMesh; TryQueueChunkForMeshing(neighborZN); }
            if (localZ == ChunkSize - 1 && _chunks.TryGetValue(chunkPos + Vector3.UnitZ, out var neighborZP))
                if (neighborZP.State == ChunkState.Rendered) { neighborZP.State = ChunkState.AwaitingMesh; TryQueueChunkForMeshing(neighborZP); }
        }
    }

    public ChunkSection? GetChunk(Vector3 position)
    {
        _chunks.TryGetValue(position, out var chunk);
        return chunk;
    }

    public bool TryDequeueGeneratedMesh(out (ChunkSection, MeshData, bool) result)
        => _generatedMeshes.TryDequeue(out result);

    public void AddRenderableChunk(ChunkSection chunk)
    {
        lock (_chunkLock)
            _renderableChunks.Add(chunk);
    }

    public List<ChunkSection> GetRenderableChunksSnapshot()
    {
        lock (_chunkLock)
        {
            return [.. _renderableChunks];
        }
    }

    public int GetRenderableChunkCount()
    {
        lock (_chunkLock)
        {
            return _renderableChunks.Count;
        }
    }

    public int GetLoadedChunkCount() => _chunks.Count;

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _mesherTask.Wait();
        _cancellationTokenSource.Dispose();
        foreach (var chunk in _chunks.Values)
            chunk.Dispose();
    }
}