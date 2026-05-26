using System.Numerics;

using MinecraftPT.Utils.Math;

namespace MinecraftPT.Game.Entities;

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