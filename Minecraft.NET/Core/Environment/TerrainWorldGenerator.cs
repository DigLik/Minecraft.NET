using Minecraft.NET.Core.Chunks;

namespace Minecraft.NET.Core.Environment;

public class TerrainWorldGenerator : IWorldGenerator
{
    private readonly FastNoise _noise = new(12345);

    private const float Frequency = 0.008f;
    private const int Octaves = 5;
    private const float Lacunarity = 2.0f;
    private const float Persistence = 0.3f;

    private const int SeaLevel = 0;
    private const float MinHeight = 50.0f;
    private const float MaxHeight = 250.0f;
    private const float HeightExponent = 1.0f;

    public unsafe void Generate(ChunkColumn column)
    {
        float* heightMap = stackalloc float[ChunkSize * ChunkSize];

        _noise.FillHeightMapSIMD(
            heightMap,
            column.Position.X * ChunkSize,
            column.Position.Y * ChunkSize,
            Frequency,
            Octaves,
            Lacunarity,
            Persistence
        );

        for (int z = 0; z < ChunkSize; z++)
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                float rawNoise = heightMap[x + z * ChunkSize];

                float normalized = (rawNoise + 1.2f) / 2.4f;
                normalized = Math.Clamp(normalized, 0.0f, 1.0f);

                float curve = MathF.Pow(normalized, HeightExponent);

                int height = (int)(MinHeight + curve * (MaxHeight - MinHeight));
                height = Math.Clamp(height, 0, WorldHeightInBlocks - 1);

                for (int y = 0; y <= height; y++)
                {
                    BlockId block;

                    if (y == height)
                    {
                        if (y >= SeaLevel + 2) block = BlockId.Grass;
                        else block = BlockId.Dirt;
                    }
                    else if (y > height - 4)
                    {
                        block = BlockId.Dirt;
                    }
                    else
                    {
                        block = BlockId.Stone;
                    }

                    column.SetBlock(x, y, z, block);
                }
            }
        }
    }
}