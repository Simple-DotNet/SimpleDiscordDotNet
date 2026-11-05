using System.Text.Json.Serialization;

namespace SimpleDiscordNet.Primitives;

// Components for modals. Most bots don't need these directly: a class marked [Modal] builds them for you.

/// <summary>
/// Gives a modal component a label and optional description.
/// Example: new Label("Your feedback", new TextInput("feedback", style: 2));
/// </summary>
public sealed class Label : IComponent
{
    [JsonIgnore] public int type => 18;
    public string label { get; }
    public string? description { get; }
    public IComponent component { get; }

    public Label(string label, IComponent component, string? description = null)
    {
        this.label = label;
        this.component = component;
        this.description = description;
    }
}

/// <summary>
/// A choice in a <see cref="RadioGroup"/> or <see cref="CheckboxGroup"/>.
/// </summary>
public sealed class ChoiceOption
{
    public string label { get; }
    public string value { get; }
    public string? description { get; }
    public bool? @default { get; }

    public ChoiceOption(string label, string value, string? description = null, bool? @default = null)
    {
        this.label = label;
        this.value = value;
        this.description = description;
        this.@default = @default;
    }
}

/// <summary>
/// A single-choice list for a modal. Wrap it in a <see cref="Label"/>.
/// </summary>
public sealed class RadioGroup : IComponent
{
    [JsonIgnore] public int type => 21;
    public string custom_id { get; }
    public ChoiceOption[] options { get; }
    public bool? required { get; }

    public RadioGroup(string customId, IEnumerable<ChoiceOption> options, bool? required = null)
    {
        custom_id = customId;
        this.options = options.ToArray();
        this.required = required;
    }
}

/// <summary>
/// A multiple-choice list of checkboxes for a modal. Wrap it in a <see cref="Label"/>.
/// </summary>
public sealed class CheckboxGroup : IComponent
{
    [JsonIgnore] public int type => 22;
    public string custom_id { get; }
    public ChoiceOption[] options { get; }
    public int? min_values { get; }
    public int? max_values { get; }
    public bool? required { get; }

    public CheckboxGroup(string customId, IEnumerable<ChoiceOption> options, bool? required = null, int? min = null, int? max = null)
    {
        custom_id = customId;
        this.options = options.ToArray();
        this.required = required;
        min_values = min;
        max_values = max;
    }
}

/// <summary>
/// A single yes/no checkbox for a modal. Wrap it in a <see cref="Label"/>.
/// </summary>
public sealed class Checkbox : IComponent
{
    [JsonIgnore] public int type => 23;
    public string custom_id { get; }
    public bool? @default { get; }

    public Checkbox(string customId, bool? @default = null)
    {
        custom_id = customId;
        this.@default = @default;
    }
}

/// <summary>
/// Lets the user upload files in a modal. Wrap it in a <see cref="Label"/>.
/// </summary>
public sealed class FileUpload : IComponent
{
    [JsonIgnore] public int type => 19;
    public string custom_id { get; }
    public int? min_values { get; }
    public int? max_values { get; }
    public bool? required { get; }
    public string[]? file_types { get; }

    /// <param name="customId">The field's ID</param>
    /// <param name="required">Whether a file must be uploaded</param>
    /// <param name="min">Fewest files allowed</param>
    /// <param name="max">Most files allowed, up to 10</param>
    /// <param name="fileTypes">Allowed types: image, video, audio or an extension such as .pdf</param>
    public FileUpload(string customId, bool? required = null, int? min = null, int? max = null, IEnumerable<string>? fileTypes = null)
    {
        custom_id = customId;
        this.required = required;
        min_values = min;
        max_values = max;
        file_types = fileTypes?.ToArray();
    }
}
