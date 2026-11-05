using System.Threading.Channels;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Requests;
using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Commands;

public sealed partial class InteractionContext
{
    private const int MaxButtonsPerRow = 5;
    private const int MaxChoices = 25;
    private const int SecondaryButton = 2;
    private const int PrimaryButton = 1;
    private const string OriginalMessage = "@original";
    private const string PreviousAction = "prev";
    private const string NextAction = "next";
    private const string PageCountAction = "count";
    private const string NotYourButtons = "Only the person who started this can use these buttons.";

    private static readonly TimeSpan DefaultQuestionTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DefaultPagesTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Asks a yes/no question with buttons and waits for the answer.
    /// Returns false if the user says no or doesn't answer in time.
    /// Example: if (await ctx.ConfirmAsync("Delete this channel?")) await channel.DeleteAsync();
    /// </summary>
    /// <param name="question">The question to show</param>
    /// <param name="timeout">How long to wait. Defaults to 1 minute</param>
    /// <param name="yes">The label of the yes button</param>
    /// <param name="no">The label of the no button</param>
    /// <param name="ct">Cancellation token</param>
    public async Task<bool> ConfirmAsync(string question, TimeSpan? timeout = null, string yes = "Yes", string no = "No", CancellationToken ct = default)
        => await ChooseAsync(question, [yes, no], timeout, ct).ConfigureAwait(false) == yes;

    /// <summary>
    /// Shows a button for each option and waits for a choice.
    /// Returns the chosen option, or null if the user doesn't answer in time.
    /// Example: string? size = await ctx.ChooseAsync("Pick a size", ["Small", "Large"]);
    /// </summary>
    /// <param name="question">The question to show</param>
    /// <param name="options">Up to 25 button labels</param>
    /// <param name="timeout">How long to wait. Defaults to 1 minute</param>
    /// <param name="ct">Cancellation token</param>
    public async Task<string?> ChooseAsync(string question, IReadOnlyList<string> options, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        if (options.Count is 0 or > MaxChoices)
            throw new ArgumentException($"Provide between 1 and {MaxChoices} options.", nameof(options));

        string session = InteractiveSessions.Open(out ChannelReader<InteractionContext> clicks);
        try
        {
            Button[] buttons = options
                .Select((label, index) => new Button(label, InteractiveSessions.ButtonId(session, index.ToString(System.Globalization.CultureInfo.InvariantCulture)), index == 0 ? PrimaryButton : SecondaryButton))
                .ToArray();
            IComponent[] rows = buttons.Chunk(MaxButtonsPerRow).Select(static row => (IComponent)new ActionRow(row)).ToArray();

            string messageId = await SendInteractiveAsync(new MessageBuilder().WithContent(question).WithComponents(rows), ct).ConfigureAwait(false);

            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(timeout ?? DefaultQuestionTimeout);
            try
            {
                while (true)
                {
                    InteractionContext click = await clicks.ReadAsync(deadline.Token).ConfigureAwait(false);
                    if (await RejectOthersAsync(click, ct).ConfigureAwait(false)) continue;

                    string choice = options[int.Parse(InteractiveSessions.ActionOf(click.CustomId!), System.Globalization.CultureInfo.InvariantCulture)];
                    await click.ReplaceMessageAsync(new MessageBuilder().WithContent($"{question}\n**{choice}**"), ct).ConfigureAwait(false);
                    return choice;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await RemoveButtonsAsync(this, messageId, new MessageBuilder().WithContent(question), ct).ConfigureAwait(false);
                return null;
            }
        }
        finally
        {
            InteractiveSessions.Close(session);
        }
    }

    /// <summary>
    /// Shows pages of text with next and previous buttons. Returns once the buttons time out,
    /// so call this last in your handler.
    /// Example: await ctx.PaginateAsync(["Page one", "Page two"]);
    /// </summary>
    /// <param name="pages">The pages to show</param>
    /// <param name="timeout">How long the buttons work after the last click. Defaults to 3 minutes</param>
    /// <param name="ct">Cancellation token</param>
    public Task PaginateAsync(IEnumerable<string> pages, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        string[] all = pages.ToArray();
        return PaginateCoreAsync(all.Length, page => new MessageBuilder().WithContent(all[page]), timeout, ct);
    }

    /// <summary>
    /// Shows pages of embeds with next and previous buttons. Returns once the buttons time out,
    /// so call this last in your handler.
    /// Example: await ctx.PaginateAsync(embeds);
    /// </summary>
    /// <param name="pages">The pages to show</param>
    /// <param name="timeout">How long the buttons work after the last click. Defaults to 3 minutes</param>
    /// <param name="ct">Cancellation token</param>
    public Task PaginateAsync(IEnumerable<EmbedBuilder> pages, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        EmbedBuilder[] all = pages.ToArray();
        return PaginateCoreAsync(all.Length, page => new MessageBuilder().WithEmbed(all[page]), timeout, ct);
    }

    private async Task PaginateCoreAsync(int count, Func<int, MessageBuilder> render, TimeSpan? timeout, CancellationToken ct)
    {
        if (count == 0) throw new ArgumentException("Provide at least one page.");

        if (count == 1)
        {
            await RespondAsync(render(0), ct: ct).ConfigureAwait(false);
            return;
        }

        string session = InteractiveSessions.Open(out ChannelReader<InteractionContext> clicks);
        try
        {
            int page = 0;
            InteractionContext latest = this;
            string messageId = await SendInteractiveAsync(WithPageButtons(render(page), session, page, count), ct).ConfigureAwait(false);

            try
            {
                while (true)
                {
                    using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    deadline.CancelAfter(timeout ?? DefaultPagesTimeout);

                    InteractionContext click = await clicks.ReadAsync(deadline.Token).ConfigureAwait(false);
                    if (await RejectOthersAsync(click, ct).ConfigureAwait(false)) continue;

                    string action = InteractiveSessions.ActionOf(click.CustomId!);
                    page = Math.Clamp(action == NextAction ? page + 1 : page - 1, 0, count - 1);

                    await click.ReplaceMessageAsync(WithPageButtons(render(page), session, page, count), ct).ConfigureAwait(false);
                    latest = click;
                    messageId = OriginalMessage;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await RemoveButtonsAsync(latest, messageId, render(page), ct).ConfigureAwait(false);
            }
        }
        finally
        {
            InteractiveSessions.Close(session);
        }
    }

    private static MessageBuilder WithPageButtons(MessageBuilder message, string session, int page, int count)
    {
        return message.WithComponents(
            new Button("◀", InteractiveSessions.ButtonId(session, PreviousAction), SecondaryButton, disabled: page == 0),
            new Button($"{page + 1} / {count}", InteractiveSessions.ButtonId(session, PageCountAction), SecondaryButton, disabled: true),
            new Button("▶", InteractiveSessions.ButtonId(session, NextAction), SecondaryButton, disabled: page == count - 1));
    }

    private async ValueTask<bool> RejectOthersAsync(InteractionContext click, CancellationToken ct)
    {
        if (click.User?.Id == User?.Id) return false;

        await click.RespondAsync(NotYourButtons, null, true, ct: ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Sends a message with buttons and returns the ID to edit it by later.
    /// </summary>
    private async Task<string> SendInteractiveAsync(MessageBuilder builder, CancellationToken ct)
    {
        MessagePayload payload = builder.Build();
        int? flags = _deferredEphemeral ? EphemeralFlag : null;

        // Once anything has been acknowledged, this has to be a followup
        if (_deferred || _deferredUpdate || Volatile.Read(ref _responded) == 1)
        {
            DiscordMessage? message = await _rest.PostAsync<DiscordMessage>($"/webhooks/{ApplicationId}/{InteractionToken}", BuildWebhookRequest(payload, flags), ct).ConfigureAwait(false);
            _deferred = true;
            return message is null ? OriginalMessage : message.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        await RespondAsync(builder, ct: ct).ConfigureAwait(false);
        _deferred = true;
        return OriginalMessage;
    }

    /// <summary>
    /// Answers a button click by replacing its message, including removing buttons the new message doesn't have.
    /// </summary>
    private Task ReplaceMessageAsync(MessageBuilder builder, CancellationToken ct)
    {
        Interlocked.Exchange(ref _responded, 1);

        MessagePayload payload = builder.Build();
        InteractionResponseData data = BuildInteractionData(payload, null);
        data.components = payload.components ?? [];
        return _rest.PostInteractionCallbackAsync(InteractionId, InteractionToken, new InteractionResponse { type = 7, data = data }, ct);
    }

    /// <summary>
    /// Takes the buttons off a message after a timeout. This is cosmetic, so a failure is ignored:
    /// the message may be gone, or the interaction's token may have expired.
    /// </summary>
    private static async Task RemoveButtonsAsync(InteractionContext owner, string messageId, MessageBuilder content, CancellationToken ct)
    {
        WebhookMessageRequest request = BuildWebhookRequest(content.Build(), null);
        request.components = [];
        try
        {
            await owner.EditFollowupCoreAsync(messageId, request, null, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
        }
    }
}
