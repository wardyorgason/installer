namespace Installer.Cli.Output;

/// <summary>Wraps <see cref="Console"/>. stdout carries only the build result; everything else goes to stderr.</summary>
public interface IConsoleWrapper
{
    TextWriter Out { get; }

    TextWriter Error { get; }
}
