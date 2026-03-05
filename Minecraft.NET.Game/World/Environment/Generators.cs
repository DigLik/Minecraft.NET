using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Game.World.Chunks;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Environment;

public interface IWorldGenerator
{
    void Generate(ref ChunkSection column);
}

public class FlatWorldGenerator : IWorldGenerator
{
    public void Generate(ref ChunkSection section)
    {
        if (section.Position.Z > 0) return;

        for (int x = 0; x < ChunkSize; x++)
            for (int y = 0; y < ChunkSize; y++)
                for (int z = 0; z < ChunkSize; z++)
                    section.SetBlock(new(x, y, z), z switch
                    {
                        0 => BlockId.Stone,
                        1 or 2 => BlockId.Dirt,
                        3 => BlockId.Grass,
                        _ => BlockId.Air
                    });
    }
}

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
    private const int ChunkSize = 16;
    private const int WorldHeightInBlocks = 256;

    public unsafe void Generate(ref ChunkSection section)
    {
        Random random = new Random(section.Position.X * 73856093 ^ section.Position.Y * 1920164449 ^ 12345);

        float* heightMap = stackalloc float[ChunkSize * ChunkSize];
        _noise.FillHeightMapSIMD(
            heightMap,
            section.Position.X * ChunkSize,
            section.Position.Y * ChunkSize,
            Frequency,
            Octaves,
            Lacunarity,
            Persistence
        );

        int sectionBaseZ = section.Position.Z * ChunkSize;

        for (int y = 0; y < ChunkSize; y++)
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                float rawNoise = heightMap[x + y * ChunkSize];
                float normalized = (rawNoise + 1.2f) / 2.4f;
                normalized = Math.Clamp(normalized, 0.0f, 1.0f);

                float curve = MathF.Pow(normalized, HeightExponent);
                int height = (int)(MinHeight + curve * (MaxHeight - MinHeight));
                height = Math.Clamp(height, 0, WorldHeightInBlocks - 1);

                for (int z = 0; z < ChunkSize; z++)
                {
                    int globalZ = sectionBaseZ + z;

                    if (globalZ <= height)
                    {
                        BlockId block = globalZ == height
                            ? globalZ >= SeaLevel + 2
                                ? BlockId.Grass
                                : BlockId.Dirt
                            : globalZ > height - 4
                                ? BlockId.Dirt
                                : BlockId.Stone;

                        section.SetBlock(new(x, y, z), block);
                    }
                }

                if (height >= SeaLevel + 2 && x >= 2 && x <= 13 && y >= 2 && y <= 13)
                {
                    if (random.NextDouble() < 0.015)
                    {
                        GenerateTree(ref section, x, y, height + 1, random);
                    }
                }
            }
        }
    }

    private static void GenerateTree(ref ChunkSection section, int x, int y, int surfaceGlobalZ, Random random)
    {
        int trunkHeight = random.Next(4, 7);
        int sectionBaseZ = section.Position.Z * ChunkSize;

        for (int lz = surfaceGlobalZ + trunkHeight - 3; lz <= surfaceGlobalZ + trunkHeight + 1; lz++)
        {
            int radius = (lz >= surfaceGlobalZ + trunkHeight) ? 1 : 2;

            for (int lx = x - radius; lx <= x + radius; lx++)
            {
                for (int ly = y - radius; ly <= y + radius; ly++)
                {
                    if (lx >= 0 && lx < ChunkSize && ly >= 0 && ly < ChunkSize && lz < WorldHeightInBlocks)
                    {
                        if (Math.Abs(lx - x) == radius && Math.Abs(ly - y) == radius && (lz >= surfaceGlobalZ + trunkHeight || random.Next(2) == 0))
                            continue;

                        if (lz >= sectionBaseZ && lz < sectionBaseZ + ChunkSize)
                        {
                            int localZ = lz - sectionBaseZ;
                            if (section.GetBlock(lx, ly, localZ) == BlockId.Air)
                                section.SetBlock(new(lx, ly, localZ), BlockId.OakLeaves);
                        }
                    }
                }
            }
        }

        for (int tz = 0; tz < trunkHeight; tz++)
        {
            int currentGlobalZ = surfaceGlobalZ + tz;
            if (currentGlobalZ >= sectionBaseZ && currentGlobalZ < sectionBaseZ + ChunkSize && currentGlobalZ < WorldHeightInBlocks)
            {
                int localZ = currentGlobalZ - sectionBaseZ;
                section.SetBlock(new(x, y, localZ), BlockId.OakLog);
            }
        }
    }
}