using Installer.Cli.Commands;
using Installer.Dtos.Build;
using Installer.Services.Windows;

namespace UnitTests.Installer.Cli.Commands;

public class SignFileCommandHandlerTests
{
    [Test]
    public async Task Signs_through_the_service()
    {
        var signing = new Mock<IWindowsSigningService>();
        var console = new StringConsole();

        var exit = await new SignFileCommandHandler(signing.Object, console).HandleAsync("/w/setup.exe", "/w/sign.json", CancellationToken.None);

        Assert.That(exit, Is.Zero);
        signing.Verify(s => s.SignFileFromCommandFileAsync("/w/setup.exe", "/w/sign.json", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Failure_writes_the_error_to_stderr_and_exits_1()
    {
        var signing = new Mock<IWindowsSigningService>();
        signing.Setup(s => s.SignFileFromCommandFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BuildFailedException(ErrorCodes.SignFailed, "certificate expired"));
        var console = new StringConsole();

        var exit = await new SignFileCommandHandler(signing.Object, console).HandleAsync("/w/setup.exe", "/w/sign.json", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(console.ErrorWriter.ToString(), Does.Contain("certificate expired"));
            Assert.That(console.OutWriter.ToString(), Is.Empty);
        });
    }
}
