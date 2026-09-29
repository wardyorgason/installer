using Installer.Dao.Workspace;
using Installer.Dtos.Build;
using Installer.Services.Imaging;

namespace Installer.Services.Linux;

internal sealed class AppDirService(IWorkspaceDao workspace, IDesktopEntryService desktopEntries, IIconService icons) : IAppDirService
{
    public const int IconSize = 256;

    public void Build(TargetContext context, string appDir)
    {
        ArgumentNullException.ThrowIfNull(context);
        var app = context.App;
        var libRelative = $"usr/lib/{app.Id}";
        workspace.ResetDirectory(appDir);
        workspace.MoveDirectory(context.Payload.Root, Path.Combine(appDir, "usr", "lib", app.Id));
        workspace.CreateRelativeSymlink(Path.Combine(appDir, "AppRun"), $"{libRelative}/{context.Profile.MainExecutable}");
        workspace.WriteText(Path.Combine(appDir, app.Id + ".desktop"), desktopEntries.Generate(app, context.Target.Linux?.Categories ?? ["Utility"]));
        workspace.WriteBytes(Path.Combine(appDir, app.Id + ".png"), icons.CreatePng(app.IconPath, IconSize));
        workspace.CreateRelativeSymlink(Path.Combine(appDir, ".DirIcon"), app.Id + ".png");
    }
}
