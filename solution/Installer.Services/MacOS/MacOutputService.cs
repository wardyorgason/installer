using Installer.Dao.MacOS;
using Installer.Dao.Workspace;
using Installer.Dtos.Build;
using Microsoft.Extensions.Logging;

namespace Installer.Services.MacOS;

internal sealed class MacOutputService(
    IDiskImageDao diskImages,
    IMacArchiveDao archives,
    IMacSigningService signing,
    IWorkspaceDao workspace,
    ILogger<MacOutputService> logger) : IMacOutputService
{
    public async Task CreateDmgAsync(TargetContext context, string stagingFolder, string output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var link = Path.Combine(stagingFolder, "Applications");
        if (!workspace.FileExists(link) && !workspace.DirectoryExists(link))
        {
            workspace.CreateSymlink(link, "/Applications");
        }

        logger.LogInformation("Creating {Dmg}", Path.GetFileName(output));
        await diskImages.CreateAsync(context.App.Name, stagingFolder, output, cancellationToken).ConfigureAwait(false);
        await signing.SignArtifactAsync(context, output, cancellationToken).ConfigureAwait(false);
    }

    public Task CreateZipAsync(string appPath, string output, CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating {Zip}", Path.GetFileName(output));
        return archives.ZipAsync(appPath, output, cancellationToken);
    }
}
