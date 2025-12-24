namespace SimpleDiscordNet.Primitives;

/// <summary>
/// Fluent builder for creating Discord buttons with named styles.
/// Example: var btn = ButtonBuilder.Primary("Click me", "button_id");
/// </summary>
public static class ButtonBuilder
{
    /// <summary>
    /// Creates a primary (blurple) button.
    /// Example: var btn = ButtonBuilder.Primary("Confirm", "confirm_btn");
    /// </summary>
    public static Button Primary(string label, string customId, bool disabled = false)
        => new(label, customId, style: 1, disabled);

    /// <summary>
    /// Creates a secondary (grey) button.
    /// Example: var btn = ButtonBuilder.Secondary("Cancel", "cancel_btn");
    /// </summary>
    public static Button Secondary(string label, string customId, bool disabled = false)
        => new(label, customId, style: 2, disabled);

    /// <summary>
    /// Creates a success (green) button.
    /// Example: var btn = ButtonBuilder.Success("Accept", "accept_btn");
    /// </summary>
    public static Button Success(string label, string customId, bool disabled = false)
        => new(label, customId, style: 3, disabled);

    /// <summary>
    /// Creates a danger (red) button.
    /// Example: var btn = ButtonBuilder.Danger("Delete", "delete_btn");
    /// </summary>
    public static Button Danger(string label, string customId, bool disabled = false)
        => new(label, customId, style: 4, disabled);

    /// <summary>
    /// Creates a link button that opens a URL.
    /// Example: var btn = ButtonBuilder.Link("Visit Website", "https://example.com");
    /// </summary>
    public static Button Link(string label, string url)
        => new(label, url);
}
