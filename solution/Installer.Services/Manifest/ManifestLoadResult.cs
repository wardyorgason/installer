using Installer.Dtos.Build;
using Installer.Dtos.Manifest;

namespace Installer.Services.Manifest;

/// <summary><see cref="Manifest"/> is set only when <see cref="Problems"/> holds no errors.</summary>
public sealed record ManifestLoadResult(PackageManifest? Manifest, IReadOnlyList<Problem> Problems);
