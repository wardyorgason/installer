using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace Installer.Services.Payload;

public interface IPayloadService
{
    /// <summary>
    /// Copies or extracts the target's payload into <paramref name="destination"/> (never touching the original) and, for
    /// macOS and Linux targets, sets the executable permission on native executables and scripts.
    /// </summary>
    PreparedPayload Prepare(TargetSpec target, string destination);
}
