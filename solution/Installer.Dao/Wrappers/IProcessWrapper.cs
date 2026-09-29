using Installer.Dtos.Tools;

namespace Installer.Dao.Wrappers;

/// <summary>Wraps <see cref="System.Diagnostics.Process"/>.</summary>
public interface IProcessWrapper
{
    /// <summary>
    /// Runs the process to completion, capturing stdout and stderr. Each output line is also passed to
    /// <paramref name="onOutputLine"/> as it arrives. Cancelling kills the whole process tree.
    /// </summary>
    Task<ProcessResult> RunAsync(ProcessRequest request, Action<string>? onOutputLine, CancellationToken cancellationToken);
}
