using Installer.Dtos.Build;

namespace Installer.Services.MacOS;

public interface IMacSigningService
{
    /// <summary>
    /// Signs the bundle inside out with the hardened runtime: every file in Contents/MacOS except the main executable
    /// (links not followed), every Mach-O file in Contents/Resources, then the bundle with its identifier and
    /// entitlements; then verifies the signature strictly.
    /// </summary>
    Task SignBundleAsync(TargetContext context, string appPath, string entitlementsPath, CancellationToken cancellationToken);

    /// <summary>Signs a finished artifact (the .dmg) with the target's identity.</summary>
    Task SignArtifactAsync(TargetContext context, string path, CancellationToken cancellationToken);
}
