using Installer.Dtos.Manifest;

namespace Installer.Services.Linux;

public interface IAppImageService
{
    /// <summary>The image tag for the embedded Dockerfile: <c>installer-appimage:&lt;first 12 hex of its SHA-256&gt;</c>.</summary>
    string ImageTag { get; }

    /// <summary>Packs <paramref name="appDir"/> into <paramref name="output"/> inside the container, building the image first if needed.</summary>
    Task BuildAsync(string appDir, TargetArch arch, string output, CancellationToken cancellationToken);
}
