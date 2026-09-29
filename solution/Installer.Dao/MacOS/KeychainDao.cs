using System.Text.RegularExpressions;
using Installer.Dao.Tools;
using Installer.Dtos.Tools;

namespace Installer.Dao.MacOS;

internal sealed partial class KeychainDao(IToolDao tools) : IKeychainDao
{
    public async Task<IReadOnlyList<SigningIdentity>> FindCodeSigningIdentitiesAsync(CancellationToken cancellationToken)
    {
        var result = await tools.RunAsync(new ToolCommand("security", ["find-identity", "-v", "-p", "codesigning"]), cancellationToken).ConfigureAwait(false);
        return Parse(result.StandardOutput);
    }

    /// <summary>Parses lines such as <c>  1) 0123…CDEF "Developer ID Application: Name (TEAM)"</c>.</summary>
    internal static IReadOnlyList<SigningIdentity> Parse(string output) =>
        IdentityLine().Matches(output).Select(match => new SigningIdentity(match.Groups["hash"].Value, match.Groups["name"].Value)).ToList();

    [GeneratedRegex("""^\s*\d+\)\s+(?<hash>[0-9A-Fa-f]{40})\s+"(?<name>.+)"\s*$""", RegexOptions.Multiline)]
    private static partial Regex IdentityLine();
}
