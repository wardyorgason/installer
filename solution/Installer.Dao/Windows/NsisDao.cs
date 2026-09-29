using System.Globalization;
using System.Text.RegularExpressions;
using Installer.Dao.Tools;
using Installer.Dtos.Tools;

namespace Installer.Dao.Windows;

internal sealed partial class NsisDao(IToolDao tools) : INsisDao
{
    public async Task<Version?> GetVersionAsync(CancellationToken cancellationToken)
    {
        var result = await tools.RunAsync(new ToolCommand("makensis", ["-VERSION"]), cancellationToken).ConfigureAwait(false);
        return ParseVersion(result.StandardOutput);
    }

    public Task CompileAsync(string scriptPath, CancellationToken cancellationToken) =>
        tools.RunAsync(
            new ToolCommand("makensis", ["-V2", "-INPUTCHARSET", "UTF8", scriptPath], WorkingDirectory: Path.GetDirectoryName(scriptPath)),
            cancellationToken);

    /// <summary>Parses output such as <c>v3.10</c> or <c>v3.08-2</c>.</summary>
    internal static Version? ParseVersion(string output)
    {
        var match = VersionPattern().Match(output);
        return match.Success
            ? new Version(int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture))
            : null;
    }

    [GeneratedRegex(@"v?(?<major>\d+)\.(?<minor>\d+)")]
    private static partial Regex VersionPattern();
}
