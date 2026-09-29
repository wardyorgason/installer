using Installer.Dao.Signing;
using Installer.Dao.Tools;
using Installer.Dtos.Build;
using Installer.Dtos.Tools;

namespace UnitTests.Installer.Dao.Signing;

public class SignCommandDaoTests
{
    [Test]
    public async Task Runs_the_first_item_with_the_rest_as_arguments()
    {
        var tools = new Mock<IToolDao>();
        tools.Setup(t => t.RunAsync(It.IsAny<ToolCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ProcessResult(0, "", ""));

        await new SignCommandDao(tools.Object).RunAsync(["osslsigncode", "sign", "-in", "/w/App.exe"], CancellationToken.None);

        tools.Verify(t => t.RunAsync(
            It.Is<ToolCommand>(c => c.Tool == "osslsigncode" && c.Arguments.SequenceEqual(new[] { "sign", "-in", "/w/App.exe" }) && c.AllowFailure),
            It.IsAny<CancellationToken>()));
    }

    [Test]
    public void Signing_failure_carries_the_tool_output()
    {
        var tools = new Mock<IToolDao>();
        tools.Setup(t => t.RunAsync(It.IsAny<ToolCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ProcessResult(2, "", "Failed to read certificate\n"));

        var ex = Assert.ThrowsAsync<BuildFailedException>(() => new SignCommandDao(tools.Object).RunAsync(["osslsigncode", "sign"], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Problem.Code, Is.EqualTo(ErrorCodes.SignFailed));
            Assert.That(ex.Message, Does.Contain("Failed to read certificate").And.Contain("code 2"));
        });
    }
}
