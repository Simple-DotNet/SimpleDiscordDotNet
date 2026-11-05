using System.Security.Cryptography;
using System.Text;

namespace SimpleDiscordNet.Sharding;

/// <summary>
/// Authenticates traffic between coordinators and workers. Every node already holds the bot token,
/// so the secret is derived from it and needs no extra configuration. It is a one-way hash, so seeing
/// the header on an unencrypted link doesn't reveal the token.
/// </summary>
internal static class ClusterAuth
{
    public const string HeaderName = "X-Cluster-Secret";

    private static ReadOnlySpan<byte> Label => "SimpleDiscordNet.ClusterAuth.v1"u8;

    public static string DeriveSecret(string token)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(token), Label));

    public static bool IsValid(string? provided, string expected)
        => provided is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
}
