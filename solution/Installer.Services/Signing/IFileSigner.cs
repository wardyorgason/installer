using Installer.Dtos.Signing;

namespace Installer.Services.Signing;

/// <summary>The signing hook contract: sign one file in place. Failures throw with the signing tool's error output.</summary>
public interface IFileSigner
{
    Task SignAsync(string path, SigningOptions options, CancellationToken cancellationToken);
}

/// <summary>Signs with the manifest's Windows <c>signCommand</c> (<see cref="CommandSigningOptions"/>).</summary>
public interface ICommandFileSigner : IFileSigner;

/// <summary>Signs with macOS <c>codesign</c> (<see cref="CodesignSigningOptions"/>).</summary>
public interface ICodesignFileSigner : IFileSigner;
