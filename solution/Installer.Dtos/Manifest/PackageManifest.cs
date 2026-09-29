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

public sealed record TargetSpec(
    string Name,
    TargetOs Os,
    TargetArch Arch,
    string PayloadPath,
    PayloadKind PayloadKind,
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
