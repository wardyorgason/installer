namespace Installer.Services.Windows;

/// <summary>
/// Everything the generated NSIS script needs. Paths are absolute on the build host; <see cref="MainExecutable"/> is
/// relative to the payload with '/' separators. <see cref="SignHook"/> is the shell command NSIS runs, with <c>%1</c> for
/// the file, to sign the uninstaller and the setup executable; null when signing isn't configured.
/// </summary>
public sealed record NsisScriptRequest(
    string Id,
    string Name,
    string Publisher,
    string Description,
    string DisplayVersion,
    string FourPartVersion,
    string PayloadDirectory,
    string MainExecutable,
    string IconPath,
    string OutputFile,
    long EstimatedSizeKb,
    string? SignHook);
