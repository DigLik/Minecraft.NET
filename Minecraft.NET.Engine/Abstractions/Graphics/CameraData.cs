using System.Numerics;

using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Engine.Abstractions.Graphics;

public struct CameraData
{
    public Matrix4x4 ViewProjection;
    public Matrix4x4 InverseViewProjection;

    // 16 bytes
    public Vector3Int ChunkPosition;
    public uint FrameCount;

    // 16 bytes
    public Vector3 LocalPosition;
    public uint SamplesPerPixel;

    // 16 bytes
    public Vector4 SunDirection;

    // 16 bytes
    public uint Seed;
    public uint Pad1;
    public uint Pad2;
    public uint Pad3;
}