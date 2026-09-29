using Installer.Dao.Imaging;
using Installer.Services.Imaging;

namespace UnitTests.Installer.Services.Imaging;

public class IconServiceTests
{
    private Mock<IImageDao> _images = null!;
    private Mock<IIcoEncoderService> _ico = null!;
    private Mock<IIcnsEncoderService> _icns = null!;
    private IconService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _images = new Mock<IImageDao>();
        _images.Setup(i => i.RenderPng("/icon.png", It.IsAny<int>())).Returns((string _, int size) => [(byte)(size % 251)]);
        _images.Setup(i => i.GetSize("/icon.png")).Returns((1024, 1024));
        _ico = new Mock<IIcoEncoderService>();
        _icns = new Mock<IIcnsEncoderService>();
        _service = new IconService(_images.Object, _ico.Object, _icns.Object);
    }

    [Test]
    public void Ico_uses_the_windows_sizes()
    {
        IReadOnlyList<(int Size, byte[] Png)>? encoded = null;
        _ico.Setup(i => i.Encode(It.IsAny<IReadOnlyList<(int, byte[])>>())).Callback<IReadOnlyList<(int, byte[])>>(e => encoded = e).Returns([]);

        _service.CreateIco("/icon.png");

        Assert.That(encoded!.Select(e => e.Size), Is.EqualTo(new[] { 16, 24, 32, 48, 64, 256 }));
    }

    [Test]
    public void Icns_uses_every_type_and_renders_each_size_once()
    {
        IReadOnlyList<(string Type, byte[] Png)>? encoded = null;
        _icns.Setup(i => i.Encode(It.IsAny<IReadOnlyList<(string, byte[])>>())).Callback<IReadOnlyList<(string, byte[])>>(e => encoded = e).Returns([]);

        _service.CreateIcns("/icon.png");

        Assert.That(encoded!.Select(e => e.Type), Is.EqualTo(new[] { "ic11", "ic12", "ic07", "ic13", "ic08", "ic14", "ic09", "ic10" }));
        _images.Verify(i => i.RenderPng("/icon.png", 256), Times.Once);
        _images.Verify(i => i.RenderPng("/icon.png", 512), Times.Once);
    }

    [Test]
    public void Icns_leaves_out_sizes_larger_than_the_source()
    {
        _images.Setup(i => i.GetSize("/icon.png")).Returns((512, 512));
        IReadOnlyList<(string Type, byte[] Png)>? encoded = null;
        _icns.Setup(i => i.Encode(It.IsAny<IReadOnlyList<(string, byte[])>>())).Callback<IReadOnlyList<(string, byte[])>>(e => encoded = e).Returns([]);

        _service.CreateIcns("/icon.png");

        Assert.That(encoded!.Select(e => e.Type), Does.Not.Contain("ic10"));
        _images.Verify(i => i.RenderPng("/icon.png", 1024), Times.Never);
    }

    [Test]
    public void Png_is_resized_to_the_requested_size()
    {
        _service.CreatePng("/icon.png", 256);

        _images.Verify(i => i.RenderPng("/icon.png", 256));
    }
}
