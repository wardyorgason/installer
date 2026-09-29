using Installer.Dtos.Build;
using Installer.Dtos.Manifest;

namespace Installer.Services.Build;

public interface IPreflightService
{
    /// <summary>
    /// Checks every target before any is built: the host OS its format needs, its tools, then the format's own checks.
    /// Returns the problems per target name; a target with no entry passed.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<Problem>>> CheckAsync(IReadOnlyList<TargetSpec> targets, CancellationToken cancellationToken);
}
