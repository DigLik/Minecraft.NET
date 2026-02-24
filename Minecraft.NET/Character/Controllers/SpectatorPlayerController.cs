using Minecraft.NET.Core.Common;
using Minecraft.NET.Engine;

namespace Minecraft.NET.Character.Controllers;

public class SpectatorPlayerController : IPlayerController
{
    public void HandleInput(InputManager inputHandler, Player player)
    {
        double cameraSpeed = 200.0;

        if (inputHandler.IsKeyPressed(Keys.ControlLeft)) cameraSpeed *= 2;

        var moveDir = Vector3d.Zero;
        if (inputHandler.IsKeyPressed(Keys.W)) moveDir += (Vector3d)player.Camera.Front;
        if (inputHandler.IsKeyPressed(Keys.S)) moveDir -= (Vector3d)player.Camera.Front;
        if (inputHandler.IsKeyPressed(Keys.A)) moveDir -= (Vector3d)player.Camera.Right;
        if (inputHandler.IsKeyPressed(Keys.D)) moveDir += (Vector3d)player.Camera.Right;
        if (inputHandler.IsKeyPressed(Keys.Space)) moveDir += Vector3d.UnitY;
        if (inputHandler.IsKeyPressed(Keys.ShiftLeft)) moveDir -= Vector3d.UnitY;

        player.Velocity = Vector3d.Zero;

        if (moveDir.LengthSquared() > 0)
        {
            player.Velocity = Vector3d.Normalize(moveDir) * cameraSpeed;
        }
    }
}