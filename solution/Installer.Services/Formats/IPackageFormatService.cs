using Installer.Dtos.Build;
using Installer.Dtos.Manifest;

namespace Installer.Services.Formats;

/// <summary>One output format. The target's OS selects it; each OS has exactly one format.</summary>
public interface IPackageFormatService
{
    TargetOs Os { get; }

    /// <summary>The host OS this format must run on, or null for any host.</summary>
    TargetOs? RequiredHost { get; }

    /// <summary>External tools that must be installed; checked before <see cref="PreflightAsync"/>.</summary>
    IReadOnlyList<string> RequiredTools { get; }

    /// <summary>Format-specific checks (tool versions, identities, engines) that run before any target is built.</summary>
    Task<IReadOnlyList<Problem>> PreflightAsync(TargetSpec target, CancellationToken cancellationToken);

    /// <summary>Builds the target's artifacts inside <see cref="TargetContext.WorkDir"/>; the pipeline publishes them.</summary>
    Task<IReadOnlyList<ProducedFile>> BuildAsync(TargetContext context, CancellationToken cancellationToken);
}
