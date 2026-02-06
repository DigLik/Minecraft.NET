using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Core.Chunks;

public unsafe struct ChunkSection
{
    public const int SectionSize = ChunkSize * ChunkSize * ChunkSize;

    public BlockId* Blocks;
    public int NonAirBlockCount;
    public BlockId UniformId;

    public readonly bool IsAllocated => Blocks != null;
    public readonly bool IsEmpty => (!IsAllocated && UniformId == BlockId.Air) || (IsAllocated && NonAirBlockCount == 0);
    public readonly bool IsFull => (!IsAllocated && UniformId != BlockId.Air) || (IsAllocated && NonAirBlockCount == SectionSize);

    public void Reset()
    {
        if (Blocks != null)
        {
            NativeMemory.Free(Blocks);
            Blocks = null;
        }
        NonAirBlockCount = 0;
        UniformId = BlockId.Air;
    }

    public void Fill(BlockId id)
    {
        if (Blocks != null)
        {
            NativeMemory.Free(Blocks);
            Blocks = null;
        }
        UniformId = id;
        NonAirBlockCount = (id == BlockId.Air) ? 0 : SectionSize;
    }

    private void Allocate()
    {
        if (Blocks == null)
        {
            Blocks = (BlockId*)NativeMemory.Alloc(SectionSize, sizeof(BlockId));
            NativeMemory.Fill(Blocks, (nuint)SectionSize, (byte)UniformId);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BlockId GetBlock(int x, int y, int z)
    {
        if (Blocks == null) return UniformId;
        return Blocks[GetIndex(x, y, z)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(int x, int y, int z, BlockId id)
    {
        if (Blocks != null)
        {
            int index = GetIndex(x, y, z);
            BlockId oldId = Blocks[index];
            if (oldId == id) return;

            Blocks[index] = id;
            if (oldId == BlockId.Air && id != BlockId.Air) NonAirBlockCount++;
            else if (oldId != BlockId.Air && id == BlockId.Air) NonAirBlockCount--;

            return;
        }

        if (UniformId == id) return;

        Allocate();
        SetBlock(x, y, z, id);
    }

    public void Optimize()
    {
        if (Blocks == null) return;

        BlockId first = Blocks[0];
        for (int i = 1; i < SectionSize; i++)
            if (Blocks[i] != first) return;

        NativeMemory.Free(Blocks);
        Blocks = null;
        UniformId = first;
        NonAirBlockCount = (first == BlockId.Air) ? 0 : SectionSize;
    }

    public void Free()
    {
        Reset();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndex(int x, int y, int z)
        => x + (z << ChunkShift) + (y << (ChunkShift * 2));
}