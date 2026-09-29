using Installer.Dao.Signing;
using Installer.Dtos.Signing;
using Microsoft.Extensions.Logging;

namespace Installer.Services.Signing;

internal sealed class CommandFileSigner(ISignCommandDao commands, ILogger<CommandFileSigner> logger) : ICommandFileSigner
{
    internal const string FilePlaceholder = "{file}";

    public Task SignAsync(string path, SigningOptions options, CancellationToken cancellationToken)
    {
        if (options is not CommandSigningOptions command)
        {
            throw new ArgumentException($"{nameof(CommandFileSigner)} needs {nameof(CommandSigningOptions)}.", nameof(options));
        }

        logger.LogInformation("Signing {File}", Path.GetFileName(path));
        var arguments = command.Command.Select(argument => argument.Replace(FilePlaceholder, path, StringComparison.Ordinal)).ToList();
        return commands.RunAsync(arguments, cancellationToken);
    }
}
