using Minecraft.NET.Character;
using Minecraft.NET.Core.Common;
using Minecraft.NET.Core.Environment;

namespace Minecraft.NET.Services;

public readonly record struct RaycastResult(Vector3<float> HitPosition, Vector3<float> PlacePosition);

public class WorldInteractionService()
{
    public void BreakBlock()
    {
    }

    public void PlaceBlock()
    {
    }

    private static bool Intersects(BoundingBox<float> a, BoundingBox<float> b)
        => a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
           a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
           a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;

    private RaycastResult? Raycast(Vector3<float> origin, Vector3<float> direction, float maxDistance)
    {
        Vector3<float> dir = direction.Normalize<float>();

        int x = (int)Math.Floor(origin.X);
        int y = (int)Math.Floor(origin.Y);
        int z = (int)Math.Floor(origin.Z);

        int stepX = Math.Sign(dir.X);
        int stepY = Math.Sign(dir.Y);
        int stepZ = Math.Sign(dir.Z);

        float tDeltaX = stepX != 0 ? MathF.Abs(1 / dir.X) : float.MaxValue;
        float tDeltaY = stepY != 0 ? MathF.Abs(1 / dir.Y) : float.MaxValue;
        float tDeltaZ = stepZ != 0 ? MathF.Abs(1 / dir.Z) : float.MaxValue;

        float tMaxX = (stepX > 0) ? (MathF.Floor(origin.X) + 1 - origin.X) * tDeltaX : (origin.X - MathF.Floor(origin.X)) * tDeltaX;
        float tMaxY = (stepY > 0) ? (MathF.Floor(origin.Y) + 1 - origin.Y) * tDeltaY : (origin.Y - MathF.Floor(origin.Y)) * tDeltaY;
        float tMaxZ = (stepZ > 0) ? (MathF.Floor(origin.Z) + 1 - origin.Z) * tDeltaZ : (origin.Z - MathF.Floor(origin.Z)) * tDeltaZ;

        int lastX = x;
        int lastY = y;
        int lastZ = z;

        int maxSteps = (int)(maxDistance * 3);

        for (int i = 0; i < maxSteps; i++)
        {
            var currentBlockPos = new Vector3<float>(x, y, z);

            var blockId = BlockId.Air; // world.GetBlock(currentBlockPos);
            if (blockId != BlockId.Air)
                return new RaycastResult(currentBlockPos, new Vector3<float>(lastX, lastY, lastZ));

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