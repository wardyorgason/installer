namespace Installer.Services.MacOS;

internal static class MacIdentity
{
    public const string DeveloperIdPrefix = "Developer ID Application:";

    /// <summary>Developer ID identities get a secure timestamp and may notarize; other identities (self-signed) may not.</summary>
    public static bool IsDeveloperId(string identity) => identity.StartsWith(DeveloperIdPrefix, StringComparison.Ordinal);
}
