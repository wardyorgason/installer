using Installer.Dao.Tools;
using Installer.Dtos.Build;
using Installer.Dtos.Tools;

namespace Installer.Dao.Signing;

internal sealed class SignCommandDao(IToolDao tools) : ISignCommandDao
{
    public async Task RunAsync(IReadOnlyList<string> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Count == 0)
        {
            throw new ArgumentException("The signing command is empty.", nameof(command));
        }

        var result = await tools.RunAsync(new ToolCommand(command[0], command.Skip(1).ToList(), AllowFailure: true), cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var output = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            throw new BuildFailedException(ErrorCodes.SignFailed, $"Signing command {command[0]} exited with code {result.ExitCode}: {output.Trim()}");
        }
    }
}
