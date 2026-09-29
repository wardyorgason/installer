using Installer.Dtos.Build;

namespace Installer.Services.MacOS;

public interface IMacOutputService
{
    /// <summary>
    /// Creates the drag-to-install disk image from the staging folder that holds the .app (adding the Applications
    /// link), and signs it with the target's identity.
    /// </summary>
    Task CreateDmgAsync(TargetContext context, string stagingFolder, string output, CancellationToken cancellationToken);

    /// <summary>Zips the .app, keeping its signature, permissions and links.</summary>
    Task CreateZipAsync(string appPath, string output, CancellationToken cancellationToken);
}
