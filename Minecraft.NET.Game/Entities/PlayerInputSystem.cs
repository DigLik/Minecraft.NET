using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Core;
using Minecraft.NET.Engine.ECS;
using Minecraft.NET.Engine.Input;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.Entities;

public class PlayerInputSystem(IInputManager inputManager) : ISystem
{
    private const float WalkSpeed = 5.0f;
    private const float JumpForce = 9.0f;
    private const float MouseSensitivity = 0.002f;

    private Vector2<float> _lastMousePos;

    public void Update(Registry registry, in Time time)
    {
        if (inputManager.IsKeyPressed(Key.Tab))
            inputManager.ToggleMouseCapture();

        foreach (var item in registry.GetView<VelocityComponent, TransformComponent>())
        {
            ref var velocity = ref item.Comp1;
            ref var transform = ref item.Comp2;

            if (inputManager.IsMouseCaptured)
            {
                var mousePos = inputManager.MousePosition;

                if (_lastMousePos == default) _lastMousePos = mousePos;

                float deltaX = mousePos.X - _lastMousePos.X;
                float deltaY = mousePos.Y - _lastMousePos.Y;

                transform.Rotation.Y += deltaX * MouseSensitivity;
                transform.Rotation.X += deltaY * MouseSensitivity;

                transform.Rotation.X = Math.Clamp(transform.Rotation.X, -MathF.PI / 2.0f + 0.01f, MathF.PI / 2.0f - 0.01f);

                _lastMousePos = mousePos;
            }
            else
            {
                _lastMousePos = default;
            }

            float yaw = transform.Rotation.Y;
            var forward = new Vector3<float>(MathF.Sin(yaw), 0, MathF.Cos(yaw)).Normalize<float>();
            var right = new Vector3<float>(MathF.Cos(yaw), 0, -MathF.Sin(yaw)).Normalize<float>();

            Vector3<float> moveDir = Vector3<float>.Zero;

            if (inputManager.IsKeyPressed(Key.W)) moveDir += forward;
            if (inputManager.IsKeyPressed(Key.S)) moveDir -= forward;
            if (inputManager.IsKeyPressed(Key.D)) moveDir -= right;
            if (inputManager.IsKeyPressed(Key.A)) moveDir += right;

            if (moveDir.LengthSquared() > 0)
            {
                moveDir = moveDir.Normalize<float>();
                velocity.Velocity.X = moveDir.X * WalkSpeed;
                velocity.Velocity.Z = moveDir.Z * WalkSpeed;
            }
            else
            {
                velocity.Velocity.X = 0;
                velocity.Velocity.Z = 0;
            }

            if (velocity.IsOnGround && inputManager.IsKeyPressed(Key.Space))
            {
                velocity.Velocity.Y = JumpForce;
                velocity.IsOnGround = false;
            }
        };
    }
}