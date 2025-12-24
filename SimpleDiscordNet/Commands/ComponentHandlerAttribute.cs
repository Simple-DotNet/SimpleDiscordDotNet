using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Commands;

/// <summary>
/// Marks a method as a handler for component or modal interactions.
/// Matches on <c>custom_id</c> exactly or by prefix depending on <see cref="Prefix"/>.
/// Methods may optionally take an <see cref="InteractionContext"/> as the first parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class ComponentHandlerAttribute : Attribute
{
    /// <summary>The custom_id to match (exact or as a prefix).</summary>
    public string CustomId { get; }

    /// <summary>When true, matches any interaction whose custom_id starts with <see cref="CustomId"/>.</summary>
    public bool Prefix { get; }

    /// <summary>When set, only matches interactions of this component type. Null matches all types.</summary>
    public ComponentType? ComponentType { get; set; }

    public ComponentHandlerAttribute(string customId, bool prefix = false)
    {
        if (string.IsNullOrWhiteSpace(customId)) throw new ArgumentException("customId is required", nameof(customId));
        CustomId = customId;
        Prefix = prefix;
    }
}

/// <summary>
/// Marks a method as a handler specifically for button interactions.
/// Example: [ButtonHandler("mybutton")]
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ButtonHandlerAttribute : ComponentHandlerAttribute
{
    public ButtonHandlerAttribute(string customId, bool prefix = false) : base(customId, prefix)
    {
        ComponentType = Primitives.ComponentType.Button;
    }
}

/// <summary>
/// Marks a method as a handler specifically for string select menu interactions.
/// Example: [SelectMenuHandler("myselect")]
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class SelectMenuHandlerAttribute : ComponentHandlerAttribute
{
    public SelectMenuHandlerAttribute(string customId, bool prefix = false) : base(customId, prefix)
    {
        ComponentType = Primitives.ComponentType.StringSelect;
    }
}

/// <summary>
/// Marks a method as a handler specifically for user select menu interactions.
/// Example: [UserSelectHandler("userselect")]
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class UserSelectHandlerAttribute : ComponentHandlerAttribute
{
    public UserSelectHandlerAttribute(string customId, bool prefix = false) : base(customId, prefix)
    {
        ComponentType = Primitives.ComponentType.UserSelect;
    }
}

/// <summary>
/// Marks a method as a handler specifically for role select menu interactions.
/// Example: [RoleSelectHandler("roleselect")]
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RoleSelectHandlerAttribute : ComponentHandlerAttribute
{
    public RoleSelectHandlerAttribute(string customId, bool prefix = false) : base(customId, prefix)
    {
        ComponentType = Primitives.ComponentType.RoleSelect;
    }
}

/// <summary>
/// Marks a method as a handler specifically for channel select menu interactions.
/// Example: [ChannelSelectHandler("channelselect")]
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ChannelSelectHandlerAttribute : ComponentHandlerAttribute
{
    public ChannelSelectHandlerAttribute(string customId, bool prefix = false) : base(customId, prefix)
    {
        ComponentType = Primitives.ComponentType.ChannelSelect;
    }
}

/// <summary>
/// Marks a method as a handler specifically for mentionable select menu interactions.
/// Example: [MentionableSelectHandler("mentionselect")]
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MentionableSelectHandlerAttribute : ComponentHandlerAttribute
{
    public MentionableSelectHandlerAttribute(string customId, bool prefix = false) : base(customId, prefix)
    {
        ComponentType = Primitives.ComponentType.MentionableSelect;
    }
}
