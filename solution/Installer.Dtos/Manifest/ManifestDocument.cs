namespace Installer.Dtos.Manifest;

/// <summary>
/// The manifest as written, after environment expansion and before validation: every field optional, platform sections
/// not yet merged. Produced by the manifest reader, which reports unknown properties and wrong types itself.
/// </summary>
public sealed record ManifestDocument
{
    public int? SchemaVersion { get; init; }
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Version { get; init; }
    public string? DisplayVersion { get; init; }
    public string? Publisher { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public string? Profile { get; init; }
    public string? Executable { get; init; }
    public MacOptionsDocument? MacOS { get; init; }
    public WindowsOptionsDocument? Windows { get; init; }
    public LinuxOptionsDocument? Linux { get; init; }
    public IReadOnlyList<TargetDocument>? Targets { get; init; }
}

public sealed record TargetDocument
{
    /// <summary>JSON path of this target, e.g. <c>$.targets[1]</c>, for error messages.</summary>
    public required string Path { get; init; }
    public string? Name { get; init; }
    public string? Os { get; init; }
    public string? Arch { get; init; }
    public string? Payload { get; init; }
    public MacOptionsDocument? MacOS { get; init; }
    public WindowsOptionsDocument? Windows { get; init; }
    public LinuxOptionsDocument? Linux { get; init; }
}

public sealed record MacOptionsDocument
{
    public string? Identity { get; init; }
    public IReadOnlyDictionary<string, bool>? Entitlements { get; init; }
    public PlistDictionary? InfoPlist { get; init; }
    public IReadOnlyList<string>? Outputs { get; init; }
    public NotarizeDocument? Notarize { get; init; }
}

public sealed record NotarizeDocument
{
    public string? KeychainProfile { get; init; }
}

public sealed record WindowsOptionsDocument
{
    public IReadOnlyList<string>? SignCommand { get; init; }
}

public sealed record LinuxOptionsDocument
{
    public IReadOnlyList<string>? Categories { get; init; }
}
