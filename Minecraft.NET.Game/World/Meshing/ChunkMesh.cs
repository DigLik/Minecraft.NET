using System.Numerics;
using System.Runtime.InteropServices;

using Minecraft.NET.Utils.Collections;

namespace Minecraft.NET.Game.World.Meshing;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ChunkVertex(
    Vector4 position,
    int textureIndex,
    Vector2 uv,
    int overlayTextureIndex,
    Vector4 color,
    Vector4 overlayColor)
{
    public Vector4 Position = position;
    public int TextureIndex = textureIndex;
    public Vector2 UV = uv;
    public int OverlayTextureIndex = overlayTextureIndex;
    public Vector4 Color = color;
    public Vector4 OverlayColor = overlayColor;
}

public struct ChunkMesh
{
    public NativeList<ChunkVertex> Vertices;
    public NativeList<uint> Indices;

    public readonly bool IsEmpty => !Vertices.IsCreated || !Indices.IsCreated || Vertices.Count == 0 || Indices.Count == 0;
}