using Installer.Services.Build;
using Installer.Services.Formats;
using Installer.Services.Imaging;
using Installer.Services.Linux;
using Installer.Services.MacOS;
using Installer.Services.Manifest;
using Installer.Services.Payload;
using Installer.Services.Profiles;
using Installer.Services.Signing;
using Installer.Services.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Installer.Services;

public static class ServicesServiceCollectionExtensions
{
    public static IServiceCollection AddInstallerServices(this IServiceCollection services)
    {
        services.AddSingleton<IEnvironmentExpansionService, EnvironmentExpansionService>();
        services.AddSingleton<IVersionService, VersionService>();
        services.AddSingleton<IManifestReader, ManifestReader>();
        services.AddSingleton<IManifestValidationService, ManifestValidationService>();
        services.AddSingleton<IManifestService, ManifestService>();
        services.AddSingleton<IRuntimeProfileResolver, RuntimeProfileResolver>();
        services.AddSingleton<IPackageFormatResolver, PackageFormatResolver>();
        services.AddSingleton<IPreflightService, PreflightService>();
        services.AddSingleton<IBuildService, BuildService>();
        services.AddSingleton<IBinaryInspectionService, BinaryInspectionService>();
        services.AddSingleton<IPayloadService, PayloadService>();
        services.AddSingleton<IRuntimeProfileService, GenericProfileService>();
        services.AddSingleton<IRuntimeProfileService, DotnetProfileService>();
        services.AddSingleton<IEntitlementService, EntitlementService>();
        services.AddSingleton<IIcoEncoderService, IcoEncoderService>();
        services.AddSingleton<IIcnsEncoderService, IcnsEncoderService>();
        services.AddSingleton<IIconService, IconService>();
        services.AddSingleton<ICommandFileSigner, CommandFileSigner>();
        services.AddSingleton<IWindowsSigningService, WindowsSigningService>();
        services.AddSingleton<INsisScriptService, NsisScriptService>();
        services.AddSingleton<IPackageFormatService, WindowsFormatService>();
        services.AddSingleton<IDesktopEntryService, DesktopEntryService>();
        services.AddSingleton<IAppDirService, AppDirService>();
        services.AddSingleton<IAppImageService, AppImageService>();
        services.AddSingleton<IPackageFormatService, LinuxFormatService>();
        services.AddSingleton<ICodesignFileSigner, CodesignFileSigner>();
        services.AddSingleton<IPlistService, PlistService>();
        services.AddSingleton<IAppBundleService, AppBundleService>();
        services.AddSingleton<IMacSigningService, MacSigningService>();
        services.AddSingleton<INotarizationService, NotarizationService>();
        services.AddSingleton<IMacOutputService, MacOutputService>();
        services.AddSingleton<IPackageFormatService, MacFormatService>();
        return services;
    }
}
