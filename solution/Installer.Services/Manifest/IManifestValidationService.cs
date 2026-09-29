using Installer.Dtos.Build;
using Installer.Dtos.Manifest;

namespace Installer.Services.Manifest;

/// <summary>
/// Checks a read, merged and path-resolved document against every package-definition rule and, when there are no
/// errors, turns it into a <see cref="PackageManifest"/>.
/// </summary>
public interface IManifestValidationService
{
    PackageManifest? Validate(string manifestPath, ManifestDocument document, ICollection<Problem> problems);
}
