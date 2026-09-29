using Installer.Dtos.Manifest;

namespace Installer.Services.Linux;

public interface IDesktopEntryService
{
    /// <summary>The AppImage's <c>&lt;id&gt;.desktop</c> file.</summary>
    string Generate(AppInfo app, IReadOnlyList<string> categories);
}
