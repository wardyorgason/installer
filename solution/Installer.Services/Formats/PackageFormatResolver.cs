using Installer.Dtos.Manifest;

namespace Installer.Services.Formats;

internal sealed class PackageFormatResolver(IEnumerable<IPackageFormatService> formats) : IPackageFormatResolver
{
    private readonly IReadOnlyList<IPackageFormatService> _formats = formats.ToList();

    public IPackageFormatService? Find(TargetOs os) => _formats.FirstOrDefault(format => format.Os == os);
}
