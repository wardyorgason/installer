namespace Installer.Dtos.Manifest;

/// <summary>A validated manifest: environment references expanded, paths absolute, platform sections merged into each target.</summary>
public sealed record PackageManifest(string ManifestPath, AppInfo App, IReadOnlyList<TargetSpec> Targets);

public sealed record AppInfo(
    string Id,
    string Name,
    AppVersion Version,
    string DisplayVersion,
    string Publisher,
    string Description,
    string IconPath,
    string Profile,
    string? Executable);

/// <summary>A numeric version of three or four parts, each 0..65535, as written in the manifest.</summary>
public sealed record AppVersion(string Text, IReadOnlyList<int> Parts);

/// <summary>
/// One target. <see cref="PayloadKind"/> is null when the payload didn't exist when the manifest was loaded; that fails
/// the target in preflight (only if it is selected), not the whole manifest.
/// </summary>
public sealed record TargetSpec(
    string Name,
    TargetOs Os,
    TargetArch Arch,
    string PayloadPath,
    PayloadKind? PayloadKind,
    MacOptions? MacOS,
    WindowsOptions? Windows,
    LinuxOptions? Linux);

public sealed record MacOptions(
    string Identity,
    IReadOnlyDictionary<string, bool> Entitlements,
    PlistDictionary InfoPlist,
    IReadOnlyList<MacOutput> Outputs,
    string? NotaryKeychainProfile);

public sealed record WindowsOptions(IReadOnlyList<string>? SignCommand);

public sealed record LinuxOptions(IReadOnlyList<string> Categories);
