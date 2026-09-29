using System.Security.Cryptography;
using System.Text;
using Installer.Dao.Linux;
using Installer.Dtos.Manifest;
using Microsoft.Extensions.Logging;

namespace Installer.Services.Linux;

internal sealed class AppImageService(IDockerDao docker, IContainerDefinitionDao definitions, ILogger<AppImageService> logger) : IAppImageService
{
    private const string ContainerAppDir = "/work/AppDir";
    private const string ContainerOutput = "/work/out.AppImage";

    private readonly Lazy<string> _dockerfile = new(definitions.GetAppImageDockerfile);

    public string ImageTag => "installer-appimage:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(_dockerfile.Value)))[..12];

    public async Task BuildAsync(string appDir, TargetArch arch, string output, CancellationToken cancellationToken)
    {
        var tag = ImageTag;
        if (!await docker.ImageExistsAsync(tag, cancellationToken).ConfigureAwait(false))
        {
            logger.LogInformation("Building the AppImage image {Tag} (first use of this definition on this host)", tag);
            await docker.BuildImageAsync(tag, _dockerfile.Value, cancellationToken).ConfigureAwait(false);
        }

        var appImageArch = arch == TargetArch.Arm64 ? "aarch64" : "x86_64";
        var container = await docker.CreateContainerAsync(
            tag,
            ["/opt/appimage/appimagetool/AppRun", "--no-appstream", "--runtime-file", $"/opt/appimage/runtime-{appImageArch}", ContainerAppDir, ContainerOutput],
            new Dictionary<string, string> { ["ARCH"] = appImageArch },
            cancellationToken).ConfigureAwait(false);
        try
        {
            await docker.CopyToContainerAsync(container, appDir, ContainerAppDir, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Packing {AppImage}", Path.GetFileName(output));
            await docker.StartAsync(container, cancellationToken).ConfigureAwait(false);
            await docker.CopyFromContainerAsync(container, ContainerOutput, output, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await docker.RemoveContainerAsync(container, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
