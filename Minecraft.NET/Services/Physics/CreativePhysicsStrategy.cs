using Minecraft.NET.Character;
using Minecraft.NET.Core.Environment;

namespace Minecraft.NET.Services.Physics;

public class CreativePhysicsStrategy : IPhysicsStrategy
{
    public void Update(Player player, World world, float deltaTime)
    {
        player.Velocity -= new Vector3<float>(0, Gravity * deltaTime, 0);

        var velocity = player.Velocity;
        (player.Position, player.IsOnGround) = MoveAndSlide(player, world, player.Position, ref velocity, (float)deltaTime);
        player.Velocity = velocity;

        if (player.IsOnGround && player.Velocity.Y <= 0)
            player.Velocity = player.Velocity with { Y = 0 };
    }

    private static (Vector3<float> newPosition, bool onGround) MoveAndSlide(
        Player player, World world, Vector3<float> position, ref Vector3<float> velocity, float dt)
    {
        var initialVelocity = velocity;
        var totalDisplacement = velocity * dt;
        float distance = totalDisplacement.Length<float>();
        bool isOnGround = false;

        if (distance < 1e-8)
        {
            var tempPos = player.Position;
            tempPos.Y -= 0.01f;
            var playerBox = Player.GetBoundingBoxForPosition(tempPos);
            return (position, CheckCollision(world, playerBox));
        }

        const double stepSize = PlayerWidth * 0.4;
        int numSteps = (int)Math.Ceiling(distance / stepSize);
        var stepDisplacement = totalDisplacement / numSteps;

        for (int i = 0; i < numSteps; i++)
        {
            position += stepDisplacement;

            const int maxResolutionAttempts = 5;
            for (int j = 0; j < maxResolutionAttempts; j++)
            {
                var playerBox = Player.GetBoundingBoxForPosition(position);
                var (collided, mtv) = FindCollisionMTV(world, playerBox);

                if (!collided) break;

                position += mtv;
                var normal = mtv.Normalize<float>();

                if (normal.Y > 0.707 && initialVelocity.Y <= 0) isOnGround = true;

                var dotProduct = velocity.X * normal.X + velocity.Y * normal.Y + velocity.Z * normal.Z;

                if (dotProduct < 0) velocity -= normal * dotProduct;
            }
        }
        return (position, isOnGround);
    }

    private static (bool, Vector3<float>) FindCollisionMTV(World world, BoundingBox<float> playerBox)
    {
        Vector3<float> overallMtv = Vector3<float>.Zero;

        var min = new Vector3<float>(MathF.Floor(playerBox.Min.X - 1), MathF.Floor(playerBox.Min.Y - 1), MathF.Floor(playerBox.Min.Z - 1));
        var max = new Vector3<float>(MathF.Floor(playerBox.Max.X + 1), MathF.Floor(playerBox.Max.Y + 1), MathF.Floor(playerBox.Max.Z + 1));

        for (var x = min.X; x <= max.X; x++)
            for (var y = min.Y; y <= max.Y; y++)
                for (var z = min.Z; z <= max.Z; z++)
                {
                    var blockPos = new Vector3<float>(x, y, z);
                    if (world.GetBlock(blockPos) == BlockId.Air) continue;

                    var blockBox = new BoundingBox<float>(
                        new Vector3<float>(x,     y,     z),
                        new Vector3<float>(x + 1, y + 1, z + 1)
                    );

                    if (!Intersects(playerBox, blockBox)) continue;

                    var mtv = CalculateMTV(playerBox, blockBox);

                    if (mtv.LengthSquared() > overallMtv.LengthSquared())
                        overallMtv = mtv;
                }

        return (overallMtv.LengthSquared() > 0, overallMtv);
    }

    private static Vector3<float> CalculateMTV(BoundingBox<float> playerBox, BoundingBox<float> blockBox)
    {
        var overlapX = (playerBox.Max.X - playerBox.Min.X) + (blockBox.Max.X - blockBox.Min.X) -
                       (Math.Max(playerBox.Max.X, blockBox.Max.X) - Math.Min(playerBox.Min.X, blockBox.Min.X));
        var overlapY = (playerBox.Max.Y - playerBox.Min.Y) + (blockBox.Max.Y - blockBox.Min.Y) -
                       (Math.Max(playerBox.Max.Y, blockBox.Max.Y) - Math.Min(playerBox.Min.Y, blockBox.Min.Y));
        var overlapZ = (playerBox.Max.Z - playerBox.Min.Z) + (blockBox.Max.Z - blockBox.Min.Z) -
                       (Math.Max(playerBox.Max.Z, blockBox.Max.Z) - Math.Min(playerBox.Min.Z, blockBox.Min.Z));

        if (overlapX < overlapY && overlapX < overlapZ)
        {
            var direction = playerBox.Min.X + (playerBox.Max.X - playerBox.Min.X) / 2 < blockBox.Min.X + (blockBox.Max.X - blockBox.Min.X) / 2 ? -1 : 1;
            return new Vector3<float>(overlapX * direction, 0, 0);
        }
        if (overlapY < overlapZ)
        {
            var direction = playerBox.Min.Y + (playerBox.Max.Y - playerBox.Min.Y) / 2 < blockBox.Min.Y + (blockBox.Max.Y - blockBox.Min.Y) / 2 ? -1 : 1;
            return new Vector3<float>(0, overlapY * direction, 0);
        }
        else
        {
            var direction = playerBox.Min.Z + (playerBox.Max.Z - playerBox.Min.Z) / 2 < blockBox.Min.Z + (blockBox.Max.Z - blockBox.Min.Z) / 2 ? -1 : 1;
            return new Vector3<float>(0, 0, overlapZ * direction);
        }
    }

    private static bool CheckCollision(World world, BoundingBox<float> box)
    {
        var min = new Vector3<float>(MathF.Floor(box.Min.X), MathF.Floor(box.Min.Y), MathF.Floor(box.Min.Z));
        var max = new Vector3<float>(MathF.Floor(box.Max.X), MathF.Floor(box.Max.Y), MathF.Floor(box.Max.Z));

        for (var x = min.X; x <= max.X; x++)
            for (var y = min.Y; y <= max.Y; y++)
                for (var z = min.Z; z <= max.Z; z++)
                {
                    var blockPos = new Vector3<float>(x, y, z);
                    if (world.GetBlock(blockPos) != BlockId.Air)
                    {
                        var blockBox = new BoundingBox<float>(
                            new Vector3<float>((float)x, (float)y, (float)z),
                            new Vector3<float>((float)x + 1, (float)y + 1, (float)z + 1)
                        );
                        if (Intersects(box, blockBox))
                            return true;
                    }
                }
        return false;
    }

    private static bool Intersects(BoundingBox<float> a, BoundingBox<float> b)
        => a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
           a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
           a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;
}