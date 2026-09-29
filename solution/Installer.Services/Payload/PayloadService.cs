using Installer.Dao.Payload;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;
using Microsoft.Extensions.Logging;

namespace Installer.Services.Payload;

internal sealed class PayloadService(IPayloadDao payloads, IBinaryInspectionService binaries, ILogger<PayloadService> logger) : IPayloadService
{
    private const UnixFileMode ExecuteBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    public PreparedPayload Prepare(TargetSpec target, string destination)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.PayloadKind == PayloadKind.Directory)
        {
            logger.LogInformation("Copying payload {Payload}", target.PayloadPath);
            payloads.CopyDirectory(target.PayloadPath, destination);
        }
        else
        {
            logger.LogInformation("Extracting payload {Payload}", target.PayloadPath);
            Extract(target, destination);
        }

        var files = payloads.ListFiles(destination);
        if (target.Os != TargetOs.Windows)
        {
            RestoreExecutePermissions(destination, files);
        }

        return new PreparedPayload(destination, files);
    }

    private void Extract(TargetSpec target, string destination)
    {
        // Zips made on Windows may use '\'; decisions use '/' names, extraction uses the stored names.
        var entries = payloads.ListZipEntries(target.PayloadPath)
            .Select(entry => (Stored: entry.FullName, Normalized: entry with { FullName = entry.FullName.Replace('\\', '/') }))
            .ToList();
        var escaping = entries.FirstOrDefault(entry => EscapesRoot(entry.Normalized.FullName));
        if (escaping.Normalized is not null)
        {
            throw new BuildFailedException(
                ErrorCodes.PayloadPathEscape,
                $"Target '{target.Name}': zip entry '{escaping.Stored}' in {target.PayloadPath} would be extracted outside the payload folder.");
        }

        var prefix = SingleTopLevelFolder(entries.Select(entry => entry.Normalized).ToList());
        var map = entries
            .Where(entry => !entry.Normalized.IsDirectory && entry.Normalized.FullName.Length > prefix.Length)
            .ToDictionary(entry => entry.Stored, entry => entry.Normalized.FullName[prefix.Length..], StringComparer.Ordinal);
        payloads.ExtractZipEntries(target.PayloadPath, destination, map);
    }

    /// <summary>"App/" when every entry sits under one top-level folder and no file is at the root; otherwise "".</summary>
    internal static string SingleTopLevelFolder(IReadOnlyList<ZipEntryInfo> entries)
    {
        if (entries.Count == 0 || entries.Any(entry => !entry.IsDirectory && !entry.FullName.Contains('/', StringComparison.Ordinal)))
        {
            return string.Empty;
        }

        var names = entries.Select(entry => entry.FullName.Split('/')[0]).Distinct(StringComparer.Ordinal).ToList();
        return names.Count == 1 ? names[0] + "/" : string.Empty;
    }

    internal static bool EscapesRoot(string entryName) =>
        entryName.StartsWith('/')
        || (entryName.Length >= 2 && entryName[1] == ':')
        || entryName.Split('/').Any(segment => segment == "..");

    private void RestoreExecutePermissions(string root, IReadOnlyList<string> files)
    {
        foreach (var file in files)
        {
            var path = Path.Combine(root, file);
            if (!binaries.NeedsExecutePermission(payloads.ReadHeader(path, binaries.HeaderLength)))
            {
                continue;
            }

            var mode = payloads.GetMode(path);
            if ((mode & ExecuteBits) != ExecuteBits)
            {
                payloads.SetMode(path, mode | ExecuteBits | UnixFileMode.UserRead);
            }
        }
    }
}
