using Minecraft.NET.Abstractions;
using Minecraft.NET.Core.Common;
using Silk.NET.Input;

namespace Minecraft.NET.Player.Controllers;

public class CreativePlayerController : IPlayerController
{
    public void HandleInput(IInputHandler inputHandler, IPlayer player)
    {
        var wishDir = Vector3d.Zero;
        var forward = Vector3d.Normalize(new Vector3d(player.Camera.Front.X, 0, player.Camera.Front.Z));
        var right = Vector3d.Normalize(new Vector3d(player.Camera.Right.X, 0, player.Camera.Right.Z));

        if (inputHandler.IsKeyPressed(Key.W)) wishDir += forward;
        if (inputHandler.IsKeyPressed(Key.S)) wishDir -= forward;
        if (inputHandler.IsKeyPressed(Key.A)) wishDir -= right;
        if (inputHandler.IsKeyPressed(Key.D)) wishDir += right;

        if (wishDir.LengthSquared() > 0)
            wishDir = Vector3d.Normalize(wishDir);

        double currentMaxSpeed = MaxSpeed;
        if (inputHandler.IsKeyPressed(Key.ControlLeft))
        {
            currentMaxSpeed *= SprintSpeedMultiplier;
        }

        var currentYVelocity = player.Velocity.Y;
        var horizontalVelocity = wishDir * currentMaxSpeed;

        player.Velocity = new Vector3d(horizontalVelocity.X, currentYVelocity, horizontalVelocity.Z);

        if (inputHandler.IsKeyPressed(Key.Space) && player.IsOnGround)
            player.Velocity = player.Velocity with { Y = JumpForce };
    }
}