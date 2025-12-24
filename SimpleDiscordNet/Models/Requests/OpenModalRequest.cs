namespace SimpleDiscordNet.Models.Requests;

internal sealed class OpenModalRequest
{
    public int type { get; set; } // 9
    public required ModalData data { get; set; }
}

internal sealed class ModalData
{
    public required string custom_id { get; set; }
    public required string title { get; set; }
    public object[] components { get; set; } = Array.Empty<object>();
}
