namespace Minecraft.NET.Game.World.Blocks;

public enum BlockId : byte
{
    Air = 0,
    Stone = 1,
    Dirt = 2,
    Grass = 3,
    OakLog = 5,
    OakLeaves = 6
}

public enum SpecialMaterialId : byte
{
    GrassSideOverlay
}

public enum BlockTransparency : byte
{
    Opaque,
    Transparent,
    Foliage
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<BlockFace>))]
public enum BlockFace : byte
{
    Top, Bottom, Side, All
}

public readonly record struct BlockFaceTextures(int Top, int Bottom, int Side);

public readonly record struct BlockDefinition(BlockId Id, BlockFaceTextures Textures,
                                              BlockTransparency Transparency = BlockTransparency.Opaque);