using Installer.Dao.Wrappers;
using Installer.Dtos.Build;
using Installer.Dtos.Tools;
using Microsoft.Extensions.Logging;

namespace Installer.Dao.Tools;

internal sealed class ToolDao(IProcessWrapper process, IFileSystemWrapper fileSystem, IEnvironmentWrapper environment, ILogger<ToolDao> logger)
    : IToolDao
{
    /// <summary>Searched after PATH: a launchd-started Jenkins has neither Homebrew location on its PATH.</summary>
    internal static readonly string[] ExtraDirectories = ["/opt/homebrew/bin", "/usr/local/bin"];

    private static readonly Dictionary<string, string> InstallHints = new(StringComparer.Ordinal)
    {
        ["makensis"] = "Install NSIS 3.08 or later, e.g. `brew install makensis`.",
        ["docker"] = "Install a Docker engine, e.g. `brew install colima docker` then `colima start`.",
        ["codesign"] = "It ships with macOS.",
        ["security"] = "It ships with macOS.",
        ["hdiutil"] = "It ships with macOS.",
        ["ditto"] = "It ships with macOS.",
        ["xattr"] = "It ships with macOS.",
        ["xcrun"] = "Install the Command Line Tools: `xcode-select --install`.",
    };

    private const int MaxErrorChars = 4000;

    public string? FindTool(string tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        if (tool.Contains('/'))
        {
            return fileSystem.FileExists(tool) ? tool : null;
        }

        var names = environment.IsWindows() && !tool.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? new[] { tool + ".exe", tool } : [tool];
        var path = environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var directories = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Concat(ExtraDirectories);
        return directories
            .SelectMany(directory => names.Select(name => Path.Combine(directory, name)))
            .FirstOrDefault(fileSystem.FileExists);
    }

    public async Task<ProcessResult> RunAsync(ToolCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var fileName = FindTool(command.Tool) ?? throw new BuildFailedException(ErrorCodes.ToolMissing, DescribeMissing(command.Tool));

        logger.LogDebug("$ {CommandLine}", FormatCommandLine(command.Tool, command.Arguments));
        var request = new ProcessRequest(fileName, command.Arguments, command.WorkingDirectory, command.StandardInput, command.Environment);
        var result = await process.RunAsync(request, line => logger.LogDebug("  {Line}", line), cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0 && !command.AllowFailure)
        {
            var output = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            output = output.Trim();
            if (output.Length > MaxErrorChars)
            {
                output = "…" + output[^MaxErrorChars..];
            }

            throw new BuildFailedException(
                ErrorCodes.ToolFailed,
                $"{command.Tool} exited with code {result.ExitCode}: {output}");
        }

        return result;
    }

    public string DescribeMissing(string tool)
    {
        var hint = InstallHints.TryGetValue(tool, out var text) ? " " + text : string.Empty;
        return $"{tool} was not found on PATH or in {string.Join(", ", ExtraDirectories)}.{hint}";
    }

    internal static string FormatCommandLine(string tool, IEnumerable<string> arguments) =>
        string.Join(' ', new[] { tool }.Concat(arguments).Select(Quote));

    private static string Quote(string argument) =>
        argument.Length > 0 && argument.All(c => !char.IsWhiteSpace(c) && c is not '"' and not '\'')
            ? argument
            : "'" + argument.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
