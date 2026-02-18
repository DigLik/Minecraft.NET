using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Core.Environment;

namespace Minecraft.NET.Graphics.Models;

public static class ChunkMesher
{
    private static readonly float[] AO_Factors = [0.5f, 0.7f, 0.85f, 1.0f];
    private static readonly ThreadLocal<MeshBuilder> _threadLocalBuilder = new(() => new MeshBuilder());

    private const int PaddedSize = ChunkSize + 2;
    private const int PaddedSlice = PaddedSize * PaddedSize;
    private const int TotalSize = PaddedSize * PaddedSize * PaddedSize;

    private const int X_STRIDE = 1;
    private const int Z_STRIDE = PaddedSize;
    private const int Y_STRIDE = PaddedSlice;

    public static unsafe MeshData GenerateMesh(ChunkColumn column, int sectionY, World world, CancellationToken token)
    {
        ref var section = ref column.Sections[sectionY];
        if (section.IsEmpty)
            return default;

        if (section.IsFull && AreAllNeighborsFull(column, sectionY, world))
            return default;

        BlockId* paddedBlocks = stackalloc BlockId[TotalSize];
        FillPaddedBufferOptimized(paddedBlocks, column, sectionY, world);

        var builder = _threadLocalBuilder.Value!;
        builder.Reset();

        ProcessAxisSimd(builder, paddedBlocks, 1, 0, 2, Y_STRIDE, X_STRIDE, Z_STRIDE);

        if (token.IsCancellationRequested) return default;

        ProcessAxisSimd(builder, paddedBlocks, 2, 0, 1, Z_STRIDE, X_STRIDE, Y_STRIDE);

        if (token.IsCancellationRequested) return default;

        ProcessAxisScalar(builder, paddedBlocks, 0, 2, 1, X_STRIDE, Z_STRIDE, Y_STRIDE);

        return builder.BuildToData();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ProcessAxisSimd(
        MeshBuilder builder, BlockId* blocks,
        int axisNum, int uAxis, int vAxis,
        int axisStride, int uStride, int vStride)
    {
        for (int layer = 0; layer < ChunkSize; layer++)
        {
            BlockId* layerPtr = blocks + (layer + 1) * axisStride + 1 * vStride + 1 * uStride;

            for (int dir = 0; dir < 2; dir++)
            {
                int neighborOffset = (dir == 1 ? 1 : -1) * axisStride;

                for (int v = 0; v < ChunkSize; v++)
                {
                    BlockId* rowPtr = layerPtr + v * vStride;
                    BlockId* neighborPtr = rowPtr + neighborOffset;

                    Vector128<byte> vCurrent = Vector128.Load((byte*)rowPtr);
                    Vector128<byte> vNeighbor = Vector128.Load((byte*)neighborPtr);

                    var vSolid = ~Vector128.Equals(vCurrent, Vector128<byte>.Zero);
                    var vNeighborAir = Vector128.Equals(vNeighbor, Vector128<byte>.Zero);
                    var vFaceMask = vSolid & vNeighborAir;

                    uint mask = vFaceMask.ExtractMostSignificantBits();

                    while (mask != 0)
                    {
                        int u = BitOperations.TrailingZeroCount(mask);

                        BlockId blockId = rowPtr[u];

                        int x, y, z;
                        if (axisNum == 1)
                        { x = u; y = layer; z = v; }
                        else
                        { x = u; y = v; z = layer; }

                        int aoX = x + (axisNum == 0 ? (dir == 1 ? 1 : -1) : 0);
                        int aoY = y + (axisNum == 1 ? (dir == 1 ? 1 : -1) : 0);
                        int aoZ = z + (axisNum == 2 ? (dir == 1 ? 1 : -1) : 0);

                        uint aoData = CalculateFaceAO(blocks, axisNum, dir, aoX, aoY, aoZ);

                        int w = 1;
                        while (true)
                        {
                            int nextU = u + w;
                            if (nextU >= ChunkSize) break;

                            if ((mask & (1u << nextU)) == 0) break;
                            if (rowPtr[nextU] != blockId) break;

                            int nextAoX = x + (axisNum == 0 ? 0 : w);
                            int nextAoY = y + (axisNum == 1 ? 0 : (axisNum == 2 ? w : 0));

                            uint nextAoData = CalculateFaceAO(blocks, axisNum, dir, aoX + w, aoY, aoZ);

                            if (nextAoData != aoData) break;

                            w++;
                        }

                        AddQuad(builder, axisNum, dir, u, v, w, 1, layer, blockId, aoData);

                        uint clearMask = ((1u << w) - 1) << u;
                        mask &= ~clearMask;
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ProcessAxisScalar(
        MeshBuilder builder, BlockId* blocks,
        int axisNum, int uAxis, int vAxis,
        int axisStride, int uStride, int vStride)
    {
        for (int layer = 0; layer < ChunkSize; layer++)
        {
            BlockId* layerPtr = blocks + (layer + 1) * axisStride + 1 * vStride + 1 * uStride;

            for (int dir = 0; dir < 2; dir++)
            {
                int neighborOffset = (dir == 1 ? 1 : -1) * axisStride;

                for (int v = 0; v < ChunkSize; v++)
                {
                    BlockId* rowPtr = layerPtr + v * vStride;
                    BlockId* neighborPtr = rowPtr + neighborOffset;

                    uint mask = 0;
                    for (int u = 0; u < ChunkSize; u++)
                    {
                        BlockId curr = rowPtr[u * uStride];
                        BlockId neigh = neighborPtr[u * uStride];

                        if (curr != BlockId.Air && neigh == BlockId.Air)
                        {
                            mask |= (1u << u);
                        }
                    }

                    while (mask != 0)
                    {
                        int u = BitOperations.TrailingZeroCount(mask);

                        BlockId blockId = rowPtr[u * uStride];

                        int x = layer;
                        int y = v;
                        int z = u;

                        int aoX = x + (dir == 1 ? 1 : -1);
                        int aoY = y;
                        int aoZ = z;

                        uint aoData = CalculateFaceAO(blocks, axisNum, dir, aoX, aoY, aoZ);

                        int w = 1;
                        while (true)
                        {
                            int nextU = u + w;
                            if (nextU >= ChunkSize) break;
                            if ((mask & (1u << nextU)) == 0) break;
                            if (rowPtr[nextU * uStride] != blockId) break;

                            uint nextAoData = CalculateFaceAO(blocks, axisNum, dir, aoX, aoY, aoZ + w);
                            if (nextAoData != aoData) break;

                            w++;
                        }

                        AddQuad(builder, axisNum, dir, u, v, w, 1, layer, blockId, aoData);

                        uint clearMask = ((1u << w) - 1) << u;
                        mask &= ~clearMask;
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe uint CalculateFaceAO(BlockId* blocks, int axis, int dir, int x, int y, int z)
    {
        int px = x + 1;
        int py = y + 1;
        int pz = z + 1;

        BlockId* ptr = blocks + px * X_STRIDE + pz * Z_STRIDE + py * Y_STRIDE;

        int uOff, vOff;

        if (axis == 0)
        {
            uOff = Z_STRIDE;
            vOff = Y_STRIDE;
        }
        else if (axis == 1)
        {
            uOff = X_STRIDE;
            vOff = Z_STRIDE;
        }
        else
        {
            uOff = X_STRIDE;
            vOff = Y_STRIDE;
        }

        bool l = ptr[-uOff] != BlockId.Air;
        bool r = ptr[uOff] != BlockId.Air;
        bool b = ptr[-vOff] != BlockId.Air;
        bool t = ptr[vOff] != BlockId.Air;

        bool bl = ptr[-uOff - vOff] != BlockId.Air;
        bool br = ptr[uOff - vOff] != BlockId.Air;
        bool tl = ptr[-uOff + vOff] != BlockId.Air;
        bool tr = ptr[uOff + vOff] != BlockId.Air;

        return VertexAO(l, bl, b) |
            (VertexAO(r, br, b) << 8) |
            (VertexAO(r, tr, t) << 16) |
            (VertexAO(l, tl, t) << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint VertexAO(bool side1, bool corner, bool side2)
    {
        if (side1 && side2) return 0;
        return (uint)(3 - ((side1 ? 1 : 0) + (side2 ? 1 : 0) + (corner ? 1 : 0)));
    }

    private static void AddQuad(
        MeshBuilder builder,
        int axis, int dir,
        int u, int v, int w, int h,
        int layer,
        BlockId blockId, uint aoData)
    {
        var blockDef = BlockRegistry.Definitions[(int)blockId];
        if (blockId == 0) return;

        int texIndex;
        if (axis == 1)
            texIndex = (dir == 1) ? blockDef.Textures.Top : blockDef.Textures.Bottom;
        else
            texIndex = blockDef.Textures.Side;

        float x1, y1, z1;
        float x2, y2, z2;
        float x3, y3, z3;
        float x4, y4, z4;

        if (axis == 1)
        {
            float y = layer + (dir == 1 ? 1 : 0);
            x1 = u; z1 = v; y1 = y;
            x2 = u + w; z2 = v; y2 = y;
            x3 = u + w; z3 = v + h; y3 = y;
            x4 = u; z4 = v + h; y4 = y;
        }
        else if (axis == 2)
        {
            float z = layer + (dir == 1 ? 1 : 0);
            x1 = u; y1 = v; z1 = z;
            x2 = u + w; y2 = v; z2 = z;
            x3 = u + w; y3 = v + h; z3 = z;
            x4 = u; y4 = v + h; z4 = z;
        }
        else
        {
            float x = layer + (dir == 1 ? 1 : 0);
            z1 = u; y1 = v; x1 = x;
            z2 = u + w; y2 = v; x2 = x;
            z3 = u + w; y3 = v + h; x3 = x;
            z4 = u; y4 = v + h; x4 = x;
        }

        Vector3 v1, v2, v3, v4;
        bool reversed = axis == 2 ? (dir == 0) : (dir == 1);

        v1 = new Vector3(x1, y1, z1);
        v2 = new Vector3(x2, y2, z2);
        v3 = new Vector3(x3, y3, z3);
        v4 = new Vector3(x4, y4, z4);

        float ao_bl = AO_Factors[aoData & 0xFF];
        float ao_br = AO_Factors[(aoData >> 8) & 0xFF];
        float ao_tr = AO_Factors[(aoData >> 16) & 0xFF];
        float ao_tl = AO_Factors[(aoData >> 24) & 0xFF];

        ushort baseIndex = (ushort)builder.VertexCount;

        builder.AddVertex(new ChunkVertex(v1, texIndex, new Vector2(0, h), ao_bl));
        builder.AddVertex(new ChunkVertex(v2, texIndex, new Vector2(w, h), ao_br));
        builder.AddVertex(new ChunkVertex(v3, texIndex, new Vector2(w, 0), ao_tr));
        builder.AddVertex(new ChunkVertex(v4, texIndex, new Vector2(0, 0), ao_tl));

        bool flipDiagonal = ao_bl + ao_tr < ao_br + ao_tl;

        if (reversed)
        {
            if (flipDiagonal)
            {
                builder.AddIndices((ushort)(baseIndex + 0), (ushort)(baseIndex + 2), (ushort)(baseIndex + 1));
                builder.AddIndices((ushort)(baseIndex + 0), (ushort)(baseIndex + 3), (ushort)(baseIndex + 2));
            }
            else
            {
                builder.AddIndices((ushort)(baseIndex + 0), (ushort)(baseIndex + 3), (ushort)(baseIndex + 1));
                builder.AddIndices((ushort)(baseIndex + 1), (ushort)(baseIndex + 3), (ushort)(baseIndex + 2));
            }
        }
        else
        {
            if (flipDiagonal)
            {
                builder.AddIndices((ushort)(baseIndex + 1), (ushort)(baseIndex + 2), (ushort)(baseIndex + 0));
                builder.AddIndices((ushort)(baseIndex + 2), (ushort)(baseIndex + 3), (ushort)(baseIndex + 0));
            }
            else
            {
                builder.AddIndices((ushort)(baseIndex + 1), (ushort)(baseIndex + 3), (ushort)(baseIndex + 0));
                builder.AddIndices((ushort)(baseIndex + 2), (ushort)(baseIndex + 3), (ushort)(baseIndex + 1));
            }
        }
    }

    private static bool AreAllNeighborsFull(ChunkColumn column, int sectionY, World world)
    {
        if (sectionY < WorldHeightInChunks - 1 && !column.Sections[sectionY + 1].IsFull) return false;
        if (sectionY > 0 && !column.Sections[sectionY - 1].IsFull) return false;
        var pos = column.Position;
        if (!IsNeighborFull(world, pos.X + 1, pos.Y, sectionY)) return false;
        if (!IsNeighborFull(world, pos.X - 1, pos.Y, sectionY)) return false;
        if (!IsNeighborFull(world, pos.X, pos.Y + 1, sectionY)) return false;
        if (!IsNeighborFull(world, pos.X, pos.Y - 1, sectionY)) return false;
        return true;
    }

    private static bool IsNeighborFull(World world, int cx, int cz, int sy)
    {
        var col = world.GetColumn(new Vector2D<int>(cx, cz));
        return col != null && col.Sections[sy].IsFull;
    }

    private static unsafe void FillPaddedBufferOptimized(BlockId* buffer, ChunkColumn center, int sectionY, World world)
    {
        ref var section = ref center.Sections[sectionY];
        if (section.IsAllocated)
        {
            BlockId* srcPtr = section.Blocks;
            for (int y = 0; y < ChunkSize; y++)
            {
                int dstOffset = (y + 1) * Y_STRIDE + 1 * Z_STRIDE + 1;
                int srcOffset = y * 256;

                for (int z = 0; z < ChunkSize; z++)
                {
                    Unsafe.CopyBlock(
                        buffer + dstOffset + z * Z_STRIDE,
                        srcPtr + srcOffset + z * 16,
                        ChunkSize
                    );
                }
            }
        }
        else
        {
            byte val = (byte)section.UniformId;
            for (int y = 0; y < ChunkSize; y++)
            {
                int dstOffset = (y + 1) * Y_STRIDE + 1 * Z_STRIDE + 1;
                for (int z = 0; z < ChunkSize; z++)
                    Unsafe.InitBlock(buffer + dstOffset + z * Z_STRIDE, val, ChunkSize);
            }
        }

        for (int y = -1; y <= ChunkSize; y++)
            for (int z = -1; z <= ChunkSize; z++)
                for (int x = -1; x <= ChunkSize; x++)
                {
                    if (x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize && z >= 0 && z < ChunkSize)
                        continue;

                    BlockId b = GetBlockSafeSmart(sectionY, x, y, z, center, world);

                    int pIndex = (x + 1) + (z + 1) * Z_STRIDE + (y + 1) * Y_STRIDE;
                    buffer[pIndex] = b;
                }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe BlockId GetBlockSafeSmart(int centerSectionY, int x, int y, int z, ChunkColumn cur, World world)
    {
        if (x >= 0 && x < ChunkSize && z >= 0 && z < ChunkSize && y >= 0 && y < ChunkSize)
            return cur.GetBlock(x, y + (centerSectionY << ChunkShift), z);

        int cx = cur.Position.X;
        int cz = cur.Position.Y;
        int cy = centerSectionY;

        if (x < 0) { cx--; x += ChunkSize; }
        else if (x >= ChunkSize) { cx++; x -= ChunkSize; }

        if (z < 0) { cz--; z += ChunkSize; }
        else if (z >= ChunkSize) { cz++; z -= ChunkSize; }

        if (y < 0) { cy--; y += ChunkSize; }
        else if (y >= ChunkSize) { cy++; y -= ChunkSize; }

        if (cy is < 0 or >= WorldHeightInChunks) return BlockId.Air;

        ChunkColumn? target = (cx == cur.Position.X && cz == cur.Position.Y) ? cur : world.GetColumn(new Vector2D<int>(cx, cz));

        if (target == null) return BlockId.Air;
        return target.Sections[cy].GetBlock(x, y, z);
    }
}