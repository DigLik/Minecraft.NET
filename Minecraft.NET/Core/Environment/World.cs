using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Common;
using Minecraft.NET.Services;
using System.Runtime.CompilerServices;

namespace Minecraft.NET.Core.Environment;

public sealed class World(ChunkManager chunkManager, WorldStorage storage) : IDisposable
{
    public void OnLoad()
    {
        BlockRegistry.Initialize();
        storage.OnLoad();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(Vector3d worldPosition, BlockId id)
    {
        int worldX_i = (int)Math.Floor(worldPosition.X);
        int worldY_i = (int)Math.Floor(worldPosition.Y);
        int worldZ_i = (int)Math.Floor(worldPosition.Z);

        var chunkPos = new Vector2D<int>(worldX_i >> ChunkShift, worldZ_i >> ChunkShift);
        var column = chunkManager.GetColumn(chunkPos);
        if (column is null)
            return;

        int localX = worldX_i & ChunkMask;
        int localZ = worldZ_i & ChunkMask;
        int worldY = worldY_i + (VerticalChunkOffset << ChunkShift);

        if (worldY is < 0 or >= WorldHeightInBlocks)
            return;

        column.SetBlock(localX, worldY, localZ, id);
        storage.RecordModification(chunkPos, localX, worldY, localZ, id);

        int sectionY = worldY >> ChunkShift;
        int localY = worldY & ChunkMask;

        MarkSectionDirty(chunkPos, sectionY);

        if (localY == 0 && sectionY > 0)
            MarkSectionDirty(chunkPos, sectionY - 1);
        if (localY == ChunkMask && sectionY < WorldHeightInChunks - 1)
            MarkSectionDirty(chunkPos, sectionY + 1);

        if (localX == 0)
            MarkSectionDirty(chunkPos - new Vector2D<int>(1, 0), sectionY);
        if (localX == ChunkMask)
            MarkSectionDirty(chunkPos + new Vector2D<int>(1, 0), sectionY);
        if (localZ == 0)
            MarkSectionDirty(chunkPos - new Vector2D<int>(0, 1), sectionY);
        if (localZ == ChunkMask)
            MarkSectionDirty(chunkPos + new Vector2D<int>(0, 1), sectionY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MarkSectionDirty(Vector2D<int> pos, int sectionY)
    {
        var col = chunkManager.GetColumn(pos);
        if (col != null)
            chunkManager.MarkSectionForRemeshing(col, sectionY);
    }

    public BlockId GetBlock(Vector3d worldPosition)
    {
        int wx = (int)Math.Floor(worldPosition.X);
        int wz = (int)Math.Floor(worldPosition.Z);
        var col = chunkManager.GetColumn(new Vector2D<int>(wx >> ChunkShift, wz >> ChunkShift));
        if (col == null)
            return BlockId.Air;

        int wy = (int)Math.Floor(worldPosition.Y) + (VerticalChunkOffset << ChunkShift);
        return col.GetBlock(wx & ChunkMask, wy, wz & ChunkMask);
    }

    public ChunkColumn? GetColumn(Vector2D<int> position) => chunkManager.GetColumn(position);

    public void Dispose() => storage.OnClose();
}