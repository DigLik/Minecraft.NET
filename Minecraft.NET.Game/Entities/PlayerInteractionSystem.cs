using System.Numerics;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Core;
using Minecraft.NET.Engine.ECS;
using Minecraft.NET.Engine.Input;
using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Utils.Math;

using GameWorld = Minecraft.NET.Game.World.Environment.World;

namespace Minecraft.NET.Game.Entities;

public class PlayerInteractionSystem(IInputManager inputManager, GameWorld world) : ISystem
{
    private double _breakCooldown = 0;
    private double _placeCooldown = 0;

    private const double CooldownTime = 0.2;
    private const float InteractionDistance = 5.0f;

    public void Update(Registry registry, in Time time)
    {
        double deltaTime = time.DeltaTime;

        if (_breakCooldown > 0) _breakCooldown -= deltaTime;
        if (_placeCooldown > 0) _placeCooldown -= deltaTime;

        if (!inputManager.IsMouseCaptured) return;

        bool tryBreak = inputManager.IsMouseButton(MouseButton.Left) && _breakCooldown <= 0;
        bool tryPlace = inputManager.IsMouseButton(MouseButton.Right) && _placeCooldown <= 0;

        if (!tryBreak && !tryPlace) return;

        foreach (var item in registry.GetView<TransformComponent, PlayerControlledComponent>())
        {
            ref var transform = ref item.Comp1;

            float pitch = transform.Rotation.X;
            float yaw = transform.Rotation.Y;

            float x = MathF.Sin(yaw) * MathF.Cos(pitch);
            float y = MathF.Cos(yaw) * MathF.Cos(pitch);
            float z = MathF.Sin(pitch);

            var direction = Vector3.Normalize(new(x, y, z));

            var localOrigin = transform.LocalPosition;
            localOrigin.Z += PlayerEyeHeight;

            var hit = Raycast(transform.ChunkPosition, localOrigin, direction, InteractionDistance);

            if (hit.HasValue)
            {
                if (tryBreak)
                {
                    world.SetBlock(hit.Value.HitPosition, BlockId.Air);
                    _breakCooldown = CooldownTime;
                }
                else if (tryPlace)
                {
                    world.SetBlock(hit.Value.PlacePosition, BlockId.Stone);
                    _placeCooldown = CooldownTime;
                }
            }
        }
    }

    private RaycastResult? Raycast(Vector3Int chunkPos, Vector3 localOrigin, Vector3 direction, float maxDistance)
    {
        Vector3 dir = Vector3.Normalize(direction);

        int x = (int)MathF.Floor(localOrigin.X);
        int y = (int)MathF.Floor(localOrigin.Y);
        int z = (int)MathF.Floor(localOrigin.Z);

        int stepX = Math.Sign(dir.X);
        int stepY = Math.Sign(dir.Y);
        int stepZ = Math.Sign(dir.Z);

        float tDeltaX = stepX != 0 ? MathF.Abs(1 / dir.X) : float.MaxValue;
        float tDeltaY = stepY != 0 ? MathF.Abs(1 / dir.Y) : float.MaxValue;
        float tDeltaZ = stepZ != 0 ? MathF.Abs(1 / dir.Z) : float.MaxValue;

        float tMaxX = (stepX > 0) ? (MathF.Floor(localOrigin.X) + 1 - localOrigin.X) * tDeltaX : (localOrigin.X - MathF.Floor(localOrigin.X)) * tDeltaX;
        float tMaxY = (stepY > 0) ? (MathF.Floor(localOrigin.Y) + 1 - localOrigin.Y) * tDeltaY : (localOrigin.Y - MathF.Floor(localOrigin.Y)) * tDeltaY;
        float tMaxZ = (stepZ > 0) ? (MathF.Floor(localOrigin.Z) + 1 - localOrigin.Z) * tDeltaZ : (localOrigin.Z - MathF.Floor(localOrigin.Z)) * tDeltaZ;

        int lastX = x, lastY = y, lastZ = z;
        int maxSteps = (int)(maxDistance * 3);

        for (int i = 0; i < maxSteps; i++)
        {
            var localBlockPos = new Vector3Int(x, y, z);
            var globalBlockPos = chunkPos * ChunkSize + localBlockPos;

            if (globalBlockPos.Z is < 0 or >= WorldHeightInBlocks) break;

            var blockId = world.GetBlock(globalBlockPos);

            if (blockId != BlockId.Air)
                return new RaycastResult(globalBlockPos, chunkPos * ChunkSize + new Vector3Int(lastX, lastY, lastZ));

            lastX = x;
            lastY = y;
            lastZ = z;

            if (tMaxX < tMaxY)
            {
                if (tMaxX < tMaxZ)
                {
                    if (tMaxX > maxDistance) break;
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    if (tMaxZ > maxDistance) break;
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }
            else
            {
                if (tMaxY < tMaxZ)
                {
                    if (tMaxY > maxDistance) break;
                    y += stepY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    if (tMaxZ > maxDistance) break;
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }
        }

        return null;
    }

    public readonly record struct RaycastResult(Vector3Int HitPosition, Vector3Int PlacePosition);
}