using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Game.World.Chunks;

namespace Minecraft.NET.Game.World.Environment;

public interface IWorldGenerator
{
    void Generate(ref ChunkSection column);
}

public class FlatWorldGenerator : IWorldGenerator
{
    public void Generate(ref ChunkSection section)
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