using System.Globalization;
using Installer.Dtos.Manifest;

namespace Installer.Services.Manifest;

internal sealed class VersionService : IVersionService
{
    public AppVersion? Parse(string text, out string? error)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = text.Split('.');
        if (parts.Length is < 3 or > 4 || parts.Any(part => part.Length == 0 || !part.All(char.IsAsciiDigit)))
        {
            error = $"version '{text}' must be 3 or 4 dot-separated numbers (e.g. 1.2.0 or 1.2.0.45); put free-form text such as 1.2.0-beta.1 in displayVersion.";
            return null;
        }

        var numbers = new List<int>();
        foreach (var part in parts)
        {
            if (part.Length > 5 || int.Parse(part, CultureInfo.InvariantCulture) > 65535)
            {
                error = $"version '{text}': each part must be between 0 and 65535.";
                return null;
            }

            numbers.Add(int.Parse(part, CultureInfo.InvariantCulture));
        }

        error = null;
        return new AppVersion(text, numbers);
    }

    public string ShortVersion(AppVersion version) => Join(version.Parts.Take(3));

    public string FullVersion(AppVersion version) => Join(version.Parts);

    public string FourPartVersion(AppVersion version) => Join(version.Parts.Concat(Enumerable.Repeat(0, 4 - version.Parts.Count)));

    private static string Join(IEnumerable<int> parts) => string.Join('.', parts.Select(part => part.ToString(CultureInfo.InvariantCulture)));
}
