# SimpleDiscordDotNet

A lightweight, dependency-free Discord bot SDK for .NET 10 that provides direct access to Discord API v10 (REST + Gateway).

## Purpose

SimpleDiscordDotNet is designed for developers who want:
- **Zero dependencies** - BCL only, no external packages
- **Performance** - Memory-optimized with Span<T> and modern .NET 10 APIs for 30-50% less GC pressure
- **Simplicity** - Clean, approachable API with builder patterns
- **Modern C#** - Built for .NET 10 with C# 14 features and span-based APIs
- **Production-ready** - Advanced rate limiting, comprehensive error handling, and extensive API coverage

## Key Features

- ✅ Slash commands, components, and modals with attribute-based handlers
- ✅ Source generator for zero-reflection command/component discovery
- ✅ Ambient context for accessing cached guilds, channels, members, roles
- ✅ **Live observable collections** - Thread-safe `INotifyCollectionChanged` for WPF/MAUI/Avalonia UI binding
- ✅ Comprehensive gateway events for all entity changes
- ✅ Advanced rate limiting with bucket management and monitoring
- ✅ Full Discord API v10 support (messages, reactions, permissions, roles, channels, threads, etc.)
- ✅ Native AOT and trimming compatible
- ✅ Memory-optimized with `Span<T>`, `Memory<T>`, and zero-allocation APIs
- ✅ **Horizontal sharding** - 3 modes: single process, multi-shard, or distributed coordinator/worker

## Quick Example

```csharp
using SimpleDiscordNet;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Primitives;

public sealed class AppCommands
{
    [SlashCommand("hello", "Say hello")]
    public async Task HelloAsync(InteractionContext ctx)
    {
        var embed = new EmbedBuilder()
            .WithTitle("Hello!")
            .WithDescription("Welcome to SimpleDiscordDotNet")
            .WithColor(0x00FF00);

        await ctx.RespondAsync(embed: embed);
    }

    [SlashCommand("chart", "Send a chart image")]
    public async Task ChartAsync(InteractionContext ctx)
    {
        byte[] chartBytes = GenerateChart();

        // Auto-defers and sends file — no manual defer needed
        await ctx.RespondAsync("Here's the latest chart:",
            fileName: "chart.png",
            fileData: chartBytes);
    }

    [SlashCommand("userinfo", "Get user information")]
    public async Task UserInfoAsync(InteractionContext ctx)
    {
        var user = ctx.User;
        var member = ctx.Member;

        await ctx.RespondAsync($"Hello {user?.Username}! You joined this server on {member?.Joined_At}");
    }
}

var bot = DiscordBot.NewBuilder()
    .WithToken(Environment.GetEnvironmentVariable("DISCORD_TOKEN")!)
    .WithIntents(DiscordIntents.Guilds | DiscordIntents.GuildMessages)
    .Build();

await bot.StartAsync();
await Task.Delay(Timeout.Infinite);
```

## Documentation

**📖 Full documentation is available in the [Wiki](./wiki)**

- [Installation](./wiki/Installation.md) - Get started with NuGet or source reference
- [Getting Started](./wiki/Getting-Started.md) - Your first bot in minutes
- [Beginner's Guide](./wiki/Beginners-Guide.md) - **NEW!** Step-by-step guide for Discord bot beginners
- [Configuration](./wiki/Configuration.md) - Builder patterns, DI, intents
- [Commands](./wiki/Commands.md) - Slash commands, components, modals
- [Permissions](./wiki/Permissions.md) - **NEW v1.8.0!** Dual-layer command permissions
- [Moderation](./wiki/Moderation.md) - **NEW v1.8.0!** Bans, kicks, timeouts, voice control, audit logs
- [Working with Entities](./wiki/Entities.md) - Channels, guilds, members, messages, and roles
- [Events](./wiki/Events.md) - Gateway events and logging
- [Sharding](./wiki/Sharding.md) - Horizontal scaling with distributed sharding
- [API Reference](./wiki/API-Reference.md) - Complete API documentation
- [Rate Limit Monitoring](./wiki/Rate-Limit-Monitoring.md) - Advanced monitoring and analytics
- [FAQ](./wiki/FAQ.md) - Common questions and troubleshooting

## Installation

Install from NuGet:

```bash
dotnet add package SimpleDiscordDotNet
```

Or via Package Manager:

```powershell
Install-Package SimpleDiscordDotNet
```

## Requirements

- .NET SDK 10.0 or newer
- A Discord bot token from the [Discord Developer Portal](https://discord.com/developers/applications)
- Gateway intents configured as needed

## Contributing

Issues and pull requests are welcome! Please keep the code dependency-free and aligned with the existing style.

## Community

Join our Discord community for support, discussions, and updates: https://discord.gg/2KrUPCgh

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE) for details.

---

**Ready to build your Discord bot?** Head to the [Wiki](./wiki) to get started!

## Version History

### v1.10.9 - File Attachments on Interaction Responses & Followups
- **File Attachments on Responses** — `RespondAsync`, `FollowupAsync`, `EditFollowupAsync`, and `UpdateMessageAsync` now support file attachments via `MessageBuilder.AddFile()` or direct `fileName`/`fileData` parameters. The SDK automatically defers then sends files as followups when used on initial interaction responses (Discord does not support files on type 4/7 callbacks).
- **File Attachments on Followups & Edits** — `FollowupAsync(MessageBuilder)`, `EditFollowupAsync(string, MessageBuilder)`, and `EditOriginalResponseAsync(MessageBuilder)` new overloads support multipart file uploads via webhook endpoints, including PATCH multipart for editing messages with attachments.
- **`allowed_mentions` Propagation** — `InteractionResponseData` and `WebhookMessageRequest` now include `allowed_mentions` from `MessagePayload`, fixing silent data loss when using `MessageBuilder.WithMention()` on interaction responses.
- **Auto-Defer Detection** — `RespondAsync` and `UpdateMessageAsync` now automatically defer (type 5/type 6) when file attachments are present on the initial response path. The user never needs to manually defer just to send files. `RestClient.SendMultipartAsync` refactored to accept `HttpMethod` for PATCH multipart support.
- **`[Ephemeral]` Propagation** — The `[Ephemeral]` attribute now properly propagates to followup messages. When a handler has `[Ephemeral]`, followups sent via `FollowupAsync`/`RespondAsync` default to ephemeral without requiring explicit `ephemeral: true` on every call. Use `ephemeral: false` to override per-message. `FollowupAsync` now accepts `bool? ephemeral` for nullable semantics.
- **AOT Serialization Fixes** — Added missing `[JsonSerializable]` registrations: `DiscordMessage[]`, `IEnumerable<DiscordBan>`, `int?`. New typed entities replace `object`/`object[]` deserialization: `DiscordWebhook`, `DiscordSticker`, `DiscordInvite` with array variants. Webhook/Sticker/Invite API methods now return strongly-typed entities instead of `IEnumerable<object>` or `Task<object?>`.
- ✅ **0 breaking changes** — All new parameters are optional with defaults. `Task` return types unchanged. `bool` → `bool?` implicit conversion compatible.

### v1.10.8 - Component Serialization Fix for .NET 10
- **Polymorphic Serialization Fix** — `IComponent` implementations (`ActionRow`, `Button`, `StringSelect`, `TextInput`, `UserSelect`, `RoleSelect`, `MentionableSelect`, `ChannelSelect`) now use `[JsonIgnore]` on the `type` property, resolving an `InvalidOperationException` crash caused by a metadata name conflict with the `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` discriminator in .NET 10.

### v1.10.7 - Gateway Hardening, Missing Event Wiring & Presence Fix
- **Gateway Reliability** — WebSocket write serialization via `SemaphoreSlim` prevents concurrent `SendAsync` race causing disconnects. `_ws` field made `volatile`; all send/receive methods capture `_ws` locally eliminating TOCTOU races. `ConnectSocketAsync` defers `_ws` assignment until connection succeeds. State checks moved inside write lock.
- **Missing Events Wired** — 26 dispatch cases added to `HandleDispatch` for previously-dead gateway events: `VoiceStateUpdate`, `PresenceUpdate`, `TypingStart`, `WebhooksUpdate`, `InviteCreate`, `InviteDelete`, `GuildIntegrationsUpdate`, `AutoModeration*`, `StageInstance*`, `GuildScheduledEvent*`, `Integration*`, `VoiceServerUpdate`, `GuildJoinRequest*`, `PollVote*`. 7 new `TryEmit*` helpers + 20 handler methods wired through `DiscordBot`, `ShardManager`, and `DiscordEvents`.
- **Presence Fix** — `UpdatePresencePayload.since` uses `[JsonIgnore(Condition = Never)]` — Discord requires this field present even when `null`. `BotActivity.url` uses `[JsonIgnore(Condition = WhenWritingNull)]` to omit `null` for non-streaming activities. `DefaultIgnoreCondition = WhenWritingNull` added to source-gen options.
- **AOT Serialization** — `ApplicationCommandDefinition[]` no longer boxed to `object[]` (caused 4002 errors). All command sync paths use strongly-typed arrays registered in source-gen context.
- **Diagnostics** — Close frame reason now logged via `Error` event. `Disconnected` event fires before auto-reconnect. Added `WithGatewayDebug()` opt-in diagnostic logging for all outgoing gateway payloads. `EnableGatewayDebug` added to `DiscordBotOptions`.
- **API Enhancements** — Optional `status` parameter added to `SetGameAsync`, `SetWatchingAsync`, `SetListeningAsync`, `SetStreamingAsync`, `SetCompetingAsync`. `UpdatePresenceAsync` and `RequestGuildMembersAsync` now fire `Error` instead of silently returning. `ActivityType` enum gap at value 4 documented.

### v1.10.0 - Comprehensive Bug Fixes & Hardening Pass
- **Rate Limiter** — Route-to-bucketId mapping fixed (rate limiting was non-functional). Semaphore deadlock eliminated. Sync-over-async removed. `Handle429Async` uses `Max(existing, new)` for reset time.
- **Gateway Reliability** — `async void` heartbeat → `async Task` loop. `ReceiveLoop` uses while-loop instead of recursion; no longer dies on reconnect contention. `_ctsHeartbeat` protected by lock. Author parsing handles webhook messages. `Dispose` sets `_autoReconnect=false`.
- **AOT Compatibility** — Zero anonymous types in any serialization path. `IComponent` gets `[JsonPolymorphic]+[JsonDerivedType]` for all 8 implementations. 30+ new `[JsonSerializable]` registrations. All `object`/`object[]` payload properties replaced with typed alternatives. `[UnconditionalSuppressMessage]` checkIds fixed.
- **Memory Safety** — `_users` LRU eviction with stale-entry drain (max 100k). Event handler leaks closed in `Shard`, `DiscordBot`. `ObservableConcurrentList` now disposes `ReaderWriterLockSlim` on replacement.
- **Thread Safety** — Dozens of race conditions fixed: `_ctsHeartbeat` TOCTOU, `PeerNode.AssignedShards` concurrent reads via `GetShardsSnapshot()`, `EntityCache` role array writes under lock, `RemoveChannel`/`RemoveMember` use atomic `Remove()`, `_started` atomic gate, `DisposeAsync` re-entrancy guard.
- **Autocomplete** — `InteractionOption.Focused` parsed from gateway. Handler key includes subcommand group/command path. `AutocompleteHandler` uses primary constructor. Generator respects `HasContext` flag, emits per-subcommand permissions, SDN003 duplicate diagnostic.
- **InteractionContext** — Double-acknowledge guard via `_responded`. `_deferred`/`_deferredUpdate` made volatile. `UpdateMessageAsync` routes to edit-original when type-5 deferred. `_responded` reset on defer failure.
- **Performance** — ChannelId ulong cached; O(1) option lookup dictionary. `EntityCache` O(1) channel/role secondary indices. `SnapshotUsers` O(N*M)→O(N). Prefix matching prefers longest match.
- **Cleanup** — Dead fields/classes removed (`_heartbeatTimer`, `PendingRequest`, `RequestMetrics` timer). `DiscordEvents.UnsubscribeAll()` helper. `SendMessageWithButtonsAsync` accepts `CancellationToken`.
- **Sharding** — `ShardCoordinator.HandleResumptionAsync` calls `StopAsync` after handoff. Timer disposal uses `Dispose(WaitHandle)`. Assignment operations protected by `_assignmentLock`.

### v1.9.0 - Context Menus, Autocomplete, Presence & Full API Coverage
- **Presence/Status API** — `SetGameAsync("Minecraft")`, `SetWatchingAsync("YouTube")`, `SetListeningAsync("Spotify")`, `SetStreamingAsync("Live!", url)`, `SetCompetingAsync("tournament")`, `SetStatusAsync(PresenceStatus.DoNotDisturb)`
- **Context Menu Commands** — `[UserContextMenu("Info")]` and `[MessageContextMenu("Report")]` attributes
- **Autocomplete Handlers** — `[Autocomplete("command_name", "option_name")]` with full interaction routing
- **Global Command Sync** — `bot.SyncGlobalCommandsAsync()`
- **Guild Management** — `ModifyGuildAsync`, `LeaveGuildAsync`, `PruneMembersAsync`, `GetPruneCountAsync`
- **Webhook Management** — `CreateWebhookAsync`, `GetChannelWebhooksAsync`, `ModifyWebhookAsync`, `DeleteWebhookAsync`
- **Emoji & Sticker Management** — `CreateEmojiAsync`, `ModifyEmojiAsync`, `DeleteEmojiAsync`, `GetGuildStickersAsync`, sticker CRUD
- **Invite Management** — `CreateInviteAsync`, `GetChannelInvitesAsync`, `GetGuildInvitesAsync`
- **Message Replies & Forum Posts** — `builder.WithReply(messageId)`, `builder.WithForumPost(title, tags)`, `channel.ReplyAsync()`
- **Bot User Modify** — `bot.ModifyCurrentUserAsync(username:, avatarBase64:)`
- **Interaction Followup Editing** — `GetOriginalResponseAsync`, `EditOriginalResponseAsync`, `DeleteOriginalResponseAsync`, `EditFollowupAsync`, `DeleteFollowupAsync`
- **ulong Overloads** — All ID-accepting methods now support both `string` and `ulong`
- **Stage Instances, Scheduled Events, Auto-Mod Rules** — Full CRUD for all remaining Discord REST endpoints
- **Bug Fix** — `LoadCompleteGuildDataAsync` NRE in sharded modes (SingleProcess + Distributed worker)
- **Source Generator** — Extended to handle context menu commands and autocomplete handlers
- ✅ **Voice control operations** - Deafen/undeafen members, move between channels, disconnect from voice
  - `bot.DeafenMemberAsync()`, `bot.UndeafenMemberAsync()`
  - `bot.MoveMemberToVoiceChannelAsync()`, `bot.DisconnectMemberFromVoiceAsync()`
- ✅ **Ban management** - Retrieve guild bans with reasons
  - `bot.GetGuildBansAsync()` - Get all bans
  - `bot.GetGuildBanAsync()` - Get specific ban
  - New `DiscordBan` entity with user and reason
- ✅ **Audit log access** - Complete audit log with advanced filtering
  - `bot.GetAuditLogAsync()` with filters: userId, actionType, before, after, limit
  - Enhanced `DiscordAuditLog` entity with audit_log_entries, users, threads
  - Full `AuditLogAction` enum for all Discord actions
- ✅ **Bug fixes** - Resolved 3 nullable reference warnings (CS8602, CS8601)
- ✅ **API compatibility** - All methods support both string and ulong overloads
- 📖 New [Moderation Guide](./wiki/Moderation.md) with comprehensive examples

### v1.6.8 - Dual-Layer Command Permissions (2025-01-15)
- ✅ **Discord-level permissions** - `[RequirePermissions]` attribute for default visibility
  - Sets `default_member_permissions` during command registration
  - Integrated with source generator for zero-reflection operation
- ✅ **Runtime per-guild rules** - `bot.Permissions` API for dynamic restrictions
  - `RegisterGuildRule()` for custom permission checks
  - `CheckPermission()` evaluates rules before command execution
  - Thread-safe with ConcurrentDictionary storage
- ✅ **100% AOT compatible** - Delegate-based, no reflection
- ✅ **Shard-safe** - Works across all sharding modes
- ✅ **Beginner-friendly** - Simple attribute + delegate API
- 📖 New [Permissions Guide](./wiki/Permissions.md) with database integration examples

### v1.6.0 - Cache-First Entity Retrieval & Live Collections (2025-01-11)
- ✅ **Cache-first entity retrieval** - Optional `useCache` parameter (default: true)
  - `GetUserAsync()`, `GetGuildMemberAsync()`, `GetGuildAsync()`, `GetChannelAsync()`
  - Reduces unnecessary API calls while maintaining backward compatibility
- ✅ **Live observable collections** - Thread-safe `INotifyCollectionChanged` for UI binding
  - `bot.GetLiveGuilds()`, `guild.GetLiveChannels()`, `guild.GetLiveMembers()`
  - WPF/MAUI/Avalonia support with `SynchronizationContext`
  - Real-time updates from gateway events
- ✅ **API completeness** - Added missing methods: `GetGuildChannelsAsync()`, `GetGuildRolesAsync()`

### v1.5.0 - Complete Gateway Events (2024-12-24)
- ✅ **Added missing Discord gateway events** - All previously unwired events are now fully exposed:
  - `InteractionCreated` – raised for every interaction (slash commands, components, modals)
  - `GuildEmojisUpdated` – emoji updates within a guild
  - `VoiceStateUpdated` – voice state changes
  - `PresenceUpdated` – presence updates
  - `TypingStarted` – typing indicators
  - `WebhooksUpdated` – webhook changes
  - `InviteCreated` – new invites
  - `InviteDeleted` – invite deletions
  - `GuildIntegrationsUpdated` – integration updates
- ✅ **Event wiring** – Events are now wired in both single‑gateway and sharded modes.
- ✅ **Modern C# 14 patterns** – Explicit type names and collection expressions where applicable.
- ✅ **Backward compatible** – No breaking changes; existing code continues to work unchanged.

### v1.4.4 - Advanced Components & Collection Improvements (2025-12-20)
- ✅ **Advanced select menu features** - Default values, resolved entity data, emoji support
  - Pre-populate select menus: `new UserSelect("id", defaultValues: new[] { SelectDefaultValue.User(userId) })`
  - Access resolved entities: `ctx.GetResolvedUsers()`, `ctx.GetResolvedRoles()`, `ctx.GetResolvedChannels()`
  - Emoji support in SelectOption: `new SelectOption("Label", "value", emojiName: "🎉")`
- ✅ **Specialized component handler attributes** - Type-safe handler registration
  - `[ButtonHandler("id")]`, `[UserSelectHandler("id")]`, `[RoleSelectHandler("id")]`
  - `[ChannelSelectHandler("id")]`, `[MentionableSelectHandler("id")]`
- ✅ **File attachments in MessageBuilder** - Send files with messages
  - `builder.AddFile("file.pdf", bytes)`, `builder.AddFile("image.png", stream)`
  - Multiple overloads: byte[], ReadOnlyMemory, Stream, MemoryStream, async support
- ✅ **Message collection methods** - Fetch and manage message history
  - `channel.GetMessagesAsync(limit)` - Fetch up to 100 messages
  - `channel.BulkDeleteMessagesAsync(count)` - Delete 2-100 recent messages
- ✅ **IEnumerable returns** - All collection methods now return `IEnumerable<T>` instead of `T[]`
  - Better LINQ support: `foreach (var msg in await channel.GetMessagesAsync(50)) { }`
  - Consistent API across all collection methods
- ✅ **UpdateAsync aliases** - Convenient update methods for component interactions
  - `ctx.UpdateAsync("text")` and `ctx.UpdateAsync(builder)` overloads
  - Works with both string content and MessageBuilder
- ✅ **ComponentType enum** - Type-safe component type constants
- ✅ **Source generator updates** - Recognizes all new specialized handler attributes

### v1.4.3 - Channel Permissions & Source Generator Fixes (2025-12-20)
- ✅ **Channel permission management** - Add, remove, deny, and modify permissions for roles and members
  - `channel.AddPermissionAsync(roleId, PermissionFlags.AttachFiles)`
  - `role.AddChannelPermissionAsync(channel, permission)`
  - `member.AddChannelPermissionAsync(channel, permission)`
- ✅ **Source generator fixes** - `[CommandOption]` attribute now optional for backward compatibility
- ✅ **Added `ulong` parameter support** - Commands can now use `ulong` parameters (Discord snowflake IDs)
- ✅ **Fixed mixed static/instance class handling** - Classes with both static and instance methods now generate correctly
- 🔧 **Type compatibility** - Fixed `IReadOnlyList<InteractionOption>` handling

### v1.4.1 - Entity-First Architecture & Rich API (2025-12-20)
- ✅ **Removed WithGuild wrappers** - All entities (Channel, Member, Role, User) now have direct Guild/Guilds properties
- ✅ **Rich entity methods** - Entities can perform operations on themselves (channel.SendMessageAsync, member.AddRoleAsync, message.PinAsync)
- ✅ **Enhanced channel management** - SetTopicAsync, SetNameAsync, SetNsfwAsync, SetBitrateAsync, SetUserLimitAsync, SetSlowmodeAsync
- ✅ **Message operations return entities** - All SendMessageAsync/SendDMAsync methods return DiscordMessage for chaining
- ✅ **Guild channel creation** - CreateChannelAsync and CreateCategoryAsync directly on DiscordGuild
- ✅ **DM channel caching** - DM channels are now cached in EntityCache for performance
- ✅ **Type consistency** - Author.Id changed from string to ulong, InteractionContext.User now returns DiscordUser
- ✅ **Optional parameters** - RespondAsync content parameter defaults to empty string for embed-only responses
- 📖 **Comprehensive documentation** - New Beginners-Guide.md and Entities.md wiki pages with detailed examples

### v1.4.0 - Enhanced InteractionContext & Security (2025-12-19)
- ✅ **Member and Guild objects in InteractionContext** - Direct access to member/guild without cache lookups
- ✅ **HTTPS-only ShardCoordinator** - Secure TLS communication for distributed sharding (upgraded from HTTP)
- ✅ **100% Zero Reflection** - All anonymous objects replaced with strongly-typed classes for full AoT compatibility
- ✅ **Enhanced type safety** - MessagePayload, BulkDeleteMessagesRequest, BanMemberRequest, HttpErrorResponse
- ✅ **Code quality improvements** - Removed redundant type specifications and method overload warnings
- 🔒 **Security hardened** - TLS 1.3+ for shard coordination endpoints

### v1.3.0 - Sharding Support (2025-12-19)
- ✅ Added 3-mode sharding system: single process, multi-shard, distributed
- ✅ Distributed coordinator/worker architecture with auto-discovery
- ✅ Health monitoring, load balancing, coordinator succession
- ✅ Cross-shard entity cache queries
- ✅ Shard-aware InteractionContext for commands
- ✅ Full AoT compliance with source-generated JSON serialization
- ✅ Zero reflection usage, ready for native compilation
- 📖 See [SHARDING_IMPLEMENTATION.md](SHARDING_IMPLEMENTATION.md) and [SHARDING_INTEGRATION_GUIDE.md](SHARDING_INTEGRATION_GUIDE.md)
