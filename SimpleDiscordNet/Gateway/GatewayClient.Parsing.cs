using System.Globalization;
using System.Text.Json;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models;

namespace SimpleDiscordNet.Gateway;

/// <summary>
/// Converts raw gateway JSON into entities. Discord sends partial objects in many payloads
/// (a presence user may only have an id, a resolved member has no user), so these parsers
/// tolerate missing fields instead of relying on required members.
/// </summary>
internal sealed partial class GatewayClient
{
    private static string? GetStringOrNull(JsonElement obj, string name)
        => obj.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? GetIntOrNull(JsonElement obj, string name)
        => obj.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

    private static bool? GetBoolOrNull(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out JsonElement value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static ulong GetIdOrZero(JsonElement obj, string name)
        => obj.TryGetProperty(name, out JsonElement value) ? value.GetDiscordId() : 0UL;

    private static ulong? GetIdOrNull(JsonElement obj, string name)
        => obj.TryGetProperty(name, out JsonElement value) && value.ValueKind != JsonValueKind.Null ? value.GetDiscordIdNullable() : null;

    private static DiscordUser ParseUser(JsonElement obj)
    {
        return new DiscordUser
        {
            Id = obj.GetProperty("id").GetDiscordId(),
            Username = GetStringOrNull(obj, "username") ?? string.Empty,
            Discriminator = ushort.TryParse(GetStringOrNull(obj, "discriminator"), NumberStyles.None, CultureInfo.InvariantCulture, out ushort discriminator) ? discriminator : (ushort)0,
            Global_Name = GetStringOrNull(obj, "global_name"),
            Avatar = GetStringOrNull(obj, "avatar"),
            Bot = obj.TryGetProperty("bot", out JsonElement bot) && bot.ValueKind == JsonValueKind.True ? true : null,
            System = obj.TryGetProperty("system", out JsonElement system) && system.ValueKind == JsonValueKind.True ? true : null,
            Banner = GetStringOrNull(obj, "banner"),
            Accent_Color = GetIntOrNull(obj, "accent_color"),
            Public_Flags = GetIntOrNull(obj, "public_flags")
        };
    }

    private static DiscordMember ParseMember(JsonElement obj, DiscordUser user, DiscordGuild guild)
    {
        return new DiscordMember
        {
            User = user,
            Guild = guild,
            Nick = GetStringOrNull(obj, "nick"),
            Roles = obj.TryGetProperty("roles", out JsonElement roles) && roles.ValueKind == JsonValueKind.Array
                ? roles.EnumerateArray().Select(static x => x.GetDiscordId()).ToArray()
                : [],
            Avatar = GetStringOrNull(obj, "avatar"),
            Joined_At = GetStringOrNull(obj, "joined_at"),
            Premium_Since = GetStringOrNull(obj, "premium_since"),
            Deaf = GetBoolOrNull(obj, "deaf"),
            Mute = GetBoolOrNull(obj, "mute"),
            Flags = GetIntOrNull(obj, "flags"),
            Pending = GetBoolOrNull(obj, "pending"),
            Permissions = GetIdOrNull(obj, "permissions"),
            Communication_Disabled_Until = GetStringOrNull(obj, "communication_disabled_until")
        };
    }

    /// <summary>
    /// Parses a member array, skipping entries without a user object.
    /// </summary>
    private static DiscordMember[] ParseMembers(JsonElement parent, DiscordGuild guild)
    {
        if (!parent.TryGetProperty("members", out JsonElement members) || members.ValueKind != JsonValueKind.Array)
            return [];

        List<DiscordMember> result = new(members.GetArrayLength());
        foreach (JsonElement member in members.EnumerateArray())
        {
            if (member.TryGetProperty("user", out JsonElement user))
            {
                result.Add(ParseMember(member, ParseUser(user), guild));
            }
        }
        return result.ToArray();
    }

    private static DiscordRole ParseRole(JsonElement obj, DiscordGuild guild)
    {
        return new DiscordRole
        {
            Id = obj.GetProperty("id").GetDiscordId(),
            Name = GetStringOrNull(obj, "name") ?? string.Empty,
            Guild = guild,
            Color = GetIntOrNull(obj, "color") ?? 0,
            Position = GetIntOrNull(obj, "position") ?? 0,
            Permissions = GetIdOrZero(obj, "permissions")
        };
    }

    private static DiscordChannel ParseChannel(JsonElement obj, ulong? guildId)
    {
        ChannelPermissionOverwrite[]? overwrites = null;
        if (obj.TryGetProperty("permission_overwrites", out JsonElement overwritesEl) && overwritesEl.ValueKind == JsonValueKind.Array)
        {
            overwrites = overwritesEl.EnumerateArray()
                .Select(static o => new ChannelPermissionOverwrite
                {
                    Id = o.GetProperty("id").GetDiscordId(),
                    Type = GetIntOrNull(o, "type") ?? 0,
                    Allow = GetIdOrZero(o, "allow"),
                    Deny = GetIdOrZero(o, "deny")
                })
                .ToArray();
        }

        return new DiscordChannel
        {
            Id = obj.GetProperty("id").GetDiscordId(),
            Name = GetStringOrNull(obj, "name") ?? string.Empty,
            Type = GetIntOrNull(obj, "type") ?? 0,
            Parent_Id = GetIdOrNull(obj, "parent_id"),
            Guild_Id = guildId ?? GetIdOrNull(obj, "guild_id"),
            Permission_Overwrites = overwrites,
            Flags = GetIntOrNull(obj, "flags"),
            Owner_Id = GetIdOrNull(obj, "owner_id"),
            Thread_Metadata = ParseThreadMetadata(obj),
            Available_Tags = ParseForumTags(obj),
            Applied_Tags = obj.TryGetProperty("applied_tags", out JsonElement tags) ? ParseIdArray(obj, "applied_tags") : null
        };
    }

    private static DiscordChannel[]? ParseChannels(JsonElement parent, string property, ulong guildId)
    {
        if (!parent.TryGetProperty(property, out JsonElement channels) || channels.ValueKind != JsonValueKind.Array)
            return null;

        return channels.EnumerateArray().Select(c => ParseChannel(c, guildId)).ToArray();
    }

    private static DiscordEmoji[]? ParseEmojis(JsonElement parent)
    {
        if (!parent.TryGetProperty("emojis", out JsonElement emojis) || emojis.ValueKind != JsonValueKind.Array)
            return null;

        return emojis.EnumerateArray()
            .Select(static e => new DiscordEmoji
            {
                Id = GetStringOrNull(e, "id"),
                Name = GetStringOrNull(e, "name"),
                Animated = e.TryGetProperty("animated", out JsonElement animated) && animated.ValueKind == JsonValueKind.True
            })
            .ToArray();
    }

    private DiscordGuild ParseGuild(JsonElement data)
    {
        DiscordGuild guild = data.Deserialize<DiscordGuild>(json)
            ?? throw new JsonException("Guild payload was null.");

        if (guild.Roles is not null)
        {
            foreach (DiscordRole role in guild.Roles)
            {
                role.Guild = guild;
            }
        }
        return guild;
    }

    /// <summary>
    /// Reads the command name, subcommand path, options, target and resolved data shared by
    /// slash commands, context menus and autocomplete interactions.
    /// </summary>
    private ApplicationCommandData ParseCommandData(JsonElement data, ulong guildId)
    {
        string name = data.GetProperty("name").GetString()!;
        string? subcommandGroup = null;
        string? subcommand = null;
        List<InteractionOption> options = [];

        if (data.TryGetProperty("options", out JsonElement level) && level.ValueKind == JsonValueKind.Array)
        {
            // Option type 2 is a subcommand group and type 1 is a subcommand. Each nests the next level.
            if (TryGetFirstOptionOfType(level, 2, out JsonElement group))
            {
                subcommandGroup = group.GetProperty("name").GetString();
                level = GetNestedOptions(group);
            }

            if (TryGetFirstOptionOfType(level, 1, out JsonElement sub))
            {
                subcommand = sub.GetProperty("name").GetString();
                level = GetNestedOptions(sub);
            }

            ReadOptions(options, level);
        }

        return new ApplicationCommandData
        {
            Name = name,
            SubcommandGroup = subcommandGroup,
            Subcommand = subcommand,
            Options = options,
            TargetId = GetStringOrNull(data, "target_id"),
            Resolved = ParseResolved(data, guildId)
        };
    }

    private static bool TryGetFirstOptionOfType(JsonElement options, int type, out JsonElement option)
    {
        if (options.ValueKind == JsonValueKind.Array && options.GetArrayLength() > 0 && GetIntOrNull(options[0], "type") == type)
        {
            option = options[0];
            return true;
        }
        option = default;
        return false;
    }

    private static JsonElement GetNestedOptions(JsonElement option)
        => option.TryGetProperty("options", out JsonElement nested) ? nested : default;

    private static void ReadOptions(List<InteractionOption> target, JsonElement options)
    {
        if (options.ValueKind != JsonValueKind.Array) return;

        foreach (JsonElement option in options.EnumerateArray())
        {
            string? text = null;
            long? integer = null;
            double? number = null;
            bool? boolean = null;
            bool focused = option.TryGetProperty("focused", out JsonElement focusedEl) && focusedEl.ValueKind == JsonValueKind.True;

            if (option.TryGetProperty("value", out JsonElement value))
            {
                switch (value.ValueKind)
                {
                    case JsonValueKind.String:
                        text = value.GetString();
                        break;
                    case JsonValueKind.Number:
                        number = value.GetDouble();
                        if (value.TryGetInt64(out long whole)) integer = whole;
                        // A focused number is still being typed, and autocomplete reads it as text
                        if (focused) text = value.GetRawText();
                        break;
                    case JsonValueKind.True:
                        boolean = true;
                        break;
                    case JsonValueKind.False:
                        boolean = false;
                        break;
                }
            }

            target.Add(new InteractionOption
            {
                Name = option.GetProperty("name").GetString()!,
                String = text,
                Integer = integer,
                Number = number,
                Boolean = boolean,
                Focused = focused ? true : null
            });
        }
    }

    private ResolvedData? ParseResolved(JsonElement data, ulong guildId)
    {
        if (!data.TryGetProperty("resolved", out JsonElement resolved) || resolved.ValueKind != JsonValueKind.Object)
            return null;

        DiscordGuild guild = new() { Id = guildId, Name = string.Empty };
        Dictionary<string, DiscordUser>? users = null;
        Dictionary<string, DiscordMember>? members = null;
        Dictionary<string, DiscordRole>? roles = null;
        Dictionary<string, DiscordChannel>? channels = null;
        Dictionary<string, DiscordMessage>? messages = null;
        Dictionary<string, Attachment>? attachments = null;

        if (resolved.TryGetProperty("users", out JsonElement usersEl) && usersEl.ValueKind == JsonValueKind.Object)
        {
            users = new Dictionary<string, DiscordUser>(StringComparer.Ordinal);
            foreach (JsonProperty entry in usersEl.EnumerateObject())
            {
                users[entry.Name] = ParseUser(entry.Value);
            }
        }

        // Resolved members omit their user, which lives in the users map under the same id
        if (users is not null && resolved.TryGetProperty("members", out JsonElement membersEl) && membersEl.ValueKind == JsonValueKind.Object)
        {
            members = new Dictionary<string, DiscordMember>(StringComparer.Ordinal);
            foreach (JsonProperty entry in membersEl.EnumerateObject())
            {
                if (users.TryGetValue(entry.Name, out DiscordUser? user))
                {
                    members[entry.Name] = ParseMember(entry.Value, user, guild);
                }
            }
        }

        if (resolved.TryGetProperty("roles", out JsonElement rolesEl) && rolesEl.ValueKind == JsonValueKind.Object)
        {
            roles = new Dictionary<string, DiscordRole>(StringComparer.Ordinal);
            foreach (JsonProperty entry in rolesEl.EnumerateObject())
            {
                roles[entry.Name] = ParseRole(entry.Value, guild);
            }
        }

        if (resolved.TryGetProperty("channels", out JsonElement channelsEl) && channelsEl.ValueKind == JsonValueKind.Object)
        {
            channels = new Dictionary<string, DiscordChannel>(StringComparer.Ordinal);
            foreach (JsonProperty entry in channelsEl.EnumerateObject())
            {
                channels[entry.Name] = ParseChannel(entry.Value, guildId == 0 ? null : guildId);
            }
        }

        if (resolved.TryGetProperty("messages", out JsonElement messagesEl) && messagesEl.ValueKind == JsonValueKind.Object)
        {
            messages = new Dictionary<string, DiscordMessage>(StringComparer.Ordinal);
            foreach (JsonProperty entry in messagesEl.EnumerateObject())
            {
                DiscordMessage? message = entry.Value.Deserialize<DiscordMessage>(json);
                if (message is not null) messages[entry.Name] = message;
            }
        }

        if (resolved.TryGetProperty("attachments", out JsonElement attachmentsEl) && attachmentsEl.ValueKind == JsonValueKind.Object)
        {
            attachments = new Dictionary<string, Attachment>(StringComparer.Ordinal);
            foreach (JsonProperty entry in attachmentsEl.EnumerateObject())
            {
                Attachment? attachment = entry.Value.Deserialize<Attachment>(json);
                if (attachment is not null) attachments[entry.Name] = attachment;
            }
        }

        return new ResolvedData
        {
            Users = users,
            Members = members,
            Roles = roles,
            Channels = channels,
            Messages = messages,
            Attachments = attachments
        };
    }
}
