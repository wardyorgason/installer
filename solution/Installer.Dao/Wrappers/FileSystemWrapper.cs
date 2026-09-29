using System.Diagnostics.CodeAnalysis;
using Installer.Dtos.Payload;

namespace Installer.Dao.Wrappers;

[ExcludeFromCodeCoverage(Justification = "Thin wrapper; covered by integration tests.")]
internal sealed class FileSystemWrapper : IFileSystemWrapper
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    public void DeleteFile(string path) => File.Delete(path);

    public void CopyFile(string source, string destination, bool overwrite) => File.Copy(source, destination, overwrite);

    public void MoveDirectory(string source, string destination) => Directory.Move(source, destination);

    public void MoveFile(string source, string destination, bool overwrite) => File.Move(source, destination, overwrite);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public void WriteAllBytes(string path, byte[] contents) => File.WriteAllBytes(path, contents);

    public Stream OpenRead(string path) => File.OpenRead(path);

    public long GetFileLength(string path) => new FileInfo(path).Length;

    public IReadOnlyList<FileSystemEntry> GetEntries(string directory) =>
        new DirectoryInfo(directory).EnumerateFileSystemInfos()
            .Select(info => new FileSystemEntry(
                info.FullName,
                info.LinkTarget is not null ? FileSystemEntryKind.SymbolicLink
                : info is DirectoryInfo ? FileSystemEntryKind.Directory
                : FileSystemEntryKind.File))
            .OrderBy(entry => entry.FullPath, StringComparer.Ordinal)
            .ToList();

    public void CreateSymbolicLink(string path, string target) => File.CreateSymbolicLink(path, target);

    public string? GetLinkTarget(string path) => new FileInfo(path).LinkTarget;

    public UnixFileMode GetUnixFileMode(string path) =>
        OperatingSystem.IsWindows() ? UnixFileMode.None : File.GetUnixFileMode(path);

    public void SetUnixFileMode(string path, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
        }
    }
}
