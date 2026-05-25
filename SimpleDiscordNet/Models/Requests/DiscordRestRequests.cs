namespace SimpleDiscordNet.Models.Requests;

internal sealed class ChannelPermissionOverrideRequest
{
    public int type { get; init; }
    public string allow { get; init; } = string.Empty;
    public string deny { get; init; } = string.Empty;
}

internal sealed class PruneMembersRequest
{
    public int days { get; init; }
    public string[]? include_roles { get; init; }
}

internal sealed class WebhookRequest
{
    public string? name { get; init; }
    public string? avatar { get; init; }
}

internal sealed class CreateEmojiRequest
{
    public string? name { get; init; }
    public string? image { get; init; }
    public string[]? roles { get; init; }
}

internal sealed class ModifyEmojiRequest
{
    public string? name { get; init; }
    public string[]? roles { get; init; }
}

internal sealed class CreateStickerRequest
{
    public string? name { get; init; }
    public string? description { get; init; }
    public string? tags { get; init; }
    public string? file { get; init; }
}

internal sealed class ModifyStickerRequest
{
    public string? name { get; init; }
    public string? description { get; init; }
    public string? tags { get; init; }
}

internal sealed class CreateStageInstanceRequest
{
    public string channel_id { get; init; } = string.Empty;
    public string? topic { get; init; }
    public int? privacy_level { get; init; }
}

internal sealed class ModifyStageInstanceRequest
{
    public string? topic { get; init; }
    public int? privacy_level { get; init; }
}

internal sealed class CreateInviteRequest
{
    public int? max_age { get; init; }
    public int? max_uses { get; init; }
    public bool? temporary { get; init; }
    public bool? unique { get; init; }
}

internal sealed class ModifyCurrentUserRequest
{
    public string? username { get; init; }
    public string? avatar { get; init; }
}

internal sealed class ModifyNicknameRequest
{
    public string nick { get; init; } = string.Empty;
}

internal sealed class EmptyPayload
{
    public static readonly EmptyPayload Instance = new();
}

internal sealed class CreateGuildRoleRequest
{
    public string? name { get; init; }
    public string? permissions { get; init; }
    public int? color { get; init; }
    public bool? hoist { get; init; }
    public bool? mentionable { get; init; }
}

internal sealed class CreateGuildChannelRequest
{
    public string name { get; init; } = string.Empty;
    public int type { get; init; }
    public string? parent_id { get; init; }
    public object[]? permission_overwrites { get; init; }
}

internal sealed class ModifyChannelRequest
{
    public string? name { get; init; }
    public int? type { get; init; }
    public string? parent_id { get; init; }
    public int? position { get; init; }
    public string? topic { get; init; }
    public bool? nsfw { get; init; }
    public int? bitrate { get; init; }
    public int? user_limit { get; init; }
    public int? rate_limit_per_user { get; init; }
}

internal sealed class EditMessageRequest
{
    public string content { get; init; } = string.Empty;
    public Embed[]? embeds { get; init; }
}

internal sealed class ModifyGuildRequest
{
    public string? name { get; init; }
    public int? verification_level { get; init; }
    public int? default_message_notifications { get; init; }
    public int? explicit_content_filter { get; init; }
    public string? afk_channel_id { get; init; }
    public int? afk_timeout { get; init; }
    public string? owner_id { get; init; }
    public string? description { get; init; }
    public string? preferred_locale { get; init; }
}
