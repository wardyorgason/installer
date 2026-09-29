using Installer.Dao.Tools;
using Installer.Dao.Wrappers;
using Installer.Dtos.Build;
using Installer.Dtos.Tools;
using Microsoft.Extensions.Logging;
using UnitTests.Installer.Dao.TestSupport;

namespace UnitTests.Installer.Dao.Tools;

public class ToolDaoTests
{
    private Mock<IProcessWrapper> _process = null!;
    private Mock<IFileSystemWrapper> _fileSystem = null!;
    private Mock<IEnvironmentWrapper> _environment = null!;
    private ListLogger<ToolDao> _logger = null!;
    private ToolDao _dao = null!;

    [SetUp]
    public void SetUp()
    {
        _process = new Mock<IProcessWrapper>(MockBehavior.Strict);
        _fileSystem = new Mock<IFileSystemWrapper>();
        _environment = new Mock<IEnvironmentWrapper>();
        _environment.Setup(e => e.GetEnvironmentVariable("PATH")).Returns(string.Join(Path.PathSeparator, "/usr/bin", "/bin"));
        _logger = new ListLogger<ToolDao>();
        _dao = new ToolDao(_process.Object, _fileSystem.Object, _environment.Object, _logger);
    }

    [Test]
    public void Finds_tool_on_path_before_homebrew()
    {
        _fileSystem.Setup(f => f.FileExists("/bin/docker")).Returns(true);
        _fileSystem.Setup(f => f.FileExists("/opt/homebrew/bin/docker")).Returns(true);

        Assert.That(_dao.FindTool("docker"), Is.EqualTo("/bin/docker"));
    }

    [Test]
    public void Finds_homebrew_tool_when_path_lacks_it()
    {
        _fileSystem.Setup(f => f.FileExists("/opt/homebrew/bin/makensis")).Returns(true);

        Assert.That(_dao.FindTool("makensis"), Is.EqualTo("/opt/homebrew/bin/makensis"));
    }

    [Test]
    public void Missing_tool_throws_with_install_hint()
    {
        var ex = Assert.ThrowsAsync<BuildFailedException>(() => _dao.RunAsync(new ToolCommand("makensis", ["-VERSION"]), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Problem.Code, Is.EqualTo(ErrorCodes.ToolMissing));
            Assert.That(ex.Message, Does.Contain("makensis").And.Contain("brew install makensis"));
        });
    }

    [Test]
    public async Task Runs_resolved_tool_with_arguments_and_input()
    {
        _fileSystem.Setup(f => f.FileExists("/usr/bin/docker")).Returns(true);
        _process
            .Setup(p => p.RunAsync(
                It.Is<ProcessRequest>(r => r.FileName == "/usr/bin/docker" && r.Arguments.SequenceEqual(new[] { "build", "-t", "x", "-" }) && r.StandardInput == "FROM scratch"),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "ok", string.Empty));

        var result = await _dao.RunAsync(new ToolCommand("docker", ["build", "-t", "x", "-"], StandardInput: "FROM scratch"), CancellationToken.None);

        Assert.That(result.StandardOutput, Is.EqualTo("ok"));
    }

    [Test]
    public void Non_zero_exit_throws_with_stderr()
    {
        _fileSystem.Setup(f => f.FileExists("/usr/bin/codesign")).Returns(true);
        _process
            .Setup(p => p.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, string.Empty, "errSecInternalComponent\n"));

        var ex = Assert.ThrowsAsync<BuildFailedException>(() => _dao.RunAsync(new ToolCommand("codesign", ["--sign", "X", "a"]), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Problem.Code, Is.EqualTo(ErrorCodes.ToolFailed));
            Assert.That(ex.Message, Is.EqualTo("codesign exited with code 1: errSecInternalComponent"));
        });
    }

    [Test]
    public async Task Allowed_failure_returns_the_result()
    {
        _fileSystem.Setup(f => f.FileExists("/usr/bin/docker")).Returns(true);
        _process
            .Setup(p => p.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, string.Empty, "No such image"));

        var result = await _dao.RunAsync(new ToolCommand("docker", ["image", "inspect", "x"], AllowFailure: true), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
    }

    [Test]
    public async Task Logs_command_line_and_output_lines_at_debug()
    {
        _fileSystem.Setup(f => f.FileExists("/usr/bin/hdiutil")).Returns(true);
        _process
            .Setup(p => p.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, Action<string>?, CancellationToken>((_, onLine, _) => onLine!("created: out.dmg"))
            .ReturnsAsync(new ProcessResult(0, "created: out.dmg", string.Empty));

        await _dao.RunAsync(new ToolCommand("hdiutil", ["create", "-volname", "My App", "out.dmg"]), CancellationToken.None);

        Assert.That(_logger.Entries, Is.EqualTo(new[]
        {
            (LogLevel.Debug, "$ hdiutil create -volname 'My App' out.dmg"),
            (LogLevel.Debug, "  created: out.dmg"),
        }));
    }
}
