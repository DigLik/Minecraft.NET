using Minecraft.NET.Core.Chunks;

namespace Minecraft.NET.Core.Environment;

public class FlatWorldGenerator : IWorldGenerator
{
    public void Generate(ChunkColumn column)
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                column.SetBlock(x, 0, z, BlockId.Stone);
                column.SetBlock(x, 1, z, BlockId.Dirt);
                column.SetBlock(x, 2, z, BlockId.Dirt);
                column.SetBlock(x, 3, z, BlockId.Grass);
            }
        }
    }
}