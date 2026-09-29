using Installer.Dao.MacOS;
using Installer.Dao.Workspace;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Services.Formats;

namespace Installer.Services.MacOS;

/// <summary>
/// A signed .app, packaged as a drag-to-install .dmg and/or a .zip, optionally notarized. The flow generalizes
/// screen-rec's New-MacApp.ps1 and Publish.ps1.
/// </summary>
internal sealed class MacFormatService(
    IKeychainDao keychain,
    IAppBundleService bundles,
    IEntitlementService entitlements,
    IPlistService plists,
    IMacSigningService signing,
    INotarizationService notarization,
    IMacOutputService outputs,
    INotaryDao notary,
    IWorkspaceDao workspace) : IPackageFormatService
{
    public TargetOs Os => TargetOs.MacOS;

    public TargetOs? RequiredHost => TargetOs.MacOS;

    public IReadOnlyList<string> RequiredTools => ["codesign", "security", "xattr", "hdiutil", "ditto", "xcrun"];

    public async Task<IReadOnlyList<Problem>> PreflightAsync(TargetSpec target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        var identity = target.MacOS?.Identity;
        var identities = await keychain.FindCodeSigningIdentitiesAsync(cancellationToken).ConfigureAwait(false);
        if (identity is not null && identities.Any(candidate => candidate.Name == identity))
        {
            return [];
        }

        var available = identities.Count == 0 ? "none" : string.Join(", ", identities.Select(candidate => $"\"{candidate.Name}\""));
        return
        [
            Problem.Error(
                ErrorCodes.IdentityMissing,
                $"Code-signing identity \"{identity}\" is not installed or not valid for code signing in this user's keychain. Available: {available}. " +
                "Create or import it (Keychain Access; trust it for code signing), then check with `security find-identity -v -p codesigning`."),
        ];
    }

    public async Task<IReadOnlyList<ProducedFile>> BuildAsync(TargetContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var mac = context.Target.MacOS ?? throw new InvalidOperationException("macOS options are missing.");
        var stage = Path.Combine(context.WorkDir, "stage");
        var appPath = Path.Combine(stage, context.App.Name + ".app");

        bundles.Build(context, appPath);
        var entitlementsPath = Path.Combine(context.WorkDir, "entitlements.plist");
        workspace.WriteText(entitlementsPath, plists.Entitlements(entitlements.Resolve(context.Profile, mac)));
        await signing.SignBundleAsync(context, appPath, entitlementsPath, cancellationToken).ConfigureAwait(false);

        string? dmg = null;
        if (mac.Outputs.Contains(MacOutput.Dmg))
        {
            dmg = Path.Combine(context.WorkDir, context.ArtifactBaseName + ".dmg");
            await outputs.CreateDmgAsync(context, stage, dmg, cancellationToken).ConfigureAwait(false);
        }

        string? zip = mac.Outputs.Contains(MacOutput.Zip) ? Path.Combine(context.WorkDir, context.ArtifactBaseName + ".zip") : null;
        if (mac.NotaryKeychainProfile is not null)
        {
            // One submission per target: the first output. Its ticket covers the app inside, so both get stapled.
            if (dmg is null)
            {
                await outputs.CreateZipAsync(appPath, zip!, cancellationToken).ConfigureAwait(false);
            }

            await notarization.NotarizeAsync(context, dmg ?? zip!, cancellationToken).ConfigureAwait(false);
            if (dmg is not null)
            {
                await notary.StapleAsync(dmg, cancellationToken).ConfigureAwait(false);
            }

            await notary.StapleAsync(appPath, cancellationToken).ConfigureAwait(false);
        }

        if (zip is not null)
        {
            await outputs.CreateZipAsync(appPath, zip, cancellationToken).ConfigureAwait(false);
        }

        var produced = new List<ProducedFile>();
        foreach (var output in mac.Outputs)
        {
            produced.Add(output == MacOutput.Dmg ? new ProducedFile(ArtifactKind.Dmg, dmg!) : new ProducedFile(ArtifactKind.Zip, zip!));
        }

        return produced;
    }
}
