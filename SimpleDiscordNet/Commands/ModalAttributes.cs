using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Commands;

/// <summary>
/// Turns a class into a modal form. Mark its properties with field attributes, open it with
/// ctx.OpenModalAsync&lt;TForm&gt;() from a [NoDefer] handler, and receive it in a [ModalHandler] method.
/// The source generator builds and fills the form, so this needs no reflection. A modal holds up to 5 fields.
/// <code>
/// [Modal("Send feedback")]
/// public sealed class FeedbackForm
/// {
///     [ModalText("Your name", MaxLength = 50)] public string Name { get; set; } = "";
///     [ModalText("Details", Paragraph = true)] public string? Details { get; set; }
/// }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ModalAttribute : Attribute
{
    /// <summary>The title shown at the top of the modal.</summary>
    public string Title { get; }

    /// <summary>The modal's custom_id. Defaults to the class name.</summary>
    public string? Id { get; set; }

    public ModalAttribute(string title)
    {
        Title = title;
    }
}

/// <summary>
/// Handles a submitted modal. The method takes the form class as a parameter, after any InteractionContext.
/// <code>
/// [ModalHandler]
/// public async Task OnFeedback(InteractionContext ctx, FeedbackForm form)
///     => await ctx.RespondAsync($"Thanks, {form.Name}!");
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ModalHandlerAttribute : Attribute;

/// <summary>
/// Base class for modal field attributes.
/// A field is required unless its property is nullable or you set Required = false, where offered.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public abstract class ModalFieldAttribute : Attribute
{
    /// <summary>The field's label, up to 45 characters.</summary>
    public string Label { get; }

    /// <summary>Optional help text under the label, up to 100 characters.</summary>
    public string? Description { get; set; }

    protected ModalFieldAttribute(string label)
    {
        Label = label;
    }
}

/// <summary>
/// A text box. Use on a string property.
/// Example: [ModalText("Your name", MaxLength = 50)] public string Name { get; set; } = "";
/// </summary>
public sealed class ModalTextAttribute(string label) : ModalFieldAttribute(label)
{
    /// <summary>True for a multi-line box.</summary>
    public bool Paragraph { get; set; }
    public int MinLength { get; set; }
    public int MaxLength { get; set; }
    public string? Placeholder { get; set; }
    public bool Required { get; set; }
}

/// <summary>
/// A dropdown. Use on a string for one choice, or a string[] for several.
/// An option can be "Label" or "Label:value".
/// Example: [ModalSelect("Area", "Bug report:bug", "Feature request:feature")] public string Area { get; set; } = "";
/// </summary>
public sealed class ModalSelectAttribute(string label, params string[] options) : ModalFieldAttribute(label)
{
    public string[] Options { get; } = options;
    public string? Placeholder { get; set; }
}

/// <summary>
/// A list where one option can be picked. Use on a string property.
/// An option can be "Label" or "Label:value".
/// </summary>
public sealed class ModalRadioAttribute(string label, params string[] options) : ModalFieldAttribute(label)
{
    public string[] Options { get; } = options;
}

/// <summary>
/// A list of checkboxes. Use on a string[] property.
/// An option can be "Label" or "Label:value".
/// </summary>
public sealed class ModalCheckboxesAttribute(string label, params string[] options) : ModalFieldAttribute(label)
{
    public string[] Options { get; } = options;
}

/// <summary>
/// A single yes/no checkbox. Use on a bool property.
/// </summary>
public sealed class ModalCheckboxAttribute(string label) : ModalFieldAttribute(label);

/// <summary>
/// A user picker. Use on a ulong for one user, or a ulong[] for several.
/// </summary>
public sealed class ModalUserSelectAttribute(string label) : ModalFieldAttribute(label)
{
    public string? Placeholder { get; set; }
    public int MaxValues { get; set; }
}

/// <summary>
/// A role picker. Use on a ulong for one role, or a ulong[] for several.
/// </summary>
public sealed class ModalRoleSelectAttribute(string label) : ModalFieldAttribute(label)
{
    public string? Placeholder { get; set; }
    public int MaxValues { get; set; }
}

/// <summary>
/// A channel picker. Use on a ulong for one channel, or a ulong[] for several.
/// </summary>
public sealed class ModalChannelSelectAttribute(string label) : ModalFieldAttribute(label)
{
    public string? Placeholder { get; set; }
    public int MaxValues { get; set; }
}

/// <summary>
/// A file upload. Use on an Attachment for one file, or an Attachment[] for several.
/// </summary>
public sealed class ModalFileAttribute(string label) : ModalFieldAttribute(label)
{
    /// <summary>Most files allowed, up to 10. Defaults to 10 on an array property.</summary>
    public int MaxFiles { get; set; }

    /// <summary>Allowed types: image, video, audio or an extension such as .pdf.</summary>
    public string[]? FileTypes { get; set; }
}

/// <summary>
/// Describes a [Modal] class. Generated code registers one of these for each form.
/// </summary>
public sealed record ModalFormDefinition(Type FormType, string Id, string Title, Func<object?, IComponent[]> BuildComponents);

/// <summary>
/// Forms registered by generated code.
/// </summary>
public static class ModalFormRegistry
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<Type, ModalFormDefinition> Forms = [];

    public static void Register(ModalFormDefinition form)
    {
        lock (Gate) Forms[form.FormType] = form;
    }

    internal static ModalFormDefinition Get(Type formType)
    {
        lock (Gate)
        {
            return Forms.TryGetValue(formType, out ModalFormDefinition? form)
                ? form
                : throw new InvalidOperationException($"{formType.Name} isn't a modal form. Add [Modal] to the class, and make sure the project uses the SimpleDiscordNet source generator.");
        }
    }
}
