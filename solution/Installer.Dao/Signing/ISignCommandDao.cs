namespace Installer.Dao.Signing;

public interface ISignCommandDao
{
    /// <summary>
    /// Runs a user-supplied signing command (first item: the tool, found on PATH or given as a path). A non-zero exit throws
    /// a <see cref="Dtos.Build.BuildFailedException"/> with code sign.failed carrying the tool's error output.
    /// </summary>
    Task RunAsync(IReadOnlyList<string> command, CancellationToken cancellationToken);
}
