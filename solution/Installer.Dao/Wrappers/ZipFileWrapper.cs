using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using Installer.Dtos.Payload;

namespace Installer.Dao.Wrappers;

[ExcludeFromCodeCoverage(Justification = "Thin wrapper; covered by integration tests.")]
internal sealed class ZipFileWrapper : IZipFileWrapper
{
    public IReadOnlyList<ZipEntryInfo> ListEntries(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        return archive.Entries
            .Select(entry => new ZipEntryInfo(entry.FullName, entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')))
            .ToList();
    }

    public void ExtractEntries(string zipPath, IReadOnlyDictionary<string, string> destinations)
    {
        ArgumentNullException.ThrowIfNull(destinations);
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (destinations.TryGetValue(entry.FullName, out var destination))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, overwrite: true); // applies stored Unix permissions on Unix
            }
        }
    }
}
