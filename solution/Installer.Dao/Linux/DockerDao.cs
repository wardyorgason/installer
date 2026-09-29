using Installer.Dao.Tools;
using Installer.Dtos.Tools;

namespace Installer.Dao.Linux;

internal sealed class DockerDao(IToolDao tools) : IDockerDao
{
    public async Task<bool> IsEngineReachableAsync(CancellationToken cancellationToken)
    {
        var result = await tools.RunAsync(new ToolCommand("docker", ["info", "--format", "{{.ServerVersion}}"], AllowFailure: true), cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    public async Task<bool> ImageExistsAsync(string image, CancellationToken cancellationToken)
    {
        var result = await tools.RunAsync(new ToolCommand("docker", ["image", "inspect", "--format", "{{.Id}}", image], AllowFailure: true), cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    public async Task<string> GetImageIdAsync(string image, CancellationToken cancellationToken)
    {
        var result = await tools.RunAsync(new ToolCommand("docker", ["image", "inspect", "--format", "{{.Id}}", image]), cancellationToken).ConfigureAwait(false);
        return result.StandardOutput.Trim();
    }

    public Task BuildImageAsync(string tag, string dockerfile, CancellationToken cancellationToken) =>
        tools.RunAsync(new ToolCommand("docker", ["build", "-t", tag, "-"], StandardInput: dockerfile), cancellationToken);

    public async Task<string> CreateContainerAsync(string image, IReadOnlyList<string> command, IReadOnlyDictionary<string, string> environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var arguments = new List<string> { "create" };
        foreach (var (name, value) in environment.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            arguments.Add("-e");
            arguments.Add($"{name}={value}");
        }

        arguments.Add(image);
        arguments.AddRange(command);
        var result = await tools.RunAsync(new ToolCommand("docker", arguments), cancellationToken).ConfigureAwait(false);
        return result.StandardOutput.Trim();
    }

    public Task CopyToContainerAsync(string containerId, string source, string containerPath, CancellationToken cancellationToken) =>
        tools.RunAsync(new ToolCommand("docker", ["cp", source, $"{containerId}:{containerPath}"]), cancellationToken);

    public Task StartAsync(string containerId, CancellationToken cancellationToken) =>
        tools.RunAsync(new ToolCommand("docker", ["start", "-a", containerId]), cancellationToken);

    public Task CopyFromContainerAsync(string containerId, string containerPath, string destination, CancellationToken cancellationToken) =>
        tools.RunAsync(new ToolCommand("docker", ["cp", $"{containerId}:{containerPath}", destination]), cancellationToken);

    public Task RemoveContainerAsync(string containerId, CancellationToken cancellationToken) =>
        tools.RunAsync(new ToolCommand("docker", ["rm", "-f", containerId], AllowFailure: true), CancellationToken.None);
}
