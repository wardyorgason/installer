using Installer.Dao.Wrappers;
using Installer.Dtos.Payload;

namespace Installer.Dao;

/// <summary>Recursive walks over <see cref="IFileSystemWrapper"/> that never follow symbolic links.</summary>
internal static class FileTree
{
    /// <summary>Every entry under <paramref name="root"/>, depth first, with paths relative to it using '/'.</summary>
    public static IEnumerable<(string RelativePath, FileSystemEntry Entry)> Walk(IFileSystemWrapper fileSystem, string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        var results = new List<(string, FileSystemEntry)>();
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in fileSystem.GetEntries(directory))
            {
                results.Add((Relative(root, entry.FullPath), entry));
                if (entry.Kind == FileSystemEntryKind.Directory)
                {
                    pending.Push(entry.FullPath);
                }
            }
        }

        return results.OrderBy(result => result.Item1, StringComparer.Ordinal);
    }

    public static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>Joins a '/'-separated relative path onto <paramref name="root"/>.</summary>
    public static string Combine(string root, string relativePath) =>
        Path.Combine([root, .. relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries)]);
}
