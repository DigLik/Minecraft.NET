using Minecraft.NET.Core.Common;
using Minecraft.NET.Graphics;

namespace Minecraft.NET.Character;

public enum GameMode : byte { Creative, Spectator }

public class Player(Vector3d initialPosition)
{
    public Vector3d Position
    {
        get => Camera.Position;
        set => Camera.Position = value;
    }

    public Vector3d Velocity { get; set; }
    public Camera Camera { get; } = new Camera(initialPosition);
    public bool IsOnGround { get; set; }
    public GameMode CurrentGameMode { get; set; } = GameMode.Creative;

    public BoundingBox GetBoundingBox()
        => GetBoundingBoxForPosition(Position);

    public static BoundingBox GetBoundingBoxForPosition(Vector3d position)
    {
        var halfWidth = PlayerWidth / 2;
        var playerPos = position - new Vector3d(0, PlayerEyeHeight, 0);
        var min = new Vector3((float)(playerPos.X - halfWidth), (float)playerPos.Y, (float)(playerPos.Z - halfWidth));
        var max = new Vector3((float)(playerPos.X + halfWidth), (float)(playerPos.Y + PlayerHeight), (float)(playerPos.Z + halfWidth));
        return new BoundingBox(min, max);
    }
}