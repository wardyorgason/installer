using System.Security.Cryptography;
using Installer.Dao.Wrappers;
using Installer.Dtos.Payload;

namespace Installer.Dao.Workspace;

internal sealed class WorkspaceDao(IFileSystemWrapper fileSystem) : IWorkspaceDao
{
    public void ResetDirectory(string path)
    {
        DeleteDirectory(path);
        fileSystem.CreateDirectory(path);
    }

    public void EnsureDirectory(string path) => fileSystem.CreateDirectory(path);

    public void DeleteDirectory(string path)
    {
        if (fileSystem.DirectoryExists(path))
        {
            fileSystem.DeleteDirectory(path, recursive: true);
        }
    }

    public bool DirectoryExists(string path) => fileSystem.DirectoryExists(path);

    public bool FileExists(string path) => fileSystem.FileExists(path);

    public void WriteText(string path, string contents)
    {
        fileSystem.CreateDirectory(Path.GetDirectoryName(path)!);
        fileSystem.WriteAllText(path, contents);
    }

    public void WriteBytes(string path, byte[] contents)
    {
        fileSystem.CreateDirectory(Path.GetDirectoryName(path)!);
        fileSystem.WriteAllBytes(path, contents);
    }

    public void CopyFile(string source, string destination)
    {
        fileSystem.CreateDirectory(Path.GetDirectoryName(destination)!);
        fileSystem.CopyFile(source, destination, overwrite: true);
    }

    public void MoveDirectory(string source, string destination)
    {
        fileSystem.CreateDirectory(Path.GetDirectoryName(destination)!);
        fileSystem.MoveDirectory(source, destination);
    }

    public void CreateRelativeSymlink(string linkPath, string relativeTarget)
    {
        if (Path.IsPathRooted(relativeTarget))
        {
            throw new ArgumentException($"Symlink target must be relative: {relativeTarget}", nameof(relativeTarget));
        }

        fileSystem.CreateSymbolicLink(linkPath, relativeTarget);
    }

    public void CreateSymlink(string linkPath, string target) => fileSystem.CreateSymbolicLink(linkPath, target);

    public IReadOnlyList<FileSystemEntry> GetEntries(string directory) => fileSystem.GetEntries(directory);

    public IReadOnlyList<(string RelativePath, FileSystemEntryKind Kind)> ListTree(string root) =>
        FileTree.Walk(fileSystem, root).Select(item => (item.RelativePath, item.Entry.Kind)).ToList();

    public byte[] ReadHeader(string path, int count)
    {
        using var stream = fileSystem.OpenRead(path);
        var buffer = new byte[count];
        var read = stream.ReadAtLeast(buffer, count, throwOnEndOfStream: false);
        return buffer[..read];
    }

    public long GetDirectorySize(string path) =>
        FileTree.Walk(fileSystem, path)
            .Where(item => item.Entry.Kind == FileSystemEntryKind.File)
            .Sum(item => fileSystem.GetFileLength(item.Entry.FullPath));

    public FileFingerprint Publish(string source, string destination)
    {
        fileSystem.CreateDirectory(Path.GetDirectoryName(destination)!);
        fileSystem.CopyFile(source, destination, overwrite: true);
        using var stream = fileSystem.OpenRead(destination);
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
        return new FileFingerprint(fileSystem.GetFileLength(destination), hash);
    }
}
