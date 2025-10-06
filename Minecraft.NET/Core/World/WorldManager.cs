using Minecraft.NET.Core.Abstractions;

namespace Minecraft.NET.Core.World;

public class WorldManager : IWorldManager
{
    private const uint CHUNK_HEIGHT = 16;
    private const ushort BLOCK_ID_STONE = 1;
    private const ushort BLOCK_ID_AIR = 0;

    private readonly Dictionary<Vector2, ChunkColumn> _chunkColumns = [];

    public ChunkColumn? GetChunkColumn(Vector2 position)
    {
        if (_chunkColumns.TryGetValue(position, out var chunkColumn))
        {
            return chunkColumn;
        }

        chunkColumn = GenerateChunkColumn(position);
        _chunkColumns.Add(position, chunkColumn);
        return chunkColumn;
    }

    private static ChunkColumn GenerateChunkColumn(Vector2 position)
    {
        var column = new ChunkColumn(CHUNK_HEIGHT);

        for (int chunkY = 0; chunkY < CHUNK_HEIGHT; chunkY++)
        {
            var chunk = new Chunk();

            if (chunkY < 4)
            {
                var stoneBlock = new Block(BLOCK_ID_STONE, new ModelID(BLOCK_ID_STONE));

                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        for (int z = 0; z < 16; z++)
                        {
                            chunk.BlockIDs[x, y, z] = stoneBlock;
                        }
                    }
                }
            }
            else
            {
                var airBlock = new Block(BLOCK_ID_AIR, new ModelID(BLOCK_ID_AIR));
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        for (int z = 0; z < 16; z++)
                        {
                            chunk.BlockIDs[x, y, z] = airBlock;
                        }
                    }
                }
            }

            chunk.IsDirty = true;
            column.Chunks[chunkY] = chunk;
        }

        return column;
    }
}