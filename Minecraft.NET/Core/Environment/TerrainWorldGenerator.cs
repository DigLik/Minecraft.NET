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
        Random random = new Random(column.Position.X * 73856093 ^ column.Position.Y * 1920164449 ^ 12345);

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
                    BlockId block = y == height
                        ? y >= SeaLevel + 2
                            ? BlockId.Grass
                            : BlockId.Dirt
                        : y > height - 4
                            ? block = BlockId.Dirt
                            : block = BlockId.Stone;

                    column.SetBlock(x, y, z, block);
                }

                if (height >= SeaLevel + 2 && x >= 2 && x <= 13 && z >= 2 && z <= 13)
                    if (random.NextDouble() < 0.015)
                        GenerateTree(column, x, height + 1, z, random);
            }
        }
    }

    private static void GenerateTree(ChunkColumn column, int x, int y, int z, Random random)
    {
        int trunkHeight = random.Next(4, 7);

        for (int ly = y + trunkHeight - 3; ly <= y + trunkHeight + 1; ly++)
        {
            int radius = (ly >= y + trunkHeight) ? 1 : 2;

            for (int lx = x - radius; lx <= x + radius; lx++)
            {
                for (int lz = z - radius; lz <= z + radius; lz++)
                {
                    if (lx >= 0 && lx < ChunkSize && lz >= 0 && lz < ChunkSize && ly < WorldHeightInBlocks)
                    {
                        if (Math.Abs(lx - x) == radius && Math.Abs(lz - z) == radius && (ly >= y + trunkHeight || random.Next(2) == 0))
                            continue;

                        if (column.GetBlock(lx, ly, lz) == BlockId.Air)
                            column.SetBlock(lx, ly, lz, BlockId.OakLeaves);
                    }
                }
            }
        }

        for (int ty = 0; ty < trunkHeight; ty++)
            if (y + ty < WorldHeightInBlocks)
                column.SetBlock(x, y + ty, z, BlockId.OakLog);
    }
}