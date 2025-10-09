using Minecraft.NET.Abstractions;
using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Common;

namespace Minecraft.NET.Core.World;

public sealed class World(
    IChunkManager chunkManager,
    IWorldStorage storage
) : IWorld, ILifecycleHandler
{
    public void OnLoad()
    {
        BlockRegistry.Initialize();
        storage.OnLoad();
    }

    public BlockId GetBlock(Vector3d worldPosition)
    {
        var chunkPos = new Vector2D<int>(
            (int)Math.Floor(worldPosition.X / ChunkSize),
            (int)Math.Floor(worldPosition.Z / ChunkSize)
        );

        var column = chunkManager.GetColumn(chunkPos);
        if (column is null)
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

        var column = chunkManager.GetColumn(chunkPos);
        if (column is null)
            return;

        int localX = (int)(worldPosition.X - (double)chunkPos.X * ChunkSize);
        if (localX < 0) localX += ChunkSize;
        int localZ = (int)(worldPosition.Z - (double)chunkPos.Y * ChunkSize);
        if (localZ < 0) localZ += ChunkSize;

        int worldY = (int)Math.Floor(worldPosition.Y) + VerticalChunkOffset * ChunkSize;
        if (worldY < 0 || worldY >= WorldHeightInBlocks)
            return;

        column.SetBlock(localX, worldY, localZ, id);
        storage.RecordModification(chunkPos, localX, worldY, localZ, id);

        int sectionY = worldY / ChunkSize;
        int localY = worldY % ChunkSize;

        chunkManager.MarkSectionForRemeshing(column, sectionY);

        if (localY == 0 && sectionY > 0) chunkManager.MarkSectionForRemeshing(column, sectionY - 1);
        if (localY == ChunkSize - 1 && sectionY < WorldHeightInChunks - 1) chunkManager.MarkSectionForRemeshing(column, sectionY + 1);

        if (localX == 0 && chunkManager.GetColumn(chunkPos - new Vector2D<int>(1, 0)) is { } nXN) chunkManager.MarkSectionForRemeshing(nXN, sectionY);
        if (localX == ChunkSize - 1 && chunkManager.GetColumn(chunkPos + new Vector2D<int>(1, 0)) is { } nXP) chunkManager.MarkSectionForRemeshing(nXP, sectionY);

        if (localZ == 0 && chunkManager.GetColumn(chunkPos - new Vector2D<int>(0, 1)) is { } nZN) chunkManager.MarkSectionForRemeshing(nZN, sectionY);
        if (localZ == ChunkSize - 1 && chunkManager.GetColumn(chunkPos + new Vector2D<int>(0, 1)) is { } nZP) chunkManager.MarkSectionForRemeshing(nZP, sectionY);
    }

    public ChunkColumn? GetColumn(Vector2D<int> position) => chunkManager.GetColumn(position);

    public void OnClose() => storage.OnClose();

    public void Dispose() => OnClose();
}