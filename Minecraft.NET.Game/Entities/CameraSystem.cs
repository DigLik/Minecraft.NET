using System.Numerics;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Engine.Core;
using Minecraft.NET.Engine.ECS;

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

            var forward = new Vector3(cx, cy, cz);
            var up = new Vector3(0, 0, 1);

            var chunkPos = transform.ChunkPosition;
            var localPos = transform.LocalPosition;
            localPos.Z += PlayerEyeHeight;

            var view = Matrix4x4.CreateLookAt(
                localPos, localPos + forward, up
            );

            float aspect = window.FramebufferSize.X / (float)Math.Max(1, window.FramebufferSize.Y);
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(float.Pi / 2.5f, aspect, 0.1f, 3000f);

            proj.M22 *= -1;

            var viewProj = view * proj;

            Matrix4x4.Invert(viewProj, out var invViewProj);

            var sunDir = Vector3.Normalize(new(0.5f, 0.8f, 1.0f));

            var oldCamera = engine.Camera;
            engine.Camera = new CameraData
            {
                ViewProjection = viewProj,
                InverseViewProjection = invViewProj,
                ChunkPosition = chunkPos,
                LocalPosition = localPos,
                SunDirection = new Vector4(sunDir.X, sunDir.Y, sunDir.Z, 0.0f),
                SamplesPerPixel = oldCamera.SamplesPerPixel,
                FrameCount = oldCamera.FrameCount,
                Seed = oldCamera.Seed
            };

            break;
        }
    }
}