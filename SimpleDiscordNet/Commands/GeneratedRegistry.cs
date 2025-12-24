using SimpleDiscordNet.Models;

namespace SimpleDiscordNet.Commands;

/// <summary>
/// Manifest produced by the source generator and consumed by the runtime to register handlers and definitions.
/// </summary>
public interface IGeneratedManifest
{
    IReadOnlyDictionary<string, CommandHandler> Ungrouped { get; }
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, CommandHandler>> Grouped { get; }
    IReadOnlyList<ComponentHandler> Components { get; }
    ApplicationCommandDefinition[] Definitions { get; }
    IReadOnlyDictionary<string, string> HelpIndex { get; }
}

/// <summary>
/// Factory for manifests. Implemented by generated code in the consumer assembly.
/// </summary>
public interface IGeneratedManifestProvider
{
    IGeneratedManifest CreateManifest();
}

/// <summary>
/// Default runtime manifest implementation for convenience. Generators can populate this.
/// </summary>
public sealed class RuntimeManifest : IGeneratedManifest
{
    public required IReadOnlyDictionary<string, CommandHandler> Ungrouped { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, CommandHandler>> Grouped { get; init; }
    public required IReadOnlyList<ComponentHandler> Components { get; init; }
    public required ApplicationCommandDefinition[] Definitions { get; init; }
    public required IReadOnlyDictionary<string, string> HelpIndex { get; init; }
}

/// <summary>
/// Global registry where generated providers self-register via a ModuleInitializer in the consumer assemblies.
/// </summary>
public static class GeneratedRegistry
{
    private static readonly object Gate = new();
    private static readonly List<IGeneratedManifestProvider> ProvidersList = new();

    public static IReadOnlyList<IGeneratedManifestProvider> Providers
    {
        get { lock (Gate) return ProvidersList.ToArray(); }
    }

    public static void Register(IGeneratedManifestProvider? provider)
    {
        if (provider is null) return;
        lock (Gate)
        {
            ProvidersList.Add(provider);
        }
    }
}
