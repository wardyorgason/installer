namespace Installer.Dao.MacOS;

public interface IDiskImageDao
{
    /// <summary>Creates a compressed (UDZO, HFS+) disk image of <paramref name="sourceFolder"/>, replacing <paramref name="output"/>.</summary>
    Task CreateAsync(string volumeName, string sourceFolder, string output, CancellationToken cancellationToken);
}
