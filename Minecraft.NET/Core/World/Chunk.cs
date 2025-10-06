namespace Minecraft.NET.Core.World;

public class Chunk(int chunkSize)
{
    public Block[,,] BlockIDs { get; init; } = new Block[chunkSize, chunkSize, chunkSize];
    public bool IsDirty { get; set; } = false;
}
