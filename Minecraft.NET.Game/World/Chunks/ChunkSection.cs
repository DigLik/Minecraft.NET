using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Chunks;

public unsafe struct ChunkSection
{
    public BlockId* Blocks;
    public int NonAirBlockCount;
    public BlockId UniformId;

    public Vector3<int> Position { get; set; }

    public readonly bool IsAllocated => Blocks != null;
    public readonly bool IsEmpty => (!IsAllocated && UniformId == BlockId.Air) || (IsAllocated && NonAirBlockCount == 0);
    public readonly bool IsFull => (!IsAllocated && UniformId != BlockId.Air) || (IsAllocated && NonAirBlockCount == BlocksInChunk);

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
        NonAirBlockCount = (id == BlockId.Air) ? 0 : BlocksInChunk;
    }

    private void Allocate()
    {
        if (Blocks == null)
        {
            Blocks = (BlockId*)NativeMemory.Alloc(BlocksInChunk, sizeof(BlockId));
            NativeMemory.Fill(Blocks, BlocksInChunk, (byte)UniformId);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BlockId GetBlock(Vector3<int> position)
    {
        if (Blocks == null) return UniformId;
        return Blocks[GetIndex(position)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(Vector3<int> position, BlockId id)
    {
        if (Blocks != null)
        {
            int index = GetIndex(position);
            BlockId oldId = Blocks[index];
            if (oldId == id) return;

            Blocks[index] = id;
            if (oldId == BlockId.Air && id != BlockId.Air) NonAirBlockCount++;
            else if (oldId != BlockId.Air && id == BlockId.Air) NonAirBlockCount--;

            return;
        }

        if (UniformId == id) return;
        Allocate();
        SetBlock(position, id);
    }

    public void Optimize()
    {
        if (Blocks == null) return;
        BlockId first = Blocks[0];
        for (int i = 1; i < BlocksInChunk; i++)
            if (Blocks[i] != first) return;

        NativeMemory.Free(Blocks);
        Blocks = null;
        UniformId = first;
        NonAirBlockCount = (first == BlockId.Air) ? 0 : BlocksInChunk;
    }

    public void Free() => Reset();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndex(Vector3<int> position)
        => position.X + position.Z * ChunkSize + position.Y * ChunkSize * ChunkSize;
}