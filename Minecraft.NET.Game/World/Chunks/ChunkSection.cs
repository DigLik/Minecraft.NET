using System.Runtime.CompilerServices;

using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Utils.Collections;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Chunks;

public unsafe struct ChunkSection
{
    public NativeList<BlockId> Blocks;
    public int NonAirBlockCount;
    public BlockId UniformId;
    public bool IsModified;

    public Vector3<int> Position { get; set; }

    public readonly bool IsAllocated => Blocks.IsCreated;
    public readonly bool IsEmpty => (!IsAllocated && UniformId == BlockId.Air) || (IsAllocated && NonAirBlockCount == 0);
    public readonly bool IsFull => (!IsAllocated && UniformId != BlockId.Air) || (IsAllocated && NonAirBlockCount == BlocksInChunk);

    public void Reset()
    {
        if (Blocks.IsCreated)
        {
            Blocks.Dispose();
            Blocks = default;
        }
        NonAirBlockCount = 0;
        UniformId = BlockId.Air;
        IsModified = false;
    }

    public void Fill(BlockId id)
    {
        if (Blocks.IsCreated)
        {
            Blocks.Dispose();
            Blocks = default;
        }
        UniformId = id;
        NonAirBlockCount = (id == BlockId.Air) ? 0 : BlocksInChunk;
        IsModified = true;
    }

    public void Allocate()
    {
        if (!Blocks.IsCreated)
        {
            Blocks = new NativeList<BlockId>(BlocksInChunk);
            Blocks.Resize(BlocksInChunk, UniformId);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BlockId GetBlock(int index)
    {
        if (!Blocks.IsCreated) return UniformId;
        return Blocks[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BlockId GetBlock(int x, int y, int z)
    {
        if (!Blocks.IsCreated) return UniformId;
        return Blocks[GetIndex(x, y, z)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BlockId GetBlock(Vector3<int> position)
    {
        if (!Blocks.IsCreated) return UniformId;
        return Blocks[GetIndex(position)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(Vector3<int> position, BlockId id)
    {
        if (Blocks.IsCreated)
        {
            int index = GetIndex(position);
            BlockId oldId = Blocks[index];
            if (oldId == id) return;

            Blocks[index] = id;
            if (oldId == BlockId.Air && id != BlockId.Air) NonAirBlockCount++;
            else if (oldId != BlockId.Air && id == BlockId.Air) NonAirBlockCount--;

            IsModified = true;
            return;
        }

        if (UniformId == id) return;
        Allocate();
        SetBlock(position, id);
    }

    public void Optimize()
    {
        if (!Blocks.IsCreated) return;

        BlockId first = Blocks[0];
        for (int i = 1; i < BlocksInChunk; i++)
            if (Blocks[i] != first) return;

        Blocks.Dispose();
        Blocks = default;
        UniformId = first;
        NonAirBlockCount = (first == BlockId.Air) ? 0 : BlocksInChunk;
    }

    public void Free() => Reset();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndex(int x, int y, int z)
        => x | (z << 4) | (y << 8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndex(Vector3<int> position)
        => position.X | (position.Z << 4) | (position.Y << 8);
}