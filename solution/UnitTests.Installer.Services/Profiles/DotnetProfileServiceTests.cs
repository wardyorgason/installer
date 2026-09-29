using Installer.Dao.Payload;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Services.Profiles;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.Profiles;

public class DotnetProfileServiceTests
{
    private Mock<IPayloadDao> _payloads = null!;
    private DotnetProfileService _profile = null!;

    [SetUp]
    public void SetUp()
    {
        _payloads = new Mock<IPayloadDao>();
        _profile = new DotnetProfileService(_payloads.Object);
    }

    private BuildFailedException Fails(string fixture, TargetOs os, TargetArch arch, string? executable = null) =>
        Assert.Throws<BuildFailedException>(() =>
            _profile.Analyze(Fixtures.App(executable: executable), Fixtures.Target(os, arch), Fixtures.Payload(fixture, _payloads)))!;

    [Test]
    public void Standard_publish_output_needs_no_executable_name()
    {
        var analysis = _profile.Analyze(Fixtures.App(), Fixtures.Target(TargetOs.MacOS, TargetArch.Arm64), Fixtures.Payload("dotnet-fd-osx-arm64", _payloads));

        Assert.That(analysis.MainExecutable, Is.EqualTo("Samples.DotnetApp"));
    }

    [Test]
    public void Windows_apphost_has_exe_suffix()
    {
        var analysis = _profile.Analyze(Fixtures.App(), Fixtures.Target(TargetOs.Windows, TargetArch.X64), Fixtures.Payload("dotnet-fd-win-x64", _payloads));

        Assert.That(analysis.MainExecutable, Is.EqualTo("Samples.DotnetApp.exe"));
    }

    [Test]
    public void Single_file_or_native_aot_output()
    {
        var ex = Fails("dotnet-singlefile", TargetOs.Linux, TargetArch.X64);

        Assert.Multiple(() =>
        {
            Assert.That(ex.Problem.Code, Is.EqualTo(ErrorCodes.ProfileNoRuntimeConfig));
            Assert.That(ex.Message, Does.Contain("Single-file and NativeAOT").And.Contain("generic profile"));
        });
    }

    [Test]
    public void Several_apps_in_one_payload()
    {
        var ex = Fails("dotnet-two-apps", TargetOs.Linux, TargetArch.X64);

        Assert.Multiple(() =>
        {
            Assert.That(ex.Problem.Code, Is.EqualTo(ErrorCodes.ProfileAmbiguousApp));
            Assert.That(ex.Message, Does.Contain("App, Helper").And.Contain("executable"));
        });
    }

    [Test]
    public void Executable_picks_one_of_several_apps()
    {
        var analysis = _profile.Analyze(Fixtures.App(executable: "Helper"), Fixtures.Target(TargetOs.Linux, TargetArch.X64), Fixtures.Payload("dotnet-two-apps", _payloads));

        Assert.That(analysis.MainExecutable, Is.EqualTo("Helper"));
    }

    [Test]
    public void Executable_without_a_runtimeconfig()
    {
        var ex = Fails("dotnet-two-apps", TargetOs.Linux, TargetArch.X64, executable: "Missing");

        Assert.That(ex.Problem.Code, Is.EqualTo(ErrorCodes.ProfileNoRuntimeConfig));
    }

    [Test]
    public void Published_without_an_apphost()
    {
        var ex = Fails("dotnet-no-apphost", TargetOs.Windows, TargetArch.X64);

        Assert.Multiple(() =>
        {
            Assert.That(ex.Problem.Code, Is.EqualTo(ErrorCodes.ProfileNoAppHost));
            Assert.That(ex.Message, Does.Contain("Samples.DotnetApp.exe").And.Contain("apphost"));
        });
    }

    [Test]
    public void Framework_dependent_payload_warns_about_the_runtime()
    {
        var analysis = _profile.Analyze(Fixtures.App(), Fixtures.Target(TargetOs.MacOS, TargetArch.Arm64), Fixtures.Payload("dotnet-fd-osx-arm64", _payloads));

        var warning = analysis.Warnings.Single();
        Assert.Multiple(() =>
        {
            Assert.That(warning.Code, Is.EqualTo(ErrorCodes.DotnetRuntimeRequired));
            Assert.That(warning.Severity, Is.EqualTo(Severity.Warning));
            Assert.That(warning.Message, Does.Contain("Microsoft.NETCore.App 10.0"));
        });
    }

    [Test]
    public void Every_required_framework_is_named()
    {
        var analysis = _profile.Analyze(Fixtures.App(), Fixtures.Target(TargetOs.Linux, TargetArch.X64), Fixtures.Payload("dotnet-fd-aspnet-linux-x64", _payloads));

        Assert.That(analysis.Warnings.Select(w => w.Message), Has.Some.Contain("Microsoft.NETCore.App 10.0").And.Some.Contain("Microsoft.AspNetCore.App 10.0"));
    }

    [Test]
    public void Self_contained_payload_has_no_runtime_warning()
    {
        var analysis = _profile.Analyze(Fixtures.App(), Fixtures.Target(TargetOs.Linux, TargetArch.X64), Fixtures.Payload("dotnet-sc-linux-x64", _payloads));

        Assert.That(analysis.Warnings, Is.Empty);
    }

    [TestCase(TargetOs.MacOS, TargetArch.X64)]
    [TestCase(TargetOs.Linux, TargetArch.Arm64)]
    public void Mismatched_runtime_identifier(TargetOs os, TargetArch arch)
    {
        var ex = Fails("dotnet-fd-osx-arm64", os, arch);

        Assert.Multiple(() =>
        {
            Assert.That(ex.Problem.Code, Is.EqualTo(ErrorCodes.ProfileRidMismatch));
            Assert.That(ex.Message, Does.Contain("osx-arm64"));
        });
    }

    [Test]
    public void Windows_payload_given_to_a_mac_target_is_named_by_its_runtime_identifier()
    {
        var payload = Fixtures.Payload("dotnet-fd-win-x64", _payloads);
        var app = Fixtures.App(executable: "Samples.DotnetApp");

        // A mac target looks for the apphost without .exe; make it exist so the runtime identifier check is reached.
        var withMacApphost = payload with { Files = [.. payload.Files, "Samples.DotnetApp"] };
        var ex = Assert.Throws<BuildFailedException>(() => _profile.Analyze(app, Fixtures.Target(TargetOs.MacOS, TargetArch.Arm64), withMacApphost));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Problem.Code, Is.EqualTo(ErrorCodes.ProfileRidMismatch));
            Assert.That(ex.Message, Does.Contain("win-x64").And.Contain("MacOS Arm64"));
        });
    }

    [Test]
    public void Portable_publish_is_accepted()
    {
        Assert.That(
            () => _profile.Analyze(Fixtures.App(), Fixtures.Target(TargetOs.Linux, TargetArch.Arm64), Fixtures.Payload("dotnet-portable", _payloads)),
            Throws.Nothing);
    }

    [Test]
    public void Supplies_the_dotnet_entitlements()
    {
        var analysis = _profile.Analyze(Fixtures.App(), Fixtures.Target(TargetOs.MacOS, TargetArch.Arm64), Fixtures.Payload("dotnet-fd-osx-arm64", _payloads));

        Assert.That(analysis.DefaultEntitlements, Is.EqualTo(new Dictionary<string, bool>
        {
            ["com.apple.security.cs.allow-jit"] = true,
            ["com.apple.security.cs.allow-unsigned-executable-memory"] = true,
            ["com.apple.security.cs.disable-library-validation"] = true,
        }));
    }
}
