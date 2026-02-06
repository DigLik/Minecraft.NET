using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;
using System.Runtime.CompilerServices;

namespace Minecraft.NET.Graphics.Models;

public static class ChunkMesher
{
    private static readonly float[] AO_Factors = [0.5f, 0.7f, 0.85f, 1.0f];
    private static readonly ThreadLocal<MeshBuilder> _threadLocalBuilder = new(() => new MeshBuilder());

    public static unsafe MeshData GenerateMesh(ChunkColumn column, int sectionY, World world, CancellationToken token)
    {
        ref var section = ref column.Sections[sectionY];
        if (section.IsEmpty)
            return default;

        if (section.IsFull && AreAllNeighborsFull(column, sectionY, world))
            return default;

        const int paddedSize = ChunkSize + 2;
        const int totalSize = paddedSize * paddedSize * paddedSize;

        BlockId* paddedBlocks = stackalloc BlockId[totalSize];
        FillPaddedBufferOptimized(paddedBlocks, column, sectionY, world);

        var builder = _threadLocalBuilder.Value!;
        builder.Reset();

        const int maskSize = ChunkSize * ChunkSize;
        BlockId* mask = stackalloc BlockId[maskSize];
        uint* maskAO = stackalloc uint[maskSize];

        int* x = stackalloc int[3];
        int* q = stackalloc int[3];
        int* du = stackalloc int[3];
        int* dv = stackalloc int[3];

        for (int axis = 0; axis < 3; axis++)
        {
            if (token.IsCancellationRequested)
                return default;

            for (int dir = 0; dir < 2; dir++)
            {
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;
                if (axis == 0)
                { u = 2; v = 1; }
                else if (axis == 1)
                { u = 0; v = 2; }
                else
                { u = 0; v = 1; }

                x[0] = 0;
                x[1] = 0;
                x[2] = 0;
                q[0] = 0;
                q[1] = 0;
                q[2] = 0;
                q[axis] = 1;

                for (x[axis] = -1; x[axis] < ChunkSize;)
                {
                    if (token.IsCancellationRequested)
                        return default;

                    int n = 0;

                    for (x[v] = 0; x[v] < ChunkSize; x[v]++)
                    {
                        for (x[u] = 0; x[u] < ChunkSize; x[u]++)
                        {
                            int cx = x[0] + 1;
                            int cy = x[1] + 1;
                            int cz = x[2] + 1;

                            var blockCurrent = paddedBlocks[cx + cz * 18 + cy * 324];
                            var blockNeighbor = paddedBlocks[(cx + q[0]) + (cz + q[2]) * 18 + (cy + q[1]) * 324];

                            bool isCurrentSolid = blockCurrent != BlockId.Air;
                            bool isNeighborSolid = blockNeighbor != BlockId.Air;

                            BlockId faceBlockId = BlockId.Air;

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

                            uint aoData = 0;
                            if (faceBlockId != BlockId.Air)
                            {
                                aoData = CalculateFaceAO(axis, x[0] + (dir == 1 ? q[0] : 0),
                                                       x[1] + (dir == 1 ? q[1] : 0),
                                                       x[2] + (dir == 1 ? q[2] : 0),
                                                       sectionY, column, world);
                            }

                            mask[n] = faceBlockId;
                            maskAO[n] = aoData;
                            n++;
                        }
                    }

                    x[axis]++;
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
                                    if (done)
                                        break;
                                }

                                x[u] = i;
                                x[v] = j;

                                du[0] = 0;
                                du[1] = 0;
                                du[2] = 0;
                                dv[0] = 0;
                                dv[1] = 0;
                                dv[2] = 0;
                                du[u] = w;
                                dv[v] = h;

                                if (BlockRegistry.Definitions.TryGetValue(blockId, out var blockDef))
                                {
                                    int texIndex;
                                    if (axis == 0)
                                        texIndex = blockDef.Textures.Side;
                                    else if (axis == 1)
                                        texIndex = (dir == 0) ? blockDef.Textures.Bottom : blockDef.Textures.Top;
                                    else
                                        texIndex = blockDef.Textures.Side;

                                    var v1 = new Vector3(x[0], x[1], x[2]);
                                    var v2 = new Vector3(x[0] + du[0], x[1] + du[1], x[2] + du[2]);
                                    var v3 = new Vector3(x[0] + dv[0], x[1] + dv[1], x[2] + dv[2]);
                                    var v4 = new Vector3(x[0] + du[0] + dv[0], x[1] + du[1] + dv[1], x[2] + du[2] + dv[2]);

                                    bool reversed = dir == 0;
                                    if (axis == 2)
                                        reversed = !reversed;

                                    AddQuad(builder, v1, v2, v3, v4, w, h, reversed, texIndex, aoData);
                                }

                                for (int l = 0; l < h; l++)
                                {
                                    for (int k = 0; k < w; k++)
                                    {
                                        int idx = n + k + l * ChunkSize;
                                        mask[idx] = BlockId.Air;
                                        maskAO[idx] = 0;
                                    }
                                }

                                i += w;
                                n += w;
                            }
                            else
                            { i++; n++; }
                        }
                    }
                }
            }
        }

        return builder.BuildToData();
    }

    private static bool AreAllNeighborsFull(ChunkColumn column, int sectionY, World world)
    {
        if (sectionY < WorldHeightInChunks - 1 && !column.Sections[sectionY + 1].IsFull)
            return false;
        if (sectionY > 0 && !column.Sections[sectionY - 1].IsFull)
            return false;

        var pos = column.Position;
        var xp = world.GetColumn(pos + new Vector2D<int>(1, 0));
        if (xp == null || !xp.Sections[sectionY].IsFull)
            return false;
        var xn = world.GetColumn(pos + new Vector2D<int>(-1, 0));
        if (xn == null || !xn.Sections[sectionY].IsFull)
            return false;
        var zp = world.GetColumn(pos + new Vector2D<int>(0, 1));
        if (zp == null || !zp.Sections[sectionY].IsFull)
            return false;
        var zn = world.GetColumn(pos + new Vector2D<int>(0, -1));
        if (zn == null || !zn.Sections[sectionY].IsFull)
            return false;

        return true;
    }

    private static unsafe void FillPaddedBufferOptimized(BlockId* buffer, ChunkColumn center, int sectionY, World world)
    {
        ref var section = ref center.Sections[sectionY];
        if (section.IsAllocated)
        {
            BlockId* srcPtr = section.Blocks;
            for (int y = 0; y < ChunkSize; y++)
            {
                int srcYOffset = y << (ChunkShift * 2);
                int dstYOffset = (y + 1) * 324;

                for (int z = 0; z < ChunkSize; z++)
                {
                    int srcOffset = srcYOffset + (z << ChunkShift);
                    int dstOffset = dstYOffset + (z + 1) * 18 + 1;
                    Unsafe.CopyBlock(buffer + dstOffset, srcPtr + srcOffset, ChunkSize);
                }
            }
        }
        else
        {
            BlockId uniformId = section.UniformId;
            for (int y = 0; y < ChunkSize; y++)
            {
                int dstYOffset = (y + 1) * 324;
                for (int z = 0; z < ChunkSize; z++)
                {
                    int dstOffset = dstYOffset + (z + 1) * 18 + 1;
                    Unsafe.InitBlock(buffer + dstOffset, (byte)uniformId, ChunkSize);
                }
            }
        }

        var pos = center.Position;
        var xp = world.GetColumn(pos + new Vector2D<int>(1, 0));
        var xn = world.GetColumn(pos + new Vector2D<int>(-1, 0));
        var zp = world.GetColumn(pos + new Vector2D<int>(0, 1));
        var zn = world.GetColumn(pos + new Vector2D<int>(0, -1));

        for (int y = -1; y <= ChunkSize; y += ChunkSize + 1)
            for (int z = -1; z <= ChunkSize; z++)
                for (int x = -1; x <= ChunkSize; x++)
                    buffer[GetPaddedIndex(x + 1, y + 1, z + 1)] = GetBlockSafeSmart(sectionY, x, y, z, center, xp, xn, zp, zn);

        for (int y = 0; y < ChunkSize; y++)
            for (int z = -1; z <= ChunkSize; z += ChunkSize + 1)
                for (int x = -1; x <= ChunkSize; x++)
                    buffer[GetPaddedIndex(x + 1, y + 1, z + 1)] = GetBlockSafeSmart(sectionY, x, y, z, center, xp, xn, zp, zn);

        for (int y = 0; y < ChunkSize; y++)
            for (int z = 0; z < ChunkSize; z++)
                for (int x = -1; x <= ChunkSize; x += ChunkSize + 1)
                    buffer[GetPaddedIndex(x + 1, y + 1, z + 1)] = GetBlockSafeSmart(sectionY, x, y, z, center, xp, xn, zp, zn);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe BlockId GetBlockSafeSmart(int centerSectionY, int x, int y, int z,
        ChunkColumn cur, ChunkColumn? xp, ChunkColumn? xn, ChunkColumn? zp, ChunkColumn? zn)
    {
        ChunkColumn? targetColumn = cur;
        int localX = x;
        int localZ = z;

        if (x < 0)
        { targetColumn = xn; localX += ChunkSize; }
        else if (x >= ChunkSize)
        { targetColumn = xp; localX -= ChunkSize; }

        if (z < 0)
        { targetColumn = zn; localZ += ChunkSize; }
        else if (z >= ChunkSize)
        { targetColumn = zp; localZ -= ChunkSize; }

        if (targetColumn == null)
            return BlockId.Air;

        int absoluteSectionIndex = centerSectionY;
        int localY = y;

        if (y < 0)
        { absoluteSectionIndex--; localY += ChunkSize; }
        else if (y >= ChunkSize)
        { absoluteSectionIndex++; localY -= ChunkSize; }

        if (absoluteSectionIndex < 0 || absoluteSectionIndex >= WorldHeightInChunks)
            return BlockId.Air;

        ref var section = ref targetColumn.Sections[absoluteSectionIndex];

        if (!section.IsAllocated)
            return section.UniformId;

        return section.Blocks[ChunkSection.GetIndex(localX, localY, localZ)];
    }

    private static unsafe uint CalculateFaceAO(int axis, int x, int y, int z, int sectionY,
        ChunkColumn cur, World world)
    {
        var pos = cur.Position;
        var xp = world.GetColumn(pos + new Vector2D<int>(1, 0));
        var xn = world.GetColumn(pos + new Vector2D<int>(-1, 0));
        var zp = world.GetColumn(pos + new Vector2D<int>(0, 1));
        var zn = world.GetColumn(pos + new Vector2D<int>(0, -1));

        int ux = 0, uy = 0, uz = 0;
        int vx = 0, vy = 0, vz = 0;

        if (axis == 0)
        { uz = 1; vy = 1; }
        else if (axis == 1)
        { ux = 1; vz = 1; }
        else
        { ux = 1; vy = 1; }

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
        if (side1 == 1 && side2 == 1)
            return 0;
        return (uint)(3 - (side1 + side2 + corner));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int GetOpacity(int sectionY, int x, int y, int z,
        ChunkColumn cur, ChunkColumn? xp, ChunkColumn? xn, ChunkColumn? zp, ChunkColumn? zn)
    {
        BlockId id = GetBlockSafeSmart(sectionY, x, y, z, cur, xp, xn, zp, zn);
        return id == BlockId.Air ? 0 : 1;
    }

    private static void AddQuad(
        MeshBuilder builder,
        Vector3 v1_bl, Vector3 v2_br, Vector3 v3_tl, Vector3 v4_tr,
        int w, int h, bool reversed, int texIndex, uint aoData)
    {
        ushort baseIndex = (ushort)builder.VertexCount;
        float ao_bl = AO_Factors[aoData & 0xFF];
        float ao_br = AO_Factors[(aoData >> 8) & 0xFF];
        float ao_tr = AO_Factors[(aoData >> 16) & 0xFF];
        float ao_tl = AO_Factors[(aoData >> 24) & 0xFF];

        builder.AddVertex(new ChunkVertex(v1_bl, texIndex, new Vector2(0, h), ao_bl));
        builder.AddVertex(new ChunkVertex(v2_br, texIndex, new Vector2(w, h), ao_br));
        builder.AddVertex(new ChunkVertex(v4_tr, texIndex, new Vector2(w, 0), ao_tr));
        builder.AddVertex(new ChunkVertex(v3_tl, texIndex, new Vector2(0, 0), ao_tl));

        if (reversed)
        {
            builder.AddIndices((ushort)(baseIndex + 0), (ushort)(baseIndex + 1), (ushort)(baseIndex + 2));
            builder.AddIndices((ushort)(baseIndex + 0), (ushort)(baseIndex + 2), (ushort)(baseIndex + 3));
        }
        else
        {
            builder.AddIndices((ushort)(baseIndex + 0), (ushort)(baseIndex + 2), (ushort)(baseIndex + 1));
            builder.AddIndices((ushort)(baseIndex + 0), (ushort)(baseIndex + 3), (ushort)(baseIndex + 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetPaddedIndex(int x, int y, int z)
        => x + z * 18 + y * 324;
}