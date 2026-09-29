using Installer.Dtos.Tools;

namespace Installer.Dao.Tools;

/// <summary>Finds and runs external tools. Every tool-specific Dao runs through it.</summary>
public interface IToolDao
{
    /// <summary>The tool's absolute path, searching PATH plus the Homebrew locations; null when it isn't installed.</summary>
    string? FindTool(string tool);

    /// <summary>
    /// Runs the tool. A missing tool, or a non-zero exit unless <see cref="ToolCommand.AllowFailure"/>, throws a
    /// <see cref="Dtos.Build.BuildFailedException"/> whose message carries the tool's error output.
    /// </summary>
    Task<ProcessResult> RunAsync(ToolCommand command, CancellationToken cancellationToken);

    /// <summary>The error message for a tool that isn't installed, including where it was searched and how to install it.</summary>
    string DescribeMissing(string tool);
}
