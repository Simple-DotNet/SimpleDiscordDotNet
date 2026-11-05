namespace SimpleDiscordNet.Commands;

/// <summary>
/// Limits a command to servers, so it can't be used in DMs. Applies to global commands.
/// Use on a method, or on a command group class.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class GuildOnlyAttribute : Attribute;

/// <summary>
/// Lets a command be used in servers and in DMs with the bot. Applies to global commands.
/// Use on a method, or on a command group class.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class AllowInDMsAttribute : Attribute;

/// <summary>
/// Lets people install the command to their own account and use it anywhere, including servers
/// the bot isn't in and private DMs. Enable User Install for your app in the Developer Portal too.
/// Applies to global commands. Use on a method, or on a command group class.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class UserInstallableAttribute : Attribute;

/// <summary>
/// Translates a command, command group or option for one language. Repeat it for each language.
/// Example: [Localized("de", "hallo", "Sag hallo")]
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class LocalizedAttribute : Attribute
{
    /// <summary>A Discord locale code, such as "de", "fr" or "es-ES".</summary>
    public string Locale { get; }

    /// <summary>The translated name. Slash command and option names must be lowercase with no spaces.</summary>
    public string Name { get; }

    /// <summary>The translated description. Context menu commands don't have one.</summary>
    public string? Description { get; }

    public LocalizedAttribute(string locale, string name, string? description = null)
    {
        Locale = locale;
        Name = name;
        Description = description;
    }
}
