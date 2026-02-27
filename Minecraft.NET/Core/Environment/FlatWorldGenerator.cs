using Minecraft.NET.Core.Chunks;

namespace Minecraft.NET.Core.Environment;

public class FlatWorldGenerator : IWorldGenerator
{
    public void Generate(ChunkSection section)
    {
        for (int x = 0; x < ChunkSize; x++)
            for (int z = 0; z < ChunkSize; z++)
                for (int y = 0; y < ChunkSize; y++)
                    section.SetBlock(new(x, y, z), y switch
                    {
                        0 => BlockId.Stone,
                        1 or 2 => BlockId.Dirt,
                        3 => BlockId.Grass,
                        _ => BlockId.Air
                    });
    }
}