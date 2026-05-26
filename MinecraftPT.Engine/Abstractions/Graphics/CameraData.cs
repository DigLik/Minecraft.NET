using System.Numerics;

using MinecraftPT.Utils.Math;

namespace MinecraftPT.Engine.Abstractions.Graphics;

public struct CameraData
{
    public Matrix4x4 ViewProjection;
    public Matrix4x4 InverseViewProjection;
    public Matrix4x4 PrevViewProjection;

    // 16 bytes
    public Vector3Int ChunkPosition;
    public uint FrameCount;

    // 16 bytes
    public Vector3 LocalPosition;
    public uint SamplesPerPixel;

    // 16 bytes
    public Vector4 SunDirection;

    // 16 bytes
    public Vector3 CameraUp;
    public uint Seed;

    // 16 bytes
    public Vector3 CameraRight;
    public float JitterX;

    // 16 bytes
    public Vector3 CameraFwd;
    public float JitterY;
}