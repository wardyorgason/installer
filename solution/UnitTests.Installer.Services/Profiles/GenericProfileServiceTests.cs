using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;
using Installer.Services.Profiles;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.Profiles;

public class GenericProfileServiceTests
{
    private readonly GenericProfileService _profile = new();

    [Test]
    public void Requires_executable() => Assert.That(_profile.RequiresExecutable, Is.True);

    [Test]
    public void Executable_named()
    {
        var analysis = _profile.Analyze(Fixtures.App("generic", "bin/mytool"), Fixtures.Target(TargetOs.Linux, TargetArch.X64), new PreparedPayload("/p", ["bin/mytool", "README"]));

        Assert.Multiple(() =>
        {
            Assert.That(analysis.MainExecutable, Is.EqualTo("bin/mytool"));
            Assert.That(analysis.Warnings, Is.Empty);
            Assert.That(analysis.DefaultEntitlements, Is.Empty);
        });
    }

    [Test]
    public void Executable_not_in_the_payload()
    {
        var ex = Assert.Throws<BuildFailedException>(() =>
            _profile.Analyze(Fixtures.App("generic", "mytool"), Fixtures.Target(TargetOs.Linux, TargetArch.X64), new PreparedPayload("/p", ["other"])));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Problem.Code, Is.EqualTo(ErrorCodes.PayloadExecutableMissing));
            Assert.That(ex.Message, Does.Contain("mytool"));
        });
    }
}
