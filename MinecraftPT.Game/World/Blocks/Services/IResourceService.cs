using MinecraftPT.Engine.Abstractions.Graphics;

namespace MinecraftPT.Game.World.Blocks.Services;

public interface IResourceService
{
    int GetSpecialMaterialIndex(SpecialMaterialId specialMaterialId);
    ReadOnlySpan<MaterialData> GetMaterialConfigs();
    byte[][] LoadTexturePixels(int sizeMap);
    bool IsMaterialOpaque(int materialIndex);
}