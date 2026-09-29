namespace Installer.Cli.Commands;

public interface IBuildCommandHandler
{
    /// <summary>Runs the build, writes the result to stdout and returns the exit code (0 success, 1 a target failed, 2 invalid input).</summary>
    Task<int> HandleAsync(BuildCommandArguments arguments, CancellationToken cancellationToken);
}
