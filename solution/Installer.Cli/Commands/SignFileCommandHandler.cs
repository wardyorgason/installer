using Installer.Cli.Output;
using Installer.Dtos.Build;
using Installer.Services.Windows;

namespace Installer.Cli.Commands;

internal sealed class SignFileCommandHandler(IWindowsSigningService signing, IConsoleWrapper console) : ISignFileCommandHandler
{
    public async Task<int> HandleAsync(string file, string commandJsonPath, CancellationToken cancellationToken)
    {
        try
        {
            await signing.SignFileFromCommandFileAsync(file, commandJsonPath, cancellationToken).ConfigureAwait(false);
            return ExitCodes.Success;
        }
        catch (BuildFailedException ex)
        {
            await console.Error.WriteLineAsync("error: " + ex.Message).ConfigureAwait(false);
            return ExitCodes.TargetFailed;
        }
    }
}
