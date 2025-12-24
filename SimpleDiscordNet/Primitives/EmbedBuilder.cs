using System;
using System.Collections.Generic;
using System.Linq;
using SimpleDiscordNet.Models;

namespace SimpleDiscordNet;

/// <summary>
/// Fluent helper for building Discord embeds.
/// </summary>
/// <remarks>
/// Example:
/// <code>
/// var embed = new EmbedBuilder()
///     .WithTitle("Hello")
///     .WithDescription("Welcome to SimpleDiscordNet")
///     .WithColor(DiscordColor.Blue)
///     .WithUrl("https://example.com")
///     .WithTimestamp(DateTimeOffset.UtcNow)
///     .WithAuthor("Bot", iconUrl: "https://example.com/icon.png")
///     .WithThumbnail("https://example.com/thumb.png")
///     .WithImage("https://example.com/image.png")
///     .WithFooter("Footer text", iconUrl: "https://example.com/footer.png")
///     .AddField("Name", "Value", inline: true)
///     .AddField("Another", "Second value");
/// </code>
/// </remarks>
public sealed class EmbedBuilder
{
    /// <summary>The embed title (256 chars max).</summary>
    public string? Title { get; private set; }
    /// <summary>The embed description (4096 chars max).</summary>
    public string? Description { get; private set; }
    /// <summary>Optional URL that the title links to.</summary>
    public string? Url { get; private set; }
    /// <summary>Optional timestamp shown under the embed.</summary>
    public DateTimeOffset? Timestamp { get; private set; }
    /// <summary>Color of the embed sidebar.</summary>
    public DiscordColor? Color { get; private set; }
    /// <summary>Footer text and optional icon.</summary>
    public (string text, string? iconUrl)? Footer { get; private set; }
    /// <summary>Author section (name, url, icon).</summary>
    public (string name, string? url, string? iconUrl)? Author { get; private set; }
    /// <summary>Thumbnail image URL.</summary>
    public string? ThumbnailUrl { get; private set; }
    /// <summary>Thumbnail width.</summary>
    public int? ThumbnailWidth { get; private set; }
    /// <summary>Thumbnail height.</summary>
    public int? ThumbnailHeight { get; private set; }
    /// <summary>Main image URL.</summary>
    public string? ImageUrl { get; private set; }
    /// <summary>Main image width.</summary>
    public int? ImageWidth { get; private set; }
    /// <summary>Main image height.</summary>
    public int? ImageHeight { get; private set; }
    /// <summary>Embed fields.</summary>
    public List<(string name, string value, bool inline)> Fields { get; } = [];

    /// <summary>Sets the title.</summary>
    public EmbedBuilder WithTitle(string title) { Title = title; return this; }
    /// <summary>Sets the description.</summary>
    public EmbedBuilder WithDescription(string description) { Description = description; return this; }
    /// <summary>Sets the embed color.</summary>
    public EmbedBuilder WithColor(DiscordColor color) { Color = color; return this; }
    /// <summary>Sets the URL (title becomes a link).</summary>
    public EmbedBuilder WithUrl(string url) { Url = url; return this; }
    /// <summary>Sets the timestamp displayed by Discord.</summary>
    public EmbedBuilder WithTimestamp(DateTimeOffset timestamp) { Timestamp = timestamp; return this; }
    /// <summary>Clears the timestamp.</summary>
    public EmbedBuilder ClearTimestamp() { Timestamp = null; return this; }
    /// <summary>Sets footer text and optional icon.</summary>
    public EmbedBuilder WithFooter(string text, string? iconUrl = null) { Footer = (text, iconUrl); return this; }
    /// <summary>Clears the footer.</summary>
    public EmbedBuilder ClearFooter() { Footer = null; return this; }
    /// <summary>Sets author with optional URL and icon.</summary>
    public EmbedBuilder WithAuthor(string name, string? url = null, string? iconUrl = null) { Author = (name, url, iconUrl); return this; }
    /// <summary>Clears the author.</summary>
    public EmbedBuilder ClearAuthor() { Author = null; return this; }
    /// <summary>Sets a thumbnail image URL with optional width and height.</summary>
    public EmbedBuilder WithThumbnail(string url, int? width = null, int? height = null)
    {
        ThumbnailUrl = url;
        ThumbnailWidth = width;
        ThumbnailHeight = height;
        return this;
    }
    /// <summary>Clears the thumbnail.</summary>
    public EmbedBuilder ClearThumbnail() { ThumbnailUrl = null; ThumbnailWidth = null; ThumbnailHeight = null; return this; }
    /// <summary>Sets a main image URL with optional width and height.</summary>
    public EmbedBuilder WithImage(string url, int? width = null, int? height = null)
    {
        ImageUrl = url;
        ImageWidth = width;
        ImageHeight = height;
        return this;
    }
    /// <summary>Clears the image.</summary>
    public EmbedBuilder ClearImage() { ImageUrl = null; ImageWidth = null; ImageHeight = null; return this; }
    /// <summary>Adds a field.</summary>
    public EmbedBuilder AddField(string name, string value, bool inline = false) { Fields.Add((name, value, inline)); return this; }
    /// <summary>Adds multiple fields.</summary>
    public EmbedBuilder AddFields(IEnumerable<(string name, string value, bool inline)> fields) { Fields.AddRange(fields); return this; }
    /// <summary>Clears all fields.</summary>
    public EmbedBuilder ClearFields() { Fields.Clear(); return this; }

    internal Embed Build()
    {
        Embed model = new()
        {
            title = Title,
            description = Description,
            url = Url,
            timestamp = Timestamp,
            color = Color?.Value,
            footer = Footer is null ? null : new EmbedFooter { text = Footer.Value.text, icon_url = Footer.Value.iconUrl },
            author = Author is null ? null : new EmbedAuthor { name = Author.Value.name, url = Author.Value.url, icon_url = Author.Value.iconUrl },
            thumbnail = ThumbnailUrl is null ? null : new EmbedThumbnail { url = ThumbnailUrl, width = ThumbnailWidth, height = ThumbnailHeight },
            image = ImageUrl is null ? null : new EmbedImage { url = ImageUrl, width = ImageWidth, height = ImageHeight },
            fields = Fields.Count == 0 ? null : Fields.Select(static f => new EmbedField { name = f.name, value = f.value, inline = f.inline ? true : null }).ToArray()
        };
        return model;
    }
}
