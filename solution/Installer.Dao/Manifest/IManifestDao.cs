namespace Installer.Dao.Manifest;

public interface IManifestDao
{
    bool Exists(string path);

    string ReadText(string path);
}
