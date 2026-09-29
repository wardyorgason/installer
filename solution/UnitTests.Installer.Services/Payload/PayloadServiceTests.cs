using Installer.Dao.Payload;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;
using Installer.Services.Payload;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.Payload;

public class PayloadServiceTests
{
    private const UnixFileMode Rw = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode Rwx = Rw | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    private Mock<IPayloadDao> _payloads = null!;
    private PayloadService _service = null!;
    private IReadOnlyDictionary<string, string>? _extracted;

    [SetUp]
    public void SetUp()
    {
        _payloads = new Mock<IPayloadDao>();
        _payloads.Setup(p => p.ExtractZipEntries("/in/p.zip", "/w/payload", It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Callback<string, string, IReadOnlyDictionary<string, string>>((_, _, map) => _extracted = map);
        _payloads.Setup(p => p.ReadHeader(It.IsAny<string>(), It.IsAny<int>())).Returns("text"u8.ToArray());
        _payloads.Setup(p => p.GetMode(It.IsAny<string>())).Returns(Rw);
        _payloads.Setup(p => p.ListFiles(It.IsAny<string>())).Returns([]);
        _service = new PayloadService(_payloads.Object, new BinaryInspectionService(_payloads.Object), NullLogger<PayloadService>.Instance);
    }

    private static TargetSpec Zip(TargetOs os = TargetOs.Linux) => new("t", os, TargetArch.X64, "/in/p.zip", PayloadKind.Zip, null, null, null);

    private void ZipEntries(params string[] names) =>
        _payloads.Setup(p => p.ListZipEntries("/in/p.zip")).Returns(names.Select(n => new ZipEntryInfo(n, n.EndsWith('/'))).ToList());

    [Test]
    public void Missing_payload_fails_the_target()
    {
        var ex = Assert.Throws<BuildFailedException>(() => _service.Prepare(Zip() with { PayloadKind = null }, "/w/payload"));

        Assert.That(ex!.Problem.Code, Is.EqualTo(ErrorCodes.PayloadNotFound));
    }

    [Test]
    public void Zip_with_a_single_top_level_folder()
    {
        ZipEntries("ScreenRec/", "ScreenRec/App.dll", "ScreenRec/wwwroot/", "ScreenRec/wwwroot/index.html");

        _service.Prepare(Zip(), "/w/payload");

        Assert.That(_extracted, Is.EqualTo(new Dictionary<string, string>
        {
            ["ScreenRec/App.dll"] = "App.dll",
            ["ScreenRec/wwwroot/index.html"] = "wwwroot/index.html",
        }));
    }

    [Test]
    public void Zip_with_files_at_the_root()
    {
        ZipEntries("App.dll", "wwwroot/index.html");

        _service.Prepare(Zip(), "/w/payload");

        Assert.That(_extracted!.Values, Is.EquivalentTo(new[] { "App.dll", "wwwroot/index.html" }));
    }

    [Test]
    public void Zip_made_on_windows_with_backslashes()
    {
        ZipEntries(@"App\App.dll", @"App\sub\x.txt");

        _service.Prepare(Zip(), "/w/payload");

        Assert.That(_extracted, Is.EqualTo(new Dictionary<string, string> { [@"App\App.dll"] = "App.dll", [@"App\sub\x.txt"] = "sub/x.txt" }));
    }

    [TestCase("../outside.txt")]
    [TestCase("App/../../outside.txt")]
    [TestCase("/etc/passwd")]
    [TestCase(@"C:\evil.dll")]
    public void Path_escape(string entry)
    {
        ZipEntries("App/ok.txt", entry);

        var ex = Assert.Throws<BuildFailedException>(() => _service.Prepare(Zip(), "/w/payload"));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Problem.Code, Is.EqualTo(ErrorCodes.PayloadPathEscape));
            Assert.That(ex.Message, Does.Contain(entry));
        });
        _payloads.Verify(p => p.ExtractZipEntries(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);
    }

    [Test]
    public void Executable_permission_restored_on_native_binaries()
    {
        ZipEntries("ScreenRec", "createdump", "ScreenRec.dll");
        _payloads.Setup(p => p.ListFiles("/w/payload")).Returns(["ScreenRec", "ScreenRec.dll", "createdump"]);
        _payloads.Setup(p => p.ReadHeader(Path.Combine("/w/payload", "ScreenRec"), It.IsAny<int>())).Returns(Fixtures.Header("macho-arm64-apphost"));
        _payloads.Setup(p => p.ReadHeader(Path.Combine("/w/payload", "createdump"), It.IsAny<int>())).Returns(Fixtures.Header("macho-arm64-apphost"));

        var payload = _service.Prepare(Zip(TargetOs.MacOS), "/w/payload");

        _payloads.Verify(p => p.SetMode(Path.Combine("/w/payload", "ScreenRec"), Rwx));
        _payloads.Verify(p => p.SetMode(Path.Combine("/w/payload", "createdump"), Rwx));
        _payloads.Verify(p => p.SetMode(Path.Combine("/w/payload", "ScreenRec.dll"), It.IsAny<UnixFileMode>()), Times.Never);
        Assert.That(payload.Files, Is.EqualTo(new[] { "ScreenRec", "ScreenRec.dll", "createdump" }));
    }

    [Test]
    public void Windows_targets_skip_permissions()
    {
        ZipEntries("App.exe");
        _payloads.Setup(p => p.ListFiles("/w/payload")).Returns(["App.exe"]);

        _service.Prepare(Zip(TargetOs.Windows), "/w/payload");

        _payloads.Verify(p => p.ReadHeader(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        _payloads.Verify(p => p.SetMode(It.IsAny<string>(), It.IsAny<UnixFileMode>()), Times.Never);
    }

    [Test]
    public void Directory_payload_is_copied_and_the_original_left_untouched()
    {
        var target = new TargetSpec("t", TargetOs.Linux, TargetArch.X64, "/in/dir", PayloadKind.Directory, null, null, null);
        _payloads.Setup(p => p.ListFiles("/w/payload")).Returns(["app"]);
        _payloads.Setup(p => p.ReadHeader(Path.Combine("/w/payload", "app"), It.IsAny<int>())).Returns(Fixtures.Header("elf-x64-apphost"));

        _service.Prepare(target, "/w/payload");

        _payloads.Verify(p => p.CopyDirectory("/in/dir", "/w/payload"));
        _payloads.Verify(p => p.SetMode(It.Is<string>(path => path.StartsWith("/in", StringComparison.Ordinal)), It.IsAny<UnixFileMode>()), Times.Never);
        _payloads.Verify(p => p.SetMode(Path.Combine("/w/payload", "app"), Rwx));
    }
}
