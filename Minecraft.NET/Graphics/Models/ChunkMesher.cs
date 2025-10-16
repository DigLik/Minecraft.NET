using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using System; // <-- Добавьте это

namespace Minecraft.NET.Graphics.Models;

public static class ChunkMesher
{
    public static unsafe MeshData GenerateMesh(ChunkColumn column, int sectionY, World world, CancellationToken token)
    {
        const int numBlocksInColumn = ChunkSize * WorldHeightInBlocks * ChunkSize;

        BlockId* localBlocks = stackalloc BlockId[numBlocksInColumn];
        if (!column.TryCopyBlocks(new Span<BlockId>(localBlocks, numBlocksInColumn)))
            return new MeshData((nint)null, 0, null, 0);

        BlockId* neighborBlocksXN = stackalloc BlockId[numBlocksInColumn];
        BlockId* neighborBlocksXP = stackalloc BlockId[numBlocksInColumn];
        BlockId* neighborBlocksZN = stackalloc BlockId[numBlocksInColumn];
        BlockId* neighborBlocksZP = stackalloc BlockId[numBlocksInColumn];

        world.GetColumn(column.Position + new Vector2D<int>(1, 0))?
             .TryCopyBlocks(new Span<BlockId>(neighborBlocksXP, numBlocksInColumn)); // +X
        world.GetColumn(column.Position + new Vector2D<int>(-1, 0))?
             .TryCopyBlocks(new Span<BlockId>(neighborBlocksXN, numBlocksInColumn)); // -X
        world.GetColumn(column.Position + new Vector2D<int>(0, 1))?
             .TryCopyBlocks(new Span<BlockId>(neighborBlocksZP, numBlocksInColumn)); // +Z
        world.GetColumn(column.Position + new Vector2D<int>(0, -1))?
             .TryCopyBlocks(new Span<BlockId>(neighborBlocksZN, numBlocksInColumn)); // -Z

        using var builder = new MeshBuilder(initialVertexCapacity: 32768, initialIndexCapacity: 49152);
        uint vertexOffset = 0;
        const int maskSize = ChunkSize * ChunkSize;
        BlockId* mask = stackalloc BlockId[maskSize];

        for (int axis = 0; axis < 3; axis++)
        {
            if (token.IsCancellationRequested) return builder.Build();
            for (int dir = 0; dir < 2; dir++)
            {
                int d = axis;
                int u, v;
                switch (axis)
                {
                    case 0: u = 2; v = 1; break;
                    case 1: u = 0; v = 2; break;
                    default: u = 0; v = 1; break;
                }
                var x = new int[3];
                var q = new int[3];
                q[d] = 1;
                for (x[d] = -1; x[d] < ChunkSize;)
                {
                    if (token.IsCancellationRequested) return builder.Build();
                    int n = 0;
                    for (x[v] = 0; x[v] < ChunkSize; x[v]++)
                    {
                        for (x[u] = 0; x[u] < ChunkSize; x[u]++)
                        {
                            var blockCurrent = GetBlockFromLocal(sectionY, x[0], x[1], x[2], localBlocks, neighborBlocksXP, neighborBlocksXN, neighborBlocksZP, neighborBlocksZN);
                            var blockNeighbor = GetBlockFromLocal(sectionY, x[0] + q[0], x[1] + q[1], x[2] + q[2], localBlocks, neighborBlocksXP, neighborBlocksXN, neighborBlocksZP, neighborBlocksZN);
                            bool isCurrentSolid = blockCurrent != BlockId.Air;
                            bool isNeighborSolid = blockNeighbor != BlockId.Air;
                            BlockId faceBlockId = BlockId.Air;
                            if (dir == 1) { if (isCurrentSolid && !isNeighborSolid) { faceBlockId = blockCurrent; } }
                            else { if (!isCurrentSolid && isNeighborSolid) { faceBlockId = blockNeighbor; } }
                            mask[n++] = faceBlockId;
                        }
                    }
                    x[d]++;
                    n = 0;
                    for (int j = 0; j < ChunkSize; j++)
                    {
                        for (int i = 0; i < ChunkSize;)
                        {
                            if (mask[n] != BlockId.Air)
                            {
                                var blockId = mask[n];
                                int w, h;
                                for (w = 1; i + w < ChunkSize && mask[n + w] == blockId; w++) ;
                                bool done = false;
                                for (h = 1; j + h < ChunkSize; h++)
                                {
                                    for (int k = 0; k < w; k++)
                                    {
                                        if (mask[n + k + h * ChunkSize] != blockId) { done = true; break; }
                                    }
                                    if (done) break;
                                }
                                x[u] = i; x[v] = j;
                                var du = new int[3]; du[u] = w;
                                var dv = new int[3]; dv[v] = h;
                                var blockDef = BlockRegistry.Definitions[blockId];
                                Vector2 texCoords;
                                if (axis == 0) texCoords = blockDef.Textures.Side;
                                else if (axis == 1) texCoords = (dir == 0) ? blockDef.Textures.Bottom : blockDef.Textures.Top;
                                else texCoords = blockDef.Textures.Side;
                                var v1 = new Vector3(x[0], x[1], x[2]);
                                var v2 = new Vector3(x[0] + du[0], x[1] + du[1], x[2] + du[2]);
                                var v3 = new Vector3(x[0] + dv[0], x[1] + dv[1], x[2] + dv[2]);
                                var v4 = new Vector3(x[0] + du[0] + dv[0], x[1] + du[1] + dv[1], x[2] + du[2] + dv[2]);
                                bool reversed = dir == 0;
                                if (axis == 2) { reversed = !reversed; }
                                AddQuad(builder, v1, v2, v3, v4, w, h, reversed, texCoords, ref vertexOffset);
                                vertexOffset += 4;
                                for (int l = 0; l < h; l++) for (int k = 0; k < w; k++) mask[n + k + l * ChunkSize] = BlockId.Air;
                                i += w; n += w;
                            }
                            else { i++; n++; }
                        }
                    }
                }
            }
        }
        return builder.Build();
    }

    private static void AddQuad(
        MeshBuilder builder,
        Vector3 v1_bl, Vector3 v2_br, Vector3 v3_tl, Vector3 v4_tr,
        int w, int h, bool reversed, Vector2 texCoords, ref uint vertexOffset)
    {
        uint baseIndex = vertexOffset;
        float tx = texCoords.X, ty = texCoords.Y;
        builder.AddVertex(v1_bl.X, v1_bl.Y, v1_bl.Z, tx, ty, 0, h);
        builder.AddVertex(v2_br.X, v2_br.Y, v2_br.Z, tx, ty, w, h);
        builder.AddVertex(v4_tr.X, v4_tr.Y, v4_tr.Z, tx, ty, w, 0);
        builder.AddVertex(v3_tl.X, v3_tl.Y, v3_tl.Z, tx, ty, 0, 0);
        if (reversed)
        {
            builder.AddIndices(baseIndex + 0, baseIndex + 1, baseIndex + 2);
            builder.AddIndices(baseIndex + 0, baseIndex + 2, baseIndex + 3);
        }
        else
        {
            builder.AddIndices(baseIndex + 0, baseIndex + 2, baseIndex + 1);
            builder.AddIndices(baseIndex + 0, baseIndex + 3, baseIndex + 2);
        }
    }

    private static unsafe BlockId GetBlockFromLocal(int sectionY, int x, int y, int z,
        BlockId* currentBlocks, BlockId* nXP, BlockId* nXN, BlockId* nZP, BlockId* nZN)
    {
        int worldY = sectionY * ChunkSize + y;
        if (worldY < 0 || worldY >= WorldHeightInBlocks)
            return BlockId.Air;

        BlockId* targetBuffer;
        int localX = x, localZ = z;

        if (x >= 0 && x < ChunkSize && z >= 0 && z < ChunkSize)
        {
            targetBuffer = currentBlocks;
        }
        else if (x < 0) { localX += ChunkSize; targetBuffer = nXN; }
        else if (x >= ChunkSize) { localX -= ChunkSize; targetBuffer = nXP; }
        else if (z < 0) { localZ += ChunkSize; targetBuffer = nZN; }
        else { localZ -= ChunkSize; targetBuffer = nZP; }

        if (targetBuffer == null)
            return BlockId.Air;

        return targetBuffer[ChunkColumn.GetIndex(localX, worldY, localZ)];
    }
}