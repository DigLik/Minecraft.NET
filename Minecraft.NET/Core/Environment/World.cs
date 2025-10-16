using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Common;
using Minecraft.NET.Services;
using System.Runtime.CompilerServices;

namespace Minecraft.NET.Core.Environment;

public sealed class World(
    ChunkManager chunkManager,
    WorldStorage storage
) : IDisposable
{
    public void OnLoad()
    {
        BlockRegistry.Initialize();
        storage.OnLoad();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BlockId GetBlock(Vector3d worldPosition)
    {
        int worldX_i = (int)Math.Floor(worldPosition.X);
        int worldY_i = (int)Math.Floor(worldPosition.Y);
        int worldZ_i = (int)Math.Floor(worldPosition.Z);

        var chunkPos = new Vector2D<int>(
            worldX_i >> ChunkShift,
            worldZ_i >> ChunkShift
        );

        var column = chunkManager.GetColumn(chunkPos);
        if (column is null) return BlockId.Air;

        int localX = worldX_i & ChunkMask;
        int localZ = worldZ_i & ChunkMask;

        int worldY = worldY_i + (VerticalChunkOffset << ChunkShift);

        return column.GetBlock(localX, worldY, localZ);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(Vector3d worldPosition, BlockId id)
    {
        int worldX_i = (int)Math.Floor(worldPosition.X);
        int worldY_i = (int)Math.Floor(worldPosition.Y);
        int worldZ_i = (int)Math.Floor(worldPosition.Z);

        var chunkPos = new Vector2D<int>(
            worldX_i >> ChunkShift,
            worldZ_i >> ChunkShift
        );

        var column = chunkManager.GetColumn(chunkPos);
        if (column is null)
            return;

        int localX = worldX_i & ChunkMask;
        int localZ = worldZ_i & ChunkMask;

        int worldY = worldY_i + (VerticalChunkOffset << ChunkShift);

        if (worldY < 0 || worldY >= WorldHeightInBlocks)
            return;

        column.SetBlock(localX, worldY, localZ, id);
        storage.RecordModification(chunkPos, localX, worldY, localZ, id);

        int sectionY = worldY >> ChunkShift;
        int localY = worldY & ChunkMask;

        chunkManager.MarkSectionForRemeshing(column, sectionY);

        if (localY == 0 && sectionY > 0) chunkManager.MarkSectionForRemeshing(column, sectionY - 1);
        if (localY == ChunkMask && sectionY < WorldHeightInChunks - 1) chunkManager.MarkSectionForRemeshing(column, sectionY + 1);

        if (localX == 0 && chunkManager.GetColumn(chunkPos - new Vector2D<int>(1, 0)) is { } nXN) chunkManager.MarkSectionForRemeshing(nXN, sectionY);
        if (localX == ChunkMask && chunkManager.GetColumn(chunkPos + new Vector2D<int>(1, 0)) is { } nXP) chunkManager.MarkSectionForRemeshing(nXP, sectionY);

        if (localZ == 0 && chunkManager.GetColumn(chunkPos - new Vector2D<int>(0, 1)) is { } nZN) chunkManager.MarkSectionForRemeshing(nZN, sectionY);
        if (localZ == ChunkMask && chunkManager.GetColumn(chunkPos + new Vector2D<int>(0, 1)) is { } nZP) chunkManager.MarkSectionForRemeshing(nZP, sectionY);
    }

    public ChunkColumn? GetColumn(Vector2D<int> position) => chunkManager.GetColumn(position);

    public void OnClose() => storage.OnClose();

    public void Dispose() => OnClose();
}