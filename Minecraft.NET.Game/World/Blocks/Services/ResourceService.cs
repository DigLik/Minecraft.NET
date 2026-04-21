using System.Runtime.InteropServices;
using System.Text.Json;

using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Game.World.Serialization;

using ZstdSharp;

namespace Minecraft.NET.Game.World.Blocks.Services;

public class ResourceService : IResourceService
{
    private readonly List<string> _textureFiles = [];
    private readonly List<MaterialData> _materialConfigs = [];
    private readonly Dictionary<string, int> _textureCache = [];
    private readonly Dictionary<SpecialMaterialId, int> _specialMaterials = [];

    public ResourceService(IBlockService blockService)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Configs", "Materials.json");
        using var fs = File.OpenRead(path);
        var materials = JsonSerializer.Deserialize(fs, GameJsonSerializerContext.Default.ListMaterialDefinitionJsonModel) ?? [];

        foreach (var mat in materials)
        {
            int index = RegisterTexture(mat.TexturePath, new MaterialData
            {
                Roughness = mat.Roughness,
                Metallic = mat.Metallic,
                Emission = mat.Emission
            });

            if (mat.Name == "grass_side_overlay")
                _specialMaterials[SpecialMaterialId.GrassSideOverlay] = index;

            foreach (var bind in mat.Bindings)
                if (Enum.TryParse<BlockId>(bind.Block, out var blockId))
                    blockService.SetBlockFaceTexture(blockId, bind.Face, index);
        }
    }

    private int RegisterTexture(string path, MaterialData material)
    {
        if (_textureCache.TryGetValue(path, out int index)) return index;
        index = _textureFiles.Count;
        _textureFiles.Add(path);
        _materialConfigs.Add(material);
        _textureCache[path] = index;
        return index;
    }

    public int GetSpecialMaterialIndex(SpecialMaterialId specialMaterialId)
        => _specialMaterials.TryGetValue(specialMaterialId, out int index) ? index : 0;

    public ReadOnlySpan<MaterialData> GetMaterialConfigs() => CollectionsMarshal.AsSpan(_materialConfigs);

    public byte[][] LoadTexturePixels(int sizeMap)
    {
        byte[][] pixels = new byte[_textureFiles.Count][];
        using var decompressor = new Decompressor();
        string baseDir = AppContext.BaseDirectory;

        for (int i = 0; i < _textureFiles.Count; i++)
        {
            string path = Path.Combine(baseDir, _textureFiles[i]);
            pixels[i] = new byte[sizeMap * sizeMap * 4];

            if (File.Exists(path))
            {
                byte[] compressed = File.ReadAllBytes(path);
                decompressor.Unwrap(compressed, pixels[i]);
            }
            else
            {
                for (int p = 0; p < pixels[i].Length; p += 4)
                {
                    pixels[i][p] = 255;
                    pixels[i][p + 1] = 0;
                    pixels[i][p + 2] = 255;
                    pixels[i][p + 3] = 255;
                }
            }
        }
        return pixels;
    }
}
