using Installer.Dao.Host;
using Installer.Dao.Tools;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Services.Build;
using Installer.Services.Formats;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Installer.Services.Build;

public class PreflightServiceTests
{
    private readonly Dictionary<TargetOs, Mock<IPackageFormatService>> _formats = [];
    private Mock<IEnvironmentDao> _environment = null!;
    private Mock<IToolDao> _tools = null!;
    private PreflightService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _environment = new Mock<IEnvironmentDao>();
        _environment.Setup(e => e.GetHost()).Returns(new HostInfo(TargetOs.MacOS, TargetArch.Arm64, "/tmp"));
        _tools = new Mock<IToolDao>();
        _tools.Setup(t => t.FindTool(It.IsAny<string>())).Returns((string tool) => "/usr/bin/" + tool);
        _tools.Setup(t => t.DescribeMissing(It.IsAny<string>())).Returns((string tool) => $"{tool} was not found.");
        _formats[TargetOs.Windows] = Format(TargetOs.Windows, null, "makensis");
        _formats[TargetOs.MacOS] = Format(TargetOs.MacOS, TargetOs.MacOS, "codesign");
        _formats[TargetOs.Linux] = Format(TargetOs.Linux, null, "docker");
        var resolver = new Mock<IPackageFormatResolver>();
        resolver.Setup(r => r.Find(It.IsAny<TargetOs>())).Returns((TargetOs os) => _formats[os].Object);
        _service = new PreflightService(resolver.Object, _environment.Object, _tools.Object, NullLogger<PreflightService>.Instance);
    }

    private static Mock<IPackageFormatService> Format(TargetOs os, TargetOs? host, string tool)
    {
        var format = new Mock<IPackageFormatService>();
        format.Setup(f => f.Os).Returns(os);
        format.Setup(f => f.RequiredHost).Returns(host);
        format.Setup(f => f.RequiredTools).Returns([tool]);
        format.Setup(f => f.PreflightAsync(It.IsAny<TargetSpec>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        return format;
    }

    private static TargetSpec Target(string name, TargetOs os) => new(name, os, TargetArch.X64, "/p", PayloadKind.Directory, null, null, null);

    [Test]
    public async Task Missing_tool_fails_only_its_target()
    {
        _tools.Setup(t => t.FindTool("makensis")).Returns((string?)null);

        var results = await _service.CheckAsync([Target("windows-x64", TargetOs.Windows), Target("macos-arm64", TargetOs.MacOS)], CancellationToken.None);

        Assert.That(results.Keys, Is.EqualTo(new[] { "windows-x64" }));
        Assert.Multiple(() =>
        {
            Assert.That(results["windows-x64"].Single().Code, Is.EqualTo(ErrorCodes.ToolMissing));
            Assert.That(results["windows-x64"].Single().Message, Does.Contain("makensis"));
        });
        _formats[TargetOs.Windows].Verify(f => f.PreflightAsync(It.IsAny<TargetSpec>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Missing_payload_fails_only_its_target_before_other_checks()
    {
        var missing = Target("linux-x64", TargetOs.Linux) with { PayloadKind = null, PayloadPath = "/dist/linux-x64" };

        var results = await _service.CheckAsync([Target("windows-x64", TargetOs.Windows), missing], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(results.Keys, Is.EqualTo(new[] { "linux-x64" }));
            Assert.That(results["linux-x64"].Single().Code, Is.EqualTo(ErrorCodes.PayloadNotFound));
            Assert.That(results["linux-x64"].Single().Message, Does.Contain("linux-x64").And.Contain("/dist/linux-x64"));
        });
        _formats[TargetOs.Linux].Verify(f => f.PreflightAsync(It.IsAny<TargetSpec>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Mac_target_on_a_linux_host()
    {
        _environment.Setup(e => e.GetHost()).Returns(new HostInfo(TargetOs.Linux, TargetArch.X64, "/tmp"));

        var results = await _service.CheckAsync([Target("macos-arm64", TargetOs.MacOS)], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(results["macos-arm64"].Single().Code, Is.EqualTo(ErrorCodes.HostUnsupported));
            Assert.That(results["macos-arm64"].Single().Message, Does.Contain("macOS packages require a macOS host"));
        });
    }

    [Test]
    public async Task Format_checks_run_for_every_target_and_failures_are_kept()
    {
        _formats[TargetOs.Linux]
            .Setup(f => f.PreflightAsync(It.IsAny<TargetSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BuildFailedException(ErrorCodes.DockerUnreachable, "Docker engine not reachable."));
        _formats[TargetOs.MacOS]
            .Setup(f => f.PreflightAsync(It.IsAny<TargetSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Problem.Error(ErrorCodes.IdentityMissing, "identity missing")]);

        var results = await _service.CheckAsync(
            [Target("windows-x64", TargetOs.Windows), Target("macos-arm64", TargetOs.MacOS), Target("linux-x64", TargetOs.Linux)],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(results.ContainsKey("windows-x64"), Is.False);
            Assert.That(results["macos-arm64"].Single().Code, Is.EqualTo(ErrorCodes.IdentityMissing));
            Assert.That(results["linux-x64"].Single().Code, Is.EqualTo(ErrorCodes.DockerUnreachable));
        });
    }

    [Test]
    public async Task Unregistered_format_is_a_problem_not_a_crash()
    {
        var resolver = new Mock<IPackageFormatResolver>();
        var service = new PreflightService(resolver.Object, _environment.Object, _tools.Object, NullLogger<PreflightService>.Instance);

        var results = await service.CheckAsync([Target("linux-x64", TargetOs.Linux)], CancellationToken.None);

        Assert.That(results["linux-x64"].Single().Code, Is.EqualTo(ErrorCodes.Unexpected));
    }
}
