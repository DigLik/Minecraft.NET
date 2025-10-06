namespace Minecraft.NET.Core.World;

public class Chunk
{
    public Block[,,] BlockIDs { get; init; } = new Block[16, 16, 16];
    public bool IsDirty { get; set; } = false;
}
