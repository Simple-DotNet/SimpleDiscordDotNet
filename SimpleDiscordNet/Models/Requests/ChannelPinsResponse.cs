using SimpleDiscordNet.Entities;

namespace SimpleDiscordNet.Models.Requests;

internal sealed class ChannelPinsResponse
{
    public ChannelPinEntry[] items { get; set; } = [];
    public bool has_more { get; set; }
}

internal sealed class ChannelPinEntry
{
    public required string pinned_at { get; set; }
    public required DiscordMessage message { get; set; }
}
