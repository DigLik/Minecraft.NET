using Silk.NET.Maths;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Minecraft.NET.Core;

[JsonSerializable(typeof(Dictionary<Vector3D<int>, Dictionary<int, BlockId>>))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<int, BlockId>>))]
[JsonSourceGenerationOptions(Converters = [typeof(Vector3DIntJsonConverter)])]
internal partial class WorldStorageJsonContext : JsonSerializerContext { }

public class WorldStorage
{
    private readonly string _worldName;
    private readonly string _savePath;
    private ConcurrentDictionary<Vector3D<int>, ConcurrentDictionary<int, BlockId>> _modifications = new();

    public WorldStorage(string worldName)
    {
        _worldName = worldName;
        _savePath = Path.Combine(AppContext.BaseDirectory, "saves", _worldName, "chunks.json");
    }

    private static int GetIndex(int x, int y, int z) => x + y * ChunkSize + z * ChunkSize * ChunkSize;

    public void Load()
    {
        if (!File.Exists(_savePath))
            return;

        try
        {
            var json = File.ReadAllText(_savePath);
            var loadedDict = JsonSerializer.Deserialize(json, WorldStorageJsonContext.Default.DictionaryVector3DInt32DictionaryInt32BlockId) ?? [];

            _modifications = new ConcurrentDictionary<Vector3D<int>, ConcurrentDictionary<int, BlockId>>(
                loadedDict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ConcurrentDictionary<int, BlockId>(kvp.Value)
                )
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading world modifications: {ex.Message}");
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_savePath);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }

            var regularDict = _modifications.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToDictionary(innerKvp => innerKvp.Key, innerKvp => innerKvp.Value)
            );

            var json = JsonSerializer.Serialize(regularDict, WorldStorageJsonContext.Default.DictionaryVector3DInt32DictionaryInt32BlockId);
            File.WriteAllText(_savePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving world modifications: {ex.Message}");
        }
    }

    public void ApplyModificationsToChunk(ChunkSection chunk)
    {
        if (_modifications.TryGetValue(chunk.Position, out var chunkMods))
        {
            foreach (var (index, blockId) in chunkMods)
            {
                int x = index % ChunkSize;
                int y = (index / ChunkSize) % ChunkSize;
                int z = index / (ChunkSize * ChunkSize);
                chunk.SetBlock(x, y, z, blockId);
            }
        }
    }

    public void RecordModification(Vector3D<int> chunkPos, int x, int y, int z, BlockId blockId)
    {
        var chunkMods = _modifications.GetOrAdd(chunkPos, _ => new ConcurrentDictionary<int, BlockId>());
        chunkMods[GetIndex(x, y, z)] = blockId;
    }
}