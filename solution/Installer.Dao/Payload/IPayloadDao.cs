using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace Installer.Dao.Payload;

/// <summary>Reads payloads (zips or directories) and adjusts the working copy's files.</summary>
public interface IPayloadDao
{
    /// <summary><see cref="PayloadKind.Zip"/> for an existing <c>.zip</c> file, <see cref="PayloadKind.Directory"/> for an existing directory, otherwise null.</summary>
    PayloadKind? Probe(string path);

    bool FileExists(string path);

    IReadOnlyList<ZipEntryInfo> ListZipEntries(string zipPath);

    /// <summary>Extracts each zip entry (key) to <paramref name="destinationRoot"/>/relative path (value). The caller has already validated the paths.</summary>
    void ExtractZipEntries(string zipPath, string destinationRoot, IReadOnlyDictionary<string, string> entryToRelativePath);

    /// <summary>Copies a directory tree, recreating symbolic links as links and keeping Unix permissions.</summary>
    void CopyDirectory(string source, string destination);

    /// <summary>Regular files under <paramref name="root"/>, relative with '/', without following links.</summary>
    IReadOnlyList<string> ListFiles(string root);

    /// <summary>Reads up to <paramref name="count"/> bytes from the start of the file.</summary>
    byte[] ReadHeader(string path, int count);

    string ReadText(string path);

    UnixFileMode GetMode(string path);

    void SetMode(string path, UnixFileMode mode);
}
