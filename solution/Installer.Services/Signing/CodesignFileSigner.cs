using Installer.Dao.MacOS;
using Installer.Dtos.Signing;

namespace Installer.Services.Signing;

internal sealed class CodesignFileSigner(ICodesignDao codesign) : ICodesignFileSigner
{
    public Task SignAsync(string path, SigningOptions options, CancellationToken cancellationToken) =>
        options is CodesignSigningOptions codesignOptions
            ? codesign.SignAsync(path, codesignOptions, cancellationToken)
            : throw new ArgumentException($"{nameof(CodesignFileSigner)} needs {nameof(CodesignSigningOptions)}.", nameof(options));
}
