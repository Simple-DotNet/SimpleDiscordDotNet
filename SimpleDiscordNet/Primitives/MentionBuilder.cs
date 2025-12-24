namespace SimpleDiscordNet.Primitives;

/// <summary>
/// Builder for creating Discord mention strings with proper formatting.
/// </summary>
public sealed class MentionBuilder
{
    private readonly List<string> _mentions = [];
    private bool _allowEveryone = true;
    private bool _allowRoles = true;
    private bool _allowUsers = true;

    /// <summary>Adds a user mention.</summary>
    public MentionBuilder AddUser(ulong userId)
    {
        _mentions.Add($"<@{userId}>");
        return this;
    }

    /// <summary>Adds a user mention.</summary>
    public MentionBuilder AddUser(string userId)
    {
        _mentions.Add($"<@{userId}>");
        return this;
    }

    /// <summary>Adds a role mention.</summary>
    public MentionBuilder AddRole(ulong roleId)
    {
        _mentions.Add($"<@&{roleId}>");
        return this;
    }

    /// <summary>Adds a role mention.</summary>
    public MentionBuilder AddRole(string roleId)
    {
        _mentions.Add($"<@&{roleId}>");
        return this;
    }

    /// <summary>Adds a channel mention.</summary>
    public MentionBuilder AddChannel(ulong channelId)
    {
        _mentions.Add($"<#{channelId}>");
        return this;
    }

    /// <summary>Adds a channel mention.</summary>
    public MentionBuilder AddChannel(string channelId)
    {
        _mentions.Add($"<#{channelId}>");
        return this;
    }

    /// <summary>Adds @everyone mention.</summary>
    public MentionBuilder AddEveryone()
    {
        _mentions.Add("@everyone");
        return this;
    }

    /// <summary>Adds @here mention.</summary>
    public MentionBuilder AddHere()
    {
        _mentions.Add("@here");
        return this;
    }

    /// <summary>Disables @everyone and @here mentions.</summary>
    public MentionBuilder DisableEveryone()
    {
        _allowEveryone = false;
        return this;
    }

    /// <summary>Disables role mentions.</summary>
    public MentionBuilder DisableRoles()
    {
        _allowRoles = false;
        return this;
    }

    /// <summary>Disables user mentions.</summary>
    public MentionBuilder DisableUsers()
    {
        _allowUsers = false;
        return this;
    }

    /// <summary>Disables all mentions.</summary>
    public MentionBuilder DisableAll()
    {
        _allowEveryone = false;
        _allowRoles = false;
        _allowUsers = false;
        return this;
    }

    /// <summary>Builds the mention string.</summary>
    public string BuildMentionString() => string.Join(" ", _mentions);

    /// <summary>Builds the allowed_mentions object for Discord API.</summary>
    internal object BuildAllowedMentions()
    {
        List<string> parse = [];
        if (_allowEveryone) parse.Add("everyone");
        if (_allowRoles) parse.Add("roles");
        if (_allowUsers) parse.Add("users");

        return new { parse = parse.ToArray() };
    }

    /// <summary>Creates a builder that mentions everyone.</summary>
    public static MentionBuilder Everyone() => new MentionBuilder().AddEveryone();

    /// <summary>Creates a builder that mentions here.</summary>
    public static MentionBuilder Here() => new MentionBuilder().AddHere();

    /// <summary>Creates a builder with all mentions disabled.</summary>
    public static MentionBuilder None() => new MentionBuilder().DisableAll();

    /// <summary>Creates a builder that mentions a user.</summary>
    public static MentionBuilder User(ulong userId) => new MentionBuilder().AddUser(userId);

    /// <summary>Creates a builder that mentions a user.</summary>
    public static MentionBuilder User(string userId) => new MentionBuilder().AddUser(userId);

    /// <summary>Creates a builder that mentions a role.</summary>
    public static MentionBuilder Role(ulong roleId) => new MentionBuilder().AddRole(roleId);

    /// <summary>Creates a builder that mentions a role.</summary>
    public static MentionBuilder Role(string roleId) => new MentionBuilder().AddRole(roleId);
}
