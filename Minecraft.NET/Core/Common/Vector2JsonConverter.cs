using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Minecraft.NET.Core.Common;

public class Vector2DIntJsonConverter : JsonConverter<Dictionary<Vector2D<int>, Dictionary<int, BlockId>>>
{
    public override Dictionary<Vector2D<int>, Dictionary<int, BlockId>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        var result = new Dictionary<Vector2D<int>, Dictionary<int, BlockId>>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return result;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            ReadOnlySpan<byte> keySpan = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
            int commaIndex = keySpan.IndexOf((byte)',');

            if (commaIndex == -1 ||
                !Utf8Parser.TryParse(keySpan[..commaIndex], out int x, out _) ||
                !Utf8Parser.TryParse(keySpan[(commaIndex + 1)..], out int y, out _))
                throw new JsonException("Invalid key format");

            reader.Read();
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            var chunkMods = new Dictionary<int, BlockId>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException();

                ReadOnlySpan<byte> indexSpan = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
                if (!Utf8Parser.TryParse(indexSpan, out int blockIndex, out _))
                    throw new JsonException();

                reader.Read();
                byte blockIdVal = reader.GetByte();
                chunkMods[blockIndex] = (BlockId)blockIdVal;
            }

            result[new Vector2D<int>(x, y)] = chunkMods;
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<Vector2D<int>, Dictionary<int, BlockId>> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        Span<char> keyBuffer = stackalloc char[32];
        Span<char> indexKeyBuffer = stackalloc char[16];

        foreach (var kvp in value)
        {
            if (TryFormatChunkKey(kvp.Key, keyBuffer, out int charsWritten))
                writer.WritePropertyName(keyBuffer[..charsWritten]);
            else
                writer.WritePropertyName($"{kvp.Key.X},{kvp.Key.Y}");

            writer.WriteStartObject();
            foreach (var mod in kvp.Value)
            {
                if (mod.Key.TryFormat(indexKeyBuffer, out int keyLen, default, null))
                    writer.WritePropertyName(indexKeyBuffer[..keyLen]);
                else
                    writer.WritePropertyName(mod.Key.ToString());

                writer.WriteNumberValue((byte)mod.Value);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    private static bool TryFormatChunkKey(Vector2D<int> pos, Span<char> destination, out int charsWritten)
    {
        charsWritten = 0;
        if (!pos.X.TryFormat(destination, out int xLen)) return false;
        if (destination.Length <= xLen) return false;

        destination[xLen] = ',';

        if (!pos.Y.TryFormat(destination[(xLen + 1)..], out int yLen)) return false;

        charsWritten = xLen + 1 + yLen;
        return true;
    }
}