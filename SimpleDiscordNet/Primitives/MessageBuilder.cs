using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Requests;
using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet;

/// <summary>
/// Simple builder for creating Discord messages with content, embeds, and components.
/// Example: var msg = new MessageBuilder().WithContent("Hello").WithEmbed(embed).Build();
/// </summary>
public sealed class MessageBuilder
{
    private const int IsComponentsV2Flag = 1 << 15;

    private string? _content;
    private EmbedBuilder? _embed;
    private List<EmbedBuilder>? _embeds;
    private List<IComponent>? _components;
    private MentionBuilder? _mentionBuilder;
    private List<(string fileName, ReadOnlyMemory<byte> data)>? _files;
    private string? _replyMessageId;
    private bool? _replyFailIfNotExists;
    private string? _threadName;
    private List<string>? _appliedTags;
    private PollBuilder? _poll;

    /// <summary>
    /// Sets the message text content.
    /// Example: builder.WithContent("Hello, world!");
    /// </summary>
    public MessageBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    /// <summary>
    /// Adds an embed to the message.
    /// Example: builder.WithEmbed(new EmbedBuilder().WithTitle("Title"));
    /// </summary>
    public MessageBuilder WithEmbed(EmbedBuilder embed)
    {
        _embed = embed;
        return this;
    }

    /// <summary>
    /// Adds multiple embeds to the message (up to 10).
    /// Example: builder.WithEmbeds(embed1, embed2, embed3);
    /// </summary>
    public MessageBuilder WithEmbeds(params EmbedBuilder[] embeds)
    {
        _embeds ??= [];
        _embeds.AddRange(embeds);
        return this;
    }

    /// <summary>
    /// Adds a button to the message.
    /// Example: builder.WithButton("Click me", "button_id");
    /// </summary>
    public MessageBuilder WithButton(string label, string customId, int style = 1)
    {
        _components ??= [];
        _components.Add(new Button(label, customId, style));
        return this;
    }

    /// <summary>
    /// Adds a link button to the message.
    /// Example: builder.WithLinkButton("Visit Website", "https://example.com");
    /// </summary>
    public MessageBuilder WithLinkButton(string label, string url)
    {
        _components ??= [];
        _components.Add(new Button(label, url));
        return this;
    }

    /// <summary>
    /// Adds multiple buttons to the message.
    /// Example: builder.WithButtons(new Button("Yes", "yes"), new Button("No", "no"));
    /// </summary>
    public MessageBuilder WithButtons(params Button[] buttons)
    {
        _components ??= [];
        foreach (Button btn in buttons)
            _components.Add(btn);
        return this;
    }

    /// <summary>
    /// Adds a string select menu to the message.
    /// Example: builder.WithSelect("select_id", new SelectOption("Label", "value"));
    /// </summary>
    public MessageBuilder WithSelect(string customId, params SelectOption[] options)
    {
        _components ??= [];
        _components.Add(new StringSelect(customId, options));
        return this;
    }

    /// <summary>
    /// Adds components to the message.
    /// </summary>
    public MessageBuilder WithComponents(params IComponent[] components)
    {
        _components ??= [];
        _components.AddRange(components);
        return this;
    }

    /// <summary>
    /// Adds a block of markdown text to a layout message.
    /// Example: builder.WithText("## Build passed");
    /// </summary>
    public MessageBuilder WithText(string markdown)
        => WithComponents(new TextDisplay(markdown));

    /// <summary>
    /// Adds a box that groups components, with an optional accent color.
    /// Example: builder.WithContainer(new TextDisplay("Build passed"), new Button("Logs", "logs"));
    /// </summary>
    public MessageBuilder WithContainer(params IComponent[] components)
        => WithComponents(new Container(components));

    /// <summary>
    /// Adds text with a button or thumbnail beside it.
    /// Example: builder.WithSection("Ready to deploy?", new Button("Deploy", "deploy"));
    /// </summary>
    public MessageBuilder WithSection(string text, IComponent accessory)
        => WithComponents(new Section(text, accessory));

    /// <summary>
    /// Adds a gallery of 1 to 10 images or videos.
    /// Example: builder.WithGallery("https://example.com/a.png", "https://example.com/b.png");
    /// </summary>
    public MessageBuilder WithGallery(params string[] urls)
        => WithComponents(new MediaGallery(urls));

    /// <summary>
    /// Adds space between components, with an optional divider line.
    /// </summary>
    public MessageBuilder WithSeparator(bool divider = true, bool largeSpacing = false)
        => WithComponents(new Separator(divider, largeSpacing));

    /// <summary>
    /// Sets mention behavior using a MentionBuilder.
    /// Example: builder.WithMention(MentionBuilder.User(userId));
    /// </summary>
    public MessageBuilder WithMention(MentionBuilder mentionBuilder)
    {
        _mentionBuilder = mentionBuilder;
        return this;
    }

    /// <summary>
    /// Enables @everyone mentions.
    /// </summary>
    public MessageBuilder WithEveryoneMention()
    {
        _mentionBuilder = MentionBuilder.Everyone();
        return this;
    }

    /// <summary>
    /// Disables all mentions.
    /// </summary>
    public MessageBuilder WithNoMentions()
    {
        _mentionBuilder = MentionBuilder.None();
        return this;
    }

    /// <summary>
    /// Sets this message as a reply to another message.
    /// Example: builder.WithReply("123456789012345678");
    /// </summary>
    public MessageBuilder WithReply(string messageId, bool failIfNotExists = true)
    {
        _replyMessageId = messageId;
        _replyFailIfNotExists = failIfNotExists;
        return this;
    }

    /// <summary>
    /// Adds a poll that runs for Discord's default of 24 hours.
    /// Example: builder.WithPoll("Pizza or tacos?", "Pizza", "Tacos");
    /// </summary>
    public MessageBuilder WithPoll(string question, params string[] answers)
    {
        PollBuilder poll = new(question);
        foreach (string answer in answers)
            poll.AddAnswer(answer);
        _poll = poll;
        return this;
    }

    /// <summary>
    /// Adds a poll with custom settings.
    /// Example: builder.WithPoll(new PollBuilder("Pizza or tacos?").AddAnswer("Pizza", "🍕").AddAnswer("Tacos", "🌮").WithDuration(48));
    /// </summary>
    public MessageBuilder WithPoll(PollBuilder poll)
    {
        _poll = poll;
        return this;
    }

    /// <summary>
    /// Creates this message as a forum post with a thread name.
    /// Send it to a forum or media channel. To use a tag, pass its ID from the channel's Available_Tags.
    /// Example: builder.WithForumPost("My Forum Post");
    /// </summary>
    public MessageBuilder WithForumPost(string threadName, params string[] appliedTags)
    {
        _threadName = threadName;
        if (appliedTags.Length > 0)
        {
            _appliedTags ??= [];
            _appliedTags.AddRange(appliedTags);
        }
        return this;
    }

    /// <summary>
    /// Adds applied tags for a forum post.
    /// </summary>
    public MessageBuilder WithAppliedTags(params string[] tags)
    {
        _appliedTags ??= [];
        _appliedTags.AddRange(tags);
        return this;
    }

    /// <summary>
    /// Adds a file attachment from a byte array.
    /// Example: builder.AddFile("document.pdf", fileBytes);
    /// </summary>
    public MessageBuilder AddFile(string fileName, byte[] data)
    {
        _files ??= [];
        _files.Add((fileName, data));
        return this;
    }

    /// <summary>
    /// Adds a file attachment from ReadOnlyMemory.
    /// Example: builder.AddFile("image.png", imageData);
    /// </summary>
    public MessageBuilder AddFile(string fileName, ReadOnlyMemory<byte> data)
    {
        _files ??= [];
        _files.Add((fileName, data));
        return this;
    }

    /// <summary>
    /// Adds a file attachment from a MemoryStream.
    /// Example: builder.AddFile("data.json", memoryStream);
    /// </summary>
    public MessageBuilder AddFile(string fileName, MemoryStream stream)
    {
        _files ??= [];
        _files.Add((fileName, stream.ToArray()));
        return this;
    }

    /// <summary>
    /// Adds a file attachment from a Stream (reads entire stream into memory).
    /// Example: builder.AddFile("file.txt", fileStream);
    /// </summary>
    public MessageBuilder AddFile(string fileName, Stream stream)
    {
        using MemoryStream ms = new();
        stream.CopyTo(ms);
        _files ??= [];
        _files.Add((fileName, ms.ToArray()));
        return this;
    }

    /// <summary>
    /// Adds a file attachment from a Stream asynchronously (reads entire stream into memory).
    /// Example: await builder.AddFileAsync("file.txt", fileStream);
    /// </summary>
    public async Task<MessageBuilder> AddFileAsync(string fileName, Stream stream, CancellationToken ct = default)
    {
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms, ct);
        _files ??= [];
        _files.Add((fileName, ms.ToArray()));
        return this;
    }

    /// <summary>
    /// Builds the message payload for sending via the Discord API.
    /// </summary>
    internal MessagePayload Build()
    {
        List<Embed> embedList = [];
        if (_embed is not null)
            embedList.Add(_embed.Build());
        if (_embeds is not null)
        {
            foreach (EmbedBuilder e in _embeds)
                embedList.Add(e.Build());
        }

        IComponent[]? components = _components is { Count: > 0 } ? ComponentLayout.Arrange(_components) : null;
        string? content = _content;
        int? flags = null;

        // Layout components replace content and embeds, so plain content moves into the layout
        if (components is not null && components.Any(static c => c is not ActionRow))
        {
            if (embedList.Count > 0 || _poll is not null)
                throw new InvalidOperationException("A message with layout components, such as a Container or TextDisplay, can't also have embeds or a poll.");

            if (!string.IsNullOrEmpty(content))
            {
                components = [new TextDisplay(content), .. components];
                content = null;
            }
            flags = IsComponentsV2Flag;
        }

        AttachmentReference[]? attachments = null;
        if (_files is not null && _files.Count > 0)
        {
            attachments = new AttachmentReference[_files.Count];
            for (int i = 0; i < _files.Count; i++)
                attachments[i] = new AttachmentReference(i, _files[i].fileName);
        }

        return new MessagePayload
        {
            content = content,
            embeds = embedList.Count > 0 ? embedList.ToArray() : null,
            components = components,
            allowed_mentions = _mentionBuilder?.BuildAllowedMentions(),
            attachments = attachments,
            message_reference = _replyMessageId is not null ? new MessageReference { message_id = _replyMessageId, fail_if_not_exists = _replyFailIfNotExists } : null,
            thread_name = _threadName,
            applied_tags = _appliedTags?.ToArray(),
            poll = _poll?.Build(),
            flags = flags
        };
    }

    /// <summary>
    /// Gets the list of file attachments.
    /// </summary>
    internal List<(string fileName, ReadOnlyMemory<byte> data)>? GetFiles() => _files;

    /// <summary>
    /// Gets the message content text.
    /// </summary>
    public string? Content => _content;

    /// <summary>
    /// Clears all message content.
    /// </summary>
    public MessageBuilder Clear()
    {
        _content = null;
        _embed = null;
        _embeds?.Clear();
        _components?.Clear();
        _mentionBuilder = null;
        _files?.Clear();
        _replyMessageId = null;
        _replyFailIfNotExists = null;
        _threadName = null;
        _appliedTags?.Clear();
        _poll = null;
        return this;
    }
}
