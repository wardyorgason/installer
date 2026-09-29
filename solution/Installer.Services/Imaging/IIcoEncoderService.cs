namespace Installer.Services.Imaging;

public interface IIcoEncoderService
{
    /// <summary>A Windows .ico holding each image as a PNG-compressed entry (sizes up to 256).</summary>
    byte[] Encode(IReadOnlyList<(int Size, byte[] Png)> images);
}
