using System.Numerics;

using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.Entities;

public struct TransformComponent
{
    public Vector3Int ChunkPosition;
    public Vector3 LocalPosition;
    public Vector3 Rotation;
}

public struct VelocityComponent
{
    public Vector3 Velocity;
    public bool IsOnGround;
}

public struct PlayerControlledComponent
{
    public bool IsCreativeMode;
    public bool IsSpectatorMode;
}