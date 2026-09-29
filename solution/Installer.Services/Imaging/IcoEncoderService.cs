using System.Buffers.Binary;

namespace Installer.Services.Imaging;

internal sealed class IcoEncoderService : IIcoEncoderService
{
    private const int HeaderSize = 6;
    private const int EntrySize = 16;

    public byte[] Encode(IReadOnlyList<(int Size, byte[] Png)> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        var output = new byte[HeaderSize + (images.Count * EntrySize) + images.Sum(image => image.Png.Length)];
        var span = output.AsSpan();
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], 1); // type: icon
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], (ushort)images.Count);

        var offset = HeaderSize + (images.Count * EntrySize);
        for (var i = 0; i < images.Count; i++)
        {
            var (size, png) = images[i];
            if (size is < 1 or > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(images), size, "ICO entries must be 1 to 256 pixels.");
            }

            var entry = span[(HeaderSize + (i * EntrySize))..];
            entry[0] = (byte)(size == 256 ? 0 : size); // 0 means 256
            entry[1] = (byte)(size == 256 ? 0 : size);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[4..], 1); // planes
            BinaryPrimitives.WriteUInt16LittleEndian(entry[6..], 32); // bits per pixel
            BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], (uint)png.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[12..], (uint)offset);
            png.CopyTo(span[offset..]);
            offset += png.Length;
        }

        return output;
    }
}
