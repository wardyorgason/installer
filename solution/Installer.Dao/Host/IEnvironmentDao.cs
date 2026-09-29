using Installer.Dtos.Build;

namespace Installer.Dao.Host;

public interface IEnvironmentDao
{
    string? GetVariable(string name);

    HostInfo GetHost();

    /// <summary>
    /// The command line that starts this builder again: <c>[dotnet, Installer.Cli.dll]</c> when run through the dotnet
    /// host, or just the apphost. Used by hooks that call back into the builder (the NSIS signing hook).
    /// </summary>
    IReadOnlyList<string> GetSelfInvocation();
}
