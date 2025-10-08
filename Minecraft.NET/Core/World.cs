using Minecraft.NET.Graphics;
using Silk.NET.Maths;
using System.Collections.Concurrent;

namespace Minecraft.NET.Core;

public readonly record struct RaycastResult(Vector3d HitPosition, Vector3d PlacePosition);

public sealed class World : IDisposable
{
    private readonly ConcurrentDictionary<Vector2D<int>, ChunkColumn> _chunks = new();
    private readonly WorldGenerator _generator = new();

    private readonly WorldStorage _storage;

    private readonly ConcurrentQueue<(ChunkColumn column, int sectionY, MeshData meshData)> _generatedMeshes = new();
    private readonly ConcurrentQueue<(ChunkColumn column, int sectionY)> _chunksToMesh = new();

    private readonly Lock _chunkLock = new();
    private Task _mesherTask = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;

    private static readonly Vector2D<int>[] NeighborOffsets =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
    ];

    public World()
    {
        _storage = new WorldStorage("world");
    }

    public void Initialize()
    {
        BlockRegistry.Initialize();
        _storage.Load();

        _cancellationTokenSource = new CancellationTokenSource();
        _mesherTask = Task.Run(() => MesherLoop(_cancellationTokenSource.Token));
    }

    private void MesherLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_chunksToMesh.TryDequeue(out var item))
            {
                if (_chunks.ContainsKey(item.column.Position))
                    RemeshChunkSection(item.column, item.sectionY);
            }
            else
            {
                try { Task.Delay(10, token).Wait(token); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private bool AreNeighborsGenerated(Vector2D<int> chunkPos)
    {
        foreach (var offset in NeighborOffsets)
        {
            var neighborPos = chunkPos + offset;
            if (!_chunks.TryGetValue(neighborPos, out var neighbor) || !neighbor.IsGenerated)
                return false;
        }
        return true;
    }
    private void TryQueueChunkSectionForMeshing(ChunkColumn column, int sectionY)
    {
        if (column.SectionStates[sectionY] == ChunkSectionState.AwaitingMesh && AreNeighborsGenerated(column.Position))
        {
            column.SectionStates[sectionY] = ChunkSectionState.Meshing;
            _chunksToMesh.Enqueue((column, sectionY));
        }
    }

    public void Update(Vector3d playerPosition)
    {
        var playerChunkPos = new Vector2D<int>(
            (int)Math.Floor(playerPosition.X / ChunkSize),
            (int)Math.Floor(playerPosition.Z / ChunkSize)
        );

        lock (_chunkLock)
        {
            var chunksToRemove = new List<Vector2D<int>>();
            foreach (var pos in _chunks.Keys)
            {
                var distSq = (pos - playerChunkPos).LengthSquared;
                if (distSq > (RenderDistance + 2) * (RenderDistance + 2))
                    chunksToRemove.Add(pos);
            }

            if (chunksToRemove.Count > 0)
            {
                foreach (var posToRemove in chunksToRemove)
                {
                    if (_chunks.TryRemove(posToRemove, out var removedChunk))
                    {
                        foreach (var offset in NeighborOffsets)
                        {
                            if (_chunks.TryGetValue(posToRemove + offset, out var neighbor) && neighbor.IsGenerated)
                            {
                                for (int y = 0; y < WorldHeightInChunks; y++)
                                {
                                    if (neighbor.SectionStates[y] == ChunkSectionState.Rendered)
                                    {
                                        neighbor.SectionStates[y] = ChunkSectionState.AwaitingMesh;
                                        TryQueueChunkSectionForMeshing(neighbor, y);
                                    }
                                }
                            }
                        }
                        removedChunk.Dispose();
                    }
                }
            }

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
    }

    private void GenerateChunkData(ChunkColumn column)
    {
        WorldGenerator.Generate(column);
        _storage.ApplyModificationsToChunk(column);
        column.IsGenerated = true;

        lock (_chunkLock)
        {
            for (int y = 0; y < WorldHeightInChunks; y++)
            {
                column.SectionStates[y] = ChunkSectionState.AwaitingMesh;
                TryQueueChunkSectionForMeshing(column, y);
            }

            foreach (var offset in NeighborOffsets)
                if (_chunks.TryGetValue(column.Position + offset, out var neighbor) && neighbor.IsGenerated)
                    for (int y = 0; y < WorldHeightInChunks; y++)
                        TryQueueChunkSectionForMeshing(neighbor, y);
        }
    }

    private void RemeshChunkSection(ChunkColumn column, int sectionY)
    {
        var meshData = ChunkMesher.GenerateMesh(column, sectionY, this);
        _generatedMeshes.Enqueue((column, sectionY, meshData));
    }

    public void BreakBlock(Vector3d cameraPos, Vector3 cameraDir)
    {
        var result = Raycast(cameraPos, cameraDir, 6.0);
        if (result.HasValue)
            SetBlock(result.Value.HitPosition, BlockId.Air);
    }

    public void PlaceBlock(Camera camera)
    {
        var result = Raycast(camera.Position, camera.Front, 6.0);
        if (result.HasValue)
        {
            var placePosition = result.Value.PlacePosition;

            var newBlockBox = new BoundingBox(
                (Vector3)placePosition,
                (Vector3)placePosition + Vector3.One
            );

            var playerBox = camera.GetBoundingBox();

            const float Epsilon = 0.0001f;
            var collisionCheckBox = new BoundingBox(
                newBlockBox.Min + new Vector3(Epsilon),
                newBlockBox.Max - new Vector3(Epsilon)
            );

            if (Intersects(playerBox, collisionCheckBox)) return;

            SetBlock(placePosition, BlockId.Stone);
        }
    }

    private RaycastResult? Raycast(Vector3d origin, Vector3 direction, double maxDistance)
    {
        Vector3d pos = origin;
        Vector3d step = Vector3d.Normalize(direction) * 0.05;
        Vector3d lastAirPos = Vector3d.Zero;

        for (double dist = 0; dist < maxDistance; dist += 0.05)
        {
            var currentBlockPos = new Vector3d(Math.Floor(pos.X), Math.Floor(pos.Y), Math.Floor(pos.Z));

            var blockId = GetBlock(currentBlockPos);
            if (blockId != BlockId.Air)
                return new RaycastResult(currentBlockPos, lastAirPos);

            lastAirPos = currentBlockPos;
            pos += step;
        }

        return null;
    }

    public BlockId GetBlock(Vector3d worldPosition)
    {
        var chunkPos = new Vector2D<int>(
            (int)Math.Floor(worldPosition.X / ChunkSize),
            (int)Math.Floor(worldPosition.Z / ChunkSize)
        );

        if (!_chunks.TryGetValue(chunkPos, out var column))
            return BlockId.Air;

        int localX = (int)(worldPosition.X - (double)chunkPos.X * ChunkSize);
        if (localX < 0) localX += ChunkSize;
        int localZ = (int)(worldPosition.Z - (double)chunkPos.Y * ChunkSize);
        if (localZ < 0) localZ += ChunkSize;

        int worldY = (int)Math.Floor(worldPosition.Y) + VerticalChunkOffset * ChunkSize;

        return column.GetBlock(localX, worldY, localZ);
    }

    public void SetBlock(Vector3d worldPosition, BlockId id)
    {
        var chunkPos = new Vector2D<int>(
            (int)Math.Floor(worldPosition.X / ChunkSize),
            (int)Math.Floor(worldPosition.Z / ChunkSize)
        );

        if (!_chunks.TryGetValue(chunkPos, out var column))
            return;

        int localX = (int)(worldPosition.X - (double)chunkPos.X * ChunkSize);
        if (localX < 0) localX += ChunkSize;
        int localZ = (int)(worldPosition.Z - (double)chunkPos.Y * ChunkSize);
        if (localZ < 0) localZ += ChunkSize;

        int worldY = (int)Math.Floor(worldPosition.Y) + VerticalChunkOffset * ChunkSize;
        if (worldY < 0 || worldY >= WorldHeightInBlocks)
            return;

        column.SetBlock(localX, worldY, localZ, id);
        _storage.RecordModification(chunkPos, localX, worldY, localZ, id);

        lock (_chunkLock)
        {
            int sectionY = worldY / ChunkSize;
            int localY = worldY % ChunkSize;

            void RemeshSection(ChunkColumn c, int sy)
            {
                if (c.SectionStates[sy] == ChunkSectionState.Rendered)
                {
                    c.SectionStates[sy] = ChunkSectionState.AwaitingMesh;
                    TryQueueChunkSectionForMeshing(c, sy);
                }
            }

            RemeshSection(column, sectionY);

            if (localY == 0 && sectionY > 0) RemeshSection(column, sectionY - 1);
            if (localY == ChunkSize - 1 && sectionY < WorldHeightInChunks - 1) RemeshSection(column, sectionY + 1);

            if (localX == 0 && _chunks.TryGetValue(chunkPos - new Vector2D<int>(1, 0), out var nXN)) RemeshSection(nXN, sectionY);
            if (localX == ChunkSize - 1 && _chunks.TryGetValue(chunkPos + new Vector2D<int>(1, 0), out var nXP)) RemeshSection(nXP, sectionY);

            if (localZ == 0 && _chunks.TryGetValue(chunkPos - new Vector2D<int>(0, 1), out var nZN)) RemeshSection(nZN, sectionY);
            if (localZ == ChunkSize - 1 && _chunks.TryGetValue(chunkPos + new Vector2D<int>(0, 1), out var nZP)) RemeshSection(nZP, sectionY);
        }
    }

    public void UpdatePlayerPosition(Game game, Camera player, float dt)
    {
        if (game.CurrentGameMode == GameMode.Spectator)
            UpdateSpectatorPosition(player, dt);
        else
            UpdateCreativePosition(player, dt);
    }

    private static void UpdateSpectatorPosition(Camera player, float dt)
    {
        player.Velocity = Vector3d.Zero;
        player.Position += player.Velocity * dt;
    }

    private void UpdateCreativePosition(Camera player, float dt)
    {
        player.Velocity -= new Vector3d(0, Gravity * dt, 0);

        var velocity = player.Velocity;
        (player.Position, player.IsOnGround) = MoveAndSlide(player.Position, ref velocity, dt);
        player.Velocity = velocity;

        if (player.IsOnGround && player.Velocity.Y <= 0)
            player.Velocity = player.Velocity with { Y = 0 };
    }

    private (Vector3d newPosition, bool onGround) MoveAndSlide(Vector3d position, ref Vector3d velocity, float dt)
    {
        var initialVelocity = velocity;
        var totalDisplacement = velocity * dt;
        double distance = totalDisplacement.Length();
        bool isOnGround = false;

        if (distance < 1e-8)
        {
            var playerBox = new Camera(position).GetBoundingBox();
            playerBox = playerBox with { Min = playerBox.Min with { Y = playerBox.Min.Y - 0.01f } };
            return (position, CheckCollision(playerBox));
        }

        const double stepSize = PlayerWidth * 0.4;
        int numSteps = (int)Math.Ceiling(distance / stepSize);

        var stepDisplacement = totalDisplacement / numSteps;

        for (int i = 0; i < numSteps; i++)
        {
            position += stepDisplacement;

            const int maxResolutionAttempts = 5;
            for (int j = 0; j < maxResolutionAttempts; j++)
            {
                var playerBox = new Camera(position).GetBoundingBox();
                var (collided, mtv) = FindCollisionMTV(playerBox);

                if (!collided) break;

                position += mtv;
                var normal = Vector3d.Normalize(mtv);

                if (normal.Y > 0.707 && initialVelocity.Y <= 0) isOnGround = true;

                var dotProduct = velocity.X * normal.X + velocity.Y * normal.Y + velocity.Z * normal.Z;

                if (dotProduct < 0) velocity -= normal * dotProduct;
            }
        }

        return (position, isOnGround);
    }

    private (bool, Vector3d) FindCollisionMTV(BoundingBox playerBox)
    {
        Vector3d overallMtv = Vector3d.Zero;

        var min = new Vector3d(Math.Floor(playerBox.Min.X - 1), Math.Floor(playerBox.Min.Y - 1), Math.Floor(playerBox.Min.Z - 1));
        var max = new Vector3d(Math.Floor(playerBox.Max.X + 1), Math.Floor(playerBox.Max.Y + 1), Math.Floor(playerBox.Max.Z + 1));

        for (var x = min.X; x <= max.X; x++)
            for (var y = min.Y; y <= max.Y; y++)
                for (var z = min.Z; z <= max.Z; z++)
                {
                    var blockPos = new Vector3d(x, y, z);
                    if (GetBlock(blockPos) == BlockId.Air) continue;

                    var blockBox = new BoundingBox(
                        new Vector3((float)x, (float)y, (float)z),
                        new Vector3((float)x + 1, (float)y + 1, (float)z + 1)
                    );

                    if (!Intersects(playerBox, blockBox)) continue;

                    var mtv = CalculateMTV(playerBox, blockBox);

                    if (mtv.LengthSquared() > overallMtv.LengthSquared())
                        overallMtv = mtv;
                }

        return (overallMtv.LengthSquared() > 0, overallMtv);
    }

    private static Vector3d CalculateMTV(BoundingBox playerBox, BoundingBox blockBox)
    {
        var overlapX = (playerBox.Max.X - playerBox.Min.X) + (blockBox.Max.X - blockBox.Min.X) -
                       (Math.Max(playerBox.Max.X, blockBox.Max.X) - Math.Min(playerBox.Min.X, blockBox.Min.X));
        var overlapY = (playerBox.Max.Y - playerBox.Min.Y) + (blockBox.Max.Y - blockBox.Min.Y) -
                       (Math.Max(playerBox.Max.Y, blockBox.Max.Y) - Math.Min(playerBox.Min.Y, blockBox.Min.Y));
        var overlapZ = (playerBox.Max.Z - playerBox.Min.Z) + (blockBox.Max.Z - blockBox.Min.Z) -
                       (Math.Max(playerBox.Max.Z, blockBox.Max.Z) - Math.Min(playerBox.Min.Z, blockBox.Min.Z));

        if (overlapX < overlapY && overlapX < overlapZ)
        {
            var direction = playerBox.Min.X + (playerBox.Max.X - playerBox.Min.X) / 2 < blockBox.Min.X + (blockBox.Max.X - blockBox.Min.X) / 2 ? -1 : 1;
            return new Vector3d(overlapX * direction, 0, 0);
        }
        if (overlapY < overlapZ)
        {
            var direction = playerBox.Min.Y + (playerBox.Max.Y - playerBox.Min.Y) / 2 < blockBox.Min.Y + (blockBox.Max.Y - blockBox.Min.Y) / 2 ? -1 : 1;
            return new Vector3d(0, overlapY * direction, 0);
        }
        else
        {
            var direction = playerBox.Min.Z + (playerBox.Max.Z - playerBox.Min.Z) / 2 < blockBox.Min.Z + (blockBox.Max.Z - blockBox.Min.Z) / 2 ? -1 : 1;
            return new Vector3d(0, 0, overlapZ * direction);
        }
    }

    private bool CheckCollision(BoundingBox box)
    {
        var min = new Vector3d(Math.Floor(box.Min.X), Math.Floor(box.Min.Y), Math.Floor(box.Min.Z));
        var max = new Vector3d(Math.Floor(box.Max.X), Math.Floor(box.Max.Y), Math.Floor(box.Max.Z));

        for (var x = min.X; x <= max.X; x++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                for (var z = min.Z; z <= max.Z; z++)
                {
                    var blockPos = new Vector3d(x, y, z);
                    if (GetBlock(blockPos) != BlockId.Air)
                    {
                        var blockBox = new BoundingBox(
                            new Vector3((float)x, (float)y, (float)z),
                            new Vector3((float)x + 1, (float)y + 1, (float)z + 1)
                        );
                        if (Intersects(box, blockBox))
                            return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool Intersects(BoundingBox a, BoundingBox b)
        => a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
           a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
           a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;

    public ChunkColumn? GetColumn(Vector2D<int> position)
    {
        _chunks.TryGetValue(position, out var chunk);
        return chunk;
    }

    public bool TryDequeueGeneratedMesh(out (ChunkColumn, int, MeshData) result)
        => _generatedMeshes.TryDequeue(out result);

    public List<ChunkColumn> GetRenderableChunksSnapshot()
    {
        lock (_chunkLock)
            return [.. _chunks.Values.Where(c => c.IsGenerated)];
    }

    public int GetMeshedSectionCount()
    {
        lock (_chunkLock)
        {
            return _chunks.Values.Sum(c => c.Meshes.Count(m => m != null));
        }
    }

    public int GetLoadedChunkCount() => _chunks.Count;

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _mesherTask.Wait();
        _cancellationTokenSource.Dispose();
        _storage.Save();
        foreach (var chunk in _chunks.Values)
            chunk.Dispose();
    }
}