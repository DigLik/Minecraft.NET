using Silk.NET.Maths;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Minecraft.NET.Core;

public class Vector3DIntJsonConverter : JsonConverter<Dictionary<Vector3D<int>, Dictionary<int, BlockId>>>
{
    public override Dictionary<Vector3D<int>, Dictionary<int, BlockId>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, BlockId>>>(ref reader, options) ?? [];
        var result = new Dictionary<Vector3D<int>, Dictionary<int, BlockId>>();

        foreach (var (key, value) in dictionary)
        {
            var parts = key.Split(',');
            if (parts.Length == 3 &&
                int.TryParse(parts[0], out var x) &&
                int.TryParse(parts[1], out var y) &&
                int.TryParse(parts[2], out var z))
            {
                result[new Vector3D<int>(x, y, z)] = value;
            }
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<Vector3D<int>, Dictionary<int, BlockId>> value, JsonSerializerOptions options)
    {
        var dictionary = value.ToDictionary(kvp => $"{kvp.Key.X},{kvp.Key.Y},{kvp.Key.Z}", kvp => kvp.Value);
        JsonSerializer.Serialize(writer, dictionary, options);
    }
}