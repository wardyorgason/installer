using System.Buffers.Binary;

namespace UnitTests.Installer.Services.TestSupport;

public static class Png
{
    /// <summary>The first 24 bytes of a PNG: signature plus the IHDR chunk's length, type, width and height.</summary>
    public static byte[] Header(int width, int height)
    {
        var bytes = new byte[24];
        new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }
}
