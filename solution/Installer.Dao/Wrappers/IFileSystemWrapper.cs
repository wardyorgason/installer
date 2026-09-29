using Installer.Dtos.Payload;

namespace Installer.Dao.Wrappers;

/// <summary>Wraps <see cref="File"/>, <see cref="Directory"/> and related static file-system APIs.</summary>
public interface IFileSystemWrapper
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    void DeleteDirectory(string path, bool recursive);

    void DeleteFile(string path);

    void CopyFile(string source, string destination, bool overwrite);

    void MoveDirectory(string source, string destination);

    void MoveFile(string source, string destination, bool overwrite);

    string ReadAllText(string path);

    void WriteAllText(string path, string contents);

    byte[] ReadAllBytes(string path);

    void WriteAllBytes(string path, byte[] contents);

    Stream OpenRead(string path);

    long GetFileLength(string path);

    /// <summary>Lists the entries directly inside <paramref name="directory"/>; symbolic links are reported, never followed.</summary>
    IReadOnlyList<FileSystemEntry> GetEntries(string directory);

    /// <summary>Creates a symbolic link at <paramref name="path"/> whose target is <paramref name="target"/>, stored as given (relative stays relative).</summary>
    void CreateSymbolicLink(string path, string target);

    /// <summary>The stored target of a symbolic link (relative stays relative), or null when the path isn't a link.</summary>
    string? GetLinkTarget(string path);

    UnixFileMode GetUnixFileMode(string path);

    void SetUnixFileMode(string path, UnixFileMode mode);
}
