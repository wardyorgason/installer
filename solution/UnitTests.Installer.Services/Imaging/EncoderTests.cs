using System.Buffers.Binary;
using System.Text;
using Installer.Services.Imaging;

namespace UnitTests.Installer.Services.Imaging;

public class EncoderTests
{
    private static byte[] FakePng(int marker, int length) => Enumerable.Repeat((byte)marker, length).ToArray();

    [Test]
    public void Ico_entries_have_type_size_and_offset()
    {
        var images = new List<(int, byte[])> { (16, FakePng(1, 10)), (48, FakePng(2, 20)), (256, FakePng(3, 30)) };

        var ico = new IcoEncoderService().Encode(images);

        Assert.Multiple(() =>
        {
            Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(0)), Is.Zero);
            Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(2)), Is.EqualTo(1));
            Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4)), Is.EqualTo(3));
        });
        var offset = 6 + (3 * 16);
        for (var i = 0; i < images.Count; i++)
        {
            var entry = ico.AsSpan(6 + (i * 16), 16).ToArray();
            var (size, png) = images[i];
            var entryOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(entry.AsSpan(12));
            Assert.Multiple(() =>
            {
                Assert.That(entry[0] == 0 ? 256 : entry[0], Is.EqualTo(size));
                Assert.That(entry[1] == 0 ? 256 : entry[1], Is.EqualTo(size));
                Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(entry.AsSpan(6)), Is.EqualTo(32));
                Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(entry.AsSpan(8)), Is.EqualTo(png.Length));
                Assert.That(entryOffset, Is.EqualTo(offset));
                Assert.That(ico.AsSpan(entryOffset, png.Length).ToArray(), Is.EqualTo(png));
            });
            offset += png.Length;
        }

        Assert.That(ico, Has.Length.EqualTo(offset));
    }

    [Test]
    public void Ico_rejects_sizes_over_256()
    {
        Assert.That(() => new IcoEncoderService().Encode([(512, FakePng(1, 1))]), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Icns_entries_have_type_length_and_data()
    {
        var images = new List<(string, byte[])> { ("ic07", FakePng(1, 10)), ("ic10", FakePng(2, 25)) };

        var icns = new IcnsEncoderService().Encode(images);

        Assert.Multiple(() =>
        {
            Assert.That(Encoding.ASCII.GetString(icns, 0, 4), Is.EqualTo("icns"));
            Assert.That(BinaryPrimitives.ReadUInt32BigEndian(icns.AsSpan(4)), Is.EqualTo(icns.Length));
            Assert.That(Encoding.ASCII.GetString(icns, 8, 4), Is.EqualTo("ic07"));
            Assert.That(BinaryPrimitives.ReadUInt32BigEndian(icns.AsSpan(12)), Is.EqualTo(18));
            Assert.That(icns.AsSpan(16, 10).ToArray(), Is.EqualTo(images[0].Item2));
            Assert.That(Encoding.ASCII.GetString(icns, 26, 4), Is.EqualTo("ic10"));
            Assert.That(BinaryPrimitives.ReadUInt32BigEndian(icns.AsSpan(30)), Is.EqualTo(33));
            Assert.That(icns, Has.Length.EqualTo(8 + 18 + 33));
        });
    }
}
