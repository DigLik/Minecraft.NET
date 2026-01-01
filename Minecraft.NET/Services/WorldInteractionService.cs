using Minecraft.NET.Character;
using Minecraft.NET.Core.Common;
using Minecraft.NET.Core.Environment;
using Minecraft.NET.Graphics;

namespace Minecraft.NET.Services;

public readonly record struct RaycastResult(Vector3d HitPosition, Vector3d PlacePosition);

public class WorldInteractionService(Player player, World world)
{
    public void BreakBlock()
    {
        var result = Raycast(player.Position, player.Camera.Front, 6.0);
        if (result.HasValue)
            world.SetBlock(result.Value.HitPosition, BlockId.Air);
    }

    public void PlaceBlock()
    {
        var result = Raycast(player.Position, player.Camera.Front, 6.0);
        if (result.HasValue)
        {
            var placePosition = result.Value.PlacePosition;
            var newBlockBox = new BoundingBox(
                (Vector3)placePosition,
                (Vector3)placePosition + Vector3.One
            );

            var playerBox = player.GetBoundingBox();

            const float Epsilon = 0.0001f;
            var collisionCheckBox = new BoundingBox(
                newBlockBox.Min + new Vector3(Epsilon),
                newBlockBox.Max - new Vector3(Epsilon)
            );

            if (Intersects(playerBox, collisionCheckBox)) return;

            world.SetBlock(placePosition, BlockId.Stone);
        }
    }

    private static bool Intersects(BoundingBox a, BoundingBox b)
        => a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
           a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
           a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;

    private RaycastResult? Raycast(Vector3d origin, Vector3 direction, double maxDistance)
    {
        Vector3d dir = Vector3d.Normalize((Vector3d)direction);

        int x = (int)Math.Floor(origin.X);
        int y = (int)Math.Floor(origin.Y);
        int z = (int)Math.Floor(origin.Z);

        int stepX = Math.Sign(dir.X);
        int stepY = Math.Sign(dir.Y);
        int stepZ = Math.Sign(dir.Z);

        double tDeltaX = stepX != 0 ? Math.Abs(1.0 / dir.X) : double.MaxValue;
        double tDeltaY = stepY != 0 ? Math.Abs(1.0 / dir.Y) : double.MaxValue;
        double tDeltaZ = stepZ != 0 ? Math.Abs(1.0 / dir.Z) : double.MaxValue;

        double tMaxX = (stepX > 0) ? (Math.Floor(origin.X) + 1.0 - origin.X) * tDeltaX : (origin.X - Math.Floor(origin.X)) * tDeltaX;
        double tMaxY = (stepY > 0) ? (Math.Floor(origin.Y) + 1.0 - origin.Y) * tDeltaY : (origin.Y - Math.Floor(origin.Y)) * tDeltaY;
        double tMaxZ = (stepZ > 0) ? (Math.Floor(origin.Z) + 1.0 - origin.Z) * tDeltaZ : (origin.Z - Math.Floor(origin.Z)) * tDeltaZ;

        int lastX = x;
        int lastY = y;
        int lastZ = z;

        int maxSteps = (int)(maxDistance * 3);

        for (int i = 0; i < maxSteps; i++)
        {
            var currentBlockPos = new Vector3d(x, y, z);

            var blockId = world.GetBlock(currentBlockPos);
            if (blockId != BlockId.Air)
            {
                return new RaycastResult(currentBlockPos, new Vector3d(lastX, lastY, lastZ));
            }

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
}