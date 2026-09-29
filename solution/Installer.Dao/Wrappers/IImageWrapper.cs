namespace Installer.Dao.Wrappers;

/// <summary>Wraps SkiaSharp's PNG decoding, resizing and encoding.</summary>
public interface IImageWrapper
{
    (int Width, int Height) GetSize(byte[] png);

    /// <summary>Resizes a PNG to <paramref name="size"/>×<paramref name="size"/> with high-quality resampling and returns PNG bytes.</summary>
    byte[] ResizeSquare(byte[] png, int size);
}
