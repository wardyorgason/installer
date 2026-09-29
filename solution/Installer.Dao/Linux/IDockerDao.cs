namespace Installer.Dao.Linux;

/// <summary>The docker CLI. Files move in and out with <c>docker cp</c>, never bind mounts, so any engine works.</summary>
public interface IDockerDao
{
    Task<bool> IsEngineReachableAsync(CancellationToken cancellationToken);

    Task<bool> ImageExistsAsync(string image, CancellationToken cancellationToken);

    /// <summary>The image's content ID, e.g. <c>sha256:…</c>.</summary>
    Task<string> GetImageIdAsync(string image, CancellationToken cancellationToken);

    /// <summary>Builds <paramref name="tag"/> from a Dockerfile passed on stdin, with no build context.</summary>
    Task BuildImageAsync(string tag, string dockerfile, CancellationToken cancellationToken);

    /// <summary>Creates (without starting) a container and returns its ID.</summary>
    Task<string> CreateContainerAsync(string image, IReadOnlyList<string> command, IReadOnlyDictionary<string, string> environment, CancellationToken cancellationToken);

    Task CopyToContainerAsync(string containerId, string source, string containerPath, CancellationToken cancellationToken);

    /// <summary>Starts the container attached and waits; a non-zero exit of its command throws with its output.</summary>
    Task StartAsync(string containerId, CancellationToken cancellationToken);

    Task CopyFromContainerAsync(string containerId, string containerPath, string destination, CancellationToken cancellationToken);

    /// <summary>Removes the container; never throws, so it is safe in cleanup.</summary>
    Task RemoveContainerAsync(string containerId, CancellationToken cancellationToken);
}
