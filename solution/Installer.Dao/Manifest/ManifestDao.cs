using Installer.Dao.Wrappers;

namespace Installer.Dao.Manifest;

internal sealed class ManifestDao(IFileSystemWrapper fileSystem) : IManifestDao
{
    public bool Exists(string path) => fileSystem.FileExists(path);

    public string ReadText(string path) => fileSystem.ReadAllText(path);
}
