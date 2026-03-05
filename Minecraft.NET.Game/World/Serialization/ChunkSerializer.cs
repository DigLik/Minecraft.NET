using System.Buffers;

using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Game.World.Chunks;
using Minecraft.NET.Game.World.Environment;

namespace Minecraft.NET.Game.World.Serialization;

public static unsafe class ChunkSerializer
{
    public static PooledChunkData Serialize(ref ChunkSection chunk)
    {
        int maxLen = 2 + (chunk.IsAllocated ? BlocksInChunk : 0);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLen);

        buffer[0] = (byte)chunk.UniformId;
        buffer[1] = chunk.IsAllocated ? (byte)1 : (byte)0;

        if (chunk.IsAllocated && chunk.Blocks != null)
        {
            var sourceSpan = new ReadOnlySpan<byte>(chunk.Blocks, BlocksInChunk);
            var destSpan = new Span<byte>(buffer, 2, BlocksInChunk);
            sourceSpan.CopyTo(destSpan);
        }

        return new PooledChunkData(buffer, maxLen);
    }

    public static void Deserialize(ReadOnlySpan<byte> data, ref ChunkSection chunk)
    {
        chunk.UniformId = (BlockId)data[0];
        bool isAllocated = data[1] == 1;

        if (isAllocated)
        {
            chunk.Allocate();
            var destSpan = new Span<byte>(chunk.Blocks, BlocksInChunk);
            data.Slice(2, BlocksInChunk).CopyTo(destSpan);

            int nonAir = 0;
            for (int i = 0; i < BlocksInChunk; i++)
                if (chunk.Blocks[i] != BlockId.Air) nonAir++;

            chunk.NonAirBlockCount = nonAir;
        }
        else
        {
            chunk.Free();
            chunk.NonAirBlockCount = chunk.UniformId == BlockId.Air ? 0 : BlocksInChunk;
        }

        chunk.IsModified = false;
    }
}