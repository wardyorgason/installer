namespace Installer.Dao.Linux;

public interface IContainerDefinitionDao
{
    /// <summary>The embedded Dockerfile for the AppImage build image.</summary>
    string GetAppImageDockerfile();
}
