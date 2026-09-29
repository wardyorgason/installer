namespace Installer.Services.Windows;

public interface IWindowsSigningService
{
    /// <summary>Signs one file with the manifest's signCommand. Used for the main executable and by the NSIS hooks.</summary>
    Task SignFileAsync(string file, IReadOnlyList<string> signCommand, CancellationToken cancellationToken);

    /// <summary>
    /// Signs a file for the hidden <c>sign-file</c> command, reading the signCommand from the JSON array at
    /// <paramref name="commandJsonPath"/> (written by the Windows format for the NSIS hooks).
    /// </summary>
    Task SignFileFromCommandFileAsync(string file, string commandJsonPath, CancellationToken cancellationToken);
}
