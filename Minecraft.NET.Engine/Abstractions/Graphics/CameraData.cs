using System.Numerics;

namespace Minecraft.NET.Engine.Abstractions.Graphics;

public struct CameraData
{
    public Matrix4x4 ViewProjection;
    public Matrix4x4 InverseViewProjection;
    public Vector4 Position;
    public Vector4 SunDirection;
}