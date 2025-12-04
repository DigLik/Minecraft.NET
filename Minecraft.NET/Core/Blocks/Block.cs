namespace Minecraft.NET.Core.Blocks;

public enum BlockId : byte
{
    Air = 0,
    Stone = 1,
    Dirt = 2,
    Grass = 3,
}

public readonly record struct BlockFaceTextures(Vector2 Top, Vector2 Bottom, Vector2 Side);
public readonly record struct BlockDefinition(BlockId Id, string Name, BlockFaceTextures Textures);

public static class BlockRegistry
{
    public static Dictionary<BlockId, BlockDefinition> Definitions { get; } = [];

    public static void Initialize()
    {
        var stoneTex = new Vector2(26, 31);
        var dirtTex = new Vector2(28, 5);
        var grassTopTex = new Vector2(24, 17);
        var grassSideTex = new Vector2(6, 17);

        Register(new BlockDefinition(BlockId.Air, "Air", default));
        Register(new BlockDefinition(BlockId.Stone, "Stone", new(stoneTex, stoneTex, stoneTex)));
        Register(new BlockDefinition(BlockId.Dirt, "Dirt", new(dirtTex, dirtTex, dirtTex)));
        Register(new BlockDefinition(BlockId.Grass, "Grass", new(grassTopTex, dirtTex, grassSideTex)));
    }

    private static void Register(BlockDefinition def) => Definitions.Add(def.Id, def);
}