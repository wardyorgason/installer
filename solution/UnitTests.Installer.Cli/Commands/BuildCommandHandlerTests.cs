using Installer.Cli.Commands;
using Installer.Cli.Output;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Services.Build;

namespace UnitTests.Installer.Cli.Commands;

public class BuildCommandHandlerTests
{
    private static TargetResult Target(TargetStatus status) => new("t", TargetOs.Linux, TargetArch.X64, status, [], [], [], null);

    private static async Task<(int Exit, Mock<IResultWriter> Writer, BuildRequest? Request)> Handle(BuildResult result)
    {
        BuildRequest? request = null;
        var builds = new Mock<IBuildService>();
        builds.Setup(b => b.RunAsync(It.IsAny<BuildRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BuildRequest, CancellationToken>((r, _) => request = r)
            .ReturnsAsync(result);
        var writer = new Mock<IResultWriter>();
        var exit = await new BuildCommandHandler(builds.Object, writer.Object)
            .HandleAsync(new BuildCommandArguments("installer.json", "/out", ["a"]), CancellationToken.None);
        return (exit, writer, request);
    }

    [Test]
    public async Task All_targets_succeed()
    {
        var result = new BuildResult(1, true, [], [Target(TargetStatus.Succeeded), Target(TargetStatus.NotSelected)]);

        var (exit, writer, request) = await Handle(result);

        Assert.Multiple(() =>
        {
            Assert.That(exit, Is.Zero);
            Assert.That(request, Is.EqualTo(new BuildRequest("installer.json", "/out", request!.SelectedTargets)));
            Assert.That(request!.SelectedTargets, Is.EqualTo(new[] { "a" }));
        });
        writer.Verify(w => w.Write(result), Times.Once);
    }

    [Test]
    public async Task One_target_fails()
    {
        var (exit, _, _) = await Handle(new BuildResult(1, false, [], [Target(TargetStatus.Succeeded), Target(TargetStatus.Failed), Target(TargetStatus.Succeeded)]));

        Assert.That(exit, Is.EqualTo(1));
    }

    [TestCase(ErrorCodes.ManifestUnknownProperty)]
    [TestCase(ErrorCodes.ManifestNotFound)]
    [TestCase(ErrorCodes.CliUnknownTarget)]
    public async Task Invalid_manifest_or_arguments_exit_2(string code)
    {
        var result = new BuildResult(1, false, [Problem.Error(code, "bad")], []);

        var (exit, writer, _) = await Handle(result);

        Assert.That(exit, Is.EqualTo(2));
        writer.Verify(w => w.Write(result), Times.Once);
    }
}
