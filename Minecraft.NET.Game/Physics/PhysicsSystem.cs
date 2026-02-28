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

    private readonly Vector3<float> _playerHalfExtents = new(0.3f, 0.0f, 0.3f);
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

            bool isCreative = playerCtrlPool.Has(entity.Id) && playerCtrlPool.Get(entity.Id).IsCreativeMode;

            if (!isCreative)
                velocity.Velocity.Y -= Gravity * deltaTime;

            var movement = velocity.Velocity * deltaTime;

            if (movement.LengthSquared() == 0) continue;

            var playerAABB = new BoundingBox<float>(
                new Vector3<float>(transform.Position.X - _playerHalfExtents.X, transform.Position.Y, transform.Position.Z - _playerHalfExtents.Z),
                new Vector3<float>(transform.Position.X + _playerHalfExtents.X, transform.Position.Y + _playerHeight, transform.Position.Z + _playerHalfExtents.Z)
            );

            velocity.IsOnGround = false;

            if (movement.Y != 0)
            {
                playerAABB = playerAABB.Offset(new Vector3<float>(0, movement.Y, 0));
                if (CheckCollision(playerAABB))
                {
                    playerAABB = playerAABB.Offset(new Vector3<float>(0, -movement.Y, 0));

                    if (movement.Y < 0) velocity.IsOnGround = true;

                    movement.Y = 0;
                    velocity.Velocity.Y = 0;
                }
                transform.Position.Y += movement.Y;
            }

            if (movement.X != 0)
            {
                playerAABB = playerAABB.Offset(new Vector3<float>(movement.X, 0, 0));
                if (CheckCollision(playerAABB))
                {
                    playerAABB = playerAABB.Offset(new Vector3<float>(-movement.X, 0, 0));
                    movement.X = 0;
                    velocity.Velocity.X = 0;
                }
                transform.Position.X += movement.X;
            }
            if (movement.Z != 0)
            {
                playerAABB = playerAABB.Offset(new Vector3<float>(0, 0, movement.Z));
                if (CheckCollision(playerAABB))
                {
                    movement.Z = 0;
                    velocity.Velocity.Z = 0;
                }
                transform.Position.Z += movement.Z;
            }
        };
    }

    private bool CheckCollision(BoundingBox<float> box)
    {
        int minX = (int)MathF.Floor(box.Min.X);
        int minY = (int)MathF.Floor(box.Min.Y);
        int minZ = (int)MathF.Floor(box.Min.Z);

        int maxX = (int)MathF.Floor(box.Max.X);
        int maxY = (int)MathF.Floor(box.Max.Y);
        int maxZ = (int)MathF.Floor(box.Max.Z);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    var blockPos = new Vector3<int>(x, y, z);
                    var blockId = world.GetBlock(blockPos);

                    if (blockId != BlockId.Air)
                    {
                        var blockBox = new BoundingBox<float>(
                            new Vector3<float>(x, y, z),
                            new Vector3<float>(x + 1.0f, y + 1.0f, z + 1.0f)
                        );

                        if (box.Intersects(blockBox))
                            return true;
                    }
                }
            }
        }
        return false;
    }
}