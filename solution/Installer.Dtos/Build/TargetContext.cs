using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace Installer.Dtos.Build;

/// <summary>
/// Everything a format needs to build one target, fully resolved. <see cref="ArtifactBaseName"/> is
/// <c>&lt;name&gt;-&lt;displayVersion&gt;-&lt;target&gt;</c>; formats add their suffix.
/// </summary>
public sealed record TargetContext(
    AppInfo App,
    TargetSpec Target,
    PreparedPayload Payload,
    ProfileAnalysis Profile,
    string WorkDir,
    string OutputDir,
    string ArtifactBaseName);

/// <summary>A file a format produced, before the pipeline copies it to the output directory and hashes it.</summary>
public sealed record ProducedFile(ArtifactKind Kind, string Path);

public sealed record HostInfo(TargetOs Os, TargetArch Arch, string TempDirectory);
