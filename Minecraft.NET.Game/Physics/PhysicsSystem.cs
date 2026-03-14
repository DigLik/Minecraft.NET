using System.Numerics;

using Minecraft.NET.Engine.Core;
using Minecraft.NET.Engine.ECS;
using Minecraft.NET.Game.Entities;
using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Utils.Math;

using GameWorld = Minecraft.NET.Game.World.Environment.World;

namespace Minecraft.NET.Game.Physics;

public class PhysicsSystem(GameWorld world) : ISystem
{
    private const float Gravity = 28.0f;

    private readonly Vector3 _playerHalfExtents = new(0.3f, 0.3f, 0.0f);
    private readonly float _playerHeight = 1.8f;

    public void Update(Registry registry, in Time time)
    {
        float deltaTime = (float)time.DeltaTime;
        var playerCtrlPool = registry.GetPool<PlayerControlledComponent>();

        foreach (var item in registry.GetView<TransformComponent, VelocityComponent>())
        {
            Entity entity = item.Entity;
            ref var transform = ref item.Comp1;
            ref var velocity = ref item.Comp2;

            bool isSpectator = playerCtrlPool.Has(entity.Id) && playerCtrlPool.Get(entity.Id).IsSpectatorMode;

            if (isSpectator)
            {
                transform.LocalPosition.X += velocity.Velocity.X * deltaTime;
                transform.LocalPosition.Y += velocity.Velocity.Y * deltaTime;
                transform.LocalPosition.Z += velocity.Velocity.Z * deltaTime;
                NormalizePosition(ref transform);
                continue;
            }

            bool isCreative = playerCtrlPool.Has(entity.Id) && playerCtrlPool.Get(entity.Id).IsCreativeMode;

            if (!isCreative)
                velocity.Velocity.Z -= Gravity * deltaTime;

            var movement = velocity.Velocity * deltaTime;

            if (movement.LengthSquared() == 0) continue;

            var playerAABB = new BoundingBox(
                new Vector3(transform.LocalPosition.X - _playerHalfExtents.X, transform.LocalPosition.Y - _playerHalfExtents.Y, transform.LocalPosition.Z),
                new Vector3(transform.LocalPosition.X + _playerHalfExtents.X, transform.LocalPosition.Y + _playerHalfExtents.Y, transform.LocalPosition.Z + _playerHeight)
            );

            velocity.IsOnGround = false;

            if (movement.Z != 0)
            {
                playerAABB = playerAABB.Offset(new Vector3(0, 0, movement.Z));
                if (CheckCollision(transform.ChunkPosition, playerAABB))
                {
                    playerAABB = playerAABB.Offset(new Vector3(0, 0, -movement.Z));
                    if (movement.Z < 0) velocity.IsOnGround = true;

                    movement.Z = 0;
                    velocity.Velocity.Z = 0;
                }
                transform.LocalPosition.Z += movement.Z;
            }

            if (movement.X != 0)
            {
                playerAABB = playerAABB.Offset(new Vector3(movement.X, 0, 0));
                if (CheckCollision(transform.ChunkPosition, playerAABB))
                {
                    playerAABB = playerAABB.Offset(new Vector3(-movement.X, 0, 0));
                    movement.X = 0;
                    velocity.Velocity.X = 0;
                }
                transform.LocalPosition.X += movement.X;
            }

            if (movement.Y != 0)
            {
                playerAABB = playerAABB.Offset(new Vector3(0, movement.Y, 0));
                if (CheckCollision(transform.ChunkPosition, playerAABB))
                {
                    movement.Y = 0;
                    velocity.Velocity.Y = 0;
                }
                transform.LocalPosition.Y += movement.Y;
            }

            NormalizePosition(ref transform);
        }
    }

    private static void NormalizePosition(ref TransformComponent transform)
    {
        int cx = (int)MathF.Floor(transform.LocalPosition.X / ChunkSize);
        int cy = (int)MathF.Floor(transform.LocalPosition.Y / ChunkSize);
        int cz = (int)MathF.Floor(transform.LocalPosition.Z / ChunkSize);

        if (cx != 0)
        {
            transform.ChunkPosition.X += cx;
            transform.LocalPosition.X -= cx * ChunkSize;
        }
        if (cy != 0)
        {
            transform.ChunkPosition.Y += cy;
            transform.LocalPosition.Y -= cy * ChunkSize;
        }
        if (cz != 0)
        {
            transform.ChunkPosition.Z += cz;
            transform.LocalPosition.Z -= cz * ChunkSize;
        }
    }

    private bool CheckCollision(Vector3Int chunkPos, BoundingBox localBox)
    {
        int minX = (int)MathF.Floor(localBox.Min.X);
        int minY = (int)MathF.Floor(localBox.Min.Y);
        int minZ = (int)MathF.Floor(localBox.Min.Z);

        int maxX = (int)MathF.Floor(localBox.Max.X);
        int maxY = (int)MathF.Floor(localBox.Max.Y);
        int maxZ = (int)MathF.Floor(localBox.Max.Z);

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    var globalBlockPos = chunkPos * ChunkSize + new Vector3Int(x, y, z);
                    var blockId = world.GetBlock(globalBlockPos);

                    if (blockId != BlockId.Air)
                    {
                        var blockLocalBox = new BoundingBox(
                            new Vector3(x, y, z),
                            new Vector3(x + 1.0f, y + 1.0f, z + 1.0f)
                        );

                        if (localBox.Intersects(blockLocalBox)) return true;
                    }
                }
        return false;
    }
}