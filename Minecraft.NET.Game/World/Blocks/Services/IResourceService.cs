using Minecraft.NET.Engine.Abstractions.Graphics;

namespace Minecraft.NET.Game.World.Blocks.Services;

public interface IResourceService
{
    int GetSpecialMaterialIndex(SpecialMaterialId specialMaterialId);
    ReadOnlySpan<MaterialData> GetMaterialConfigs();
    byte[][] LoadTexturePixels(int sizeMap);
}