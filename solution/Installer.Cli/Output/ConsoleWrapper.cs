using System.Diagnostics.CodeAnalysis;

namespace Installer.Cli.Output;

[ExcludeFromCodeCoverage(Justification = "Thin wrapper.")]
internal sealed class ConsoleWrapper : IConsoleWrapper
{
    public TextWriter Out => Console.Out;

    public TextWriter Error => Console.Error;
}
