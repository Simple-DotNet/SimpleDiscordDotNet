# SimpleDiscordDotNet

A lightweight, dependency-free Discord bot SDK for .NET 10 that provides direct access to Discord API v10 (REST + Gateway).

## Purpose

SimpleDiscordDotNet is designed for developers who want:
- **Zero dependencies** - BCL only, no external packages
- **Performance** - UTF-8 JSON handling, dictionary-based cache lookups and per-resource rate limiting
- **Simplicity** - Clean, approachable API with builder patterns
- **Modern C#** - Built for .NET 10 with C# 14 features
- **Broad API coverage** - Bucket-aware rate limiting and most of Discord's REST and gateway API

## Key Features

- ✅ Slash commands, components, and modals with attribute-based handlers
- ✅ Checks, cooldowns, enum options and event handlers as attributes
- ✅ Built-in confirmations and pagination
- ✅ Constructor injection for handlers, with no reflection
- ✅ Modals defined as plain classes, with fields filled in for you
- ✅ Polls, threads, forum posts and layout messages (Components V2)
- ✅ Command translation, and DM and user-install support, through attributes
- ✅ Source generator for zero-reflection command/component discovery
- ✅ Ambient context for accessing cached guilds, channels, members, roles
- ✅ **Live observable collections** - Thread-safe `INotifyCollectionChanged` for WPF/MAUI/Avalonia UI binding
- ✅ Comprehensive gateway events for all entity changes
- ✅ Advanced rate limiting with bucket management and monitoring
- ✅ Full Discord API v10 support (messages, reactions, permissions, roles, channels, threads, etc.)
- ✅ Native AOT and trimming compatible
- ✅ Span-based formatting helpers and direct UTF-8 serialization
- ✅ **Sharding** - Run all shards in one process, or use experimental distributed coordinator/worker mode

## Quick Example

```csharp
using SimpleDiscordNet;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Primitives;

var bot = DiscordBot.NewBuilder()
    .WithToken(Environment.GetEnvironmentVariable("DISCORD_TOKEN")!)
    .WithIntents(DiscordIntents.Guilds | DiscordIntents.GuildMessages)
    .Build();

await bot.StartAsync();
await Task.Delay(Timeout.Infinite);

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
        byte[] chartBytes = await File.ReadAllBytesAsync("chart.png");

        // Slash commands are deferred automatically, so slow work is fine here
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
```

## Documentation

**📖 Full documentation is available in the [Wiki](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki)**

- [Installation](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Installation) - Get started with NuGet or source reference
- [Getting Started](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Getting-Started) - Your first bot in minutes
- [Beginner's Guide](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Beginners-Guide) - Step-by-step guide for Discord bot beginners
- [Feature Examples](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Feature-Examples) - A simple and a realistic example of every feature
- [Configuration](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Configuration) - Builder patterns, DI, intents
- [Commands](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Commands) - Slash commands, components, modals
- [Permissions](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Permissions) - Dual-layer command permissions
- [Moderation](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Moderation) - Bans, kicks, timeouts, voice control, audit logs
- [Working with Entities](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Entities) - Channels, guilds, members, messages, and roles
- [Events](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Events) - Gateway events and logging
- [Sharding](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Sharding) - Scaling across shards and processes
- [API Reference](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/API-Reference) - Every public type and member
- [Rate Limit Monitoring](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Rate-Limit-Monitoring) - Monitoring and analytics
- [FAQ](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/FAQ) - Common questions and troubleshooting

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

**Ready to build your Discord bot?** Head to the [Wiki](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki) to get started!

## Version History

### v1.11.0

A large release: new attribute-based features, plus reliability fixes across the library. Most changes need no code changes, but read the upgrade notes below first.

**New features**

- **Checks** - `[Cooldown]`, `[RequireRole]`, `[RequireBotPermissions]` and `[RequireOwner]` run before a handler and tell the user why they failed.
- **Enum parameters** - An enum becomes a list of choices automatically.
- **Return a reply** - Handlers can return a string, `EmbedBuilder` or `MessageBuilder`, or a `Task` of one.
- **Data in component IDs** - `[ButtonHandler("vote:{pollId}:{choice}")]` binds parts of the ID to parameters.
- **Questions and pages** - `ctx.ConfirmAsync()`, `ctx.ChooseAsync()` and `ctx.PaginateAsync()`.
- **Event attributes** - Over 40 attributes such as `[OnMemberJoined]`, `[OnMessageCreated]`, `[OnBanAdded]` and `[OnVoiceStateUpdated]`, plus `[OnEvent]` for the rarer ones.
- **Services in handlers** - Handler classes can take services through their constructors, with `WithServices`.
- **Error handling and help** - `[OnCommandError]` for all failures, and `[assembly: GenerateHelp]` for a help command.
- **Compile errors** - Library diagnostics now point at the file and line.
- **Attribute modals** - Define a form as a class with `[Modal]` and field attributes such as `[ModalText]`, `[ModalSelect]`, `[ModalCheckbox]` and `[ModalFile]`. Open it with `ctx.OpenModalAsync<TForm>()` and receive it filled in through `[ModalHandler]`. Modals use Discord's current Label components.
- **Where commands work** - `[GuildOnly]`, `[AllowInDMs]` and `[UserInstallable]`.
- **Translations** - `[Localized]` on commands, groups, parameters and context menu commands.
- **Polls** - `builder.WithPoll(...)`, `PollBuilder`, `message.EndPollAsync()` and `message.GetPollVotersAsync()`.
- **Threads and forum posts** - `channel.CreateThreadAsync()`, `message.CreateThreadAsync()`, `channel.CreateForumPostAsync()`, `channel.GetArchivedThreadsAsync()` and `guild.GetActiveThreadsAsync()`. Channels now expose forum tags and thread details.
- **Layout messages** - Components V2 through `WithText`, `WithContainer`, `WithSection`, `WithGallery` and `WithSeparator`.
- **More actions** - `message.ForwardAsync()`, `message.UnpinAsync()`, `guild.SearchMembersAsync()`, `guild.SearchMessagesAsync()`, `guild.BulkBanAsync()` and application emoji methods.
- **Permissions** - `PermissionFlags` now includes `PinMessages`, `SendPolls`, `CreateEvents`, `CreateGuildExpressions`, `BypassSlowmode`, `UseExternalApps` and more.
- **Events** - `ChannelPinsUpdated`, `ThreadMemberUpdated`, `ThreadMembersUpdated`, `ThreadListSynced` and `GatewayRateLimited`.
- **Hidden channels** - `DiscordChannel.IsObfuscated` detects channels Discord hides from the bot.

**Upgrade notes**

- **Re-sync your commands.** `ulong` parameters are now string options, because Discord's integer options can't hold a snowflake. Context menu commands are registered with the empty description Discord requires.
- **`[CommandOption]`** - `Required`, `MinLength`, `MaxLength`, `MinValue` and `MaxValue` are now non-nullable. The old nullable properties couldn't be set from an attribute, so existing code that compiled isn't affected.
- **Channel creation** - The `permissionOverwrites` parameter is now strongly typed. Code passing an `object` needs updating.
- **`IDiscordBot`** - Gained `RateLimitMonitor`, optional `reason` parameters on the kick, ban, unban and timeout methods, and the new thread, poll, search and emoji methods. Custom implementations need updating, including custom `IDiscordContextOperations`.
- **Pins** - Pinning now needs Discord's `PinMessages` permission rather than `ManageMessages`. `GetPinnedMessagesAsync` uses Discord's current routes and returns every pin.
- **Forum posts** - `WithForumPost` now goes to Discord's thread route, so the returned message's `ChannelId` is the new post's thread.
- **`TextInput`** - The `label` argument is now optional. Wrap a text input in a `Label` instead, because Discord has deprecated the old form.
- **Gateway member limits** - Discord allows one full member request per guild every 30 seconds. Rejected requests are now retried after the wait Discord asks for.
- **Thread cache** - Threads now stay in the cache as they're created, updated and deleted.
- **Option defaults** - A command parameter's default value now applies when the option is omitted. `int age = 18` used to arrive as 0.
- **`[CommandChoice]`** - It failed to compile before and now works. `CommandChoice.Create` builds a choice for autocomplete.
- **`DiscordBot.FromOptions`** - It now registers your generated commands, which it used to skip.
- **Handler classes** - A class whose only public constructor takes parameters now gets them from `WithServices`, rather than being a compile error.
- **`DiscordRole.Guild`** is no longer `required`.
- **POST retries** - POST requests are no longer retried on 5xx responses, because Discord may have already processed them. Handle these errors yourself where it matters.
- **`GuildRemoved`** no longer fires when a guild is only temporarily unavailable during an outage.
- **Gateway debug output** from `WithGatewayDebug()` now goes through the logger at Debug level rather than the console.
- **Packaging** - The project no longer builds a package on every build. Use `dotnet pack`.
- **Distributed sharding** - All nodes must upgrade together, because cluster traffic now requires a secret derived from the bot token. This mode is experimental, and there's no automatic coordinator failover.

**Entities and caching**

- Fixed parsing of users, members, roles, guilds and audit logs that failed or lost data on real gateway payloads.
- Messages in DMs and uncached channels now reach handlers.
- Member, channel and role lookups are now dictionary lookups, and member chunks are added in batches.
- Live collections now raise change notifications for bulk updates.

**Gateway**

- Rewrote connection handling: resume uses `resume_gateway_url`, close codes follow Discord's rules, heartbeat requests are answered, and the first heartbeat is jittered.
- Fatal close codes stop reconnecting, and outgoing commands respect the 120 per 60 seconds send limit.

**REST**

- Rate limits are tracked per bucket and per channel, guild or webhook. Interaction endpoints are exempt from the global limit.
- `RateLimitMonitor` is now implemented, and tokens are redacted from logs.
- Added audit log `reason` support, `DiscordMember.KickAsync` and `DiscordMember.BanAsync`, and paginated `GetAllCurrentUserGuildsAsync`.

**Commands**

- `DiscordUser`, `DiscordMember`, `DiscordChannel`, `DiscordRole`, attachment, `double` and `float` parameters now work.
- Added `TargetId`, `TargetUser`, `TargetMember` and `TargetMessage` to `InteractionContext` for context menu commands.
- Unsupported parameter types are now a compile error (SDN004) instead of failing at runtime.

**Sharding**

- Cluster traffic is authenticated, shards honor `max_concurrency` when identifying, and URL handling is fixed for trailing slashes and wildcard hosts.
- Cluster traffic uses `http://` unless you set up TLS, so keep it on a private network or behind a TLS proxy. See the [Sharding guide](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Sharding).

**Quality**

- Added an automated test suite, a Native AOT smoke test, and CI that runs both.
- Corrected the documentation against the real API.

### v1.10.17 - Connected Event Reliability Fix
- **Connected Event Fix** — `Connected` event now fires after every full gateway reconnection (IDENTIFY), not just the initial one. Previously, `Disconnected` would fire on transient network issues, but `Connected` only fired once on the first `READY` due to an `_isReady` flag that was never reset. When a session expired during reconnect, the subsequent fresh IDENTIFY/READY cycle would silently skip firing `Connected`, leaving consumers tracking `IsConnected` permanently stuck at "Disconnected".
- **_isReady** is now reset to 0 in two code paths: `ResumeAsync` fallback-to-identify (when `_sessionId` is empty), and op=9 INVALID_SESSION handler (when resume isn't possible). This guarantees that `Connected` fires on every full reconnect, matching consumer expectations.
- ✅ **0 breaking changes** — All existing API and event types unchanged.

### v1.10.16 - Disconnected Event Reliability Fix
- **Disconnected Event Fix** — `Disconnected` now fires reliably before every silent reconnect attempt, not just on WebSocket close frames. Added to four code paths: missed heartbeat ACKs, receive loop exceptions, socket state loss (half-close without close frame), and op=7 RECONNECT requests. Upstream consumers tracking `IsConnected` via events will no longer show stale "Connected" state after silent connection loss.
- ✅ **0 breaking changes** — All existing API and event types unchanged.

### v1.10.15 - Session Resumed Event
- **New `SessionResumed` Event** — Added to `GatewayClient`, `DiscordEvents`, and `Shard` infrastructure. Fires when the gateway successfully resumes an existing session after a WebSocket disconnect (`t="RESUMED"`), distinct from `Connected` which only fires on initial or new `READY`.
- **Shard Status Fix** — `Shard._status` now correctly transitions from `Reconnecting` back to `Connected` on successful resume, rather than staying stuck in `Reconnecting` permanently.
- **`WireShardEvents` Updated** — Now accepts an `onSessionResumed` parameter so sharded consumers can subscribe to resume events through the wiring helper.
- ✅ **0 breaking changes** — All existing API and event types unchanged. `SessionResumed` is a new event; existing `Connected` behavior is preserved.

### v1.10.14 - Gateway Reconnect & Session Resume Fix
- **30-Minute Disconnect Fixed** — Close code 4003 (Not authenticated) no longer waits 30 minutes before retrying. Reduced to 30-second delay.
- **Session Resume Attempt** — On 4003, the gateway first attempts to resume the existing session. If resume fails, it clears all cached entities (guilds, channels, members, users, roles) and performs a fresh identify to guarantee a clean state.
- **Fatal Codes Stop Retrying** — Close codes 4004, 4010, 4011, 4012 are now treated as fatal configuration errors and stop the reconnect loop immediately.
- ✅ **0 breaking changes** — All existing API and event types unchanged.

### v1.10.13 - Anonymous Type Serialization Fix
- **Anonymous Type Serialization Crash** — 6 REST methods (`CreateRoleAsync`, `ModifyRoleAsync`, `CreateChannelAsync`, `ModifyChannelAsync`, `EditMessageAsync`, `ModifyGuildAsync`) used anonymous types as request payloads, causing `NotSupportedException` when serialized via source-generated `DiscordJsonContext`. Replaced all anonymous types with named request classes (`CreateGuildRoleRequest`, `CreateGuildChannelRequest`, `ModifyChannelRequest`, `EditMessageRequest`, `ModifyGuildRequest`) and registered them with `[JsonSerializable]`.
- ✅ **0 breaking changes** — All method signatures and public API unchanged.

### v1.10.12 - Gateway Reconnect Hardening & Invite Event Fixes
- **Non-Recoverable Close Code Protection** — Gateway now detects non-recoverable Discord close codes (4003, 4004, 4010, 4011, 4012) and applies a 30-minute delay before retrying instead of entering an infinite reconnect loop. On successful reconnection, the normal backoff timer resets immediately.
- **Auth Failure Diagnostics** — `Disconnected` event now logs a clear error when authentication is rejected, directing users to verify their bot token.
- **Invite Event Crash Fix** — `TryEmitInviteCreateEvent` and `TryEmitInviteDeleteEvent` now use `TryGetProperty` for optional fields (`guild_id`, `inviter`, `code`, `created_at`), preventing `KeyNotFoundException` crashes when Discord omits these fields (e.g. DM invites, vanity URL invites).
- ✅ **0 breaking changes** — All existing API and event types unchanged.

### v1.10.11 - Kick & Ban via DiscordContext.Operations
- **Moderation API Completeness** — `KickMemberAsync` and `BanMemberAsync` are now exposed on `DiscordContext.Operations`, completing moderation coverage alongside existing timeout, role, and voice operations. `KickMemberAsync` also added to the `IDiscordBot` interface.
- ✅ **0 breaking changes** — New methods only, all existing API unchanged.

### v1.10.10 - DM Channel Deserialization Fix
- **DM Channel Fix** — `DiscordChannel.Name` is no longer marked `required`, defaulting to `string.Empty`. DM/GroupDM channels omit `name` from API responses, which previously caused deserialization crashes in `SendDMAsync`, `GetChannelAsync`, and interaction resolved channel data.
- ✅ **0 breaking changes** — `Name` still returns `string` (non-nullable). Existing code setting `Name` continues to work unchanged.

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
- 📖 New [Moderation Guide](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Moderation) with comprehensive examples

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
- 📖 New [Permissions Guide](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Permissions) with database integration examples

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
- Superseded: TLS for cluster traffic now depends on your setup. See the v1.11.0 notes.

### v1.3.0 - Sharding Support (2025-12-19)
- ✅ Added 3-mode sharding system: single process, multi-shard, distributed
- ✅ Distributed coordinator/worker architecture with auto-discovery
- ✅ Health monitoring, load balancing, coordinator succession
- ✅ Cross-shard entity cache queries
- ✅ Shard-aware InteractionContext for commands
- ✅ Full AoT compliance with source-generated JSON serialization
- ✅ Zero reflection usage, ready for native compilation
- 📖 See the [Sharding guide](https://github.com/Simple-DotNet/SimpleDiscordDotNet/wiki/Sharding)
