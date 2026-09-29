using Installer.Dao.Linux;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Services.Formats;

namespace Installer.Services.Linux;

/// <summary>A single-file AppImage, always packed in a container so any host with a Docker engine can build it.</summary>
internal sealed class LinuxFormatService(IDockerDao docker, IAppDirService appDirs, IAppImageService appImages) : IPackageFormatService
{
    public TargetOs Os => TargetOs.Linux;

    public TargetOs? RequiredHost => null;

    public IReadOnlyList<string> RequiredTools => ["docker"];

    public async Task<IReadOnlyList<Problem>> PreflightAsync(TargetSpec target, CancellationToken cancellationToken) =>
        await docker.IsEngineReachableAsync(cancellationToken).ConfigureAwait(false)
            ? []
            : [Problem.Error(ErrorCodes.DockerUnreachable, "A running Docker engine is required for AppImages, but `docker info` failed. Start it (e.g. `colima start`) as this user.")];

    public async Task<IReadOnlyList<ProducedFile>> BuildAsync(TargetContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var appDir = Path.Combine(context.WorkDir, "AppDir");
        appDirs.Build(context, appDir);
        var output = Path.Combine(context.WorkDir, context.ArtifactBaseName + ".AppImage");
        await appImages.BuildAsync(appDir, context.Target.Arch, output, cancellationToken).ConfigureAwait(false);
        return [new ProducedFile(ArtifactKind.AppImage, output)];
    }
}
