using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Engine.Abstractions.Graphics;

public struct CameraData
{
    public Matrix4x4<float> ViewProjection;
    public Matrix4x4<float> InverseViewProjection;
    public Vector4<float> Position;
    public Vector4<float> SunDirection;
}