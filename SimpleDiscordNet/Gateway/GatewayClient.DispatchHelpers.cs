using System.Text.Json;
using System.Globalization;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Events;

namespace SimpleDiscordNet.Gateway;

internal sealed partial class GatewayClient
{
    private void TryEmitChannelEvent(JsonElement data, EventHandler<DiscordChannel>? evt)
    {
        try
        {
            // Ignore if not a guild channel (e.g., DM has no guild_id)
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            ulong id = data.GetProperty("id").GetDiscordId();
            string name = data.TryGetProperty("name", out JsonElement n) ? (n.GetString() ?? string.Empty) : string.Empty;
            int type = data.TryGetProperty("type", out JsonElement t) ? t.GetInt32() : 0;
            ulong? parent = data.TryGetProperty("parent_id", out JsonElement p) && p.ValueKind != JsonValueKind.Null ? p.GetDiscordIdNullable() : null;
            ulong? guildId = gidProp.GetDiscordId();

            DiscordChannel ch = new()
            {
                Id = id,
                Name = name,
                Type = type,
                Parent_Id = parent,
                Guild_Id = guildId
            };
            evt?.Invoke(this, ch);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitMemberEvent(JsonElement data, EventHandler<GatewayMemberEvent>? evt)
    {
        try
        {
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            ulong guildId = gidProp.GetDiscordId();
            DiscordUser user = ParseUser(data.GetProperty("user"));
            ulong[] roles = data.TryGetProperty("roles", out JsonElement r) && r.ValueKind == JsonValueKind.Array
                ? r.EnumerateArray().Select(static x => x.GetDiscordId()).ToArray()
                : [];
            string? nick = data.TryGetProperty("nick", out JsonElement n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null;

            // Create placeholder guild (will be replaced with actual guild in cache)
            DiscordGuild guild = new() { Id = guildId, Name = string.Empty };
            DiscordMember member = new() { User = user, Guild = guild, Nick = nick, Roles = roles };
            evt?.Invoke(this, new GatewayMemberEvent { GuildId = guildId, Member = member });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitMemberRemoveEvent(JsonElement data, EventHandler<GatewayMemberEvent>? evt)
    {
        try
        {
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            ulong guildId = gidProp.GetDiscordId();
            DiscordUser user = ParseUser(data.GetProperty("user"));
            // Create placeholder guild (member is being removed anyway)
            DiscordGuild guild = new() { Id = guildId, Name = string.Empty };
            DiscordMember member = new() { User = user, Guild = guild, Nick = null, Roles = [] };
            evt?.Invoke(this, new GatewayMemberEvent { GuildId = guildId, Member = member });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitBanEvent(JsonElement data, EventHandler<GatewayUserEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            DiscordUser user = ParseUser(data.GetProperty("user"));
            evt?.Invoke(this, new GatewayUserEvent { GuildId = guildId, User = user });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitRoleEvent(JsonElement data, EventHandler<GatewayRoleEvent>? evt)
    {
        try
        {
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            ulong guildId = gidProp.GetDiscordId();
            JsonElement roleData = data.GetProperty("role");

            // Create placeholder guild (will be replaced with actual guild in cache)
            DiscordGuild guild = new() { Id = guildId, Name = string.Empty };

            DiscordRole role = new()
            {
                Id = roleData.GetProperty("id").GetDiscordId(),
                Name = roleData.GetProperty("name").GetString() ?? string.Empty,
                Guild = guild,
                Color = roleData.TryGetProperty("color", out JsonElement c) ? c.GetInt32() : 0,
                Position = roleData.TryGetProperty("position", out JsonElement p) ? p.GetInt32() : 0,
                Permissions = roleData.TryGetProperty("permissions", out JsonElement perms) ? perms.GetDiscordId() : 0UL
            };
            evt?.Invoke(this, new GatewayRoleEvent { GuildId = guildId, Role = role });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitRoleDeleteEvent(JsonElement data, EventHandler<GatewayRoleEvent>? evt)
    {
        try
        {
            if (!data.TryGetProperty("guild_id", out JsonElement gidProp)) return;
            ulong guildId = gidProp.GetDiscordId();
            ulong roleId = data.GetProperty("role_id").GetDiscordId();

            // Create placeholder guild (role is being deleted anyway)
            DiscordGuild guild = new() { Id = guildId, Name = string.Empty };

            DiscordRole role = new()
            {
                Id = roleId,
                Name = string.Empty,
                Guild = guild
            };
            evt?.Invoke(this, new GatewayRoleEvent { GuildId = guildId, Role = role });
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitMessageUpdateEvent(JsonElement data, EventHandler<MessageUpdateEvent>? evt)
    {
        try
        {
            ulong messageId = data.GetProperty("id").GetDiscordId();
            ulong channelId = data.GetProperty("channel_id").GetDiscordId();
            ulong? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetDiscordIdNullable() : null;
            string? content = data.TryGetProperty("content", out JsonElement c) ? c.GetString() : null;
            DateTimeOffset? editedTimestamp = data.TryGetProperty("edited_timestamp", out JsonElement et) && et.ValueKind != JsonValueKind.Null
                ? DateTimeOffset.Parse(et.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null;

            MessageUpdateEvent e = new()
            {
                MessageId = messageId,
                ChannelId = channelId,
                GuildId = guildId,
                Content = content,
                EditedTimestamp = editedTimestamp
            };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitMessageDeleteEvent(JsonElement data, EventHandler<MessageEvent>? evt)
    {
        try
        {
            ulong messageId = data.GetProperty("id").GetDiscordId();
            ulong channelId = data.GetProperty("channel_id").GetDiscordId();
            ulong? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetDiscordIdNullable() : null;

            MessageEvent e = new()
            {
                MessageId = messageId,
                ChannelId = channelId,
                GuildId = guildId
            };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitMessageDeleteBulkEvent(JsonElement data, EventHandler<MessageEvent>? evt)
    {
        try
        {
            // For bulk delete, we emit one event per message
            if (data.TryGetProperty("ids", out JsonElement ids) && ids.ValueKind == JsonValueKind.Array)
            {
                ulong channelId = data.GetProperty("channel_id").GetDiscordId();
                ulong? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetDiscordIdNullable() : null;

                foreach (JsonElement id in ids.EnumerateArray())
                {
                    MessageEvent e = new()
                    {
                        MessageId = id.GetDiscordId(),
                        ChannelId = channelId,
                        GuildId = guildId
                    };
                    evt?.Invoke(this, e);
                }
            }
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitReactionEvent(JsonElement data, EventHandler<ReactionEvent>? evt)
    {
        try
        {
            ulong userId = data.GetProperty("user_id").GetDiscordId();
            ulong channelId = data.GetProperty("channel_id").GetDiscordId();
            ulong messageId = data.GetProperty("message_id").GetDiscordId();
            ulong? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetDiscordIdNullable() : null;

            JsonElement emojiData = data.GetProperty("emoji");
            string? emojiId = emojiData.TryGetProperty("id", out JsonElement eid) ? eid.GetString() : null;
            string? emojiName = emojiData.TryGetProperty("name", out JsonElement en) ? en.GetString() : null;

            DiscordEmoji emoji = new() { Id = emojiId, Name = emojiName };

            ReactionEvent e = new()
            {
                UserId = userId,
                ChannelId = channelId,
                MessageId = messageId,
                GuildId = guildId,
                Emoji = emoji
            };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitReactionRemoveEmojiEvent(JsonElement data, EventHandler<ReactionEvent>? evt)
    {
        try
        {
            ulong channelId = data.GetProperty("channel_id").GetDiscordId();
            ulong messageId = data.GetProperty("message_id").GetDiscordId();
            ulong? guildId = data.TryGetProperty("guild_id", out JsonElement gid) ? gid.GetDiscordIdNullable() : null;

            JsonElement emojiData = data.GetProperty("emoji");
            string? emojiId = emojiData.TryGetProperty("id", out JsonElement eid) ? eid.GetString() : null;
            string? emojiName = emojiData.TryGetProperty("name", out JsonElement en) ? en.GetString() : null;

            DiscordEmoji emoji = new() { Id = emojiId, Name = emojiName };

            ReactionEvent e = new()
            {
                UserId = 0UL, // No specific user for this event
                ChannelId = channelId,
                MessageId = messageId,
                GuildId = guildId,
                Emoji = emoji
            };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private static DiscordUser ParseUser(JsonElement obj)
    {
        return new DiscordUser
        {
            Id = obj.GetProperty("id").GetDiscordId(),
            Username = obj.TryGetProperty("username", out JsonElement un) ? (un.GetString() ?? string.Empty) : string.Empty,
            Bot = obj.TryGetProperty("bot", out JsonElement botEl) && botEl.ValueKind == JsonValueKind.True ? true : null
        };
    }

    // New event emission methods for missing events
    private void TryEmitAutoModerationRuleCreatedEvent(JsonElement data, EventHandler<AutoModerationRuleCreatedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong ruleId = data.GetProperty("id").GetDiscordId();
            AutoModerationRuleCreatedEvent e = new() { GuildId = guildId, RuleId = ruleId, Rule = null };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitAutoModerationRuleUpdatedEvent(JsonElement data, EventHandler<AutoModerationRuleUpdatedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong ruleId = data.GetProperty("id").GetDiscordId();
            AutoModerationRuleUpdatedEvent e = new() { GuildId = guildId, RuleId = ruleId, Rule = null };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitAutoModerationRuleDeletedEvent(JsonElement data, EventHandler<AutoModerationRuleDeletedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong ruleId = data.GetProperty("id").GetDiscordId();
            AutoModerationRuleDeletedEvent e = new() { GuildId = guildId, RuleId = ruleId, Rule = null };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitAutoModerationActionExecutionEvent(JsonElement data, EventHandler<AutoModerationActionExecutionEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong ruleId = data.GetProperty("rule_id").GetDiscordId();
            ulong userId = data.GetProperty("user_id").GetDiscordId();
            ulong channelId = data.GetProperty("channel_id").GetDiscordId();
            ulong? messageId = data.TryGetProperty("message_id", out JsonElement m) ? m.GetDiscordIdNullable() : null;
            AutoModerationActionExecutionEvent e = new()
            {
                GuildId = guildId,
                RuleId = ruleId,
                UserId = userId,
                ChannelId = channelId,
                MessageId = messageId,
                Action = null
            };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitStageInstanceCreatedEvent(JsonElement data, EventHandler<StageInstanceCreatedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong stageInstanceId = data.GetProperty("id").GetDiscordId();
            ulong channelId = data.GetProperty("channel_id").GetDiscordId();
            string? topic = data.TryGetProperty("topic", out JsonElement t) ? t.GetString() : null;
            int privacyLevel = data.TryGetProperty("privacy_level", out JsonElement pl) ? pl.GetInt32() : 0;
            StageInstanceCreatedEvent e = new() { GuildId = guildId, StageInstanceId = stageInstanceId, ChannelId = channelId, Topic = topic, PrivacyLevel = privacyLevel };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitStageInstanceUpdatedEvent(JsonElement data, EventHandler<StageInstanceUpdatedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong stageInstanceId = data.GetProperty("id").GetDiscordId();
            ulong channelId = data.GetProperty("channel_id").GetDiscordId();
            string? topic = data.TryGetProperty("topic", out JsonElement t) ? t.GetString() : null;
            int privacyLevel = data.TryGetProperty("privacy_level", out JsonElement pl) ? pl.GetInt32() : 0;
            StageInstanceUpdatedEvent e = new() { GuildId = guildId, StageInstanceId = stageInstanceId, ChannelId = channelId, Topic = topic, PrivacyLevel = privacyLevel };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitStageInstanceDeletedEvent(JsonElement data, EventHandler<StageInstanceDeletedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong stageInstanceId = data.GetProperty("id").GetDiscordId();
            ulong channelId = data.GetProperty("channel_id").GetDiscordId();
            string? topic = data.TryGetProperty("topic", out JsonElement t) ? t.GetString() : null;
            int privacyLevel = data.TryGetProperty("privacy_level", out JsonElement pl) ? pl.GetInt32() : 0;
            StageInstanceDeletedEvent e = new() { GuildId = guildId, StageInstanceId = stageInstanceId, ChannelId = channelId, Topic = topic, PrivacyLevel = privacyLevel };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitGuildScheduledEventCreatedEvent(JsonElement data, EventHandler<GuildScheduledEventCreatedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong eventId = data.GetProperty("id").GetDiscordId();
            string name = data.GetProperty("name").GetString() ?? string.Empty;
            DateTimeOffset startTime = DateTimeOffset.Parse(data.GetProperty("scheduled_start_time").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            DateTimeOffset? endTime = data.TryGetProperty("scheduled_end_time", out JsonElement et) && et.ValueKind != JsonValueKind.Null
                ? DateTimeOffset.Parse(et.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null;
            string? description = data.TryGetProperty("description", out JsonElement d) ? d.GetString() : null;
            ulong? channelId = data.TryGetProperty("channel_id", out JsonElement c) && c.ValueKind != JsonValueKind.Null ? c.GetDiscordIdNullable() : null;
            GuildScheduledEventCreatedEvent e = new()
            {
                GuildId = guildId,
                EventId = eventId,
                Name = name,
                Description = description,
                ChannelId = channelId,
                StartTime = startTime,
                EndTime = endTime
            };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitGuildScheduledEventUpdatedEvent(JsonElement data, EventHandler<GuildScheduledEventUpdatedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong eventId = data.GetProperty("id").GetDiscordId();
            string name = data.GetProperty("name").GetString() ?? string.Empty;
            DateTimeOffset startTime = DateTimeOffset.Parse(data.GetProperty("scheduled_start_time").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            DateTimeOffset? endTime = data.TryGetProperty("scheduled_end_time", out JsonElement et) && et.ValueKind != JsonValueKind.Null
                ? DateTimeOffset.Parse(et.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null;
            string? description = data.TryGetProperty("description", out JsonElement d) ? d.GetString() : null;
            ulong? channelId = data.TryGetProperty("channel_id", out JsonElement c) && c.ValueKind != JsonValueKind.Null ? c.GetDiscordIdNullable() : null;
            GuildScheduledEventUpdatedEvent e = new()
            {
                GuildId = guildId,
                EventId = eventId,
                Name = name,
                Description = description,
                ChannelId = channelId,
                StartTime = startTime,
                EndTime = endTime
            };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitGuildScheduledEventDeletedEvent(JsonElement data, EventHandler<GuildScheduledEventDeletedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong eventId = data.GetProperty("id").GetDiscordId();
            string name = data.GetProperty("name").GetString() ?? string.Empty;
            string? description = data.TryGetProperty("description", out JsonElement d) ? d.GetString() : null;
            ulong? channelId = data.TryGetProperty("channel_id", out JsonElement c) && c.ValueKind != JsonValueKind.Null ? c.GetDiscordIdNullable() : null;
            GuildScheduledEventDeletedEvent e = new()
            {
                GuildId = guildId,
                EventId = eventId,
                Name = name,
                Description = description,
                ChannelId = channelId
            };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitGuildScheduledEventUserAddedEvent(JsonElement data, EventHandler<GuildScheduledEventUserAddedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong eventId = data.GetProperty("guild_scheduled_event_id").GetDiscordId();
            ulong userId = data.GetProperty("user_id").GetDiscordId();
            GuildScheduledEventUserAddedEvent e = new() { GuildId = guildId, EventId = eventId, UserId = userId };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitGuildScheduledEventUserRemovedEvent(JsonElement data, EventHandler<GuildScheduledEventUserRemovedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong eventId = data.GetProperty("guild_scheduled_event_id").GetDiscordId();
            ulong userId = data.GetProperty("user_id").GetDiscordId();
            GuildScheduledEventUserRemovedEvent e = new() { GuildId = guildId, EventId = eventId, UserId = userId };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitIntegrationCreatedEvent(JsonElement data, EventHandler<IntegrationCreatedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong integrationId = data.GetProperty("id").GetDiscordId();
            string name = data.GetProperty("name").GetString() ?? string.Empty;
            string type = data.GetProperty("type").GetString() ?? string.Empty;
            bool enabled = data.GetProperty("enabled").GetBoolean();
            IntegrationCreatedEvent e = new() { GuildId = guildId, IntegrationId = integrationId, Name = name, Type = type, Enabled = enabled };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitIntegrationUpdatedEvent(JsonElement data, EventHandler<IntegrationUpdatedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong integrationId = data.GetProperty("id").GetDiscordId();
            string name = data.GetProperty("name").GetString() ?? string.Empty;
            string type = data.GetProperty("type").GetString() ?? string.Empty;
            bool enabled = data.GetProperty("enabled").GetBoolean();
            IntegrationUpdatedEvent e = new() { GuildId = guildId, IntegrationId = integrationId, Name = name, Type = type, Enabled = enabled };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitIntegrationDeletedEvent(JsonElement data, EventHandler<IntegrationDeletedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong integrationId = data.GetProperty("id").GetDiscordId();
            ulong? applicationId = data.TryGetProperty("application_id", out JsonElement a) && a.ValueKind != JsonValueKind.Null ? a.GetDiscordIdNullable() : null;
            IntegrationDeletedEvent e = new() { GuildId = guildId, IntegrationId = integrationId, ApplicationId = applicationId };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitVoiceServerUpdateEvent(JsonElement data, EventHandler<VoiceServerUpdateEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            string token = data.GetProperty("token").GetString() ?? string.Empty;
            string endpoint = data.GetProperty("endpoint").GetString() ?? string.Empty;
            VoiceServerUpdateEvent ev = new() { GuildId = guildId, Token = token, Endpoint = endpoint };
            evt?.Invoke(this, ev);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitGuildJoinRequestCreatedEvent(JsonElement data, EventHandler<GuildJoinRequestCreatedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong userId = data.GetProperty("user_id").GetDiscordId();
            string status = data.GetProperty("status").GetString() ?? string.Empty;
            GuildJoinRequestCreatedEvent e = new() { GuildId = guildId, UserId = userId, Status = status };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitGuildJoinRequestUpdatedEvent(JsonElement data, EventHandler<GuildJoinRequestUpdatedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong userId = data.GetProperty("user_id").GetDiscordId();
            string status = data.GetProperty("status").GetString() ?? string.Empty;
            GuildJoinRequestUpdatedEvent e = new() { GuildId = guildId, UserId = userId, Status = status };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitGuildJoinRequestDeletedEvent(JsonElement data, EventHandler<GuildJoinRequestDeletedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong userId = data.GetProperty("user_id").GetDiscordId();
            GuildJoinRequestDeletedEvent e = new() { GuildId = guildId, UserId = userId };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitPollVoteAddedEvent(JsonElement data, EventHandler<PollVoteAddedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong userId = data.GetProperty("user_id").GetDiscordId();
            ulong channelId = data.GetProperty("channel_id").GetDiscordId();
            ulong messageId = data.GetProperty("message_id").GetDiscordId();
            ulong answerId = data.GetProperty("answer_id").GetDiscordId();
            PollVoteAddedEvent e = new() { GuildId = guildId, UserId = userId, ChannelId = channelId, MessageId = messageId, AnswerId = answerId };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }

    private void TryEmitPollVoteRemovedEvent(JsonElement data, EventHandler<PollVoteRemovedEvent>? evt)
    {
        try
        {
            ulong guildId = data.GetProperty("guild_id").GetDiscordId();
            ulong userId = data.GetProperty("user_id").GetDiscordId();
            ulong channelId = data.GetProperty("channel_id").GetDiscordId();
            ulong messageId = data.GetProperty("message_id").GetDiscordId();
            ulong answerId = data.GetProperty("answer_id").GetDiscordId();
            PollVoteRemovedEvent e = new() { GuildId = guildId, UserId = userId, ChannelId = channelId, MessageId = messageId, AnswerId = answerId };
            evt?.Invoke(this, e);
        }
        catch (Exception ex) { Error?.Invoke(this, ex); }
    }
}
