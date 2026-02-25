using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Models;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct ChunkVertex(Vector3 pos, int texIndex, Vector2 uv)
{
    public readonly Vector3 Pos = pos;
    public readonly uint TexIndex = (uint)texIndex;
    public readonly Vector2 UV = uv;
    public static readonly unsafe uint Stride = (uint)sizeof(ChunkVertex);
}