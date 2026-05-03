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
