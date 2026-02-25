namespace Minecraft.NET.Core.Blocks;

public enum BlockId : byte
{
    Air = 0,
    Stone = 1,
    Dirt = 2,
    Grass = 3,
    GrassSideOverlay = 4,
    OakLog = 5,
    OakLeaves = 6
}

public enum BlockTransparency : byte
{
    Opaque,
    Transparent,
    Foliage
}

public readonly record struct BlockFaceTextures(int Top, int Bottom, int Side);
public readonly record struct BlockDefinition(
    BlockId Id,
    string Name,
    BlockFaceTextures Textures,
    BlockTransparency Transparency = BlockTransparency.Opaque);

public static class BlockRegistry
{
    public static BlockDefinition[] Definitions { get; } = new BlockDefinition[256];
    public static List<string> TextureFiles { get; } = [];
    private static readonly Dictionary<string, int> _textureCache = [];

    public static void Initialize()
    {
        Register(new BlockDefinition(
            BlockId.Air, "Air", default, BlockTransparency.Transparent));

        int stone = RegisterTexture("Assets/Textures/Blocks/stone.png");
        Register(new BlockDefinition(
            BlockId.Stone, "Stone", new(stone, stone, stone)));

        int dirt = RegisterTexture("Assets/Textures/Blocks/dirt.png");
        Register(new BlockDefinition(
            BlockId.Dirt, "Dirt", new(dirt, dirt, dirt)));

        int grassTop = RegisterTexture("Assets/Textures/Blocks/grass_top.png");
        int grassSide = RegisterTexture("Assets/Textures/Blocks/grass_side.png");
        _ = RegisterTexture("Assets/Textures/Blocks/grass_side_overlay.png");
        Register(new BlockDefinition(
            BlockId.Grass, "Grass", new(grassTop, dirt, grassSide)));

        int oakLogTop = RegisterTexture("Assets/Textures/Blocks/oak_log_top.png");
        int oakLogSide = RegisterTexture("Assets/Textures/Blocks/oak_log_side.png");
        Register(new BlockDefinition(
            BlockId.OakLog, "OakLog", new(oakLogTop, oakLogTop, oakLogSide)));

        int oakLeaves = RegisterTexture("Assets/Textures/Blocks/oak_leaves.png");
        Register(new BlockDefinition(
            BlockId.OakLeaves, "OakLeaves", new(oakLeaves, oakLeaves, oakLeaves), BlockTransparency.Transparent));
    }

    private static int RegisterTexture(string path)
    {
        if (_textureCache.TryGetValue(path, out int index))
            return index;

        index = TextureFiles.Count;
        TextureFiles.Add(path);
        _textureCache[path] = index;
        return index;
    }

    private static void Register(BlockDefinition def) => Definitions[(int)def.Id] = def;
}