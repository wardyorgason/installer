using Installer.Dtos.Build;

namespace Installer.Services.MacOS;

public interface IAppBundleService
{
    /// <summary>
    /// Builds <paramref name="appPath"/> (…/&lt;name&gt;.app) from the payload: files in Contents/MacOS, each top-level folder
    /// moved to Contents/Resources with a relative symlink left in its place, the .icns icon and Info.plist. Moves the
    /// payload out of the work directory.
    /// </summary>
    void Build(TargetContext context, string appPath);
}
