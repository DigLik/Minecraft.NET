using System.Runtime.CompilerServices;

using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Utils.Collections;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Chunks;

public struct ChunkSection
{
    public NativeList<BlockId> Blocks;
    public int NonAirBlockCount;
    public BlockId UniformId;
    public bool IsModified;
    public Vector3Int Position { get; set; }

    public readonly bool IsAllocated => Blocks.IsCreated;
    public readonly bool IsEmpty => !IsAllocated ? UniformId == BlockId.Air : NonAirBlockCount == 0;
    public readonly bool IsFull => !IsAllocated ? UniformId != BlockId.Air : NonAirBlockCount == BlocksInChunk;

    public void Reset()
    {
        if (IsAllocated) { Blocks.Dispose(); Blocks = default; }
        NonAirBlockCount = 0; UniformId = BlockId.Air; IsModified = false;
    }

    public void Fill(BlockId id)
    {
        if (IsAllocated) { Blocks.Dispose(); Blocks = default; }
        UniformId = id; NonAirBlockCount = id == BlockId.Air ? 0 : BlocksInChunk; IsModified = true;
    }

    public void Allocate()
    {
        if (IsAllocated) return;
        Blocks = new NativeList<BlockId>(BlocksInChunk);
        Blocks.Resize(BlocksInChunk, UniformId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BlockId GetBlock(int index) => IsAllocated ? Blocks[index] : UniformId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BlockId GetBlock(int x, int y, int z) => GetBlock(GetIndex(x, y, z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BlockId GetBlock(Vector3Int pos) => GetBlock(GetIndex(pos));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(Vector3Int pos, BlockId id)
    {
        if (IsAllocated)
        {
            int index = GetIndex(pos);
            if (Blocks[index] == id) return;
            NonAirBlockCount += (Blocks[index] == BlockId.Air ? 1 : 0) - (id == BlockId.Air ? 1 : 0);
            Blocks[index] = id;
            IsModified = true;
            return;
        }
        if (UniformId == id) return;
        Allocate();
        SetBlock(pos, id);
    }

    public void Optimize()
    {
        if (!IsAllocated) return;
        BlockId first = Blocks[0];
        for (int i = 1; i < BlocksInChunk; i++) if (Blocks[i] != first) return;

        Blocks.Dispose();
        Blocks = default;
        UniformId = first;
        NonAirBlockCount = first == BlockId.Air ? 0 : BlocksInChunk;
    }

    public void Free() => Reset();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndex(int x, int y, int z) => x | (z << 4) | (y << 8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndex(Vector3Int p) => p.X | (p.Z << 4) | (p.Y << 8);
}