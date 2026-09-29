using Installer.Dtos.Manifest;

namespace Installer.Services.Manifest;

/// <summary>Parses the manifest's numeric version and maps it to each platform's version fields.</summary>
public interface IVersionService
{
    /// <summary>Parses 3 or 4 dot-separated integers, each 0..65535; otherwise returns null and an error message.</summary>
    AppVersion? Parse(string text, out string? error);

    /// <summary>The first three parts, e.g. <c>1.0.0</c> (macOS <c>CFBundleShortVersionString</c>).</summary>
    string ShortVersion(AppVersion version);

    /// <summary>Every part, e.g. <c>1.0.0.12</c> (macOS <c>CFBundleVersion</c>).</summary>
    string FullVersion(AppVersion version);

    /// <summary>Padded to four parts, e.g. <c>2.1.0.0</c> (Windows file and product version).</summary>
    string FourPartVersion(AppVersion version);
}
