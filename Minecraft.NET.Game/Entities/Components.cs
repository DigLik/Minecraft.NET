using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.Entities;

public struct TransformComponent
{
    public Vector3<float> Position;
    public Vector3<float> Rotation;
}

public struct VelocityComponent
{
    public Vector3<float> Velocity;
    public bool IsOnGround;
}

public struct PlayerControlledComponent
{
    public bool IsCreativeMode;
    public bool IsSpectatorMode;
}