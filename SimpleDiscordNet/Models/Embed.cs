using System;

namespace SimpleDiscordNet.Models;

/// <summary>
/// Public Embed model that can be passed to any API. You can also use <see cref="SimpleDiscordNet.EmbedBuilder"/>.
/// </summary>
public sealed class Embed
{
    public string? title { get; set; }
    public string? description { get; set; }
    public string? url { get; set; }
    public DateTimeOffset? timestamp { get; set; }
    public int? color { get; set; }
    public EmbedFooter? footer { get; set; }
    public EmbedImage? image { get; set; }
    public EmbedThumbnail? thumbnail { get; set; }
    public EmbedAuthor? author { get; set; }
    public EmbedField[]? fields { get; set; }
}

public sealed class EmbedFooter
{
    public string? text { get; set; }
    public string? icon_url { get; set; }
}

public sealed class EmbedImage
{
    public string? url { get; set; }
    public int? width { get; set; }
    public int? height { get; set; }
}

public sealed class EmbedThumbnail
{
    public string? url { get; set; }
    public int? width { get; set; }
    public int? height { get; set; }
}

public sealed class EmbedAuthor
{
    public string? name { get; set; }
    public string? url { get; set; }
    public string? icon_url { get; set; }
}

public sealed class EmbedField
{
    public string? name { get; set; }
    public string? value { get; set; }
    public bool? inline { get; set; }
}
