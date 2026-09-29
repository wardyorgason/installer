namespace Installer.Dtos.Signing;

/// <summary>How to sign one file. Each <c>IFileSigner</c> handles one kind.</summary>
public abstract record SigningOptions;

/// <summary>
/// codesign settings. <see cref="Identifier"/> and <see cref="EntitlementsPath"/> are set only for the bundle itself.
/// </summary>
public sealed record CodesignSigningOptions(
    string Identity,
    bool HardenedRuntime,
    bool SecureTimestamp,
    string? Identifier = null,
    string? EntitlementsPath = null) : SigningOptions;

/// <summary>The user's Windows <c>signCommand</c>; <c>{file}</c> in an argument stands for the file to sign.</summary>
public sealed record CommandSigningOptions(IReadOnlyList<string> Command) : SigningOptions;
