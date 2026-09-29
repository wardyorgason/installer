using Installer.Dtos.Payload;

namespace Installer.Dao.Wrappers;

/// <summary>Wraps <see cref="System.IO.Compression.ZipFile"/>.</summary>
public interface IZipFileWrapper
{
    IReadOnlyList<ZipEntryInfo> ListEntries(string zipPath);

    /// <summary>
    /// Extracts the file entries named as keys to the paths given as values, keeping each entry's stored Unix permissions.
    /// Entries not in the map are skipped.
    /// </summary>
    void ExtractEntries(string zipPath, IReadOnlyDictionary<string, string> destinations);
}
