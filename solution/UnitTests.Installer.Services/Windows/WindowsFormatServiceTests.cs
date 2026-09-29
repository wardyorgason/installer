using System.Text.Json;
using Installer.Dao.Host;
using Installer.Dao.Windows;
using Installer.Dao.Workspace;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;
using Installer.Services.Imaging;
using Installer.Services.Manifest;
using Installer.Services.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.Windows;

public class WindowsFormatServiceTests
{
    private Mock<INsisDao> _nsis = null!;
    private Mock<INsisScriptService> _scripts = null!;
    private Mock<IWindowsSigningService> _signing = null!;
    private Mock<IWorkspaceDao> _workspace = null!;
    private WindowsFormatService _format = null!;
    private NsisScriptRequest? _request;

    [SetUp]
    public void SetUp()
    {
        _request = null;
        _nsis = new Mock<INsisDao>();
        _scripts = new Mock<INsisScriptService>();
        _scripts.Setup(s => s.Generate(It.IsAny<NsisScriptRequest>())).Callback<NsisScriptRequest>(r => _request = r).Returns("script");
        _scripts.Setup(s => s.SignHook(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>())).Returns("hook");
        _signing = new Mock<IWindowsSigningService>();
        var icons = new Mock<IIconService>();
        icons.Setup(i => i.CreateIco("/icon.png")).Returns([1, 2, 3]);
        _workspace = new Mock<IWorkspaceDao>();
        _workspace.Setup(w => w.GetDirectorySize("/w/payload")).Returns(5000);
        var environment = new Mock<IEnvironmentDao>();
        environment.Setup(e => e.GetSelfInvocation()).Returns(["/opt/installer/Installer.Cli"]);
        _format = new WindowsFormatService(_nsis.Object, _scripts.Object, _signing.Object, icons.Object, new VersionService(), _workspace.Object, environment.Object, NullLogger<WindowsFormatService>.Instance);
    }

    private static TargetContext Context(IReadOnlyList<string>? signCommand = null)
    {
        var app = Fixtures.App() with { IconPath = "/icon.png", Version = new AppVersion("2.1.0", [2, 1, 0]), DisplayVersion = "2.1.0" };
        var target = Fixtures.Target(TargetOs.Windows, TargetArch.X64, "windows-x64") with { Windows = new WindowsOptions(signCommand) };
        return new TargetContext(app, target, new PreparedPayload("/w/payload", ["App.exe"]), new ProfileAnalysis("App.exe", [], new Dictionary<string, bool>()), "/w", "/out", "Example-2.1.0-windows-x64");
    }

    [TestCase(3, 8, true)]
    [TestCase(3, 10, true)]
    [TestCase(3, 6, false)]
    public async Task Preflight_requires_makensis_3_08(int major, int minor, bool ok)
    {
        _nsis.Setup(n => n.GetVersionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Version(major, minor));

        var problems = await _format.PreflightAsync(Context().Target, CancellationToken.None);

        Assert.That(problems.Select(p => p.Code), ok ? Is.Empty : Is.EqualTo(new[] { ErrorCodes.ToolVersion }));
    }

    [Test]
    public void Requires_makensis_on_any_host()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_format.RequiredHost, Is.Null);
            Assert.That(_format.RequiredTools, Is.EqualTo(new[] { "makensis" }));
        });
    }

    [Test]
    public async Task Builds_an_unsigned_setup()
    {
        var produced = await _format.BuildAsync(Context(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(produced, Is.EqualTo(new[] { new ProducedFile(ArtifactKind.SetupExe, Path.Combine("/w", "Example-2.1.0-windows-x64-setup.exe")) }));
            Assert.That(_request!.FourPartVersion, Is.EqualTo("2.1.0.0"));
            Assert.That(_request.IconPath, Is.EqualTo(Path.Combine("/w", "app.ico")));
            Assert.That(_request.EstimatedSizeKb, Is.EqualTo(5));
            Assert.That(_request.SignHook, Is.Null);
            Assert.That(_request.OutputFile, Is.EqualTo(produced[0].Path));
        });
        _workspace.Verify(w => w.WriteBytes(Path.Combine("/w", "app.ico"), It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 1, 2, 3 }))));
        _workspace.Verify(w => w.WriteText(Path.Combine("/w", "installer.nsi"), "script"));
        _nsis.Verify(n => n.CompileAsync(Path.Combine("/w", "installer.nsi"), It.IsAny<CancellationToken>()));
        _signing.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Signing_covers_the_main_executable_and_the_nsis_hooks()
    {
        string[] command = ["osslsigncode", "sign", "-in", "{file}"];

        await _format.BuildAsync(Context(command), CancellationToken.None);

        _signing.Verify(s => s.SignFileAsync(Path.Combine("/w/payload", "App.exe"), command, It.IsAny<CancellationToken>()));
        _workspace.Verify(w => w.WriteText(Path.Combine("/w", "sign-command.json"), JsonSerializer.Serialize(command)));
        _scripts.Verify(s => s.SignHook(new[] { "/opt/installer/Installer.Cli" }, Path.Combine("/w", "sign-command.json")));
        Assert.That(_request!.SignHook, Is.EqualTo("hook"));
    }
}
