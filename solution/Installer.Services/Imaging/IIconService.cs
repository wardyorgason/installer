namespace Installer.Services.Imaging;

/// <summary>Produces every platform's icon from the manifest's single PNG.</summary>
public interface IIconService
{
    /// <summary>Windows .ico: 16, 24, 32, 48, 64 and 256 px.</summary>
    byte[] CreateIco(string pngPath);

    /// <summary>macOS .icns: 128 to 1024 px including the @2x sizes; sizes larger than the source are left out.</summary>
    byte[] CreateIcns(string pngPath);

    /// <summary>The PNG resized to <paramref name="size"/>×<paramref name="size"/> (the AppImage icon).</summary>
    byte[] CreatePng(string pngPath, int size);
}
