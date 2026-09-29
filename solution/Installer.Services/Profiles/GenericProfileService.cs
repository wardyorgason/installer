using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace Installer.Services.Profiles;

/// <summary>Any prebuilt app: no inference, no defaults. The manifest names the main executable.</summary>
internal sealed class GenericProfileService : IRuntimeProfileService
{
    public string Name => "generic";

    public bool RequiresExecutable => true;

    public ProfileAnalysis Analyze(AppInfo app, TargetSpec target, PreparedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(payload);
        var executable = (app.Executable ?? string.Empty).Replace('\\', '/').TrimStart('/');
        if (!payload.Files.Contains(executable, StringComparer.Ordinal))
        {
            throw new BuildFailedException(
                ErrorCodes.PayloadExecutableMissing,
                $"Target '{target.Name}': the executable '{executable}' was not found in the payload.");
        }

        return new ProfileAnalysis(executable, [], new Dictionary<string, bool>());
    }
}
