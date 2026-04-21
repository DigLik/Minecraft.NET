using System.Text.Json.Serialization;

namespace Minecraft.NET.Game.World.Serialization;

public class BlockDefinitionJsonModel
{
    public required string Id { get; set; }
    public required string Transparency { get; set; }
}

public class MaterialDefinitionJsonModel
{
    public required string Name { get; set; }
    public required string TexturePath { get; set; }
    public required float Roughness { get; set; }
    public required float Metallic { get; set; }
    public required float Emission { get; set; }
    public required List<MaterialBindingJsonModel> Bindings { get; set; }
}

public class MaterialBindingJsonModel
{
    public required string Block { get; set; }
    public required Blocks.BlockFace Face { get; set; }
}

[JsonSerializable(typeof(List<BlockDefinitionJsonModel>))]
[JsonSerializable(typeof(List<MaterialDefinitionJsonModel>))]
public partial class GameJsonSerializerContext : JsonSerializerContext
{
}