using System.Numerics;

using Minecraft.NET.Engine.Abstractions;
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

            var view = Matrix4x4.CreateLookAt(
                new Vector3(position.X, position.Y, position.Z),
                new Vector3(position.X + forward.X, position.Y + forward.Y, position.Z + forward.Z),
                new Vector3(up.X, up.Y, up.Z)
            );

            float aspect = window.FramebufferSize.X / (float)Math.Max(1, window.FramebufferSize.Y);
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2.5f, aspect, 0.1f, 3000f);

            proj.M22 *= -1;

            engine.CameraMatrix = new Matrix4x4(
                view.M11, view.M12, view.M13, view.M14,
                view.M21, view.M22, view.M23, view.M24,
                view.M31, view.M32, view.M33, view.M34,
                view.M41, view.M42, view.M43, view.M44
            ) * new Matrix4x4(
                proj.M11, proj.M12, proj.M13, proj.M14,
                proj.M21, proj.M22, proj.M23, proj.M24,
                proj.M31, proj.M32, proj.M33, proj.M34,
                proj.M41, proj.M42, proj.M43, proj.M44
            );

            break;
        }
    }
}