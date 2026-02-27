namespace Minecraft.NET.Character;

public enum GameMode : byte { Creative, Spectator }

public class Player(Vector3<float> initialPosition)
{
    public Vector3<float> Position { get; set; } = initialPosition;
    public Vector3<float> Rotation { get; set; }
    public Vector3<float> Velocity { get; set; }
    public bool IsOnGround { get; set; }
    public GameMode CurrentGameMode { get; set; } = GameMode.Creative;

    public BoundingBox<float> BoundingBox => GetBoundingBoxForPosition(Position);

    public static BoundingBox<float> GetBoundingBoxForPosition(Vector3<float> position)
    {
        var halfWidth = PlayerWidth / 2;
        var playerPos = position - new Vector3<float>(0, PlayerEyeHeight, 0);
        return new BoundingBox<float>(
            new Vector3<float>(playerPos.X - halfWidth, playerPos.Y, playerPos.Z - halfWidth),
            new Vector3<float>(playerPos.X + halfWidth, playerPos.Y + PlayerHeight, playerPos.Z + halfWidth)
        );
    }
}