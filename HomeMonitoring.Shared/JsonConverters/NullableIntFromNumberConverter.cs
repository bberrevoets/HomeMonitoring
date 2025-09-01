using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeMonitoring.Shared.JsonConverters;

public class NullableIntFromNumberConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                // Handle both integer and floating-point numbers
                if (reader.TryGetInt32(out var intValue)) return intValue;

                if (reader.TryGetDouble(out var doubleValue))
                    // Round floating-point to nearest integer
                    return (int)Math.Round(doubleValue);

                throw new JsonException($"Unable to convert {reader.GetString()} to int?");
            case JsonTokenType.String:
                // Handle numbers that come as strings
                var stringValue = reader.GetString();
                if (string.IsNullOrWhiteSpace(stringValue)) return null;
                if (int.TryParse(stringValue, out var parsedInt)) return parsedInt;
                if (double.TryParse(stringValue, out var parsedDouble)) return (int)Math.Round(parsedDouble);
                throw new JsonException($"Unable to convert string '{stringValue}' to int?");
            default:
                throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}