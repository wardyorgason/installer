using Installer.Dao.Wrappers;

namespace Installer.Dao.Linux;

internal sealed class ContainerDefinitionDao(IEmbeddedResourceWrapper resources) : IContainerDefinitionDao
{
    internal const string ResourceName = "Installer.Dao.Linux.appimage.Dockerfile";

    public string GetAppImageDockerfile() => resources.ReadText(ResourceName);
}
