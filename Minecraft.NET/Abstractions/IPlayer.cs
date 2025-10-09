using Minecraft.NET.Core.Common;
using Minecraft.NET.Graphics;
using Minecraft.NET.Player;

namespace Minecraft.NET.Abstractions;

public interface IPlayer : IPlayerStateProvider
{
    new Vector3d Position { get; set; }
    Vector3d Velocity { get; set; }
    Camera Camera { get; }
    bool IsOnGround { get; set; }
    GameMode CurrentGameMode { get; set; }
    BoundingBox GetBoundingBox();
}