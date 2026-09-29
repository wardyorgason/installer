using Installer.Dtos.Payload;

namespace Installer.Dao.Workspace;

/// <summary>File operations on work and output directories: everything a build writes.</summary>
public interface IWorkspaceDao
{
    /// <summary>Deletes <paramref name="path"/> if it exists, then creates it empty.</summary>
    void ResetDirectory(string path);

    void EnsureDirectory(string path);

    void DeleteDirectory(string path);

    bool DirectoryExists(string path);

    bool FileExists(string path);

    /// <summary>Writes the file, creating its parent directories.</summary>
    void WriteText(string path, string contents);

    /// <summary>Writes the file, creating its parent directories.</summary>
    void WriteBytes(string path, byte[] contents);

    void CopyFile(string source, string destination);

    void MoveDirectory(string source, string destination);

    /// <summary>Creates a symbolic link at <paramref name="linkPath"/> pointing to the relative <paramref name="relativeTarget"/>.</summary>
    void CreateRelativeSymlink(string linkPath, string relativeTarget);

    /// <summary>Creates a symbolic link with any target, e.g. a disk image's <c>Applications -> /Applications</c>.</summary>
    void CreateSymlink(string linkPath, string target);

    /// <summary>Lists the entries directly inside <paramref name="directory"/>, reporting links without following them.</summary>
    IReadOnlyList<FileSystemEntry> GetEntries(string directory);

    /// <summary>Every entry under <paramref name="root"/>, recursively, without following links; paths relative with '/'.</summary>
    IReadOnlyList<(string RelativePath, FileSystemEntryKind Kind)> ListTree(string root);

    /// <summary>Reads up to <paramref name="count"/> bytes from the start of the file.</summary>
    byte[] ReadHeader(string path, int count);

    long GetDirectorySize(string path);

    /// <summary>Copies <paramref name="source"/> to <paramref name="destination"/> (replacing it) and returns its size and SHA-256.</summary>
    FileFingerprint Publish(string source, string destination);
}
