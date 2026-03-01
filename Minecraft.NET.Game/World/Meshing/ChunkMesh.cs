using System.Runtime.InteropServices;

using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Meshing;

[StructLayout(LayoutKind.Sequential)]
public struct ChunkVertex(Vector3<float> position, Vector2<float> uv, int textureIndex, float shade)
{
    public Vector3<float> Position = position;
    public Vector2<float> UV = uv;
    public int TextureIndex = textureIndex;
    public float Shade = shade;
}

public class ChunkMesh
{
    public ChunkVertex[] Vertices = [];
    public uint[] Indices = [];
    public bool IsEmpty => Vertices.Length == 0;
}