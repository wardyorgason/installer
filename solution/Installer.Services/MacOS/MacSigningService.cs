using Installer.Dao.MacOS;
using Installer.Dao.Workspace;
using Installer.Dtos.Build;
using Installer.Dtos.Payload;
using Installer.Dtos.Signing;
using Installer.Services.Payload;
using Installer.Services.Signing;
using Microsoft.Extensions.Logging;

namespace Installer.Services.MacOS;

internal sealed class MacSigningService(
    ICodesignFileSigner signer,
    ICodesignDao codesign,
    IWorkspaceDao workspace,
    IBinaryInspectionService binaries,
    ILogger<MacSigningService> logger) : IMacSigningService
{
    public async Task SignBundleAsync(TargetContext context, string appPath, string entitlementsPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var identity = Identity(context);
        var options = new CodesignSigningOptions(identity, HardenedRuntime: true, SecureTimestamp: MacIdentity.IsDeveloperId(identity));
        var mainExecutable = "Contents/MacOS/" + Path.GetFileName(context.Profile.MainExecutable);

        await codesign.ClearExtendedAttributesAsync(appPath, cancellationToken).ConfigureAwait(false);
        var tree = workspace.ListTree(appPath);
        var code = tree
            .Where(entry => entry.Kind == FileSystemEntryKind.File)
            .Where(entry => (entry.RelativePath.StartsWith("Contents/MacOS/", StringComparison.Ordinal) && entry.RelativePath != mainExecutable)
                || (entry.RelativePath.StartsWith("Contents/Resources/", StringComparison.Ordinal) && IsMachO(Path.Combine(appPath, entry.RelativePath))))
            .Select(entry => entry.RelativePath)
            .ToList();

        logger.LogInformation("Signing {Count} files in {App} ({Identity}, hardened runtime)", code.Count, Path.GetFileName(appPath), identity);
        foreach (var file in code)
        {
            await signer.SignAsync(Path.Combine(appPath, file), options, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Signing {App}", Path.GetFileName(appPath));
        await signer.SignAsync(appPath, options with { Identifier = context.App.Id, EntitlementsPath = entitlementsPath }, cancellationToken).ConfigureAwait(false);
        await codesign.VerifyAsync(appPath, cancellationToken).ConfigureAwait(false);
    }

    public Task SignArtifactAsync(TargetContext context, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var identity = Identity(context);
        return signer.SignAsync(path, new CodesignSigningOptions(identity, HardenedRuntime: false, SecureTimestamp: MacIdentity.IsDeveloperId(identity)), cancellationToken);
    }

    private bool IsMachO(string path) => binaries.Inspect(workspace.ReadHeader(path, binaries.HeaderLength)).Format == BinaryFormat.MachO;

    private static string Identity(TargetContext context) =>
        context.Target.MacOS?.Identity ?? throw new BuildFailedException(ErrorCodes.IdentityMissing, $"Target '{context.Target.Name}' has no macOS signing identity.");
}
