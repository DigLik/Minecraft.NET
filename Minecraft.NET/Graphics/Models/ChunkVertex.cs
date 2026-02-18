using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly unsafe struct ChunkVertex(Vector3 pos, int texIndex, Vector2 uv, float ao)
{
    public readonly byte X = (byte)pos.X;
    public readonly byte Y = (byte)pos.Y;
    public readonly byte Z = (byte)pos.Z;
    public readonly byte AO = (byte)(ao * 255);

    public readonly ushort TexIndex = (ushort)texIndex;
    public readonly byte U = (byte)uv.X;
    public readonly byte V = (byte)uv.Y;

    public static readonly uint Stride = 8;
}