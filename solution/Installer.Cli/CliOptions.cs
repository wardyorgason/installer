namespace Installer.Cli;

/// <summary>Process-wide switches taken from the command line before the container is built.</summary>
public sealed record CliOptions(bool Verbose);
