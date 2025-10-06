using System.Diagnostics.CodeAnalysis;

namespace Minecraft.NET.Core.Blocks;

public static class BlockManager
{
    private static readonly Dictionary<ushort, BlockDefinition> _definitions = [];

    public static BlockDefinition Air { get; private set; }
    public static BlockDefinition Stone { get; private set; }
    public static BlockDefinition Dirt { get; private set; }
    public static BlockDefinition Grass { get; private set; }

    public static void Initialize()
    {
        int x = 24 + 0, y = 17 + 0;
        var stoneTex = new Vector2(26, 31);
        var dirtTex = new Vector2(28, 5);
        var grassTopTex = new Vector2(x, y);
        var grassSideTex = new Vector2(6, 17);

        Air = Register(new BlockDefinition(0, "Air", default));
        Stone = Register(new BlockDefinition(1, "Stone", new(stoneTex, stoneTex, stoneTex)));
        Dirt = Register(new BlockDefinition(2, "Dirt", new(dirtTex, dirtTex, dirtTex)));
        Grass = Register(new BlockDefinition(3, "Grass", new(grassTopTex, dirtTex, grassSideTex)));
    }

    private static BlockDefinition Register(BlockDefinition definition)
    {
        _definitions.Add(definition.ID, definition);
        return definition;
    }

    public static bool TryGetDefinition(ushort id, [MaybeNullWhen(false)] out BlockDefinition definition)
    {
        return _definitions.TryGetValue(id, out definition);
    }
}