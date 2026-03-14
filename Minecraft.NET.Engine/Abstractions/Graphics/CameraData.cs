using System.Numerics;

using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Engine.Abstractions.Graphics;

public struct CameraData
{
    public Matrix4x4 ViewProjection;
    public Matrix4x4 InverseViewProjection;
    public Vector3Int ChunkPosition;
    private readonly float Pad1;
    public Vector3 LocalPosition;
    private readonly float Pad2;
    public Vector4 SunDirection;
}