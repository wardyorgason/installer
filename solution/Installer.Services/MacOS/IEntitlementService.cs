using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace Installer.Services.MacOS;

public interface IEntitlementService
{
    /// <summary>
    /// The entitlements the app is signed with: the profile's defaults with the manifest's entitlements merged over them.
    /// A manifest value of false removes the entitlement. Keys come back sorted.
    /// </summary>
    IReadOnlyList<string> Resolve(ProfileAnalysis profile, MacOptions options);
}
