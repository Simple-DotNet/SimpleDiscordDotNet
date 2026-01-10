namespace SimpleDiscordNet.Primitives;

// Public DTOs for Discord message components and modals
// Property names match Discord's JSON schema for simple serialization via System.Text.Json

/// <summary>
/// Discord component types
/// </summary>
public enum ComponentType
{
    ActionRow = 1,
    Button = 2,
    StringSelect = 3,
    TextInput = 4,
    UserSelect = 5,
    RoleSelect = 6,
    MentionableSelect = 7,
    ChannelSelect = 8
}

public interface IComponent
{
    int type { get; }
}

public sealed class ActionRow : IComponent
{
    public int type => 1;
    public object[] components { get; }
    public ActionRow(params object[] components)
    {
        this.components = components;
    }
}

public sealed class Button : IComponent
{
    public int type => 2;
    public string? custom_id { get; }
    public string? label { get; }
    public int style { get; }
    public string? url { get; }
    public bool? disabled { get; }

    // style: 1=primary,2=secondary,3=success,4=danger,5=link
    public Button(string label, string customId, int style = 1, bool disabled = false)
    {
        this.label = label;
        this.custom_id = style == 5 ? null : customId; // link buttons cannot have custom_id
        this.style = style;
        this.disabled = disabled ? true : null;
    }

    public Button(string label, string url)
    {
        this.label = label;
        this.style = 5;
        this.url = url;
    }
}

public sealed class SelectOption
{
    public string label { get; }
    public string value { get; }
    public string? description { get; }
    public bool? @default { get; }
    public object? emoji { get; }
    public SelectOption(string label, string value, string? description = null, bool? @default = null, string? emojiName = null, string? emojiId = null)
    {
        this.label = label;
        this.value = value;
        this.description = description;
        this.@default = @default;

        if (emojiName != null || emojiId != null)
        {
            this.emoji = new { name = emojiName, id = emojiId };
        }
    }
}

/// <summary>
/// Represents a default value for entity select menus (user, role, channel)
/// </summary>
public sealed class SelectDefaultValue
{
    public string id { get; }
    public string type { get; } // "user", "role", or "channel"

    public SelectDefaultValue(string id, string type)
    {
        this.id = id;
        this.type = type;
    }

    public static SelectDefaultValue User(string userId) => new(userId, "user");
    public static SelectDefaultValue User(ulong userId) => new(userId.ToString(), "user");
    public static SelectDefaultValue Role(string roleId) => new(roleId, "role");
    public static SelectDefaultValue Role(ulong roleId) => new(roleId.ToString(), "role");
    public static SelectDefaultValue Channel(string channelId) => new(channelId, "channel");
    public static SelectDefaultValue Channel(ulong channelId) => new(channelId.ToString(), "channel");
}

public sealed class StringSelect : IComponent
{
    public int type => 3;
    public string custom_id { get; }
    public SelectOption[] options { get; }
    public string? placeholder { get; }
    public int? min_values { get; }
    public int? max_values { get; }
    public bool? disabled { get; }
    public StringSelect(string customId, IEnumerable<SelectOption> options, string? placeholder = null, int? min = null, int? max = null, bool disabled = false)
    {
        custom_id = customId;
        this.options = options.ToArray();
        this.placeholder = placeholder;
        this.min_values = min;
        this.max_values = max;
        this.disabled = disabled ? true : null;
    }
}

public sealed class UserSelect : IComponent
{
    public int type => 5;
    public string custom_id { get; }
    public string? placeholder { get; }
    public int? min_values { get; }
    public int? max_values { get; }
    public bool? disabled { get; }
    public SelectDefaultValue[]? default_values { get; }
    public UserSelect(string customId, string? placeholder = null, int? min = null, int? max = null, bool disabled = false, IEnumerable<SelectDefaultValue>? defaultValues = null)
    {
        custom_id = customId;
        this.placeholder = placeholder;
        this.min_values = min;
        this.max_values = max;
        this.disabled = disabled ? true : null;
        this.default_values = defaultValues?.ToArray();
    }
}

public sealed class RoleSelect : IComponent
{
    public int type => 6;
    public string custom_id { get; }
    public string? placeholder { get; }
    public int? min_values { get; }
    public int? max_values { get; }
    public bool? disabled { get; }
    public SelectDefaultValue[]? default_values { get; }
    public RoleSelect(string customId, string? placeholder = null, int? min = null, int? max = null, bool disabled = false, IEnumerable<SelectDefaultValue>? defaultValues = null)
    {
        custom_id = customId;
        this.placeholder = placeholder;
        this.min_values = min;
        this.max_values = max;
        this.disabled = disabled ? true : null;
        this.default_values = defaultValues?.ToArray();
    }
}

public sealed class MentionableSelect : IComponent
{
    public int type => 7;
    public string custom_id { get; }
    public string? placeholder { get; }
    public int? min_values { get; }
    public int? max_values { get; }
    public bool? disabled { get; }
    public SelectDefaultValue[]? default_values { get; }
    public MentionableSelect(string customId, string? placeholder = null, int? min = null, int? max = null, bool disabled = false, IEnumerable<SelectDefaultValue>? defaultValues = null)
    {
        custom_id = customId;
        this.placeholder = placeholder;
        this.min_values = min;
        this.max_values = max;
        this.disabled = disabled ? true : null;
        this.default_values = defaultValues?.ToArray();
    }
}

public sealed class ChannelSelect : IComponent
{
    public int type => 8;
    public string custom_id { get; }
    public string? placeholder { get; }
    public int? min_values { get; }
    public int? max_values { get; }
    public bool? disabled { get; }
    public int[]? channel_types { get; }
    public SelectDefaultValue[]? default_values { get; }
    public ChannelSelect(string customId, IEnumerable<Entities.ChannelType>? channelTypes = null, string? placeholder = null, int? min = null, int? max = null, bool disabled = false, IEnumerable<SelectDefaultValue>? defaultValues = null)
    {
        custom_id = customId;
        this.placeholder = placeholder;
        this.min_values = min;
        this.max_values = max;
        this.disabled = disabled ? true : null;
        this.channel_types = channelTypes?.Select(static ct => (int)ct).ToArray();
        this.default_values = defaultValues?.ToArray();
    }
}

// Text input (used inside modal action rows)
public sealed class TextInput
{
    public int type => 4; // text input
    public string custom_id { get; }
    public string label { get; }
    public int style { get; } // 1=short, 2=paragraph
    public int? min_length { get; }
    public int? max_length { get; }
    public bool? required { get; }
    public string? value { get; }
    public string? placeholder { get; }

    public TextInput(string customId, string label, int style = 1, int? minLength = null, int? maxLength = null, bool? required = null, string? value = null, string? placeholder = null)
    {
        custom_id = customId;
        this.label = label;
        this.style = style;
        this.min_length = minLength;
        this.max_length = maxLength;
        this.required = required;
        this.value = value;
        this.placeholder = placeholder;
    }
}
