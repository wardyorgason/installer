using Installer.Dtos.Manifest;

namespace Installer.Services.MacOS;

public interface IPlistService
{
    /// <summary>The bundle's Info.plist: generated keys first, then the manifest's infoPlist keys (replacing same-named ones).</summary>
    string InfoPlist(AppInfo app, string executableName, string iconName, PlistDictionary extraKeys);

    /// <summary>An entitlements plist with each key set to true.</summary>
    string Entitlements(IReadOnlyList<string> entitlements);
}
