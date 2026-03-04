namespace Minecraft.NET.Game;

public static class GameConstants
{
    public const int ChunkSize = 16;
    public const int BlocksInChunk = ChunkSize * ChunkSize * ChunkSize;

    public const int RenderDistance = 32;
    public const int WorldHeightInChunks = 16;
    public const int WorldHeightInBlocks = ChunkSize * WorldHeightInChunks;

    public const float TileSize = 16.0f;
    public const float PlayerHeight = 1.8f;
    public const float PlayerWidth = 0.6f;
    public const float PlayerEyeHeight = 1.62f;
}