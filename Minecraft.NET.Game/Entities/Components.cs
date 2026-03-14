using System.Numerics;

namespace Minecraft.NET.Game.Entities;

public struct TransformComponent
{
    public Vector3 Position;
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