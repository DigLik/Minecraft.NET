using System.Numerics;
using System.Runtime.InteropServices;

using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Meshing;

[StructLayout(LayoutKind.Sequential)]
public struct ChunkVertex(
    Vector3<float> position,
    Vector2<float> uv,
    int textureIndex,
    Vector4 color,
    int overlayTextureIndex,
    Vector4 overlayColor)
{
    public Vector3<float> Position = position;
    public Vector2<float> UV = uv;
    public int TextureIndex = textureIndex;
    public Vector4 Color = color;
    public int OverlayTextureIndex = overlayTextureIndex;
    public Vector4 OverlayColor = overlayColor;
}

public struct ChunkMesh
{
    public ChunkVertex[] Vertices;
    public uint[] Indices;

    public readonly bool IsEmpty => Vertices == null || Indices == null || Vertices.Length == 0 || Indices.Length == 0;
}