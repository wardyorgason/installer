using System.Text;
using Installer.Dtos.Manifest;

namespace Installer.Services.Linux;

internal sealed class DesktopEntryService : IDesktopEntryService
{
    public string Generate(AppInfo app, IReadOnlyList<string> categories)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(categories);
        var entry = new StringBuilder();
        entry.Append("[Desktop Entry]\n");
        entry.Append("Type=Application\n");
        entry.Append("Name=").Append(Value(app.Name)).Append('\n');
        entry.Append("Comment=").Append(Value(app.Description)).Append('\n');
        entry.Append("Exec=AppRun\n");
        entry.Append("Icon=").Append(app.Id).Append('\n');
        entry.Append("Categories=").Append(string.Concat(categories.Select(category => category + ";"))).Append('\n');
        entry.Append("Terminal=false\n");
        entry.Append("X-AppImage-Version=").Append(Value(app.DisplayVersion)).Append('\n');
        return entry.ToString();
    }

    /// <summary>Desktop entry values are one line; backslashes are escapes.</summary>
    private static string Value(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
