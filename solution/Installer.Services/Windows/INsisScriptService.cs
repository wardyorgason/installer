namespace Installer.Services.Windows;

public interface INsisScriptService
{
    /// <summary>The per-user installer script: install, shortcut, Installed apps registration, upgrade and uninstaller.</summary>
    string Generate(NsisScriptRequest request);

    /// <summary>
    /// The NSIS <c>!finalize</c> / <c>!uninstfinalize</c> command that calls back into the builder's sign-file command.
    /// </summary>
    string SignHook(IReadOnlyList<string> selfInvocation, string commandJsonPath);
}
