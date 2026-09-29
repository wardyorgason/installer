using Installer.Dao.Tools;
using Installer.Dtos.Build;
using Installer.Dtos.Signing;
using Installer.Dtos.Tools;

namespace Installer.Dao.MacOS;

internal sealed class CodesignDao(IToolDao tools) : ICodesignDao
{
    /// <summary>
    /// Signing one file takes seconds. A codesign that runs this long is waiting for a keychain access dialog nobody will
    /// click on an unattended build machine.
    /// </summary>
    internal static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    public Task ClearExtendedAttributesAsync(string path, CancellationToken cancellationToken) =>
        tools.RunAsync(new ToolCommand("xattr", ["-cr", path]), cancellationToken);

    public Task SignAsync(string path, CodesignSigningOptions options, CancellationToken cancellationToken) =>
        RunAsync(Arguments(path, options), $"codesign failed for {path}", cancellationToken);

    public Task VerifyAsync(string path, CancellationToken cancellationToken) =>
        RunAsync(["--verify", "--strict", "--deep", "--verbose=2", path], $"The signature of {path} does not verify", cancellationToken);

    internal static List<string> Arguments(string path, CodesignSigningOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var arguments = new List<string> { "--force", "--sign", options.Identity };
        if (options.HardenedRuntime)
        {
            arguments.AddRange(["--options", "runtime"]);
        }

        arguments.Add(options.SecureTimestamp ? "--timestamp" : "--timestamp=none");
        if (options.Identifier is not null)
        {
            arguments.AddRange(["--identifier", options.Identifier]);
        }

        if (options.EntitlementsPath is not null)
        {
            arguments.AddRange(["--entitlements", options.EntitlementsPath]);
        }

        arguments.Add(path);
        return arguments;
    }

    private async Task RunAsync(IReadOnlyList<string> arguments, string failure, CancellationToken cancellationToken)
    {
        ProcessResult result;
        try
        {
            result = await tools.RunAsync(new ToolCommand("codesign", arguments, AllowFailure: true, Timeout: Timeout), cancellationToken).ConfigureAwait(false);
        }
        catch (BuildFailedException ex) when (ex.Problem.Code == ErrorCodes.ToolTimeout)
        {
            throw new BuildFailedException(
                ErrorCodes.SignFailed,
                $"{failure}: codesign did not finish within {Timeout.TotalMinutes:0} minutes. It is almost certainly waiting for a keychain access " +
                "dialog on the Mac's screen. Allow codesign to use the key without asking, once, as this user: " +
                "security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k \"<login password>\" ~/Library/Keychains/login.keychain-db");
        }

        if (result.ExitCode != 0)
        {
            var output = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            throw new BuildFailedException(ErrorCodes.SignFailed, $"{failure}: {output.Trim()}");
        }
    }
}
