using Installer.Dao.Linux;
using Installer.Dao.Tools;
using Installer.Dao.Wrappers;
using Installer.Dtos.Tools;

namespace UnitTests.Installer.Dao.Linux;

public class DockerDaoTests
{
    private readonly List<ToolCommand> _commands = [];
    private Mock<IToolDao> _tools = null!;
    private DockerDao _dao = null!;
    private int _exitCode;
    private string _stdout = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _commands.Clear();
        _exitCode = 0;
        _stdout = string.Empty;
        _tools = new Mock<IToolDao>();
        _tools.Setup(t => t.RunAsync(It.IsAny<ToolCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ToolCommand, CancellationToken>((c, _) => _commands.Add(c))
            .ReturnsAsync(() => new ProcessResult(_exitCode, _stdout, string.Empty));
        _dao = new DockerDao(_tools.Object);
    }

    private string[] Last => ["docker", .. _commands[^1].Arguments];

    [TestCase(0, true)]
    [TestCase(1, false)]
    public async Task Engine_reachability(int exit, bool reachable)
    {
        _exitCode = exit;

        Assert.That(await _dao.IsEngineReachableAsync(CancellationToken.None), Is.EqualTo(reachable));
        Assert.That(_commands[0].AllowFailure, Is.True);
    }

    [TestCase(0, true)]
    [TestCase(1, false)]
    public async Task Image_missing_detection(int exit, bool exists)
    {
        _exitCode = exit;

        Assert.That(await _dao.ImageExistsAsync("installer-appimage:abc", CancellationToken.None), Is.EqualTo(exists));
        Assert.That(Last, Is.EqualTo(new[] { "docker", "image", "inspect", "--format", "{{.Id}}", "installer-appimage:abc" }));
    }

    [Test]
    public async Task Build_reads_the_dockerfile_from_stdin()
    {
        await _dao.BuildImageAsync("installer-appimage:abc", "FROM scratch", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(Last, Is.EqualTo(new[] { "docker", "build", "-t", "installer-appimage:abc", "-" }));
            Assert.That(_commands[0].StandardInput, Is.EqualTo("FROM scratch"));
        });
    }

    [Test]
    public async Task Create_passes_environment_then_image_then_command_and_returns_the_id()
    {
        _stdout = "abc123\n";

        var id = await _dao.CreateContainerAsync("img", ["tool", "--flag"], new Dictionary<string, string> { ["ARCH"] = "x86_64" }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(id, Is.EqualTo("abc123"));
            Assert.That(Last, Is.EqualTo(new[] { "docker", "create", "-e", "ARCH=x86_64", "img", "tool", "--flag" }));
        });
    }

    [Test]
    public async Task Copy_start_and_remove()
    {
        await _dao.CopyToContainerAsync("c1", "/w/AppDir", "/work/AppDir", CancellationToken.None);
        await _dao.StartAsync("c1", CancellationToken.None);
        await _dao.CopyFromContainerAsync("c1", "/work/out.AppImage", "/w/out.AppImage", CancellationToken.None);
        await _dao.RemoveContainerAsync("c1", CancellationToken.None);

        Assert.That(_commands.Select(c => string.Join(' ', c.Arguments)), Is.EqualTo(new[]
        {
            "cp /w/AppDir c1:/work/AppDir",
            "start -a c1",
            "cp c1:/work/out.AppImage /w/out.AppImage",
            "rm -f c1",
        }));
        Assert.That(_commands[^1].AllowFailure, Is.True);
    }

    [Test]
    public void Dockerfile_is_embedded_and_pinned()
    {
        var resources = new EmbeddedResourceWrapper();

        var dockerfile = new ContainerDefinitionDao(resources).GetAppImageDockerfile();

        Assert.Multiple(() =>
        {
            Assert.That(dockerfile, Does.Match(@"FROM debian:bookworm-slim@sha256:[0-9a-f]{64}"));
            Assert.That(dockerfile, Does.Contain("runtime-x86_64").And.Contain("runtime-aarch64").And.Contain("sha256sum -c -"));
        });
    }
}
