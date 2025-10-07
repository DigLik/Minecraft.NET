namespace Minecraft.NET.Core;

public class WorldGenerator
{
    private const int BaseHeight = 0;
    private const float Amplitude = 30.0f;

    private static readonly FastNoiseLite _noise;

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
    }

    public void Generate(ChunkSection chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                float worldX = chunk.Position.X * ChunkSize + x;
                float worldZ = chunk.Position.Z * ChunkSize + z;

                float noiseValue = _noise.GetNoise(worldX, worldZ);

                int terrainHeight = (int)(BaseHeight + noiseValue * Amplitude);

                for (int y = 0; y < ChunkSize; y++)
                {
                    int worldY = (int)(chunk.Position.Y * ChunkSize) + y;

                    if (worldY > terrainHeight)
                        continue;
                    else if (worldY == terrainHeight)
                        chunk.SetBlock(x, y, z, BlockId.Grass);
                    else if (worldY > terrainHeight - 4)
                        chunk.SetBlock(x, y, z, BlockId.Dirt);
                    else
                        chunk.SetBlock(x, y, z, BlockId.Stone);
                }
            }
        }
    }
}