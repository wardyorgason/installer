using Installer.Dtos.Build;

namespace Installer.Services.Build;

/// <summary>The pipeline behind the CLI's build command.</summary>
public interface IBuildService
{
    /// <summary>
    /// Loads and validates the manifest, preflights every selected target, then builds them one at a time. Never throws
    /// for build problems: they are reported in the result, per target or (for the manifest and arguments) at the top.
    /// </summary>
    Task<BuildResult> RunAsync(BuildRequest request, CancellationToken cancellationToken);
}
