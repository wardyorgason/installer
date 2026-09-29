using Installer.Dtos.Build;

namespace Installer.Services.Linux;

public interface IAppDirService
{
    /// <summary>
    /// Lays out the AppDir: the payload under <c>usr/lib/&lt;id&gt;</c>, <c>AppRun</c> linked to the main executable, the
    /// desktop entry, and the icon (with <c>.DirIcon</c> linked to it). Moves the payload out of the work directory.
    /// </summary>
    void Build(TargetContext context, string appDir);
}
