using System.Collections.Generic;
using System.Globalization;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Requests;
using SimpleDiscordNet.Primitives;
using SimpleDiscordNet.Rest;

namespace SimpleDiscordNet.Commands;

public sealed class InteractionContext
{
    private readonly RestClient _rest;
    private readonly InteractionCreateEvent _evt;
    private volatile bool _deferred;
    private volatile bool _deferredUpdate;
    private bool _deferredEphemeral;
    private int _responded;

    // Cached parsed ChannelId for O(1) lookup in Channel property
    private readonly ulong? _channelIdUlong;

    // Cached options dictionary for O(1) lookup
    private Dictionary<string, InteractionOption>? _optionsCache;

    public string InteractionId { get; }
    public string InteractionToken { get; }
    public string ApplicationId { get; }
    public string? GuildId { get; }
    public string? ChannelId { get; }

    /// <summary>
    /// The user who triggered this interaction.
    /// For guild interactions, this is Member.User.
    /// For DM interactions, this is a DiscordUser created from the interaction data.
    /// </summary>
    public Entities.DiscordUser? User { get; }

    public InteractionType Type { get; }

    /// <summary>
    /// The member entity if this interaction occurred in a guild.
    /// Null for DM interactions.
    /// </summary>
    public Entities.DiscordMember? Member => _evt.Member;

    /// <summary>
    /// The guild entity if this is a guild interaction.
    /// Null for DM interactions.
    /// </summary>
    public Entities.DiscordGuild? Guild => _evt.Guild;

    /// <summary>
    /// The channel entity if available in cache.
    /// </summary>
    public Entities.DiscordChannel? Channel => _channelIdUlong.HasValue ? Context.DiscordContext.GetChannel(_channelIdUlong.Value) : null;

    /// <summary>
    /// The shard ID that received this interaction (0-based).
    /// Null if bot is not using sharding.
    /// Example: int? shard = ctx.ShardId;
    /// </summary>
    public int? ShardId { get; internal set; }

    // Expose the raw event for maximum flexibility
    public InteractionCreateEvent Event => _evt;

    // Convenience accessors for specific interaction shapes
    public ApplicationCommandData? Command => _evt.Data;
    public MessageComponentData? Component => _evt.Component;
    public ModalSubmitData? Modal => _evt.Modal;

    // Common helpers
    public string? CustomId => Type switch
    {
        InteractionType.MessageComponent => _evt.Component?.CustomId,
        InteractionType.ModalSubmit => _evt.Modal?.CustomId,
        _ => null
    };

    public string? MessageId => _evt.Component?.MessageId;
    public IReadOnlyList<string> SelectedValues => _evt.Component?.Values ?? Array.Empty<string>();

    internal InteractionContext(RestClient rest, InteractionCreateEvent evt)
    {
        _rest = rest;
        _evt = evt;
        InteractionId = evt.Id;
        InteractionToken = evt.Token;
        ApplicationId = evt.ApplicationId;
        GuildId = evt.GuildId;
        ChannelId = evt.ChannelId;

        // Cache parsed ChannelId
        _channelIdUlong = evt.ChannelId is not null && ulong.TryParse(evt.ChannelId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong cid) ? cid : null;

        // For guild interactions, use Member.User; for DM interactions, create DiscordUser from Author
        User = evt.Member?.User ?? (evt.Author != null ? new Entities.DiscordUser
        {
            Id = evt.Author.Id,
            Username = evt.Author.Username,
            Guilds = Array.Empty<Entities.DiscordGuild>()
        } : null);

        Type = evt.Type;
    }

    /// <summary>
    /// Sends an immediate response using a MessageBuilder.
    /// Automatically defers and sends as followup when file attachments are present
    /// (Discord does not support files on initial interaction responses).
    /// Example: await ctx.RespondAsync(new MessageBuilder().WithContent("Hello").WithEmbed(embed));
    /// </summary>
    public Task RespondAsync(MessageBuilder builder, bool ephemeral = false, CancellationToken ct = default)
    {
        MessagePayload payload = builder.Build();
        var files = builder.GetFiles();
        bool hasFiles = files is not null && files.Count > 0;
        int? flags = (ephemeral || _deferredEphemeral) ? 1 << 6 : null;

        if (_deferred || _deferredUpdate)
        {
            return SendFollowupAsync(BuildWebhookRequest(payload, flags), files, ct);
        }

        if (Interlocked.Exchange(ref _responded, 1) == 1)
            return Task.CompletedTask;

        if (hasFiles)
        {
            return AutoDeferAndRespondAsync(BuildWebhookRequest(payload, flags), files!, flags, ct);
        }

        InteractionResponse resp = new() { type = 4, data = BuildInteractionData(payload, flags) };
        return _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
    }

    /// <summary>
    /// Sends an immediate response to the interaction with text and/or embed.
    /// Automatically defers and sends as followup when a file attachment is present
    /// (Discord does not support files on initial interaction responses).
    /// For complex responses, use RespondAsync(MessageBuilder) instead.
    /// Example: await ctx.RespondAsync("Hello, world!");
    /// Example: await ctx.RespondAsync(embed: myEmbed); // Embed only, no text
    /// Example: await ctx.RespondAsync("Here's a chart", fileName: "chart.png", fileData: imageBytes);
    /// </summary>
    public Task RespondAsync(string content = "", EmbedBuilder? embed = null, bool ephemeral = false,
        string? fileName = null, ReadOnlyMemory<byte>? fileData = null, CancellationToken ct = default)
    {
        int? flags = (ephemeral || _deferredEphemeral) ? 1 << 6 : null;
        bool hasFiles = fileName is not null && fileData.HasValue;
        Embed[]? embeds = embed is null ? null : [embed.Build()];

        if (_deferred || _deferredUpdate)
        {
            WebhookMessageRequest req = new()
            {
                content = content,
                embeds = embeds,
                flags = flags
            };
            if (hasFiles)
                req.attachments = [new AttachmentReference(0, fileName!)];
            var filesList = hasFiles ? new List<(string, ReadOnlyMemory<byte>)> { (fileName!, fileData!.Value) } : null;
            return SendFollowupAsync(req, filesList, ct);
        }

        if (Interlocked.Exchange(ref _responded, 1) == 1)
            return Task.CompletedTask;

        if (hasFiles)
        {
            WebhookMessageRequest req = new()
            {
                content = content,
                embeds = embeds,
                flags = flags,
                attachments = [new AttachmentReference(0, fileName!)]
            };
            var filesList = new List<(string, ReadOnlyMemory<byte>)> { (fileName!, fileData!.Value) };
            return AutoDeferAndRespondAsync(req, filesList, flags, ct);
        }

        InteractionResponseData data = new()
        {
            content = content,
            embeds = embeds,
            flags = flags
        };
        InteractionResponse resp = new() { type = 4, data = data };
        return _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
    }

    /// <summary>
    /// Defers the interaction response to allow more processing time (type 5).
    /// Use this for slash commands when you need longer than 3 seconds before sending a message.
    /// Prefer calling this explicitly or annotating the handler with [Defer] if you want the SDK to do it automatically.
    /// </summary>
    public Task DeferAsync(bool ephemeral = false, CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _responded, 1) == 1)
            return Task.CompletedTask;

        InteractionResponseData data = new() { flags = ephemeral ? 1 << 6 : null };
        InteractionResponse resp = new() { type = 5, data = data };
        Task task = _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
        return task.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                _deferred = true;
                _deferredEphemeral = ephemeral;
            }
            else
            {
                Interlocked.Exchange(ref _responded, 0);
            }

            t.GetAwaiter().GetResult();
        }, ct);
    }

    /// <summary>
    /// Alias for <see cref="DeferAsync(bool, System.Threading.CancellationToken)"/>.
    /// Provided for readability when working with slash commands.
    /// </summary>
    public Task DeferResponseAsync(bool ephemeral = false, CancellationToken ct = default)
        => DeferAsync(ephemeral, ct);

    /// <summary>
    /// Sends a follow-up message for a previously deferred interaction.
    /// Example: await ctx.FollowupAsync("Here's more info");
    /// Example: await ctx.FollowupAsync(embed: myEmbed); // Embed only, no text
    /// </summary>
    public Task FollowupAsync(string content = "", EmbedBuilder? embed = null, bool? ephemeral = null, CancellationToken ct = default)
    {
        bool effectiveEphemeral = ephemeral ?? _deferredEphemeral;
        WebhookMessageRequest payload = new()
        {
            content = content,
            embeds = embed is null ? null : [embed.Build()],
            flags = effectiveEphemeral ? 1 << 6 : null
        };
        return _rest.PostWebhookFollowupAsync(ApplicationId, InteractionToken, payload, ct);
    }

    /// <summary>
    /// Sends a follow-up message using a MessageBuilder. Supports file attachments.
    /// Example: await ctx.FollowupAsync(new MessageBuilder().WithContent("Here's a file").AddFile("doc.pdf", bytes));
    /// </summary>
    public Task FollowupAsync(MessageBuilder builder, bool? ephemeral = null, CancellationToken ct = default)
    {
        bool effectiveEphemeral = ephemeral ?? _deferredEphemeral;
        MessagePayload payload = builder.Build();
        return SendFollowupAsync(BuildWebhookRequest(payload, effectiveEphemeral ? 1 << 6 : null), builder.GetFiles(), ct);
    }

    /// <summary>
    /// Gets the original interaction response message.
    /// Example: var msg = await ctx.GetOriginalResponseAsync();
    /// </summary>
    public Task<Entities.DiscordMessage?> GetOriginalResponseAsync(CancellationToken ct = default)
        => _rest.GetWebhookMessageAsync<Entities.DiscordMessage>(ApplicationId, InteractionToken, "@original", ct);

    /// <summary>
    /// Edits a followup message.
    /// Example: await ctx.EditFollowupAsync(messageId, "Updated content");
    /// </summary>
    public Task<Entities.DiscordMessage?> EditFollowupAsync(string messageId, string content = "", EmbedBuilder? embed = null, CancellationToken ct = default)
    {
        WebhookMessageRequest payload = new()
        {
            content = content,
            embeds = embed is null ? null : [embed.Build()]
        };
        return _rest.PatchWebhookMessageAsync<Entities.DiscordMessage>(ApplicationId, InteractionToken, messageId, payload, ct);
    }

    /// <summary>
    /// Edits a followup message using a MessageBuilder. Supports file attachments.
    /// Example: await ctx.EditFollowupAsync(messageId, new MessageBuilder().WithContent("Updated").AddFile("new.png", bytes));
    /// </summary>
    public Task<Entities.DiscordMessage?> EditFollowupAsync(string messageId, MessageBuilder builder, CancellationToken ct = default)
    {
        MessagePayload payload = builder.Build();
        return EditFollowupCoreAsync(messageId, BuildWebhookRequest(payload, null), builder.GetFiles(), ct);
    }

    /// <summary>
    /// Deletes a followup message.
    /// Example: await ctx.DeleteFollowupAsync(messageId);
    /// </summary>
    public Task DeleteFollowupAsync(string messageId, CancellationToken ct = default)
        => _rest.DeleteWebhookMessageAsync(ApplicationId, InteractionToken, messageId, ct);

    /// <summary>
    /// Edits the original interaction response.
    /// Example: await ctx.EditOriginalResponseAsync("Updated!");
    /// </summary>
    public Task<Entities.DiscordMessage?> EditOriginalResponseAsync(string content = "", EmbedBuilder? embed = null, CancellationToken ct = default)
        => EditFollowupAsync("@original", content, embed, ct);

    /// <summary>
    /// Edits the original interaction response using a MessageBuilder. Supports file attachments.
    /// Example: await ctx.EditOriginalResponseAsync(new MessageBuilder().WithContent("Updated").AddFile("new.png", bytes));
    /// </summary>
    public Task<Entities.DiscordMessage?> EditOriginalResponseAsync(MessageBuilder builder, CancellationToken ct = default)
        => EditFollowupAsync("@original", builder, ct);

    /// <summary>
    /// Deletes the original interaction response.
    /// Example: await ctx.DeleteOriginalResponseAsync();
    /// </summary>
    public Task DeleteOriginalResponseAsync(CancellationToken ct = default)
        => DeleteFollowupAsync("@original", ct);

    /// <summary>
    /// Defers a component interaction update (responds with a loading state on the message).
    /// </summary>
    public Task DeferUpdateAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _responded, 1) == 1)
            return Task.CompletedTask;

        InteractionResponse resp = new() { type = 6, data = null };
        Task task = _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
        return task.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                _deferredUpdate = true;
            }
            else
            {
                Interlocked.Exchange(ref _responded, 0);
            }
            t.GetAwaiter().GetResult();
        }, ct);
    }

    /// <summary>
    /// Updates the original message in response to a component interaction.
    /// Automatically defers the update (type 6) when a file attachment is present
    /// (Discord does not support files on type 7 interaction callbacks).
    /// Example: await ctx.UpdateMessageAsync("Updated!", fileData: imageBytes, fileName: "chart.png");
    /// </summary>
    public Task UpdateMessageAsync(string content, IEnumerable<IComponent>? components = null,
        string? fileName = null, ReadOnlyMemory<byte>? fileData = null, CancellationToken ct = default)
    {
        IComponent[]? comps = components is null ? null : new IComponent[] { new ActionRow(components.ToArray()) };
        bool hasFiles = fileName is not null && fileData.HasValue;

        if (_deferredUpdate || _deferred)
        {
            WebhookMessageRequest payload = new() { content = content, components = comps };
            if (hasFiles)
                payload.attachments = [new AttachmentReference(0, fileName!)];
            var filesList = hasFiles ? new List<(string, ReadOnlyMemory<byte>)> { (fileName!, fileData!.Value) } : null;
            return EditFollowupCoreAsync("@original", payload, filesList, ct);
        }

        if (Interlocked.Exchange(ref _responded, 1) == 1)
            return Task.CompletedTask;

        if (hasFiles)
        {
            WebhookMessageRequest request = new()
            {
                content = content,
                components = comps,
                attachments = [new AttachmentReference(0, fileName!)]
            };
            var filesList = new List<(string, ReadOnlyMemory<byte>)> { (fileName!, fileData!.Value) };
            return AutoDeferAndUpdateAsync(request, filesList, ct);
        }

        InteractionResponseData data = new() { content = content, components = comps };
        InteractionResponse resp = new() { type = 7, data = data };
        return _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
    }

    /// <summary>
    /// Updates the original message in response to a component interaction using a MessageBuilder.
    /// Automatically defers the update (type 6) when file attachments are present
    /// (Discord does not support files on type 7 interaction callbacks).
    /// Example: await ctx.UpdateMessageAsync(new MessageBuilder().WithContent("Updated").WithButton("OK", "ok_btn"));
    /// </summary>
    public Task UpdateMessageAsync(MessageBuilder builder, CancellationToken ct = default)
    {
        MessagePayload payload = builder.Build();
        var files = builder.GetFiles();
        bool hasFiles = files is not null && files.Count > 0;

        if (_deferredUpdate || _deferred)
        {
            return EditFollowupCoreAsync("@original", BuildWebhookRequest(payload, null), files, ct);
        }

        if (Interlocked.Exchange(ref _responded, 1) == 1)
            return Task.CompletedTask;

        if (hasFiles)
        {
            return AutoDeferAndUpdateAsync(BuildWebhookRequest(payload, null), files!, ct);
        }

        InteractionResponse resp = new() { type = 7, data = BuildInteractionData(payload, null) };
        return _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, resp, ct);
    }

    /// <summary>
    /// Alias for <see cref="UpdateMessageAsync(string, IEnumerable{IComponent}?, CancellationToken)"/>.
    /// Updates the original message in response to a component interaction.
    /// Example: await ctx.UpdateAsync("Updated!");
    /// </summary>
    public Task UpdateAsync(string content, IEnumerable<IComponent>? components = null, CancellationToken ct = default)
        => UpdateMessageAsync(content, components, ct: ct);

    /// <summary>
    /// Alias for <see cref="UpdateMessageAsync(MessageBuilder, CancellationToken)"/>.
    /// Updates the original message in response to a component interaction using a MessageBuilder.
    /// Example: await ctx.UpdateAsync(new MessageBuilder().WithContent("Updated").WithEmbed(embed));
    /// </summary>
    public Task UpdateAsync(MessageBuilder builder, CancellationToken ct = default)
        => UpdateMessageAsync(builder, ct);

    /// <summary>
    /// Opens a modal in response to an interaction.
    /// This must be the initial response. Do not defer before opening a modal.
    /// Example: await ctx.OpenModalAsync("modal_id", "Form Title", new ActionRow(new TextInput("input_id", "Label")));
    /// </summary>
    public Task OpenModalAsync(string customId, string title, CancellationToken ct = default, params IComponent[] actionRows)
    {
        if (Interlocked.Exchange(ref _responded, 1) == 1)
            throw new InvalidOperationException("Cannot open a modal after the interaction has already been acknowledged. Do not apply [Defer] to this handler and avoid calling ctx.DeferResponseAsync or ctx.RespondAsync before opening the modal.");

        OpenModalRequest modal = new()
        {
            type = 9,
            data = new ModalData
            {
                custom_id = customId,
                title = title,
                components = actionRows
            }
        };
        return _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, modal, ct);
    }

    /// <summary>
    /// Sends a simple text-only response.
    /// Example: await ctx.ReplyAsync("Done!");
    /// </summary>
    public Task ReplyAsync(string content, bool ephemeral = false, CancellationToken ct = default)
        => RespondAsync(content, null, ephemeral, ct: ct);

    /// <summary>
    /// Sends an ephemeral (only visible to user) response.
    /// Example: await ctx.ReplyEphemeralAsync("This is private!");
    /// Example: await ctx.ReplyEphemeralAsync(embed: myEmbed); // Embed only, no text
    /// </summary>
    public Task ReplyEphemeralAsync(string content = "", EmbedBuilder? embed = null, CancellationToken ct = default)
        => RespondAsync(content, embed, ephemeral: true, ct: ct);

    /// <summary>
    /// Sends a response with an embed.
    /// Example: await ctx.ReplyWithEmbedAsync("Check this out", new EmbedBuilder().WithTitle("Cool Embed"));
    /// Example: await ctx.ReplyWithEmbedAsync(embed: myEmbed); // Embed only, no text
    /// </summary>
    public Task ReplyWithEmbedAsync(string content = "", EmbedBuilder? embed = null, bool ephemeral = false, CancellationToken ct = default)
        => RespondAsync(content, embed, ephemeral, ct: ct);

    /// <summary>
    /// Sends a response with buttons.
    /// Example: await ctx.ReplyWithButtonsAsync("Choose:", new Button ("Yes", "yes_id"), new Button("No", "no_id"));
    /// </summary>
    public Task ReplyWithButtonsAsync(string content, params Button[] buttons)
        => RespondAsync(new MessageBuilder().WithContent(content).WithComponents(buttons));

    /// <summary>
    /// Gets an option value as a string from a slash command.
    /// Returns null if the option doesn't exist.
    /// Example: string? name = ctx.GetOption("name");
    /// </summary>
    public string? GetOption(string optionName)
    {
        return GetOptionsCache().TryGetValue(optionName, out InteractionOption? opt) ? opt.String : null;
    }

    /// <summary>
    /// Gets an option value as an integer from a slash command.
    /// Returns null if the option doesn't exist.
    /// Example: long? count = ctx.GetOptionInt("count");
    /// </summary>
    public long? GetOptionInt(string optionName)
    {
        return GetOptionsCache().TryGetValue(optionName, out InteractionOption? opt) ? opt.Integer : null;
    }

    /// <summary>
    /// Gets an option value as a boolean from a slash command.
    /// Returns null if the option doesn't exist.
    /// Example: bool? enabled = ctx.GetOptionBool("enabled");
    /// </summary>
    public bool? GetOptionBool(string optionName)
    {
        return GetOptionsCache().TryGetValue(optionName, out InteractionOption? opt) ? opt.Boolean : null;
    }

    /// <summary>
    /// Gets an option value as a string, or returns a default value if not found.
    /// Example: string name = ctx.GetOptionOrDefault("name", "Anonymous");
    /// </summary>
    public string GetOptionOrDefault(string optionName, string defaultValue)
        => GetOption(optionName) ?? defaultValue;

    /// <summary>
    /// Gets the first selected value from a select menu interaction.
    /// Returns null if no values selected.
    /// Example: string? choice = ctx.GetSelectedValue();
    /// </summary>
    public string? GetSelectedValue()
        => Component?.Values?.FirstOrDefault();

    /// <summary>
    /// Gets the value from a modal text input by custom_id.
    /// Returns null if not found.
    /// Example: string? feedback = ctx.GetModalValue("feedback_input");
    /// </summary>
    public string? GetModalValue(string customId)
    {
        TextInputValue? input = Modal?.Inputs?.FirstOrDefault(i => i.CustomId.Equals(customId, StringComparison.OrdinalIgnoreCase));
        return input?.Value;
    }

    /// <summary>
    /// Returns true if this interaction is from a guild (server), false if from DMs.
    /// Example: if (ctx.IsInGuild) { ... }
    /// </summary>
    public bool IsInGuild => GuildId is not null;

    /// <summary>
    /// Gets the user's ID who triggered this interaction.
    /// Example: ulong userId = ctx.UserId;
    /// </summary>
    public ulong UserId => User?.Id ?? 0;

    /// <summary>
    /// Gets the username who triggered this interaction.
    /// Example: string username = ctx.Username;
    /// </summary>
    public string Username => User?.Username ?? "Unknown";

    /// <summary>
    /// Gets all resolved users from a user/mentionable select menu interaction.
    /// Returns empty enumerable if no resolved users.
    /// Example: foreach (var user in ctx.GetResolvedUsers()) { }
    /// </summary>
    public IEnumerable<Entities.DiscordUser> GetResolvedUsers()
    {
        if (Component?.Resolved?.Users == null) return Array.Empty<Entities.DiscordUser>();
        return Component.Resolved.Users.Values;
    }

    /// <summary>
    /// Gets all resolved members from a user/mentionable select menu interaction.
    /// Returns empty enumerable if no resolved members.
    /// Example: foreach (var member in ctx.GetResolvedMembers()) { }
    /// </summary>
    public IEnumerable<Entities.DiscordMember> GetResolvedMembers()
    {
        if (Component?.Resolved?.Members == null) return Array.Empty<Entities.DiscordMember>();
        return Component.Resolved.Members.Values;
    }

    /// <summary>
    /// Gets all resolved roles from a role/mentionable select menu interaction.
    /// Returns empty enumerable if no resolved roles.
    /// Example: foreach (var role in ctx.GetResolvedRoles()) { }
    /// </summary>
    public IEnumerable<Entities.DiscordRole> GetResolvedRoles()
    {
        if (Component?.Resolved?.Roles == null) return Array.Empty<Entities.DiscordRole>();
        return Component.Resolved.Roles.Values;
    }

    /// <summary>
    /// Gets all resolved channels from a channel select menu interaction.
    /// Returns empty enumerable if no resolved channels.
    /// Example: foreach (var channel in ctx.GetResolvedChannels()) { }
    /// </summary>
    public IEnumerable<Entities.DiscordChannel> GetResolvedChannels()
    {
        if (Component?.Resolved?.Channels == null) return Array.Empty<Entities.DiscordChannel>();
        return Component.Resolved.Channels.Values;
    }

    /// <summary>
    /// Gets a resolved user by ID from the interaction.
    /// Returns null if user not found in resolved data.
    /// Example: var user = ctx.GetResolvedUser("123456789");
    /// </summary>
    public Entities.DiscordUser? GetResolvedUser(string userId)
    {
        if (Component?.Resolved?.Users == null) return null;
        Component.Resolved.Users.TryGetValue(userId, out var user);
        return user;
    }

    /// <summary>
    /// Gets a resolved member by ID from the interaction.
    /// Returns null if member not found in resolved data.
    /// Example: var member = ctx.GetResolvedMember("123456789");
    /// </summary>
    public Entities.DiscordMember? GetResolvedMember(string userId)
    {
        if (Component?.Resolved?.Members == null) return null;
        Component.Resolved.Members.TryGetValue(userId, out var member);
        return member;
    }

    /// <summary>
    /// Gets a resolved role by ID from the interaction.
    /// Returns null if role not found in resolved data.
    /// Example: var role = ctx.GetResolvedRole("123456789");
    /// </summary>
    public Entities.DiscordRole? GetResolvedRole(string roleId)
    {
        if (Component?.Resolved?.Roles == null) return null;
        Component.Resolved.Roles.TryGetValue(roleId, out var role);
        return role;
    }

    /// <summary>
    /// Gets a resolved channel by ID from the interaction.
    /// Returns null if channel not found in resolved data.
    /// Example: var channel = ctx.GetResolvedChannel("123456789");
    /// </summary>
    public Entities.DiscordChannel? GetResolvedChannel(string channelId)
    {
        if (Component?.Resolved?.Channels == null) return null;
        Component.Resolved.Channels.TryGetValue(channelId, out var channel);
        return channel;
    }

    /// <summary>
    /// Gets the component type of the current interaction.
    /// Returns null if not a component interaction.
    /// Example: int? type = ctx.GetComponentType();
    /// </summary>
    public int? GetComponentType() => Component?.ComponentType;

    /// <summary>
    /// Returns true if this is a select menu interaction.
    /// Example: if (ctx.IsSelectMenu()) { }
    /// </summary>
    public bool IsSelectMenu() => Component?.ComponentType >= 3 && Component?.ComponentType <= 8;

    /// <summary>
    /// Returns true if this is a button interaction.
    /// Example: if (ctx.IsButton()) { }
    /// </summary>
    public bool IsButton() => Component?.ComponentType == 2;

    private static WebhookMessageRequest BuildWebhookRequest(MessagePayload payload, int? flags)
        => new()
        {
            content = payload.content,
            embeds = payload.embeds,
            components = payload.components,
            attachments = payload.attachments,
            allowed_mentions = payload.allowed_mentions,
            flags = flags
        };

    private static InteractionResponseData BuildInteractionData(MessagePayload payload, int? flags)
        => new()
        {
            content = payload.content,
            embeds = payload.embeds,
            components = payload.components,
            allowed_mentions = payload.allowed_mentions,
            flags = flags
        };

    private Task SendFollowupAsync(WebhookMessageRequest request,
        List<(string, ReadOnlyMemory<byte>)>? files, CancellationToken ct)
    {
        if (files is not null && files.Count > 0)
        {
            return _rest.PostMultipartAsync<Entities.DiscordMessage>(
                $"/webhooks/{ApplicationId}/{InteractionToken}", request, files, ct);
        }
        return _rest.PostWebhookFollowupAsync(ApplicationId, InteractionToken, request, ct);
    }

    private Task<Entities.DiscordMessage?> EditFollowupCoreAsync(string messageId,
        WebhookMessageRequest request, List<(string, ReadOnlyMemory<byte>)>? files, CancellationToken ct)
    {
        if (files is not null && files.Count > 0)
        {
            return _rest.PatchMultipartAsync<Entities.DiscordMessage>(
                $"/webhooks/{ApplicationId}/{InteractionToken}/messages/{messageId}", request, files, ct)!;
        }
        return _rest.PatchWebhookMessageAsync<Entities.DiscordMessage>(
            ApplicationId, InteractionToken, messageId, request, ct);
    }

    private async Task AutoDeferAndRespondAsync(WebhookMessageRequest request,
        List<(string, ReadOnlyMemory<byte>)> files, int? flags, CancellationToken ct)
    {
        InteractionResponseData deferData = new() { flags = flags };
        InteractionResponse deferResp = new() { type = 5, data = deferData };

        try
        {
            await _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, deferResp, ct);
            _deferred = true;
        }
        catch
        {
            Interlocked.Exchange(ref _responded, 0);
            throw;
        }

        await SendFollowupAsync(request, files, ct);
    }

    private async Task AutoDeferAndUpdateAsync(WebhookMessageRequest request,
        List<(string, ReadOnlyMemory<byte>)> files, CancellationToken ct)
    {
        InteractionResponse deferResp = new() { type = 6, data = null };

        try
        {
            await _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, deferResp, ct);
            _deferredUpdate = true;
        }
        catch
        {
            Interlocked.Exchange(ref _responded, 0);
            throw;
        }

        await EditFollowupCoreAsync("@original", request, files, ct);
    }

    private Dictionary<string, InteractionOption> GetOptionsCache()
    {
        if (_optionsCache is not null)
            return _optionsCache;

        Dictionary<string, InteractionOption> dict = new(StringComparer.OrdinalIgnoreCase);
        if (Command?.Options is { } options)
        {
            foreach (InteractionOption opt in options)
                dict[opt.Name] = opt;
        }
        _optionsCache = dict;
        return dict;
    }
}