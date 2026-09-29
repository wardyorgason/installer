using Installer.Dao.Imaging;

namespace Installer.Services.Imaging;

internal sealed class IconService(IImageDao images, IIcoEncoderService ico, IIcnsEncoderService icns) : IIconService
{
    internal static readonly int[] IcoSizes = [16, 24, 32, 48, 64, 256];

    /// <summary>PNG-capable icns types and their pixel sizes (ic11–ic14 are the @2x variants of 16–256 pt).</summary>
    internal static readonly (string Type, int Size)[] IcnsTypes =
    [
        ("ic11", 32), ("ic12", 64), ("ic07", 128), ("ic13", 256), ("ic08", 256), ("ic14", 512), ("ic09", 512), ("ic10", 1024),
    ];

    public byte[] CreateIco(string pngPath)
    {
        var rendered = new Dictionary<int, byte[]>();
        return ico.Encode(IcoSizes.Select(size => (size, Render(pngPath, size, rendered))).ToList());
    }

    public byte[] CreateIcns(string pngPath)
    {
        var (width, _) = images.GetSize(pngPath);
        var rendered = new Dictionary<int, byte[]>();
        return icns.Encode(IcnsTypes.Where(entry => entry.Size <= width).Select(entry => (entry.Type, Render(pngPath, entry.Size, rendered))).ToList());
    }

    public byte[] CreatePng(string pngPath, int size) => images.RenderPng(pngPath, size);

    private byte[] Render(string pngPath, int size, Dictionary<int, byte[]> rendered)
    {
        if (!rendered.TryGetValue(size, out var png))
        {
            png = images.RenderPng(pngPath, size);
            rendered[size] = png;
        }

        return png;
    }
}
