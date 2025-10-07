namespace Minecraft.NET;

public static class Constants
{
    public const int ChunkSize = 16;
    public const int BlocksInChunk = ChunkSize * ChunkSize * ChunkSize;

    public const int RenderDistance = 16;

    public const int WorldHeightInChunks = 16;
    public const int WorldHeightInBlocks = ChunkSize * WorldHeightInChunks;
    public const int VerticalChunkOffset = WorldHeightInChunks / 2;

    public const float TileSize = 16.0f;
    public const float AtlasWidth = 1024.0f;
    public const float AtlasHeight = 512.0f;
}