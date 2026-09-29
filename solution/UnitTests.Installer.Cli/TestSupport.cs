using Installer.Cli.Output;

namespace UnitTests.Installer.Cli;

/// <summary>Captures what the CLI writes to stdout and stderr.</summary>
public sealed class StringConsole : IConsoleWrapper
{
    public StringWriter OutWriter { get; } = new();

    public StringWriter ErrorWriter { get; } = new();

    public TextWriter Out => OutWriter;

    public TextWriter Error => ErrorWriter;
}
