using Installer.Dao.Host;
using Installer.Dao.Workspace;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;
using Installer.Services.Build;
using Installer.Services.Formats;
using Installer.Services.Manifest;
using Installer.Services.Payload;
using Installer.Services.Profiles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.Build;

public class BuildServiceTests
{
    private readonly List<string> _calls = [];
    private readonly Dictionary<string, IReadOnlyList<Problem>> _preflightProblems = [];
    private Mock<IManifestService> _manifests = null!;
    private Mock<IPreflightService> _preflight = null!;
    private Mock<IPackageFormatService> _format = null!;
    private Mock<IRuntimeProfileService> _profile = null!;
    private Mock<IPayloadService> _payloads = null!;
    private Mock<IBinaryInspectionService> _binaries = null!;
    private Mock<IWorkspaceDao> _workspace = null!;
    private BuildService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _calls.Clear();
        _preflightProblems.Clear();
        _manifests = new Mock<IManifestService>();
        Manifest("windows-x64", "macos-arm64", "linux-x64");

        _preflight = new Mock<IPreflightService>();
        _preflight.Setup(p => p.CheckAsync(It.IsAny<IReadOnlyList<TargetSpec>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<TargetSpec>, CancellationToken>((targets, _) => _calls.Add("preflight " + string.Join(",", targets.Select(t => t.Name))))
            .ReturnsAsync(() => _preflightProblems);

        _format = new Mock<IPackageFormatService>();
        _format.Setup(f => f.BuildAsync(It.IsAny<TargetContext>(), It.IsAny<CancellationToken>()))
            .Callback<TargetContext, CancellationToken>((c, _) => _calls.Add("build " + c.Target.Name))
            .ReturnsAsync((TargetContext c, CancellationToken _) => [new ProducedFile(ArtifactKind.Zip, Path.Combine(c.WorkDir, c.ArtifactBaseName + ".zip"))]);
        var formats = new Mock<IPackageFormatResolver>();
        formats.Setup(f => f.Find(It.IsAny<TargetOs>())).Returns(_format.Object);

        _profile = new Mock<IRuntimeProfileService>();
        _profile.Setup(p => p.Analyze(It.IsAny<AppInfo>(), It.IsAny<TargetSpec>(), It.IsAny<PreparedPayload>()))
            .Callback<AppInfo, TargetSpec, PreparedPayload>((_, t, _) => _calls.Add("analyze " + t.Name))
            .Returns(new ProfileAnalysis("App", [Problem.Warning(ErrorCodes.DotnetRuntimeRequired, "needs runtime")], new Dictionary<string, bool>()));
        var profiles = new Mock<IRuntimeProfileResolver>();
        profiles.Setup(p => p.Find("dotnet")).Returns(_profile.Object);

        _payloads = new Mock<IPayloadService>();
        _payloads.Setup(p => p.Prepare(It.IsAny<TargetSpec>(), It.IsAny<string>()))
            .Callback<TargetSpec, string>((t, _) => _calls.Add("prepare " + t.Name))
            .Returns((TargetSpec _, string dest) => new PreparedPayload(dest, ["App"]));
        _binaries = new Mock<IBinaryInspectionService>();
        _binaries.Setup(b => b.CheckMainExecutable(It.IsAny<PreparedPayload>(), "App", It.IsAny<TargetSpec>()))
            .Callback<PreparedPayload, string, TargetSpec>((_, _, t) => _calls.Add("check " + t.Name));

        _workspace = new Mock<IWorkspaceDao>();
        _workspace.Setup(w => w.Publish(It.IsAny<string>(), It.IsAny<string>())).Returns(new FileFingerprint(42, "abc"));
        _workspace.Setup(w => w.ListTree(It.IsAny<string>())).Returns([]);
        var environment = new Mock<IEnvironmentDao>();
        environment.Setup(e => e.GetHost()).Returns(new HostInfo(TargetOs.MacOS, TargetArch.Arm64, "/tmp"));

        _service = new BuildService(
            _manifests.Object, _preflight.Object, formats.Object, profiles.Object, _payloads.Object, _binaries.Object,
            _workspace.Object, environment.Object, new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<BuildService>.Instance);
    }

    private void Manifest(params string[] names)
    {
        var targets = names.Select(name => name switch
        {
            var n when n.StartsWith("windows", StringComparison.Ordinal) => Fixtures.Target(TargetOs.Windows, TargetArch.X64, n),
            var n when n.StartsWith("macos", StringComparison.Ordinal) => Fixtures.Target(TargetOs.MacOS, TargetArch.Arm64, n),
            var n => Fixtures.Target(TargetOs.Linux, TargetArch.X64, n),
        }).ToList();
        var app = Fixtures.App() with { Name = "ScreenRec", DisplayVersion = "1.0.0-b12" };
        _manifests.Setup(m => m.Load("installer.json")).Returns(new ManifestLoadResult(new PackageManifest("/repo/installer.json", app, targets), []));
    }

    private Task<BuildResult> Run(string? output = null, params string[] selected) =>
        _service.RunAsync(new BuildRequest("installer.json", output, selected), CancellationToken.None);

    [Test]
    public async Task Invalid_manifest_builds_nothing()
    {
        _manifests.Setup(m => m.Load("installer.json")).Returns(new ManifestLoadResult(null, [Problem.Error(ErrorCodes.ManifestUnknownProperty, "Unknown property $.x.")]));

        var result = await Run();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Single().Code, Is.EqualTo(ErrorCodes.ManifestUnknownProperty));
            Assert.That(result.Targets, Is.Empty);
            Assert.That(_calls, Is.Empty);
        });
    }

    [Test]
    public async Task Unknown_target_name_builds_nothing_and_lists_valid_names()
    {
        var result = await Run(null, "windows-x64", "freebsd-x64");

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors.Single().Code, Is.EqualTo(ErrorCodes.CliUnknownTarget));
            Assert.That(result.Errors.Single().Message, Does.Contain("freebsd-x64").And.Contain("windows-x64, macos-arm64, linux-x64"));
            Assert.That(result.Targets, Is.Empty);
            Assert.That(_calls, Is.Empty);
        });
    }

    [Test]
    public async Task Builds_every_target_after_preflighting_all_of_them()
    {
        var result = await Run();

        Assert.That(_calls, Is.EqualTo(new[]
        {
            "preflight windows-x64,macos-arm64,linux-x64",
            "prepare windows-x64", "analyze windows-x64", "check windows-x64", "build windows-x64",
            "prepare macos-arm64", "analyze macos-arm64", "check macos-arm64", "build macos-arm64",
            "prepare linux-x64", "analyze linux-x64", "check linux-x64", "build linux-x64",
        }));
        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task Selected_targets_only()
    {
        var result = await Run(null, "windows-x64", "linux-x64");

        Assert.Multiple(() =>
        {
            Assert.That(result.Targets.Select(t => t.Status), Is.EqualTo(new[] { TargetStatus.Succeeded, TargetStatus.NotSelected, TargetStatus.Succeeded }));
            Assert.That(_calls, Does.Not.Contain("build macos-arm64"));
            Assert.That(_calls[0], Is.EqualTo("preflight windows-x64,linux-x64"));
            Assert.That(result.Succeeded, Is.True);
        });
    }

    [Test]
    public async Task Preflight_failure_skips_only_that_target()
    {
        _preflightProblems["macos-arm64"] = [Problem.Error(ErrorCodes.IdentityMissing, "identity missing")];

        var result = await Run();

        Assert.Multiple(() =>
        {
            Assert.That(result.Targets[1].Status, Is.EqualTo(TargetStatus.Failed));
            Assert.That(result.Targets[1].Errors.Single().Code, Is.EqualTo(ErrorCodes.IdentityMissing));
            Assert.That(_calls, Does.Not.Contain("prepare macos-arm64"));
            Assert.That(result.Targets[2].Status, Is.EqualTo(TargetStatus.Succeeded));
            Assert.That(result.Succeeded, Is.False);
        });
    }

    [Test]
    public async Task Middle_target_fails_and_keeps_its_work_directory()
    {
        _format.Setup(f => f.BuildAsync(It.Is<TargetContext>(c => c.Target.Name == "macos-arm64"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BuildFailedException(ErrorCodes.SignFailed, "codesign failed"));

        var result = await Run();

        var failed = result.Targets[1];
        Assert.Multiple(() =>
        {
            Assert.That(result.Targets.Select(t => t.Status), Is.EqualTo(new[] { TargetStatus.Succeeded, TargetStatus.Failed, TargetStatus.Succeeded }));
            Assert.That(failed.Errors.Single().Code, Is.EqualTo(ErrorCodes.SignFailed));
            Assert.That(failed.WorkDir, Does.StartWith(Path.Combine("/tmp", "installer", "20260928-120000-")).And.EndWith("macos-arm64"));
            Assert.That(result.Targets[0].Artifacts, Has.Count.EqualTo(1));
            Assert.That(result.Targets[2].Artifacts, Has.Count.EqualTo(1));
        });
        _workspace.Verify(w => w.DeleteDirectory(failed.WorkDir!), Times.Never);
        _workspace.Verify(w => w.DeleteDirectory(It.Is<string>(d => d.EndsWith("linux-x64", StringComparison.Ordinal))));
    }

    [Test]
    public async Task Header_check_failure_stops_only_that_target()
    {
        _binaries.Setup(b => b.CheckMainExecutable(It.IsAny<PreparedPayload>(), "App", It.Is<TargetSpec>(t => t.Name == "windows-x64")))
            .Throws(new BuildFailedException(ErrorCodes.PayloadPlatformMismatch, "wrong platform"));

        var result = await Run();

        Assert.Multiple(() =>
        {
            Assert.That(result.Targets[0].Errors.Single().Code, Is.EqualTo(ErrorCodes.PayloadPlatformMismatch));
            Assert.That(_calls, Does.Not.Contain("build windows-x64").And.Contain("build macos-arm64"));
        });
    }

    [Test]
    public async Task Unexpected_exception_fails_the_target_with_a_stable_code()
    {
        _payloads.Setup(p => p.Prepare(It.Is<TargetSpec>(t => t.Name == "linux-x64"), It.IsAny<string>())).Throws(new IOException("disk full"));

        var result = await Run();

        Assert.Multiple(() =>
        {
            Assert.That(result.Targets[2].Errors.Single().Code, Is.EqualTo(ErrorCodes.Unexpected));
            Assert.That(result.Targets[2].Errors.Single().Message, Does.Contain("disk full"));
        });
    }

    [Test]
    public async Task Artifacts_are_named_published_and_fingerprinted_in_the_default_output_directory()
    {
        var result = await Run();

        var artifact = result.Targets[1].Artifacts.Single();
        var expected = Path.GetFullPath(Path.Combine("/repo", "dist", "ScreenRec-1.0.0-b12-macos-arm64.zip"));
        Assert.That(artifact, Is.EqualTo(new BuiltArtifact(ArtifactKind.Zip, expected, 42, "abc")));
        _workspace.Verify(w => w.Publish(It.Is<string>(p => p.EndsWith("ScreenRec-1.0.0-b12-macos-arm64.zip", StringComparison.Ordinal)), expected));
    }

    [Test]
    public async Task Output_directory_override()
    {
        var result = await Run("/tmp/out");

        Assert.That(result.Targets[0].Artifacts.Single().Path, Is.EqualTo(Path.GetFullPath("/tmp/out/ScreenRec-1.0.0-b12-windows-x64.zip")));
    }

    [Test]
    public async Task Profile_warnings_are_reported_per_target()
    {
        var result = await Run();

        Assert.That(result.Targets[0].Warnings.Single().Code, Is.EqualTo(ErrorCodes.DotnetRuntimeRequired));
    }

    [Test]
    public async Task Each_target_gets_a_fresh_work_directory()
    {
        await Run();

        _workspace.Verify(w => w.ResetDirectory(It.Is<string>(d => d.StartsWith(Path.Combine("/tmp", "installer", "20260928-120000-"), StringComparison.Ordinal) && d.EndsWith("windows-x64", StringComparison.Ordinal))));
        _payloads.Verify(p => p.Prepare(It.Is<TargetSpec>(t => t.Name == "windows-x64"), It.Is<string>(d => d.EndsWith(Path.Combine("windows-x64", "payload"), StringComparison.Ordinal))));
    }
}
