namespace MinecraftPT.Game.World.Blocks;

public enum BlockId : byte
{
    Air = 0,
    Stone = 1,
    Dirt = 2,
    Grass = 3,
    OakLog = 5,
    OakLeaves = 6,
    Glass = 7,
    IronBlock = 8,
    WhiteConcrete = 9,
    OrangeConcrete = 10,
    MagentaConcrete = 11,
    LightBlueConcrete = 12,
    YellowConcrete = 13,
    LimeConcrete = 14,
    PinkConcrete = 15,
    GrayConcrete = 16,
    LightGrayConcrete = 17,
    PurpleConcrete = 18,
    BlueConcrete = 19,
    RedConcrete = 20,
    BlackConcrete = 21
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