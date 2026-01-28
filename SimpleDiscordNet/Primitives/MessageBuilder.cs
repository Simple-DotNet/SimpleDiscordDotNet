using SimpleDiscordNet.Models;
using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet;

/// <summary>
/// Simple builder for creating Discord messages with content, embeds, and components.
/// Example: var msg = new MessageBuilder().WithContent("Hello").WithEmbed(embed).Build();
/// </summary>
public sealed class MessageBuilder
{
    private string? _content;
    private EmbedBuilder? _embed;
    private List<EmbedBuilder>? _embeds;
    private List<IComponent>? _components;
    private MentionBuilder? _mentionBuilder;
    private List<(string fileName, ReadOnlyMemory<byte> data)>? _files;

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

        object[]? components = null;
        if (_components is not null && _components.Count > 0)
        {
            List<object> rows = new();
            List<object> nonActionRowComponents = new();
            foreach (IComponent component in _components)
            {
                if (component is ActionRow row)
                {
                    // Flush any accumulated non-ActionRow components
                    if (nonActionRowComponents.Count > 0)
                    {
                        rows.Add(new ActionRow(nonActionRowComponents.ToArray()));
                        nonActionRowComponents.Clear();
                    }
                    rows.Add(row);
                }
                else
                {
                    nonActionRowComponents.Add(component);
                }
            }
            if (nonActionRowComponents.Count > 0)
            {
                rows.Add(new ActionRow(nonActionRowComponents.ToArray()));
            }
            components = rows.ToArray();
        }

        object[]? attachments = null;
        if (_files is not null && _files.Count > 0)
        {
            attachments = new object[_files.Count];
            for (int i = 0; i < _files.Count; i++)
                attachments[i] = new { id = i, filename = _files[i].fileName };
        }

        return new MessagePayload
        {
            content = _content,
            embeds = embedList.Count > 0 ? embedList.ToArray() : null,
            components = components,
            allowed_mentions = _mentionBuilder?.BuildAllowedMentions(),
            attachments = attachments
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
        return this;
    }
}
