namespace Installer.Services.Imaging;

public interface IIcnsEncoderService
{
    /// <summary>A macOS .icns holding each PNG under its four-character type (e.g. <c>ic10</c> for 1024×1024).</summary>
    byte[] Encode(IReadOnlyList<(string Type, byte[] Png)> images);
}
