namespace Minecraft.NET.Core.Blocks;

public enum BlockId : byte
{
    Air = 0,
    Stone = 1,
    Dirt = 2,
    Grass = 3,
}

public readonly record struct BlockFaceTextures(int Top, int Bottom, int Side);
public readonly record struct BlockDefinition(BlockId Id, string Name, BlockFaceTextures Textures);

public static class BlockRegistry
{
    public static Dictionary<BlockId, BlockDefinition> Definitions { get; } = [];

    public static List<string> TextureFiles { get; } = [];
    private static readonly Dictionary<string, int> _textureCache = [];

    public static void Initialize()
    {
        int stone = RegisterTexture("Assets/Textures/Blocks/stone.png");
        int dirt = RegisterTexture("Assets/Textures/Blocks/dirt.png");
        int grassTop = RegisterTexture("Assets/Textures/Blocks/grass_top.png");
        int grassSide = RegisterTexture("Assets/Textures/Blocks/grass_side.png");

        Register(new BlockDefinition(BlockId.Air, "Air", default));
        Register(new BlockDefinition(BlockId.Stone, "Stone", new(stone, stone, stone)));
        Register(new BlockDefinition(BlockId.Dirt, "Dirt", new(dirt, dirt, dirt)));
        Register(new BlockDefinition(BlockId.Grass, "Grass", new(grassTop, dirt, grassSide)));
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

    private static void Register(BlockDefinition def) => Definitions.Add(def.Id, def);
}