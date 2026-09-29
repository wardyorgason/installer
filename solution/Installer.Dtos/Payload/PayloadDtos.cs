using Installer.Dtos.Build;

namespace Installer.Dtos.Payload;

/// <summary>The target's working copy of the payload. <see cref="Files"/> are relative to <see cref="Root"/>, with '/' separators.</summary>
public sealed record PreparedPayload(string Root, IReadOnlyList<string> Files);

/// <summary>What a runtime profile inferred. <see cref="MainExecutable"/> is relative to the payload root.</summary>
public sealed record ProfileAnalysis(
    string MainExecutable,
    IReadOnlyList<Problem> Warnings,
    IReadOnlyDictionary<string, bool> DefaultEntitlements);

public sealed record ZipEntryInfo(string FullName, bool IsDirectory);

public enum BinaryFormat
{
    Unknown,
    Pe,
    MachO,
    Elf,
}

public enum CpuArch
{
    X64,
    Arm64,
    X86,
    Arm,
    Other,
}

/// <summary>A parsed executable header. A universal Mach-O lists every slice's architecture.</summary>
public sealed record BinaryInfo(BinaryFormat Format, IReadOnlyList<CpuArch> Architectures);

public enum FileSystemEntryKind
{
    File,
    Directory,
    SymbolicLink,
}

/// <summary>One entry of a directory listing; symbolic links are reported as links, never followed.</summary>
public sealed record FileSystemEntry(string FullPath, FileSystemEntryKind Kind);

/// <summary>Size and SHA-256 (lowercase hex) of a file.</summary>
public sealed record FileFingerprint(long Size, string Sha256);
