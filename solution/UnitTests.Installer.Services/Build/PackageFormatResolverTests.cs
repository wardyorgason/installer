using Installer.Dtos.Manifest;
using Installer.Services.Formats;
using Installer.Services.Profiles;

namespace UnitTests.Installer.Services.Build;

public class ResolverTests
{
    [Test]
    public void Format_is_selected_by_target_os()
    {
        var linux = new Mock<IPackageFormatService>();
        linux.Setup(f => f.Os).Returns(TargetOs.Linux);
        var windows = new Mock<IPackageFormatService>();
        windows.Setup(f => f.Os).Returns(TargetOs.Windows);

        var resolver = new PackageFormatResolver([windows.Object, linux.Object]);

        Assert.Multiple(() =>
        {
            Assert.That(resolver.Find(TargetOs.Linux), Is.SameAs(linux.Object));
            Assert.That(resolver.Find(TargetOs.MacOS), Is.Null);
        });
    }

    [Test]
    public void Profile_is_selected_by_name()
    {
        var dotnet = new Mock<IRuntimeProfileService>();
        dotnet.Setup(p => p.Name).Returns("dotnet");

        var resolver = new RuntimeProfileResolver([dotnet.Object]);

        Assert.Multiple(() =>
        {
            Assert.That(resolver.Find("dotnet"), Is.SameAs(dotnet.Object));
            Assert.That(resolver.Find("node"), Is.Null);
            Assert.That(resolver.Names, Is.EqualTo(new[] { "dotnet" }));
        });
    }
}
