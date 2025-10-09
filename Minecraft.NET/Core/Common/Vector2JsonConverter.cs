using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Minecraft.NET.Core.Common;

public class Vector2DIntJsonConverter : JsonConverter<Dictionary<Vector2D<int>, Dictionary<int, BlockId>>>
{
    public override Dictionary<Vector2D<int>, Dictionary<int, BlockId>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var typeInfo = (JsonTypeInfo<Dictionary<string, Dictionary<int, BlockId>>>)options.GetTypeInfo(typeof(Dictionary<string, Dictionary<int, BlockId>>));
        var dictionary = JsonSerializer.Deserialize(ref reader, typeInfo) ?? [];
        var result = new Dictionary<Vector2D<int>, Dictionary<int, BlockId>>();

        foreach (var (key, value) in dictionary)
        {
            var parts = key.Split(',');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var x) &&
                int.TryParse(parts[1], out var y))
            {
                result[new Vector2D<int>(x, y)] = value;
            }
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<Vector2D<int>, Dictionary<int, BlockId>> value, JsonSerializerOptions options)
    {
        var dictionary = value.ToDictionary(kvp => $"{kvp.Key.X},{kvp.Key.Y}", kvp => kvp.Value);
        var typeInfo = (JsonTypeInfo<Dictionary<string, Dictionary<int, BlockId>>>)options.GetTypeInfo(typeof(Dictionary<string, Dictionary<int, BlockId>>));
        JsonSerializer.Serialize(writer, dictionary, typeInfo);
    }
}