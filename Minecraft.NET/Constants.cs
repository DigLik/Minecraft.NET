namespace Minecraft.NET;

public static class Constants
{
    public const int ChunkSize = 16; // НЕ МЕНЯТЬ! | DO NOT CHANGE!

    public const int ChunkMask = ChunkSize - 1; // 15
    public const int ChunkShift = 4;
    public const int BlocksInChunk = ChunkSize * ChunkSize * ChunkSize; // 4096

    public const int RenderDistance = 16; // Расстояние рендеринга в чанках

    public const int WorldHeightInChunks = 16;
    public const int WorldHeightInBlocks = ChunkSize * WorldHeightInChunks; // 256
    public const int VerticalChunkOffset = WorldHeightInChunks / 2; // 8

    public const float TileSize = 16.0f;

    public const double PlayerHeight = 1.8;
    public const double PlayerWidth = 0.6;
    public const double PlayerEyeHeight = 1.62;
    public const double Gravity = 28.0;
    public const double MaxSpeed = 5.0;
    public const double SprintSpeedMultiplier = 2.5;
    public const double JumpForce = 9.0;
}