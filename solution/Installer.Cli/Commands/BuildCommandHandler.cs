using Installer.Cli.Output;
using Installer.Dtos.Build;
using Installer.Services.Build;

namespace Installer.Cli.Commands;

internal sealed class BuildCommandHandler(IBuildService builds, IResultWriter results) : IBuildCommandHandler
{
    public async Task<int> HandleAsync(BuildCommandArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var result = await builds.RunAsync(new BuildRequest(arguments.ManifestPath, arguments.OutputDirectory, arguments.Targets), cancellationToken).ConfigureAwait(false);
        results.Write(result);
        return ExitCodes.For(result);
    }
}
