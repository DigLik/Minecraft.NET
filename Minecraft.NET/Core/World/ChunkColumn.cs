namespace Minecraft.NET.Core.World;

public class ChunkColumn(uint chunkHeight)
{
    public Chunk[] Chunks { get; init; } = new Chunk[chunkHeight];
}
