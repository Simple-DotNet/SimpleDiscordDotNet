using System.Text.Json.Serialization;

namespace SimpleDiscordNet.Primitives;

// Layout components for messages, also known as Components V2.
// MessageBuilder sets the flag these need. Such a message can't also have embeds or a poll.

/// <summary>
/// Markdown text.
/// Example: new TextDisplay("**Server status:** online");
/// </summary>
public sealed class TextDisplay : IComponent
{
    [JsonIgnore] public int type => 10;
    public string content { get; }

    public TextDisplay(string content)
    {
        this.content = content;
    }
}

/// <summary>
/// Adds space between components, with an optional divider line.
/// </summary>
public sealed class Separator : IComponent
{
    private const int LargeSpacing = 2;

    [JsonIgnore] public int type => 14;
    public bool? divider { get; }
    public int? spacing { get; }

    public Separator(bool divider = true, bool largeSpacing = false)
    {
        this.divider = divider;
        spacing = largeSpacing ? LargeSpacing : null;
    }
}

/// <summary>
/// A link to an image, video or file. Use attachment://filename for a file on the same message.
/// </summary>
public sealed class MediaItem
{
    public string url { get; }

    public MediaItem(string url)
    {
        this.url = url;
    }
}

/// <summary>
/// A small image, used as the accessory of a <see cref="Section"/>.
/// </summary>
public sealed class Thumbnail : IComponent
{
    [JsonIgnore] public int type => 11;
    public MediaItem media { get; }
    public string? description { get; }
    public bool? spoiler { get; }

    public Thumbnail(string url, string? description = null, bool spoiler = false)
    {
        media = new MediaItem(url);
        this.description = description;
        this.spoiler = spoiler ? true : null;
    }
}

/// <summary>
/// One entry in a <see cref="MediaGallery"/>.
/// </summary>
public sealed class MediaGalleryItem
{
    public MediaItem media { get; }
    public string? description { get; }
    public bool? spoiler { get; }

    public MediaGalleryItem(string url, string? description = null, bool spoiler = false)
    {
        media = new MediaItem(url);
        this.description = description;
        this.spoiler = spoiler ? true : null;
    }
}

/// <summary>
/// Shows 1 to 10 images or videos.
/// Example: new MediaGallery("https://example.com/a.png", "https://example.com/b.png");
/// </summary>
public sealed class MediaGallery : IComponent
{
    [JsonIgnore] public int type => 12;
    public MediaGalleryItem[] items { get; }

    public MediaGallery(params string[] urls)
    {
        items = urls.Select(static url => new MediaGalleryItem(url)).ToArray();
    }

    public MediaGallery(IEnumerable<MediaGalleryItem> items)
    {
        this.items = items.ToArray();
    }
}

/// <summary>
/// Shows a file attached to the same message with MessageBuilder.AddFile.
/// Example: new FileComponent("report.pdf");
/// </summary>
public sealed class FileComponent : IComponent
{
    [JsonIgnore] public int type => 13;
    public MediaItem file { get; }
    public bool? spoiler { get; }

    public FileComponent(string fileName, bool spoiler = false)
    {
        file = new MediaItem($"attachment://{fileName}");
        this.spoiler = spoiler ? true : null;
    }
}

/// <summary>
/// Text with a button or thumbnail beside it.
/// Example: new Section("Ready to deploy?", new Button("Deploy", "deploy"));
/// </summary>
public sealed class Section : IComponent
{
    [JsonIgnore] public int type => 9;
    public IComponent[] components { get; }
    public IComponent accessory { get; }

    public Section(string text, IComponent accessory)
    {
        components = [new TextDisplay(text)];
        this.accessory = accessory;
    }

    /// <param name="lines">One to three blocks of text</param>
    /// <param name="accessory">The button or thumbnail</param>
    public Section(IEnumerable<string> lines, IComponent accessory)
    {
        components = lines.Select(static line => (IComponent)new TextDisplay(line)).ToArray();
        this.accessory = accessory;
    }
}

/// <summary>
/// Groups components in a box with an optional accent color. Buttons and selects are placed in rows for you.
/// Example: new Container(new TextDisplay("Build passed"), new Button("Logs", "logs")).WithColor(DiscordColor.Green);
/// </summary>
public sealed class Container : IComponent
{
    [JsonIgnore] public int type => 17;
    public IComponent[] components { get; }
    public int? accent_color { get; private set; }
    public bool? spoiler { get; private set; }

    public Container(params IComponent[] components)
    {
        this.components = ComponentLayout.Arrange(components);
    }

    /// <summary>Sets the accent color from an RGB value such as 0x00FF00.</summary>
    public Container WithColor(int rgb)
    {
        accent_color = rgb;
        return this;
    }

    /// <summary>Sets the accent color.</summary>
    public Container WithColor(DiscordColor color)
    {
        accent_color = color.Value;
        return this;
    }

    /// <summary>Hides the container's contents until clicked.</summary>
    public Container AsSpoiler()
    {
        spoiler = true;
        return this;
    }
}

internal static class ComponentLayout
{
    private static bool IsLayout(IComponent component)
        => component is ActionRow or TextDisplay or Section or Separator or MediaGallery or FileComponent or Container;

    /// <summary>
    /// Keeps layout components as they are and groups the others, such as buttons, into action rows.
    /// </summary>
    public static IComponent[] Arrange(IEnumerable<IComponent> components)
    {
        List<IComponent> arranged = [];
        List<IComponent> row = [];

        foreach (IComponent component in components)
        {
            if (!IsLayout(component))
            {
                row.Add(component);
                continue;
            }

            FlushRow(arranged, row);
            arranged.Add(component);
        }

        FlushRow(arranged, row);
        return arranged.ToArray();
    }

    private static void FlushRow(List<IComponent> arranged, List<IComponent> row)
    {
        if (row.Count == 0) return;
        arranged.Add(new ActionRow(row.ToArray()));
        row.Clear();
    }
}
