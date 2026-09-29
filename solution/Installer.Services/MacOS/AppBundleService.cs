using Installer.Dao.Workspace;
using Installer.Dtos.Build;
using Installer.Dtos.Payload;
using Installer.Services.Imaging;

namespace Installer.Services.MacOS;

internal sealed class AppBundleService(IWorkspaceDao workspace, IPlistService plists, IIconService icons) : IAppBundleService
{
    public void Build(TargetContext context, string appPath)
    {
        ArgumentNullException.ThrowIfNull(context);
        var app = context.App;
        var contents = Path.Combine(appPath, "Contents");
        var macOS = Path.Combine(contents, "MacOS");
        var resources = Path.Combine(contents, "Resources");
        var iconFile = app.Name + ".icns";

        workspace.ResetDirectory(appPath);
        workspace.EnsureDirectory(contents);
        workspace.MoveDirectory(context.Payload.Root, macOS);
        workspace.EnsureDirectory(resources);

        // codesign treats a folder in Contents/MacOS as a nested bundle ("bundle format unrecognized"). Folders move to
        // Contents/Resources, and a relative symlink keeps them reachable next to the executable (runtimes/, de/, wwwroot/).
        foreach (var entry in workspace.GetEntries(macOS).Where(entry => entry.Kind == FileSystemEntryKind.Directory))
        {
            var name = Path.GetFileName(entry.FullPath);
            if (name == iconFile)
            {
                throw new BuildFailedException(
                    ErrorCodes.BundleNameCollision,
                    $"Target '{context.Target.Name}': the payload folder '{name}' collides with the bundle's icon file Contents/Resources/{iconFile}.");
            }

            workspace.MoveDirectory(entry.FullPath, Path.Combine(resources, name));
            workspace.CreateRelativeSymlink(entry.FullPath, "../Resources/" + name);
        }

        workspace.WriteBytes(Path.Combine(resources, iconFile), icons.CreateIcns(app.IconPath));
        var executableName = Path.GetFileName(context.Profile.MainExecutable);
        workspace.WriteText(
            Path.Combine(contents, "Info.plist"),
            plists.InfoPlist(app, executableName, app.Name, context.Target.MacOS?.InfoPlist ?? Dtos.Manifest.PlistDictionary.Empty));
    }
}
