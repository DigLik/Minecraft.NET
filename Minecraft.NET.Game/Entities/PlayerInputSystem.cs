using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Core;
using Minecraft.NET.Engine.ECS;
using Minecraft.NET.Engine.Input;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.Entities;

public class PlayerInputSystem(IInputManager inputManager) : ISystem
{
    private const float WalkSpeed = 10.0f;
    private const float SprintSpeed = 25.0f;
    private const float SpectatorSpeed = 150.0f;
    private const float JumpForce = 9.0f;
    private const float MouseSensitivity = 0.002f;

    private Vector2<float> _lastMousePos;

    public void Update(Registry registry, in Time time)
    {
        if (inputManager.IsKeyDown(Key.Tab))
            inputManager.ToggleMouseCapture();

        var playerCtrlPool = registry.GetPool<PlayerControlledComponent>();

        foreach (var item in registry.GetView<VelocityComponent, TransformComponent>())
        {
            if (!playerCtrlPool.Has(item.Entity.Id)) continue;

            ref var velocity = ref item.Comp1;
            ref var transform = ref item.Comp2;
            ref var playerCtrl = ref playerCtrlPool.Get(item.Entity.Id);

            if (inputManager.IsKeyDown(Key.F1))
                playerCtrl.IsSpectatorMode = !playerCtrl.IsSpectatorMode;

            if (inputManager.IsMouseCaptured)
            {
                var mousePos = inputManager.MousePosition;
                if (_lastMousePos == default) _lastMousePos = mousePos;

                float deltaX = mousePos.X - _lastMousePos.X;
                float deltaY = mousePos.Y - _lastMousePos.Y;

                transform.Rotation.Y += deltaX * MouseSensitivity;
                transform.Rotation.X -= deltaY * MouseSensitivity;
                transform.Rotation.X = Math.Clamp(transform.Rotation.X, -MathF.PI / 2.0f + 0.01f, MathF.PI / 2.0f - 0.01f);

                _lastMousePos = mousePos;
            }
            else
            {
                _lastMousePos = default;
            }

            float yaw = transform.Rotation.Y;
            float pitch = transform.Rotation.X;

            Vector3<float> forward, right;

            if (playerCtrl.IsSpectatorMode)
            {
                forward = new Vector3<float>(
                    MathF.Sin(yaw) * MathF.Cos(pitch),
                    MathF.Cos(yaw) * MathF.Cos(pitch),
                    MathF.Sin(pitch)
                ).Normalize<float>();
            }
            else
            {
                forward = new Vector3<float>(MathF.Sin(yaw), MathF.Cos(yaw), 0).Normalize<float>();
            }

            right = new Vector3<float>(MathF.Cos(yaw), -MathF.Sin(yaw), 0).Normalize<float>();

            Vector3<float> moveDir = Vector3<float>.Zero;

            if (inputManager.IsKey(Key.W)) moveDir += forward;
            if (inputManager.IsKey(Key.S)) moveDir -= forward;
            if (inputManager.IsKey(Key.D)) moveDir += right;
            if (inputManager.IsKey(Key.A)) moveDir -= right;

            float currentSpeed = inputManager.IsKey(Key.ShiftLeft) ? SprintSpeed : WalkSpeed;
            if (playerCtrl.IsSpectatorMode) currentSpeed = SpectatorSpeed;

            if (moveDir.LengthSquared() > 0)
            {
                moveDir = moveDir.Normalize<float>();
                velocity.Velocity.X = moveDir.X * currentSpeed;
                velocity.Velocity.Y = moveDir.Y * currentSpeed;

                if (playerCtrl.IsSpectatorMode)
                    velocity.Velocity.Z = moveDir.Z * currentSpeed;
            }
            else
            {
                velocity.Velocity.X = 0;
                velocity.Velocity.Y = 0;

                if (playerCtrl.IsSpectatorMode)
                    velocity.Velocity.Z = 0;
            }

            if (playerCtrl.IsSpectatorMode)
            {
                if (inputManager.IsKey(Key.Space)) velocity.Velocity.Z += currentSpeed;
                if (inputManager.IsKey(Key.ShiftLeft)) velocity.Velocity.Z -= currentSpeed;
            }
            else
            {
                if (velocity.IsOnGround && inputManager.IsKey(Key.Space))
                {
                    velocity.Velocity.Z = JumpForce;
                    velocity.IsOnGround = false;
                }
            }
        }
        ;
    }
}