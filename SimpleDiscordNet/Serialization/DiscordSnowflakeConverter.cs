using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleDiscordNet.Serialization;

/// <summary>
/// JSON converter for Discord snowflake IDs.
/// Discord sends snowflake IDs as strings in JSON to prevent precision loss in JavaScript.
/// This converter handles deserialization from string to ulong and serialization back to string.
/// </summary>
internal sealed class DiscordSnowflakeConverter : JsonConverter<ulong>
{
    public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string? value = reader.GetString();
            if (value != null && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong result))
            {
                return result;
            }
            return 0UL; // Return 0 for invalid/null strings
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            // Fallback for actual JSON numbers (shouldn't happen with Discord API)
            return reader.GetUInt64();
        }

        return 0UL; // Default for any other token type
    }

    public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
    {
        // Discord expects snowflake IDs as strings
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// JSON converter for nullable Discord snowflake IDs.
/// </summary>
internal sealed class NullableDiscordSnowflakeConverter : JsonConverter<ulong?>
{
    public override ulong? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string? value = reader.GetString();
            if (value != null && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong result))
            {
                return result;
            }
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetUInt64();
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, ulong? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
