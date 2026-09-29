using Installer.Dao.Imaging;
using Installer.Dao.Wrappers;
using SkiaSharp;

namespace UnitTests.Installer.Dao.Imaging;

public class ImageDaoTests
{
    [Test]
    public void Reads_the_file_and_resizes_through_the_wrapper()
    {
        var fileSystem = new Mock<IFileSystemWrapper>();
        fileSystem.Setup(f => f.ReadAllBytes("/icon.png")).Returns([1, 2, 3]);
        var images = new Mock<IImageWrapper>();
        images.Setup(i => i.ResizeSquare(It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 1, 2, 3 })), 256)).Returns([9]);
        images.Setup(i => i.GetSize(It.IsAny<byte[]>())).Returns((1024, 1024));
        var dao = new ImageDao(fileSystem.Object, images.Object);

        Assert.Multiple(() =>
        {
            Assert.That(dao.RenderPng("/icon.png", 256), Is.EqualTo(new byte[] { 9 }));
            Assert.That(dao.GetSize("/icon.png"), Is.EqualTo((1024, 1024)));
        });
    }

    [Test]
    [Category("Integration")]
    public void Skia_resizes_a_1024_png_to_16_and_256()
    {
        using var bitmap = new SKBitmap(1024, 1024);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.OrangeRed);
        }

        using var image = SKImage.FromBitmap(bitmap);
        var png = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        var wrapper = new ImageWrapper();

        var small = wrapper.ResizeSquare(png, 16);
        var large = wrapper.ResizeSquare(png, 256);

        using var decodedSmall = SKBitmap.Decode(small);
        Assert.Multiple(() =>
        {
            Assert.That(wrapper.GetSize(png), Is.EqualTo((1024, 1024)));
            Assert.That(wrapper.GetSize(small), Is.EqualTo((16, 16)));
            Assert.That(wrapper.GetSize(large), Is.EqualTo((256, 256)));
            Assert.That(decodedSmall.GetPixel(8, 8).Red, Is.GreaterThan(200));
        });
    }
}
