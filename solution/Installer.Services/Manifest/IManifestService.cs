namespace Installer.Services.Manifest;

public interface IManifestService
{
    /// <summary>Reads, expands, parses, merges and validates the manifest, collecting every problem it finds.</summary>
    ManifestLoadResult Load(string manifestPath);
}
