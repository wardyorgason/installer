using Installer.Dtos.Tools;

namespace Installer.Dao.MacOS;

public interface IKeychainDao
{
    /// <summary>The valid code-signing identities in the user's keychains (<c>security find-identity -v -p codesigning</c>).</summary>
    Task<IReadOnlyList<SigningIdentity>> FindCodeSigningIdentitiesAsync(CancellationToken cancellationToken);
}
