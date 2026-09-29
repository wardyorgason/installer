using System.CommandLine;
using Installer.Cli.Commands;
using Installer.Cli.Output;
using Installer.Dtos.Build;
using Microsoft.Extensions.DependencyInjection;

namespace Installer.Cli;

/// <summary>
/// Defines the command tree. Each command's action builds the container (so --verbose can shape logging) and hands
/// the parsed arguments to a DI-registered handler.
/// </summary>
public static class CommandLine
{
    public static RootCommand CreateRootCommand(Func<CliOptions, ServiceProvider> providerFactory)
    {
        ArgumentNullException.ThrowIfNull(providerFactory);
        var manifest = new Argument<string>("manifest") { Description = "Path to the JSON manifest (e.g. installer.json)." };
        var output = new Option<string?>("--output", "-o") { Description = "Output directory for artifacts. Default: dist next to the manifest." };
        var targets = new Option<string[]>("--target", "-t")
        {
            Description = "Build only this target (repeatable). Default: every target.",
            AllowMultipleArgumentsPerToken = false,
        };
        var verbose = new Option<bool>("--verbose", "-v") { Description = "Include external tool output in the log (stderr)." };

        var build = new Command("build", "Build every target in the manifest (or the selected ones).") { manifest, output, targets, verbose };
        build.SetAction(async (parse, cancellationToken) =>
        {
            using var provider = providerFactory(new CliOptions(parse.GetValue(verbose)));
            var handler = provider.GetRequiredService<IBuildCommandHandler>();
            var arguments = new BuildCommandArguments(parse.GetValue(manifest)!, parse.GetValue(output), parse.GetValue(targets) ?? []);
            return await handler.HandleAsync(arguments, cancellationToken).ConfigureAwait(false);
        });

        var file = new Argument<string>("file");
        var commandJson = new Option<string>("--command-json") { Required = true };
        var signFile = new Command("sign-file", "Internal: signs one file for the NSIS signing hooks.") { file, commandJson };
        signFile.Hidden = true;
        signFile.SetAction(async (parse, cancellationToken) =>
        {
            using var provider = providerFactory(new CliOptions(Verbose: false));
            var handler = provider.GetRequiredService<ISignFileCommandHandler>();
            return await handler.HandleAsync(parse.GetValue(file)!, parse.GetValue(commandJson)!, cancellationToken).ConfigureAwait(false);
        });

        return new RootCommand("Builds installers (Windows setup .exe, macOS .app/.dmg, Linux AppImage) from a JSON manifest.") { build, signFile };
    }

    /// <summary>
    /// Parses and runs. Invalid arguments exit with code 2 and still write a result document (with the errors) to
    /// stdout, like an invalid manifest does; the usual parser messages go to stderr.
    /// </summary>
    public static async Task<int> InvokeAsync(string[] args, Func<CliOptions, ServiceProvider> providerFactory, InvocationConfiguration? configuration = null)
    {
        var root = CreateRootCommand(providerFactory);
        var parse = root.Parse(args);
        if (parse.Errors.Count == 0)
        {
            return await parse.InvokeAsync(configuration).ConfigureAwait(false);
        }

        using var provider = providerFactory(new CliOptions(Verbose: false));
        var console = provider.GetRequiredService<IConsoleWrapper>();
        var error = configuration?.Error ?? console.Error;
        foreach (var parseError in parse.Errors)
        {
            await error.WriteLineAsync("error: " + parseError.Message).ConfigureAwait(false);
        }

        await error.WriteLineAsync("Run with --help for usage.").ConfigureAwait(false);
        var problems = parse.Errors.Select(parseError => Problem.Error(ErrorCodes.CliInvalidArguments, parseError.Message)).ToList();
        provider.GetRequiredService<IResultWriter>().Write(new BuildResult(BuildResult.CurrentSchemaVersion, false, problems, []));
        return ExitCodes.InvalidInput;
    }
}
