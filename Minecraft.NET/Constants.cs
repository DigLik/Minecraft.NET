namespace Minecraft.NET;

public static class Constants
{
    public const int ChunkSize = 16; // 2^n

    public const int ChunkMask = ChunkSize - 1; // 15 или 0xF
    public static readonly int ChunkShift = BitOperations.Log2(ChunkSize); // 2^4 = 16
    public const int BlocksInChunk = ChunkSize * ChunkSize * ChunkSize;

    public const int RenderDistance = 512 / ChunkSize; // blocks render distance

    public const int WorldHeightInChunks = 16;
    public const int WorldHeightInBlocks = ChunkSize * WorldHeightInChunks;
    public const int VerticalChunkOffset = WorldHeightInChunks / 2;

    public const int MaxVisibleSections = (RenderDistance * 2 + 1) * (RenderDistance * 2 + 1) * WorldHeightInChunks;

    public const float TileSize = 16.0f;
    public const float AtlasWidth = 1024.0f;
    public const float AtlasHeight = 512.0f;

    public const double PlayerHeight = 1.8;
    public const double PlayerWidth = 0.6;
    public const double PlayerEyeHeight = 1.62;
    public const double Gravity = 28.0;
    public const double MaxSpeed = 5.0;
    public const double SprintSpeedMultiplier = 2.5;
    public const double JumpForce = 9.0;
}