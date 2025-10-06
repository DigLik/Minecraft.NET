namespace Minecraft.NET.Core;

public record GameSettings
{
    public int ChunkSize { get; init; } = 16;
    public int ChunkHeight { get; init; } = 16;
    public int WorldHeightInBlocks => ChunkSize * ChunkHeight;
    public int MaxBackgroundThreads { get; init; } = 24;
}