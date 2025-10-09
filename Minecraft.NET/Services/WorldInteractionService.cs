using Minecraft.NET.Abstractions;
using Minecraft.NET.Core.Common;
using Minecraft.NET.Graphics;

namespace Minecraft.NET.Services;

public readonly record struct RaycastResult(Vector3d HitPosition, Vector3d PlacePosition);

public class WorldInteractionService : IWorldInteractionService
{
    private readonly IPlayer _player;
    private readonly IWorld _world;

    public WorldInteractionService(IPlayer player, IWorld world)
    {
        _player = player;
        _world = world;
    }

    public void BreakBlock()
    {
        var result = Raycast(_player.Position, _player.Camera.Front, 6.0);
        if (result.HasValue)
            _world.SetBlock(result.Value.HitPosition, BlockId.Air);
    }

    public void PlaceBlock()
    {
        var result = Raycast(_player.Position, _player.Camera.Front, 6.0);
        if (result.HasValue)
        {
            var placePosition = result.Value.PlacePosition;
            var newBlockBox = new BoundingBox(
                (Vector3)placePosition,
                (Vector3)placePosition + Vector3.One
            );

            var playerBox = _player.GetBoundingBox();

            const float Epsilon = 0.0001f;
            var collisionCheckBox = new BoundingBox(
                newBlockBox.Min + new Vector3(Epsilon),
                newBlockBox.Max - new Vector3(Epsilon)
            );

            if (Intersects(playerBox, collisionCheckBox)) return;

            _world.SetBlock(placePosition, BlockId.Stone);
        }
    }

    private static bool Intersects(BoundingBox a, BoundingBox b)
        => a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
           a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
           a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;

    private RaycastResult? Raycast(Vector3d origin, Vector3 direction, double maxDistance)
    {
        Vector3d pos = origin;
        Vector3d step = Vector3d.Normalize(direction) * 0.05;
        Vector3d lastAirPos = Vector3d.Zero;

        for (double dist = 0; dist < maxDistance; dist += 0.05)
        {
            var currentBlockPos = new Vector3d(Math.Floor(pos.X), Math.Floor(pos.Y), Math.Floor(pos.Z));
            var blockId = _world.GetBlock(currentBlockPos);

            if (blockId != BlockId.Air)
                return new RaycastResult(currentBlockPos, lastAirPos);

            lastAirPos = currentBlockPos;
            pos += step;
        }

        return null;
    }
}