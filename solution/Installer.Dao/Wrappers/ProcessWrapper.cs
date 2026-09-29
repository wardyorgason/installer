using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Installer.Dtos.Tools;

namespace Installer.Dao.Wrappers;

[ExcludeFromCodeCoverage(Justification = "Thin wrapper; covered by integration tests.")]
internal sealed class ProcessWrapper : IProcessWrapper
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, Action<string>? onOutputLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var info = new ProcessStartInfo(request.FileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = request.StandardInput is not null,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in request.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (request.WorkingDirectory is not null)
        {
            info.WorkingDirectory = request.WorkingDirectory;
        }

        foreach (var (name, value) in request.Environment ?? new Dictionary<string, string>())
        {
            info.Environment[name] = value;
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var process = new Process { StartInfo = info };
        process.OutputDataReceived += (_, e) => Append(stdout, e.Data, onOutputLine);
        process.ErrorDataReceived += (_, e) => Append(stderr, e.Data, onOutputLine);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (request.StandardInput is not null)
        {
            await process.StandardInput.WriteAsync(request.StandardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }

            throw;
        }

        process.WaitForExit(); // flushes the asynchronous output handlers
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static void Append(StringBuilder buffer, string? line, Action<string>? onOutputLine)
    {
        if (line is null)
        {
            return;
        }

        lock (buffer)
        {
            buffer.AppendLine(line);
        }

        onOutputLine?.Invoke(line);
    }
}
