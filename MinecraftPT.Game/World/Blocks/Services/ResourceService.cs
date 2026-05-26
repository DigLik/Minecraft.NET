using System.Runtime.InteropServices;
using System.Text.Json;

using MinecraftPT.Engine.Abstractions.Graphics;
using MinecraftPT.Game.World.Serialization;

using ZstdSharp;

namespace MinecraftPT.Game.World.Blocks.Services;

public class ResourceService : IResourceService
{
    private readonly List<string> _textureFiles = [];
    private readonly List<MaterialData> _materialConfigs = [];
    private readonly List<bool> _materialIsOpaque = [];
    private readonly Dictionary<string, int> _textureCache = [];
    private readonly Dictionary<SpecialMaterialId, int> _specialMaterials = [];

    public ResourceService(IBlockService blockService)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Configs", "Materials.json");
        using var fs = File.OpenRead(path);
        var materials = JsonSerializer.Deserialize(fs, GameJsonSerializerContext.Default.ListMaterialDefinitionJsonModel) ?? [];

        foreach (var mat in materials)
        {
            bool isOpaque = mat.Type == null || mat.Type.Equals("Opaque", StringComparison.OrdinalIgnoreCase);

            float typeVal = 0.0f;
            if (mat.Type != null)
            {
                if (mat.Type.Equals("Transparent", StringComparison.OrdinalIgnoreCase)) typeVal = 1.0f;
                else if (mat.Type.Equals("Translucent", StringComparison.OrdinalIgnoreCase)) typeVal = 2.0f;
            }

            int index = RegisterTexture(mat.TexturePath, new MaterialData
            {
                Roughness = mat.Roughness,
                Metallic = mat.Metallic,
                Emission = mat.Emission,
                Opacity = mat.Opacity ?? 1.0f,
                Type = typeVal,
                Ior = mat.Ior ?? 1.0f,
                Absorption = mat.Absorption ?? 0.0f
            }, isOpaque);

            if (mat.Name == "grass_side_overlay")
                _specialMaterials[SpecialMaterialId.GrassSideOverlay] = index;

            foreach (var bind in mat.Bindings)
                if (Enum.TryParse<BlockId>(bind.Block, out var blockId))
                    blockService.SetBlockFaceTexture(blockId, bind.Face, index);
        }
    }

    private int RegisterTexture(string path, MaterialData material, bool isOpaque)
    {
        if (_textureCache.TryGetValue(path, out int index)) return index;
        index = _textureFiles.Count;
        _textureFiles.Add(path);
        _materialConfigs.Add(material);
        _materialIsOpaque.Add(isOpaque);
        _textureCache[path] = index;
        return index;
    }

    public int GetSpecialMaterialIndex(SpecialMaterialId specialMaterialId)
        => _specialMaterials.TryGetValue(specialMaterialId, out int index) ? index : 0;

    public ReadOnlySpan<MaterialData> GetMaterialConfigs() => CollectionsMarshal.AsSpan(_materialConfigs);

    public bool IsMaterialOpaque(int materialIndex)
    {
        if (materialIndex < 0 || materialIndex >= _materialIsOpaque.Count) return true;
        return _materialIsOpaque[materialIndex];
    }

    public byte[][] LoadTexturePixels(int sizeMap)
    {
        byte[][] pixels = new byte[_textureFiles.Count][];
        using var decompressor = new Decompressor();

        for (int i = 0; i < _textureFiles.Count; i++)
        {
            string logicalName = "MinecraftPT." + _textureFiles[i].Replace('/', '.').Replace('\\', '.');
            pixels[i] = new byte[sizeMap * sizeMap * 4];

            using var stream = System.Reflection.Assembly.GetEntryAssembly()?.GetManifestResourceStream(logicalName);
            if (stream != null)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                byte[] compressed = ms.ToArray();
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
