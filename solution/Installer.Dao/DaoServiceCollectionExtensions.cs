using Installer.Dao.Host;
using Installer.Dao.Imaging;
using Installer.Dao.Linux;
using Installer.Dao.MacOS;
using Installer.Dao.Manifest;
using Installer.Dao.Payload;
using Installer.Dao.Signing;
using Installer.Dao.Tools;
using Installer.Dao.Windows;
using Installer.Dao.Workspace;
using Installer.Dao.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace Installer.Dao;

public static class DaoServiceCollectionExtensions
{
    /// <summary>Registers every wrapper and Dao. Called only from the CLI's composition root.</summary>
    public static IServiceCollection AddInstallerDao(this IServiceCollection services)
    {
        services.AddSingleton<IProcessWrapper, ProcessWrapper>();
        services.AddSingleton<IFileSystemWrapper, FileSystemWrapper>();
        services.AddSingleton<IZipFileWrapper, ZipFileWrapper>();
        services.AddSingleton<IEnvironmentWrapper, EnvironmentWrapper>();
        services.AddSingleton<IEmbeddedResourceWrapper, EmbeddedResourceWrapper>();
        services.AddSingleton<IImageWrapper, ImageWrapper>();

        services.AddSingleton<IToolDao, ToolDao>();
        services.AddSingleton<IEnvironmentDao, EnvironmentDao>();
        services.AddSingleton<IManifestDao, ManifestDao>();
        services.AddSingleton<IWorkspaceDao, WorkspaceDao>();
        services.AddSingleton<IPayloadDao, PayloadDao>();
        services.AddSingleton<IImageDao, ImageDao>();
        services.AddSingleton<INsisDao, NsisDao>();
        services.AddSingleton<ISignCommandDao, SignCommandDao>();
        services.AddSingleton<IContainerDefinitionDao, ContainerDefinitionDao>();
        services.AddSingleton<IDockerDao, DockerDao>();
        services.AddSingleton<IKeychainDao, KeychainDao>();
        services.AddSingleton<ICodesignDao, CodesignDao>();
        services.AddSingleton<IDiskImageDao, DiskImageDao>();
        services.AddSingleton<IMacArchiveDao, MacArchiveDao>();
        services.AddSingleton<INotaryDao, NotaryDao>();
        return services;
    }
}
