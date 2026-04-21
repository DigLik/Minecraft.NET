using System.Text.Json;

using Minecraft.NET.Game.World.Serialization;

namespace Minecraft.NET.Game.World.Blocks.Services;

public class BlockService : IBlockService
{
    private readonly BlockDefinition[] _definitions = new BlockDefinition[256];

    public BlockService()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Configs", "Blocks.json");
        using var fs = File.OpenRead(path);
        var blocks = JsonSerializer.Deserialize(fs, GameJsonSerializerContext.Default.ListBlockDefinitionJsonModel) ?? [];

        foreach (var b in blocks)
        {
            if (Enum.TryParse<BlockId>(b.Id, out var id) &&
                Enum.TryParse<BlockTransparency>(b.Transparency, out var transparency))
            {
                _definitions[(int)id] = new BlockDefinition(id, default, transparency);
            }
        }
    }

    public void SetBlockFaceTexture(BlockId id, BlockFace face, int textureIndex)
    {
        ref var def = ref _definitions[(int)id];

        switch (face)
        {
            case BlockFace.Top:
                def = def with { Textures = def.Textures with { Top = textureIndex } };
                break;
            case BlockFace.Bottom:
                def = def with { Textures = def.Textures with { Bottom = textureIndex } };
                break;
            case BlockFace.Side:
                def = def with { Textures = def.Textures with { Side = textureIndex } };
                break;
            case BlockFace.All:
                def = def with { Textures = new BlockFaceTextures(textureIndex, textureIndex, textureIndex) };
                break;
        }
    }

    public ReadOnlySpan<BlockDefinition> GetDefinitionsFast() => _definitions;

    public ref readonly BlockDefinition GetBlock(BlockId id)
    {
        if ((int)id >= _definitions.Length)
            throw new ArgumentOutOfRangeException(nameof(id));

        return ref _definitions[(int)id];
    }
}
