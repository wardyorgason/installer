using Installer.Dtos.Manifest;

namespace Installer.Dtos.Build;

public enum TargetStatus
{
    Succeeded,
    Failed,
    NotSelected,
}

public enum ArtifactKind
{
    SetupExe,
    Dmg,
    Zip,
    AppImage,
}

/// <summary>The build result written to stdout. <see cref="Errors"/> holds manifest-level errors (nothing was built).</summary>
public sealed record BuildResult(int SchemaVersion, bool Succeeded, IReadOnlyList<Problem> Errors, IReadOnlyList<TargetResult> Targets)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record TargetResult(
    string Name,
    TargetOs Os,
    TargetArch Arch,
    TargetStatus Status,
    IReadOnlyList<BuiltArtifact> Artifacts,
    IReadOnlyList<Problem> Warnings,
    IReadOnlyList<Problem> Errors,
    string? WorkDir);

public sealed record BuiltArtifact(ArtifactKind Kind, string Path, long Size, string Sha256);
