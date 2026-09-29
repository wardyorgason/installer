using System.Text.Json;
using Installer.Dao.Tools;
using Installer.Dtos.Build;
using Installer.Dtos.Tools;

namespace Installer.Dao.MacOS;

internal sealed class NotaryDao(IToolDao tools) : INotaryDao
{
    public async Task<NotaryResult> SubmitAsync(string file, string keychainProfile, CancellationToken cancellationToken)
    {
        var result = await tools.RunAsync(
            new ToolCommand("xcrun", ["notarytool", "submit", file, "--keychain-profile", keychainProfile, "--wait", "--timeout", "30m", "--output-format", "json"], AllowFailure: true),
            cancellationToken).ConfigureAwait(false);
        return Parse(result) ?? throw new BuildFailedException(
            ErrorCodes.ToolFailed,
            $"notarytool submit exited with code {result.ExitCode}: {(result.StandardError + result.StandardOutput).Trim()}");
    }

    public Task SaveLogAsync(string submissionId, string keychainProfile, string output, CancellationToken cancellationToken) =>
        tools.RunAsync(new ToolCommand("xcrun", ["notarytool", "log", submissionId, "--keychain-profile", keychainProfile, output], AllowFailure: true), cancellationToken);

    public Task StapleAsync(string path, CancellationToken cancellationToken) =>
        tools.RunAsync(new ToolCommand("xcrun", ["stapler", "staple", path]), cancellationToken);

    /// <summary>Reads notarytool's JSON (<c>{"id": "…", "status": "Accepted", "message": "…"}</c>); null when it isn't there.</summary>
    internal static NotaryResult? Parse(ProcessResult result)
    {
        var json = result.StandardOutput.Trim();
        var start = json.IndexOf('{', StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json[start..]);
            var root = document.RootElement;
            if (!root.TryGetProperty("status", out var status) || !root.TryGetProperty("id", out var id))
            {
                return null;
            }

            return new NotaryResult(
                id.GetString() ?? string.Empty,
                status.GetString() ?? string.Empty,
                root.TryGetProperty("message", out var message) ? message.GetString() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
