using Installer.Dtos.Manifest;

namespace Installer.Services.Formats;

public interface IPackageFormatResolver
{
    /// <summary>The format for this OS, or null when none is registered.</summary>
    IPackageFormatService? Find(TargetOs os);
}
