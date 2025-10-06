using Minecraft.NET.Core.Abstractions;
using Minecraft.NET.Core.Blocks;

namespace Minecraft.NET.Core.World;

public class FlatWorldGenerator(GameSettings gameSettings) : IWorldGenerator
{
    public ChunkColumn GenerateChunkColumn(Vector2 position)
    {
        var column = new ChunkColumn((uint)gameSettings.ChunkHeight);

        for (int chunkY = 0; chunkY < gameSettings.ChunkHeight; chunkY++)
        {
            var chunk = new Chunk(gameSettings.ChunkSize);
            var worldY_base = chunkY * gameSettings.ChunkSize;

            for (int x = 0; x < gameSettings.ChunkSize; x++)
                for (int y = 0; y < gameSettings.ChunkSize; y++)
                    for (int z = 0; z < gameSettings.ChunkSize; z++)
                    {
                        var worldY = worldY_base + y;
                        Block block;
                        if (worldY < 60)
                            block = new Block(BlockManager.Stone.ID, new ModelID(BlockManager.Stone.ID));
                        else if (worldY < 63)
                            block = new Block(BlockManager.Dirt.ID, new ModelID(BlockManager.Dirt.ID));
                        else if (worldY == 63)
                            block = new Block(BlockManager.Grass.ID, new ModelID(BlockManager.Grass.ID));
                        else
                            block = new Block(BlockManager.Air.ID, new ModelID(BlockManager.Air.ID));

                        chunk.BlockIDs[x, y, z] = block;
                    }

            chunk.IsDirty = true;
            column.Chunks[chunkY] = chunk;
        }

        return column;
    }
}