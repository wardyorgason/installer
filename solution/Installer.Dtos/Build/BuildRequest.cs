namespace Installer.Dtos.Build;

/// <summary>What the CLI asks the build service for. An empty <see cref="SelectedTargets"/> means every target.</summary>
public sealed record BuildRequest(string ManifestPath, string? OutputDirectory, IReadOnlyList<string> SelectedTargets);
