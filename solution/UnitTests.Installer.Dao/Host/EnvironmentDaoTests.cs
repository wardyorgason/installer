using System.Runtime.InteropServices;
using Installer.Dao.Host;
using Installer.Dao.Wrappers;
using Installer.Dtos.Manifest;

namespace UnitTests.Installer.Dao.Host;

public class EnvironmentDaoTests
{
    [TestCase(true, false, Architecture.Arm64, TargetOs.MacOS, TargetArch.Arm64)]
    [TestCase(false, false, Architecture.X64, TargetOs.Linux, TargetArch.X64)]
    [TestCase(false, true, Architecture.X64, TargetOs.Windows, TargetArch.X64)]
    public void Describes_the_host(bool mac, bool windows, Architecture arch, TargetOs expectedOs, TargetArch expectedArch)
    {
        var environment = new Mock<IEnvironmentWrapper>();
        environment.Setup(e => e.IsMacOS()).Returns(mac);
        environment.Setup(e => e.IsWindows()).Returns(windows);
        environment.Setup(e => e.OSArchitecture).Returns(arch);
        environment.Setup(e => e.TempPath).Returns("/tmp/");

        var host = new EnvironmentDao(environment.Object).GetHost();

        Assert.That(host, Is.EqualTo(new global::Installer.Dtos.Build.HostInfo(expectedOs, expectedArch, "/tmp/")));
    }

    [Test]
    public void Reads_variables()
    {
        var environment = new Mock<IEnvironmentWrapper>();
        environment.Setup(e => e.GetEnvironmentVariable("APP_VERSION")).Returns("1.0.0");

        Assert.That(new EnvironmentDao(environment.Object).GetVariable("APP_VERSION"), Is.EqualTo("1.0.0"));
    }

    [Test]
    public void Self_invocation_through_the_dotnet_host_includes_the_dll()
    {
        var environment = new Mock<IEnvironmentWrapper>();
        environment.Setup(e => e.ProcessPath).Returns("/usr/local/share/dotnet/dotnet");
        environment.Setup(e => e.EntryAssemblyLocation).Returns("/opt/installer/Installer.Cli.dll");

        Assert.That(new EnvironmentDao(environment.Object).GetSelfInvocation(), Is.EqualTo(new[] { "/usr/local/share/dotnet/dotnet", "/opt/installer/Installer.Cli.dll" }));
    }

    [Test]
    public void Self_invocation_through_an_apphost_is_just_the_apphost()
    {
        var environment = new Mock<IEnvironmentWrapper>();
        environment.Setup(e => e.ProcessPath).Returns("/opt/installer/Installer.Cli");
        environment.Setup(e => e.EntryAssemblyLocation).Returns("/opt/installer/Installer.Cli.dll");

        Assert.That(new EnvironmentDao(environment.Object).GetSelfInvocation(), Is.EqualTo(new[] { "/opt/installer/Installer.Cli" }));
    }
}
