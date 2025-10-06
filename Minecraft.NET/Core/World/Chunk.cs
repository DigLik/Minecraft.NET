namespace Minecraft.NET.Core.World;

public class Chunk(int chunkSize, List<Block> palette, ushort[,,] blockIndices)
{
    public ushort[,,] BlockIndices { get; init; } = blockIndices;

    public List<Block> Palette { get; init; } = palette;

    public bool IsDirty { get; set; } = false;

    public Block GetBlock(int x, int y, int z)
    {
        ushort paletteIndex = BlockIndices[x, y, z];
        return Palette[paletteIndex];
    }
}