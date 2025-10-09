using Minecraft.NET.Abstractions;
using Minecraft.NET.Core.Common;
using Silk.NET.Input;

namespace Minecraft.NET.Player.Controllers;

public class SpectatorPlayerController : IPlayerController
{
    public void HandleInput(IInputHandler inputHandler, IPlayer player)
    {
        double cameraSpeed = 50.0;

        if (inputHandler.IsKeyPressed(Key.ControlLeft)) cameraSpeed *= 2;

        var moveDir = Vector3d.Zero;
        if (inputHandler.IsKeyPressed(Key.W)) moveDir += (Vector3d)player.Camera.Front;
        if (inputHandler.IsKeyPressed(Key.S)) moveDir -= (Vector3d)player.Camera.Front;
        if (inputHandler.IsKeyPressed(Key.A)) moveDir -= (Vector3d)player.Camera.Right;
        if (inputHandler.IsKeyPressed(Key.D)) moveDir += (Vector3d)player.Camera.Right;
        if (inputHandler.IsKeyPressed(Key.Space)) moveDir += Vector3d.UnitY;
        if (inputHandler.IsKeyPressed(Key.ShiftLeft)) moveDir -= Vector3d.UnitY;

        player.Velocity = Vector3d.Zero;

        if (moveDir.LengthSquared() > 0)
        {
            player.Velocity = Vector3d.Normalize(moveDir) * cameraSpeed;
        }
    }
}