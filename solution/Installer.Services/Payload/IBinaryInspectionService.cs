using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace Installer.Services.Payload;

/// <summary>Reads executable headers (PE, Mach-O, ELF) to find each file's platform.</summary>
public interface IBinaryInspectionService
{
    /// <summary>How many leading bytes <see cref="Inspect"/> needs to see a PE header.</summary>
    int HeaderLength { get; }

    BinaryInfo Inspect(ReadOnlySpan<byte> header);

    /// <summary>True for Mach-O and ELF binaries and <c>#!</c> scripts: the files that need the executable permission.</summary>
    bool NeedsExecutePermission(ReadOnlySpan<byte> header);

    /// <summary>Fails the target unless the main executable is a binary for the target's OS and architecture.</summary>
    void CheckMainExecutable(PreparedPayload payload, string mainExecutable, TargetSpec target);
}
