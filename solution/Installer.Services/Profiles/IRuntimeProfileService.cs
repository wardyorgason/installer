using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace Installer.Services.Profiles;

/// <summary>A runtime profile: stack-specific inference and defaults layered over the stack-agnostic core.</summary>
public interface IRuntimeProfileService
{
    /// <summary>The name used in the manifest's <c>profile</c>.</summary>
    string Name { get; }

    /// <summary>Whether the manifest must name <c>executable</c> for this profile.</summary>
    bool RequiresExecutable { get; }

    /// <summary>Finds the main executable and profile defaults, or throws <see cref="Dtos.Build.BuildFailedException"/>.</summary>
    ProfileAnalysis Analyze(AppInfo app, TargetSpec target, PreparedPayload payload);
}
