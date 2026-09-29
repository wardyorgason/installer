using Installer.Dtos.Build;

namespace Installer.Services.MacOS;

public interface INotarizationService
{
    /// <summary>
    /// Submits <paramref name="file"/> with the target's keychain profile and waits. Anything but Accepted saves the
    /// notarization log in the work directory and fails the target (notarize.rejected) with the log's path.
    /// </summary>
    Task NotarizeAsync(TargetContext context, string file, CancellationToken cancellationToken);
}
