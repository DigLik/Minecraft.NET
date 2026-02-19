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

        ProcessAxisNaiveSimd(builder, paddedBlocks, 0);
        if (token.IsCancellationRequested) return default;

        ProcessAxisNaiveSimd(builder, paddedBlocks, 1);
        if (token.IsCancellationRequested) return default;

        ProcessAxisNaiveSimd(builder, paddedBlocks, 2);

        return builder.BuildToData();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ProcessAxisNaiveSimd(MeshBuilder builder, BlockId* blocks, int axisNum)
    {
        int axisStride = axisNum == 0 ? X_STRIDE : (axisNum == 1 ? Y_STRIDE : Z_STRIDE);

        for (int y = 0; y < ChunkSize; y++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                BlockId* rowPtr = blocks + (y + 1) * Y_STRIDE + (z + 1) * Z_STRIDE + 1 * X_STRIDE;

                for (int dir = 0; dir < 2; dir++)
                {
                    int neighborOffset = (dir == 1 ? 1 : -1) * axisStride;
                    BlockId* neighborPtr = rowPtr + neighborOffset;

                    Vector128<byte> vCurrent = Vector128.Load((byte*)rowPtr);
                    Vector128<byte> vNeighbor = Vector128.Load((byte*)neighborPtr);

                    var vSolid = ~Vector128.Equals(vCurrent, Vector128<byte>.Zero);
                    var vNeighborAir = Vector128.Equals(vNeighbor, Vector128<byte>.Zero);
                    var vFaceMask = vSolid & vNeighborAir;

                    uint mask = vFaceMask.ExtractMostSignificantBits();

                    while (mask != 0)
                    {
                        int x = BitOperations.TrailingZeroCount(mask);
                        BlockId blockId = rowPtr[x];

                        int aoX = x + (axisNum == 0 ? (dir == 1 ? 1 : -1) : 0);
                        int aoY = y + (axisNum == 1 ? (dir == 1 ? 1 : -1) : 0);
                        int aoZ = z + (axisNum == 2 ? (dir == 1 ? 1 : -1) : 0);

                        uint aoData = CalculateFaceAO(blocks, axisNum, dir, aoX, aoY, aoZ);

                        AddQuadNaive(builder, axisNum, dir, x, y, z, blockId, aoData);

                        mask &= ~(1u << x);
                    }
                }
            }
        }
    }

    private static void AddQuadNaive(
        MeshBuilder builder,
        int axis, int dir,
        int x, int y, int z,
        BlockId blockId, uint aoData)
    {
        var blockDef = BlockRegistry.Definitions[(int)blockId];

        int texIndex = axis == 1
            ? ((dir == 1) ? blockDef.Textures.Top : blockDef.Textures.Bottom)
            : blockDef.Textures.Side;

        float x1, x2, x3, x4;
        float y1, y2, y3, y4;
        float z1, z2, z3, z4;

        if (axis == 1)
        {
            float yPos = y + (dir == 1 ? 1 : 0);
            x1 = x; z1 = z; y1 = yPos;
            x2 = x + 1; z2 = z; y2 = yPos;
            x3 = x + 1; z3 = z + 1; y3 = yPos;
            x4 = x; z4 = z + 1; y4 = yPos;
        }
        else if (axis == 2)
        {
            float zPos = z + (dir == 1 ? 1 : 0);
            x1 = x; y1 = y; z1 = zPos;
            x2 = x + 1; y2 = y; z2 = zPos;
            x3 = x + 1; y3 = y + 1; z3 = zPos;
            x4 = x; y4 = y + 1; z4 = zPos;
        }
        else
        {
            float xPos = x + (dir == 1 ? 1 : 0);
            z1 = z; y1 = y; x1 = xPos;
            z2 = z + 1; y2 = y; x2 = xPos;
            z3 = z + 1; y3 = y + 1; x3 = xPos;
            z4 = z; y4 = y + 1; x4 = xPos;
        }

        Vector3 v1 = new(x1, y1, z1);
        Vector3 v2 = new(x2, y2, z2);
        Vector3 v3 = new(x3, y3, z3);
        Vector3 v4 = new(x4, y4, z4);

        bool reversed = axis == 2 ? (dir == 0) : (dir == 1);

        float ao_bl = AO_Factors[aoData & 0xFF];
        float ao_br = AO_Factors[(aoData >> 8) & 0xFF];
        float ao_tr = AO_Factors[(aoData >> 16) & 0xFF];
        float ao_tl = AO_Factors[(aoData >> 24) & 0xFF];

        ushort baseIndex = (ushort)builder.VertexCount;

        builder.AddVertex(new ChunkVertex(v1, texIndex, new Vector2(0, 1), ao_bl));
        builder.AddVertex(new ChunkVertex(v2, texIndex, new Vector2(1, 1), ao_br));
        builder.AddVertex(new ChunkVertex(v3, texIndex, new Vector2(1, 0), ao_tr));
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe uint CalculateFaceAO(BlockId* blocks, int axis, int dir, int x, int y, int z)
    {
        int px = x + 1;
        int py = y + 1;
        int pz = z + 1;

        BlockId* ptr = blocks + px * X_STRIDE + pz * Z_STRIDE + py * Y_STRIDE;

        int uOff, vOff;
        if (axis == 0) { uOff = Z_STRIDE; vOff = Y_STRIDE; }
        else if (axis == 1) { uOff = X_STRIDE; vOff = Z_STRIDE; }
        else { uOff = X_STRIDE; vOff = Y_STRIDE; }

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