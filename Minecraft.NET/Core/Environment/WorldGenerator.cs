using Minecraft.NET.Core.Chunks;

namespace Minecraft.NET.Core.Environment;

public class WorldGenerator
{
    private const int BaseHeight = 0;
    private const float Amplitude = 50.0f;

    private const float CaveRadius = 0.18f;

    private static readonly FastNoiseLite _noise;

    private static readonly FastNoiseLite _caveNoiseX;
    private static readonly FastNoiseLite _caveNoiseY;

    static WorldGenerator()
    {
        _noise = new FastNoiseLite();
        _noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _noise.SetSeed(1337);
        _noise.SetFrequency(0.005f);
        _noise.SetFractalType(FastNoiseLite.FractalType.FBm);
        _noise.SetFractalOctaves(4);
        _noise.SetFractalLacunarity(2.0f);
        _noise.SetFractalGain(0.5f);

        _caveNoiseX = new FastNoiseLite();
        _caveNoiseX.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _caveNoiseX.SetSeed(1338);
        _caveNoiseX.SetFrequency(0.02f);

        _caveNoiseY = new FastNoiseLite();
        _caveNoiseY.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _caveNoiseY.SetSeed(1339);
        _caveNoiseY.SetFrequency(0.02f);
    }

    public static void Generate(ChunkColumn column)
    {
        float caveRadiusSq = CaveRadius * CaveRadius;

        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                float worldX = column.Position.X * ChunkSize + x;
                float worldZ = column.Position.Y * ChunkSize + z;

                float noiseValue = _noise.GetNoise(worldX, worldZ);
                int terrainHeight = (int)(BaseHeight + noiseValue * Amplitude);

                for (int y = 0; y < WorldHeightInBlocks; y++)
                {
                    int worldY = y - VerticalChunkOffset * ChunkSize;

                    if (worldY > terrainHeight)
                        continue;

                    float noiseValX = _caveNoiseX.GetNoise(worldX, (float)worldY, worldZ);
                    float noiseValY = _caveNoiseY.GetNoise(worldX, (float)worldY, worldZ);

                    float distFromCenterSq = noiseValX * noiseValX + noiseValY * noiseValY;

                    if (distFromCenterSq < caveRadiusSq) continue;

                    if (worldY == terrainHeight)
                        column.SetBlock(x, y, z, BlockId.Grass);
                    else if (worldY > terrainHeight - 4)
                        column.SetBlock(x, y, z, BlockId.Dirt);
                    else
                        column.SetBlock(x, y, z, BlockId.Stone);
                }
            }
        }
    }
}