using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Engine.Core;
using Minecraft.NET.Engine.ECS;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.Entities;

public class CameraSystem(EngineApp engine, IWindow window) : ISystem
{
    public void Update(Registry registry, in Time time)
    {
        foreach (var item in registry.GetView<TransformComponent, PlayerControlledComponent>())
        {
            ref var transform = ref item.Comp1;

            float pitch = transform.Rotation.X;
            float yaw = transform.Rotation.Y;

            float cx = MathF.Sin(yaw) * MathF.Cos(pitch);
            float cy = MathF.Cos(yaw) * MathF.Cos(pitch);
            float cz = MathF.Sin(pitch);

            var forward = new Vector3<float>(cx, cy, cz);
            var up = new Vector3<float>(0, 0, 1);

            var position = transform.Position;
            position.Z += PlayerEyeHeight;

            var view = Matrix4x4<float>.CreateLookAt<float>(
                position, position + forward, up
            );

            float aspect = window.FramebufferSize.X / (float)Math.Max(1, window.FramebufferSize.Y);
            var proj = Matrix4x4<float>.CreatePerspectiveFieldOfView<float>(float.Pi / 2.5f, aspect, 0.1f, 3000f);

            proj.M22 *= -1;

            var viewProj = view * proj;

            Matrix4x4<float>.Invert(viewProj, out var invViewProj);

            var sunDir = new Vector3<float>(0.5f, 0.8f, 1.0f).Normalize<float>();

            engine.Camera = new CameraData
            {
                ViewProjection = viewProj,
                InverseViewProjection = invViewProj,
                Position = new Vector4<float>(position.X, position.Y, position.Z, 1.0f),
                SunDirection = new Vector4<float>(sunDir.X, sunDir.Y, sunDir.Z, 0.0f)
            };

            break;
        }
    }
}