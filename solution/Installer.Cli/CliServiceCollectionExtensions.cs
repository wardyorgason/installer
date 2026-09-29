using Installer.Cli.Commands;
using Installer.Cli.Logging;
using Installer.Cli.Output;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Installer.Cli;

public static class CliServiceCollectionExtensions
{
    /// <summary>Registers the command handlers and logging. Logs always go to stderr; stdout carries only the result.</summary>
    public static IServiceCollection AddInstallerCli(this IServiceCollection services, CliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddLogging(builder => builder
            .SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information)
            .AddConsole(console =>
            {
                console.FormatterName = PlainConsoleFormatter.Name;
                console.LogToStandardErrorThreshold = LogLevel.Trace;
            })
            .AddConsoleFormatter<PlainConsoleFormatter, ConsoleFormatterOptions>());
        services.AddSingleton<IConsoleWrapper, ConsoleWrapper>();
        services.AddSingleton<IResultWriter, ResultWriter>();
        services.AddSingleton<IBuildCommandHandler, BuildCommandHandler>();
        services.AddSingleton<ISignFileCommandHandler, SignFileCommandHandler>();
        return services;
    }
}
