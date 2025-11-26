using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using System.Runtime.CompilerServices;

namespace Minecraft.NET.Graphics.Models;

public static class ChunkMesher
{
    private static readonly float[] AO_Factors = [0.5f, 0.7f, 0.85f, 1.0f];

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
             .TryCopyBlocks(new Span<BlockId>(neighborBlocksXP, numBlocksInColumn));
        world.GetColumn(column.Position + new Vector2D<int>(-1, 0))?
             .TryCopyBlocks(new Span<BlockId>(neighborBlocksXN, numBlocksInColumn));
        world.GetColumn(column.Position + new Vector2D<int>(0, 1))?
             .TryCopyBlocks(new Span<BlockId>(neighborBlocksZP, numBlocksInColumn));
        world.GetColumn(column.Position + new Vector2D<int>(0, -1))?
             .TryCopyBlocks(new Span<BlockId>(neighborBlocksZN, numBlocksInColumn));

        using var builder = new MeshBuilder(initialVertexCapacity: 32768, initialIndexCapacity: 49152);

        const int maskSize = ChunkSize * ChunkSize;
        BlockId* mask = stackalloc BlockId[maskSize];
        uint* maskAO = stackalloc uint[maskSize];

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
                            var blockCurrent = GetBlockSafe(sectionY, x[0], x[1], x[2], localBlocks, neighborBlocksXP, neighborBlocksXN, neighborBlocksZP, neighborBlocksZN);
                            var blockNeighbor = GetBlockSafe(sectionY, x[0] + q[0], x[1] + q[1], x[2] + q[2], localBlocks, neighborBlocksXP, neighborBlocksXN, neighborBlocksZP, neighborBlocksZN);

                            bool isCurrentSolid = blockCurrent != BlockId.Air;
                            bool isNeighborSolid = blockNeighbor != BlockId.Air;
                            BlockId faceBlockId = BlockId.Air;
                            uint aoData = 0;

                            if (dir == 1)
                            {
                                if (isCurrentSolid && !isNeighborSolid)
                                    faceBlockId = blockCurrent;
                            }
                            else
                            {
                                if (!isCurrentSolid && isNeighborSolid)
                                    faceBlockId = blockNeighbor;
                            }

                            if (faceBlockId != BlockId.Air)
                            {
                                int bx = (dir == 1) ? x[0] : x[0] + q[0];
                                int by = (dir == 1) ? x[1] : x[1] + q[1];
                                int bz = (dir == 1) ? x[2] : x[2] + q[2];

                                int neighborX = (dir == 1) ? x[0] + q[0] : x[0];
                                int neighborY = (dir == 1) ? x[1] + q[1] : x[1];
                                int neighborZ = (dir == 1) ? x[2] + q[2] : x[2];

                                aoData = CalculateFaceAO(axis, neighborX, neighborY, neighborZ, sectionY,
                                    localBlocks, neighborBlocksXP, neighborBlocksXN, neighborBlocksZP, neighborBlocksZN);
                            }

                            mask[n] = faceBlockId;
                            maskAO[n] = aoData;
                            n++;
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
                                var aoData = maskAO[n];
                                int w, h;

                                for (w = 1; i + w < ChunkSize; w++)
                                    if (mask[n + w] != blockId || maskAO[n + w] != aoData)
                                        break;

                                bool done = false;
                                for (h = 1; j + h < ChunkSize; h++)
                                {
                                    for (int k = 0; k < w; k++)
                                    {
                                        if (mask[n + k + h * ChunkSize] != blockId || maskAO[n + k + h * ChunkSize] != aoData)
                                        {
                                            done = true;
                                            break;
                                        }
                                    }
                                    if (done) break;
                                }

                                x[u] = i;
                                x[v] = j;

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
                                if (axis == 2) reversed = !reversed;

                                AddQuad(builder, v1, v2, v3, v4, w, h, reversed, texCoords, aoData);

                                for (int l = 0; l < h; l++)
                                {
                                    for (int k = 0; k < w; k++)
                                    {
                                        mask[n + k + l * ChunkSize] = BlockId.Air;
                                        maskAO[n + k + l * ChunkSize] = 0;
                                    }
                                }

                                i += w; n += w;
                            }
                            else
                            {
                                i++; n++;
                            }
                        }
                    }
                }
            }
        }

        return builder.Build();
    }

    private static unsafe uint CalculateFaceAO(int axis, int x, int y, int z, int sectionY,
        BlockId* cur, BlockId* xp, BlockId* xn, BlockId* zp, BlockId* zn)
    {
        int ux = 0, uy = 0, uz = 0;
        int vx = 0, vy = 0, vz = 0;

        if (axis == 0) { uz = 1; vy = 1; }
        else if (axis == 1) { ux = 1; vz = 1; }
        else { ux = 1; vy = 1; }

        int t = GetOpacity(sectionY, x + vx, y + vy, z + vz, cur, xp, xn, zp, zn);
        int b = GetOpacity(sectionY, x - vx, y - vy, z - vz, cur, xp, xn, zp, zn);
        int r = GetOpacity(sectionY, x + ux, y + uy, z + uz, cur, xp, xn, zp, zn);
        int l = GetOpacity(sectionY, x - ux, y - uy, z - uz, cur, xp, xn, zp, zn);

        int tr = GetOpacity(sectionY, x + ux + vx, y + uy + vy, z + uz + vz, cur, xp, xn, zp, zn);
        int tl = GetOpacity(sectionY, x - ux + vx, y - uy + vy, z - uz + vz, cur, xp, xn, zp, zn);
        int br = GetOpacity(sectionY, x + ux - vx, y + uy - vy, z + uz - vz, cur, xp, xn, zp, zn);
        int bl = GetOpacity(sectionY, x - ux - vx, y - uy - vy, z - uz - vz, cur, xp, xn, zp, zn);

        uint ao_bl = VertexAO(l, bl, b);
        uint ao_br = VertexAO(r, br, b);
        uint ao_tr = VertexAO(r, tr, t);
        uint ao_tl = VertexAO(l, tl, t);

        return ao_bl | (ao_br << 8) | (ao_tr << 16) | (ao_tl << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint VertexAO(int side1, int corner, int side2)
    {
        if (side1 == 1 && side2 == 1) return 0;
        return (uint)(3 - (side1 + side2 + corner));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int GetOpacity(int sectionY, int x, int y, int z,
        BlockId* cur, BlockId* xp, BlockId* xn, BlockId* zp, BlockId* zn)
    {
        BlockId id = GetBlockSafe(sectionY, x, y, z, cur, xp, xn, zp, zn);
        return id == BlockId.Air ? 0 : 1;
    }

    private static void AddQuad(
        MeshBuilder builder,
        Vector3 v1_bl, Vector3 v2_br, Vector3 v3_tl, Vector3 v4_tr,
        int w, int h, bool reversed, Vector2 texCoords, uint aoData)
    {
        uint baseIndex = (uint)builder.VertexCount;

        float ao_bl = AO_Factors[aoData & 0xFF];
        float ao_br = AO_Factors[(aoData >> 8) & 0xFF];
        float ao_tr = AO_Factors[(aoData >> 16) & 0xFF];
        float ao_tl = AO_Factors[(aoData >> 24) & 0xFF];

        builder.AddVertex(new ChunkVertex(v1_bl, texCoords, new Vector2(0, h), ao_bl));
        builder.AddVertex(new ChunkVertex(v2_br, texCoords, new Vector2(w, h), ao_br));
        builder.AddVertex(new ChunkVertex(v4_tr, texCoords, new Vector2(w, 0), ao_tr));
        builder.AddVertex(new ChunkVertex(v3_tl, texCoords, new Vector2(0, 0), ao_tl));

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe BlockId GetBlockSafe(int sectionY, int x, int y, int z,
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