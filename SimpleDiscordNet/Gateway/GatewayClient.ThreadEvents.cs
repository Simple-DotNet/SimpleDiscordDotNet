using System.Globalization;
using System.Text.Json;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Events;
using SimpleDiscordNet.Models;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    internal const int RequestGuildMembersOpcode = 8;

    public event EventHandler<ChannelPinsUpdateEvent>? ChannelPinsUpdate;
    public event EventHandler<ThreadMemberUpdateEvent>? ThreadMemberUpdate;
    public event EventHandler<ThreadMembersUpdateEvent>? ThreadMembersUpdate;
    public event EventHandler<ThreadListSyncEvent>? ThreadListSync;
    public event EventHandler<GatewayRateLimitedEvent>? GatewayRateLimited;

    private static ulong[] ParseIdArray(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out JsonElement ids) || ids.ValueKind != JsonValueKind.Array)
            return [];

        return ids.EnumerateArray().Select(static id => id.GetDiscordId()).ToArray();
    }

    private void TryEmitChannelPinsUpdate(JsonElement data)
    {
        try
        {
            string? lastPin = GetStringOrNull(data, "last_pin_timestamp");
            ChannelPinsUpdate?.Invoke(this, new ChannelPinsUpdateEvent
            {
                ChannelId = data.GetProperty("channel_id").GetDiscordId(),
                GuildId = GetIdOrNull(data, "guild_id"),
                LastPinTimestamp = DateTimeOffset.TryParse(lastPin, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset timestamp) ? timestamp : null
            });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitThreadMemberUpdate(JsonElement data)
    {
        try
        {
            ThreadMemberUpdate?.Invoke(this, new ThreadMemberUpdateEvent
            {
                ThreadId = data.GetProperty("id").GetDiscordId(),
                GuildId = data.GetProperty("guild_id").GetDiscordId(),
                UserId = GetIdOrZero(data, "user_id")
            });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitThreadMembersUpdate(JsonElement data)
    {
        try
        {
            ulong[] added = data.TryGetProperty("added_members", out JsonElement members) && members.ValueKind == JsonValueKind.Array
                ? members.EnumerateArray().Select(static member => GetIdOrZero(member, "user_id")).ToArray()
                : [];

            ThreadMembersUpdate?.Invoke(this, new ThreadMembersUpdateEvent
            {
                ThreadId = data.GetProperty("id").GetDiscordId(),
                GuildId = data.GetProperty("guild_id").GetDiscordId(),
                MemberCount = GetIntOrNull(data, "member_count") ?? 0,
                AddedUserIds = added,
                RemovedUserIds = ParseIdArray(data, "removed_member_ids")
            });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitThreadListSync(JsonElement data)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ThreadListSync?.Invoke(this, new ThreadListSyncEvent
            {
                GuildId = guildId,
                ChannelIds = data.TryGetProperty("channel_ids", out _) ? ParseIdArray(data, "channel_ids") : null,
                Threads = ParseChannels(data, "threads", guildId) ?? []
            });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitGatewayRateLimited(JsonElement data)
    {
        try
        {
            double seconds = data.TryGetProperty("retry_after", out JsonElement retryAfter) && retryAfter.ValueKind == JsonValueKind.Number
                ? retryAfter.GetDouble()
                : 0;

            GatewayRateLimited?.Invoke(this, new GatewayRateLimitedEvent
            {
                Opcode = GetIntOrNull(data, "opcode") ?? 0,
                RetryAfter = TimeSpan.FromSeconds(seconds),
                GuildId = data.TryGetProperty("meta", out JsonElement meta) && meta.ValueKind == JsonValueKind.Object ? GetIdOrNull(meta, "guild_id") : null
            });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    /// <summary>
    /// Collects submitted values from a modal. Fields sit inside action rows in older modals and labels in newer ones.
    /// </summary>
    private static void CollectModalInputs(JsonElement node, List<TextInputValue> inputs)
    {
        if (node.TryGetProperty("components", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in children.EnumerateArray())
                CollectModalInputs(child, inputs);
        }

        if (node.TryGetProperty("component", out JsonElement inner) && inner.ValueKind == JsonValueKind.Object)
            CollectModalInputs(inner, inputs);

        // The modal itself also has a custom_id, but no "type" field at this level
        string? customId = GetStringOrNull(node, "custom_id");
        if (string.IsNullOrEmpty(customId) || !node.TryGetProperty("type", out _))
            return;

        string? value = null;
        if (node.TryGetProperty("value", out JsonElement valueElement))
        {
            value = valueElement.ValueKind switch
            {
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                JsonValueKind.String => valueElement.GetString(),
                _ => null
            };
        }

        string[] values = node.TryGetProperty("values", out JsonElement valuesElement) && valuesElement.ValueKind == JsonValueKind.Array
            ? valuesElement.EnumerateArray().Select(static v => v.GetString() ?? string.Empty).ToArray()
            : [];

        inputs.Add(new TextInputValue { CustomId = customId, Value = value, Values = values });
    }

    private static ForumTag[]? ParseForumTags(JsonElement channel)
    {
        if (!channel.TryGetProperty("available_tags", out JsonElement tags) || tags.ValueKind != JsonValueKind.Array)
            return null;

        return tags.EnumerateArray()
            .Select(static tag => new ForumTag
            {
                Id = tag.GetProperty("id").GetDiscordId(),
                Name = GetStringOrNull(tag, "name") ?? string.Empty,
                Moderated = GetBoolOrNull(tag, "moderated") ?? false,
                Emoji_Id = GetIdOrNull(tag, "emoji_id"),
                Emoji_Name = GetStringOrNull(tag, "emoji_name")
            })
            .ToArray();
    }

    private static ThreadMetadata? ParseThreadMetadata(JsonElement channel)
    {
        if (!channel.TryGetProperty("thread_metadata", out JsonElement metadata) || metadata.ValueKind != JsonValueKind.Object)
            return null;

        return new ThreadMetadata
        {
            Archived = GetBoolOrNull(metadata, "archived") ?? false,
            Auto_Archive_Duration = GetIntOrNull(metadata, "auto_archive_duration") ?? 0,
            Archive_Timestamp = DateTimeOffset.TryParse(GetStringOrNull(metadata, "archive_timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset archived) ? archived : null,
            Locked = GetBoolOrNull(metadata, "locked") ?? false,
            Invitable = GetBoolOrNull(metadata, "invitable")
        };
    }
}
