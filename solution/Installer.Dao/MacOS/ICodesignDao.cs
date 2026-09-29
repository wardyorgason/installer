using Installer.Dtos.Signing;

namespace Installer.Dao.MacOS;

public interface ICodesignDao
{
    /// <summary><c>xattr -cr</c>: extended attributes (quarantine, Finder info) make codesign refuse with "detritus not allowed".</summary>
    Task ClearExtendedAttributesAsync(string path, CancellationToken cancellationToken);

    /// <summary>Signs one path (file or bundle). Failure throws with code sign.failed and codesign's output.</summary>
    Task SignAsync(string path, CodesignSigningOptions options, CancellationToken cancellationToken);

    /// <summary><c>codesign --verify --strict --deep</c>. Failure throws with code sign.failed.</summary>
    Task VerifyAsync(string path, CancellationToken cancellationToken);
}
