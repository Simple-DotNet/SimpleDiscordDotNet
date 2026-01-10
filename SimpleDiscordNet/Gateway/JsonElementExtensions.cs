using System.Text.Json;

namespace SimpleDiscordNet.Gateway;

/// <summary>
/// Extension methods for JsonElement to handle Discord snowflake IDs.
/// </summary>
internal static class JsonElementExtensions
{
    /// <summary>
    /// Gets a ulong value from a JsonElement that represents a Discord snowflake ID (which Discord sends as a string).
    /// </summary>
    public static ulong GetDiscordId(this JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            string? value = element.GetString();
            if (value != null && ulong.TryParse(value, out ulong result))
            {
                return result;
            }
        }
        else if (element.ValueKind == JsonValueKind.Number)
        {
            // Fallback for numeric values (shouldn't happen with Discord API)
            return element.GetUInt64();
        }

        return 0UL;
    }

    /// <summary>
    /// Gets a nullable ulong value from a JsonElement that represents a Discord snowflake ID.
    /// </summary>
    public static ulong? GetDiscordIdNullable(this JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            string? value = element.GetString();
            if (value != null && ulong.TryParse(value, out ulong result))
            {
                return result;
            }
        }
        else if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetUInt64();
        }

        return null;
    }
}
