namespace Installer.Dao.Imaging;

public interface IImageDao
{
    (int Width, int Height) GetSize(string pngPath);

    /// <summary>The PNG at <paramref name="pngPath"/> resized to <paramref name="size"/>×<paramref name="size"/>, as PNG bytes.</summary>
    byte[] RenderPng(string pngPath, int size);
}
