using System.Buffers.Binary;
using System.Text.Json.Nodes;

namespace UnitTests.Installer.Cli.Integration;

[Category("Integration")]
public class WindowsInstallerIntegrationTests
{
    [Test]
    public async Task Makensis_builds_a_setup_for_the_dotnet_sample()
    {
        using var host = new InstallerHost();
        if (host.FindTool("makensis") is null)
        {
            Assert.Ignore("makensis was not found; install NSIS 3.08 or later to run this test.");
        }

        var manifest = host.WriteManifest("dotnet", [new JsonObject { ["os"] = "windows", ["arch"] = "x64", ["payload"] = SampleApps.Dotnet("win-x64", selfContained: false) }]);

        var (exit, result) = await host.BuildAsync(manifest);

        Assert.That(exit, Is.Zero, host.Console.OutWriter.ToString());
        var artifact = result.RootElement.GetProperty("targets")[0].GetProperty("artifacts")[0];
        var setup = File.ReadAllBytes(artifact.GetProperty("path").GetString()!);
        Assert.Multiple(() =>
        {
            Assert.That(artifact.GetProperty("kind").GetString(), Is.EqualTo("setup-exe"));
            Assert.That(Path.GetFileName(artifact.GetProperty("path").GetString()), Is.EqualTo("Samples-1.2.3-b4-windows-x64-setup.exe"));
            Assert.That(setup.AsSpan(0, 2).SequenceEqual("MZ"u8), Is.True);
            Assert.That(FileVersion(setup), Is.EqualTo("1.2.3.4"));
        });
    }

    /// <summary>Reads VS_FIXEDFILEINFO's file version from a PE file.</summary>
    private static string FileVersion(byte[] pe)
    {
        var index = pe.AsSpan().IndexOf(new byte[] { 0xBD, 0x04, 0xEF, 0xFE });
        Assert.That(index, Is.GreaterThan(0), "no version resource");
        var ms = BinaryPrimitives.ReadUInt32LittleEndian(pe.AsSpan(index + 8));
        var ls = BinaryPrimitives.ReadUInt32LittleEndian(pe.AsSpan(index + 12));
        return $"{ms >> 16}.{ms & 0xFFFF}.{ls >> 16}.{ls & 0xFFFF}";
    }
}
