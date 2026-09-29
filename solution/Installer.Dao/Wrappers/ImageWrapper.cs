using System.Diagnostics.CodeAnalysis;
using SkiaSharp;

namespace Installer.Dao.Wrappers;

[ExcludeFromCodeCoverage(Justification = "Thin wrapper; covered by integration tests.")]
internal sealed class ImageWrapper : IImageWrapper
{
    public (int Width, int Height) GetSize(byte[] png)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(png)) ?? throw new InvalidOperationException("The image could not be decoded.");
        return (codec.Info.Width, codec.Info.Height);
    }

    public byte[] ResizeSquare(byte[] png, int size)
    {
        using var source = SKBitmap.Decode(png) ?? throw new InvalidOperationException("The image could not be decoded.");
        using var resized = source.Resize(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException($"The image could not be resized to {size}×{size}.");
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
