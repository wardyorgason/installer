using Installer.Dao.Wrappers;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace Installer.Dao.Payload;

internal sealed class PayloadDao(IFileSystemWrapper fileSystem, IZipFileWrapper zip) : IPayloadDao
{
    public PayloadKind? Probe(string path)
    {
        if (fileSystem.DirectoryExists(path))
        {
            return PayloadKind.Directory;
        }

        return fileSystem.FileExists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? PayloadKind.Zip : null;
    }

    public bool FileExists(string path) => fileSystem.FileExists(path);

    public IReadOnlyList<ZipEntryInfo> ListZipEntries(string zipPath) => zip.ListEntries(zipPath);

    public void ExtractZipEntries(string zipPath, string destinationRoot, IReadOnlyDictionary<string, string> entryToRelativePath)
    {
        ArgumentNullException.ThrowIfNull(entryToRelativePath);
        fileSystem.CreateDirectory(destinationRoot);
        var destinations = entryToRelativePath.ToDictionary(pair => pair.Key, pair => FileTree.Combine(destinationRoot, pair.Value), StringComparer.Ordinal);
        zip.ExtractEntries(zipPath, destinations);
    }

    public void CopyDirectory(string source, string destination)
    {
        fileSystem.CreateDirectory(destination);
        foreach (var (relativePath, entry) in FileTree.Walk(fileSystem, source))
        {
            var target = FileTree.Combine(destination, relativePath);
            switch (entry.Kind)
            {
                case FileSystemEntryKind.Directory:
                    fileSystem.CreateDirectory(target);
                    break;
                case FileSystemEntryKind.SymbolicLink:
                    fileSystem.CreateSymbolicLink(target, fileSystem.GetLinkTarget(entry.FullPath)!);
                    break;
                default:
                    fileSystem.CopyFile(entry.FullPath, target, overwrite: true);
                    fileSystem.SetUnixFileMode(target, fileSystem.GetUnixFileMode(entry.FullPath));
                    break;
            }
        }
    }

    public IReadOnlyList<string> ListFiles(string root) =>
        FileTree.Walk(fileSystem, root)
            .Where(item => item.Entry.Kind == FileSystemEntryKind.File)
            .Select(item => item.RelativePath)
            .ToList();

    public byte[] ReadHeader(string path, int count)
    {
        using var stream = fileSystem.OpenRead(path);
        var buffer = new byte[count];
        var read = stream.ReadAtLeast(buffer, count, throwOnEndOfStream: false);
        return buffer[..read];
    }

    public string ReadText(string path) => fileSystem.ReadAllText(path);

    public UnixFileMode GetMode(string path) => fileSystem.GetUnixFileMode(path);

    public void SetMode(string path, UnixFileMode mode) => fileSystem.SetUnixFileMode(path, mode);
}
