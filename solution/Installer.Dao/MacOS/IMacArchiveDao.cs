namespace Installer.Dao.MacOS;

public interface IMacArchiveDao
{
    /// <summary>Zips an .app with ditto, keeping its signature, permissions and symbolic links (Compress-Archive/zip lose them).</summary>
    Task ZipAsync(string appPath, string output, CancellationToken cancellationToken);
}
