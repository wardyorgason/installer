using Installer.Dao.Tools;
using Installer.Dao.Windows;
using Installer.Dtos.Tools;

namespace UnitTests.Installer.Dao.Windows;

public class NsisDaoTests
{
    [TestCase("v3.10\n", 3, 10)]
    [TestCase("v3.08-3+deb12u1\n", 3, 8)]
    [TestCase("3.09", 3, 9)]
    public void Parses_makensis_versions(string output, int major, int minor)
    {
        Assert.That(NsisDao.ParseVersion(output), Is.EqualTo(new Version(major, minor)));
    }

    [Test]
    public void Unparseable_version_is_null() => Assert.That(NsisDao.ParseVersion("NSIS"), Is.Null);

    [Test]
    public async Task Version_runs_makensis_dash_version()
    {
        var tools = new Mock<IToolDao>();
        tools.Setup(t => t.RunAsync(It.Is<ToolCommand>(c => c.Tool == "makensis" && c.Arguments.SequenceEqual(new[] { "-VERSION" })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "v3.10", string.Empty));

        Assert.That(await new NsisDao(tools.Object).GetVersionAsync(CancellationToken.None), Is.EqualTo(new Version(3, 10)));
    }

    [Test]
    public async Task Compile_passes_the_script_in_utf8_from_its_directory()
    {
        var tools = new Mock<IToolDao>();
        tools.Setup(t => t.RunAsync(It.IsAny<ToolCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ProcessResult(0, string.Empty, string.Empty));

        await new NsisDao(tools.Object).CompileAsync("/w/installer.nsi", CancellationToken.None);

        tools.Verify(t => t.RunAsync(
            It.Is<ToolCommand>(c => c.Tool == "makensis"
                && c.Arguments.SequenceEqual(new[] { "-V2", "-INPUTCHARSET", "UTF8", "/w/installer.nsi" })
                && c.WorkingDirectory == "/w"
                && c.Environment!["LANG"] == "en_US.UTF-8"
                && c.Environment["LC_ALL"] == "en_US.UTF-8"),
            It.IsAny<CancellationToken>()));
    }
}
