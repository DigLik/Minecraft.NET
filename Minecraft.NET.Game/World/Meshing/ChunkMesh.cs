using System.Runtime.InteropServices;

using Minecraft.NET.Utils.Collections;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Meshing;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ChunkVertex(
    Vector3<float> position,
    int textureIndex,
    Vector2<float> uv,
    int overlayTextureIndex,
    Vector4<float> color,
    Vector4<float> overlayColor)
{
    public Vector3<float> Position = position;
    public int TextureIndex = textureIndex;
    public Vector2<float> UV = uv;
    public int OverlayTextureIndex = overlayTextureIndex;
    public Vector4<float> Color = color;
    public Vector4<float> OverlayColor = overlayColor;
}

public struct ChunkMesh
{
    public NativeList<ChunkVertex> Vertices;
    public NativeList<uint> Indices;

    public readonly bool IsEmpty => !Vertices.IsCreated || !Indices.IsCreated || Vertices.Count == 0 || Indices.Count == 0;
}