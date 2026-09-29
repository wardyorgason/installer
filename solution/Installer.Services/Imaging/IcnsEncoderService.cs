using System.Buffers.Binary;
using System.Text;

namespace Installer.Services.Imaging;

internal sealed class IcnsEncoderService : IIcnsEncoderService
{
    private const int HeaderSize = 8;

    public byte[] Encode(IReadOnlyList<(string Type, byte[] Png)> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        var output = new byte[HeaderSize + images.Sum(image => HeaderSize + image.Png.Length)];
        var span = output.AsSpan();
        Encoding.ASCII.GetBytes("icns").CopyTo(span);
        BinaryPrimitives.WriteUInt32BigEndian(span[4..], (uint)output.Length);

        var offset = HeaderSize;
        foreach (var (type, png) in images)
        {
            if (type.Length != 4)
            {
                throw new ArgumentException($"ICNS types are four characters: '{type}'.", nameof(images));
            }

            Encoding.ASCII.GetBytes(type).CopyTo(span[offset..]);
            BinaryPrimitives.WriteUInt32BigEndian(span[(offset + 4)..], (uint)(HeaderSize + png.Length));
            png.CopyTo(span[(offset + HeaderSize)..]);
            offset += HeaderSize + png.Length;
        }

        return output;
    }
}
