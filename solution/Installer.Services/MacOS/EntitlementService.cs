using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace Installer.Services.MacOS;

internal sealed class EntitlementService : IEntitlementService
{
    public IReadOnlyList<string> Resolve(ProfileAnalysis profile, MacOptions options)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(options);
        var merged = new Dictionary<string, bool>(profile.DefaultEntitlements, StringComparer.Ordinal);
        foreach (var (key, value) in options.Entitlements)
        {
            merged[key] = value;
        }

        return merged.Where(pair => pair.Value).Select(pair => pair.Key).Order(StringComparer.Ordinal).ToList();
    }
}
