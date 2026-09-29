namespace Installer.Cli.Commands;

public sealed record BuildCommandArguments(string ManifestPath, string? OutputDirectory, IReadOnlyList<string> Targets);
