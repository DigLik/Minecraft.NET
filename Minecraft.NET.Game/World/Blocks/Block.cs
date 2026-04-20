using Minecraft.NET.Engine.Abstractions.Graphics;

namespace Minecraft.NET.Game.World.Blocks;

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
    BlockId Id, BlockFaceTextures Textures,
    BlockTransparency Transparency = BlockTransparency.Opaque);

public static class BlockRegistry
{
    public static BlockDefinition[] Definitions { get; } = new BlockDefinition[256];
    public static List<string> TextureFiles { get; } = [];
    public static List<MaterialData> MaterialConfigs { get; } = [];
    private static readonly Dictionary<string, int> _textureCache = [];

    public static void Initialize()
    {
        Register(new BlockDefinition(BlockId.Air, default, BlockTransparency.Transparent));

        MaterialData roughMaterial = new MaterialData { Roughness = 0.9f, Metallic = 0.0f, Emission = 0.0f };
        MaterialData grassMaterial = new MaterialData { Roughness = 1.0f, Metallic = 0.0f, Emission = 0.0f };
        MaterialData leavesMaterial = new MaterialData { Roughness = 0.7f, Metallic = 0.0f, Emission = 0.0f };

        int stone = RegisterTexture("Assets/Textures/Blocks/stone.ztex", roughMaterial);
        Register(new BlockDefinition(BlockId.Stone, new(stone, stone, stone)));

        int dirt = RegisterTexture("Assets/Textures/Blocks/dirt.ztex", roughMaterial);
        Register(new BlockDefinition(BlockId.Dirt, new(dirt, dirt, dirt)));

        int grassTop = RegisterTexture("Assets/Textures/Blocks/grass_top.ztex", grassMaterial);
        int grassSide = RegisterTexture("Assets/Textures/Blocks/grass_side.ztex", grassMaterial);
        _ = RegisterTexture("Assets/Textures/Blocks/grass_side_overlay.ztex", grassMaterial);

        Register(new BlockDefinition(BlockId.Grass, new(grassTop, dirt, grassSide)));

        int oakLogTop = RegisterTexture("Assets/Textures/Blocks/oak_log_top.ztex", roughMaterial);
        int oakLogSide = RegisterTexture("Assets/Textures/Blocks/oak_log_side.ztex", roughMaterial);
        Register(new BlockDefinition(BlockId.OakLog, new(oakLogTop, oakLogTop, oakLogSide)));

        int oakLeaves = RegisterTexture("Assets/Textures/Blocks/oak_leaves.ztex", leavesMaterial);
        Register(new BlockDefinition(BlockId.OakLeaves, new(oakLeaves, oakLeaves, oakLeaves), BlockTransparency.Transparent));
    }

    private static int RegisterTexture(string path, MaterialData material)
    {
        if (_textureCache.TryGetValue(path, out int index)) return index;
        index = TextureFiles.Count;
        TextureFiles.Add(path);
        MaterialConfigs.Add(material);
        _textureCache[path] = index;
        return index;
    }

    private static void Register(BlockDefinition def) => Definitions[(int)def.Id] = def;
}