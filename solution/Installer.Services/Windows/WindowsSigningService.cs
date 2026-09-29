using System.Text.Json;
using Installer.Dao.Payload;
using Installer.Dtos.Signing;
using Installer.Services.Signing;

namespace Installer.Services.Windows;

internal sealed class WindowsSigningService(ICommandFileSigner signer, IPayloadDao files) : IWindowsSigningService
{
    public Task SignFileAsync(string file, IReadOnlyList<string> signCommand, CancellationToken cancellationToken) =>
        signer.SignAsync(file, new CommandSigningOptions(signCommand), cancellationToken);

    public Task SignFileFromCommandFileAsync(string file, string commandJsonPath, CancellationToken cancellationToken)
    {
        var command = JsonSerializer.Deserialize<string[]>(files.ReadText(commandJsonPath))
            ?? throw new InvalidOperationException($"{commandJsonPath} does not hold a signing command.");
        return SignFileAsync(file, command, cancellationToken);
    }
}
