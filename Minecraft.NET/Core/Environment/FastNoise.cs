using System.Runtime.CompilerServices;

namespace Minecraft.NET.Core.Environment;

public sealed class FastNoise(int seed)
{
    public unsafe void FillHeightMapSIMD(float* map, int chunkX, int chunkZ, float frequency, int octaves, float lacunarity, float persistence)
    {
        float startX = chunkX;
        float startZ = chunkZ;

        var vFreq = new Vector<float>(frequency);
        var vLacunarity = new Vector<float>(lacunarity);
        var vGain = new Vector<float>(persistence);
        var vSeed = new Vector<int>(seed);

        Span<float> xOffsets = stackalloc float[Vector<float>.Count];
        for (int i = 0; i < Vector<float>.Count; i++) xOffsets[i] = i;
        var vXOffsets = new Vector<float>(xOffsets);

        int step = Vector<float>.Count;

        for (int z = 0; z < ChunkSize; z++)
        {
            var vZ = new Vector<float>(startZ + z);

            for (int x = 0; x < ChunkSize; x += step)
            {
                var vX = new Vector<float>(startX + x) + vXOffsets;

                var vNoise = GetFractalNoiseSIMD(vX, vZ, vSeed, vFreq, vLacunarity, vGain, octaves);

                vNoise.CopyTo(new Span<float>(map + (x + z * ChunkSize), step));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<float> GetFractalNoiseSIMD(
        Vector<float> x,
        Vector<float> y,
        Vector<int> seed,
        Vector<float> freq,
        Vector<float> lacunarity,
        Vector<float> gain,
        int octaves)
    {
        var sum = Vector<float>.Zero;
        var amp = Vector<float>.One;
        var currentFreq = freq;

        for (int i = 0; i < octaves; i++)
        {
            sum += GetSingleNoiseSIMD(x * currentFreq, y * currentFreq, seed) * amp;
            currentFreq *= lacunarity;
            amp *= gain;
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<float> GetSingleNoiseSIMD(Vector<float> x, Vector<float> y, Vector<int> seed)
    {
        var xs = Vector.Floor(x);
        var ys = Vector.Floor(y);

        var x0 = Vector.ConvertToInt32(xs);
        var y0 = Vector.ConvertToInt32(ys);

        var x1 = x0 + Vector<int>.One;
        var y1 = y0 + Vector<int>.One;

        var xf = x - xs;
        var yf = y - ys;

        var u = Fade(xf);
        var v = Fade(yf);

        var g00 = Grad(Hash(x0, y0, seed), xf, yf);
        var g10 = Grad(Hash(x1, y0, seed), xf - Vector<float>.One, yf);
        var g01 = Grad(Hash(x0, y1, seed), xf, yf - Vector<float>.One);
        var g11 = Grad(Hash(x1, y1, seed), xf - Vector<float>.One, yf - Vector<float>.One);

        var lerpX1 = Lerp(u, g00, g10);
        var lerpX2 = Lerp(u, g01, g11);

        return Lerp(v, lerpX1, lerpX2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<float> Fade(Vector<float> t)
        => t * t * t * (t * (t * 6.0f - new Vector<float>(15.0f)) + new Vector<float>(10.0f));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<float> Lerp(Vector<float> t, Vector<float> a, Vector<float> b) => a + t * (b - a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<int> Hash(Vector<int> x, Vector<int> y, Vector<int> seed)
    {
        var h = seed ^ x * 374761393 ^ y * 668265263;
        h = (h ^ (Vector.ShiftRightLogical(h, 13))) * 1274126177;
        return h ^ (Vector.ShiftRightLogical(h, 16));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<float> Grad(Vector<int> hash, Vector<float> x, Vector<float> y)
    {
        var hAnd1 = Vector.BitwiseAnd(hash, Vector<int>.One);
        var hAnd2 = Vector.BitwiseAnd(hash, new Vector<int>(2));

        var gradX = Vector.ConditionalSelect(
            Vector.Equals(hAnd1, Vector<int>.Zero),
            x,
            -x
        );

        var gradY = Vector.ConditionalSelect(
            Vector.Equals(hAnd2, Vector<int>.Zero),
            y,
            -y
        );

        return gradX + gradY;
    }
}