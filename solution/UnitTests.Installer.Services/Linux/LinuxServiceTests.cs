using Installer.Dao.Linux;
using Installer.Dao.Workspace;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;
using Installer.Services.Imaging;
using Installer.Services.Linux;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.Linux;

public class LinuxServiceTests
{
    private static TargetContext Context(TargetArch arch = TargetArch.X64)
    {
        var app = Fixtures.App() with { Id = "dev.screenrec.app", Name = "ScreenRec", Description = "Screen recorder", DisplayVersion = "1.0.0-b12", IconPath = "/icon.png" };
        var target = Fixtures.Target(TargetOs.Linux, arch, "linux-x64") with { Linux = new LinuxOptions(["AudioVideo"]) };
        return new TargetContext(app, target, new PreparedPayload("/w/payload", ["ScreenRec"]), new ProfileAnalysis("ScreenRec", [], new Dictionary<string, bool>()), "/w", "/out", "ScreenRec-1.0.0-b12-linux-x64");
    }

    [Test]
    public void Desktop_entry() => Golden.AssertMatches("app.desktop", new DesktopEntryService().Generate(Context().App, ["AudioVideo", "Recorder"]));

    [Test]
    public void Desktop_values_stay_on_one_line()
    {
        var entry = new DesktopEntryService().Generate(Context().App with { Description = "Two\nlines" }, ["Utility"]);

        Assert.That(entry, Does.Contain("Comment=Two lines\n"));
    }

    [Test]
    public void App_dir_layout()
    {
        var workspace = new Mock<IWorkspaceDao>();
        var icons = new Mock<IIconService>();
        icons.Setup(i => i.CreatePng("/icon.png", 256)).Returns([7]);
        var desktop = new Mock<IDesktopEntryService>();
        desktop.Setup(d => d.Generate(It.IsAny<AppInfo>(), It.Is<IReadOnlyList<string>>(c => c.SequenceEqual(new[] { "AudioVideo" })))).Returns("entry");

        new AppDirService(workspace.Object, desktop.Object, icons.Object).Build(Context(), "/w/AppDir");

        workspace.Verify(w => w.ResetDirectory("/w/AppDir"));
        workspace.Verify(w => w.MoveDirectory("/w/payload", Path.Combine("/w/AppDir", "usr", "lib", "dev.screenrec.app")));
        workspace.Verify(w => w.CreateRelativeSymlink(Path.Combine("/w/AppDir", "AppRun"), "usr/lib/dev.screenrec.app/ScreenRec"));
        workspace.Verify(w => w.WriteText(Path.Combine("/w/AppDir", "dev.screenrec.app.desktop"), "entry"));
        workspace.Verify(w => w.WriteBytes(Path.Combine("/w/AppDir", "dev.screenrec.app.png"), It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 7 }))));
        workspace.Verify(w => w.CreateRelativeSymlink(Path.Combine("/w/AppDir", ".DirIcon"), "dev.screenrec.app.png"));
    }

    [Test]
    public void Image_tag_is_a_hash_of_the_dockerfile()
    {
        var definitions = new Mock<IContainerDefinitionDao>();
        definitions.Setup(d => d.GetAppImageDockerfile()).Returns("FROM scratch\n");
        var other = new Mock<IContainerDefinitionDao>();
        other.Setup(d => d.GetAppImageDockerfile()).Returns("FROM scratch\nRUN true\n");

        var tag = new AppImageService(new Mock<IDockerDao>().Object, definitions.Object, NullLogger<AppImageService>.Instance).ImageTag;
        var otherTag = new AppImageService(new Mock<IDockerDao>().Object, other.Object, NullLogger<AppImageService>.Instance).ImageTag;

        Assert.Multiple(() =>
        {
            Assert.That(tag, Does.Match("^installer-appimage:[0-9a-f]{12}$"));
            Assert.That(otherTag, Is.Not.EqualTo(tag));
        });
    }

    [TestCase(false, TargetArch.X64, "x86_64")]
    [TestCase(true, TargetArch.Arm64, "aarch64")]
    public async Task Container_flow(bool imageExists, TargetArch arch, string appImageArch)
    {
        var calls = new List<string>();
        var docker = new Mock<IDockerDao>();
        docker.Setup(d => d.ImageExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(imageExists);
        docker.Setup(d => d.BuildImageAsync(It.IsAny<string>(), "FROM scratch\n", It.IsAny<CancellationToken>())).Callback(() => calls.Add("build")).Returns(Task.CompletedTask);
        docker.Setup(d => d.CreateContainerAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<string>, IReadOnlyDictionary<string, string>, CancellationToken>((_, command, env, _) => calls.Add($"create {string.Join(' ', command)} ARCH={env["ARCH"]}"))
            .ReturnsAsync("c1");
        docker.Setup(d => d.CopyToContainerAsync("c1", "/w/AppDir", "/work/AppDir", It.IsAny<CancellationToken>())).Callback(() => calls.Add("cp in")).Returns(Task.CompletedTask);
        docker.Setup(d => d.StartAsync("c1", It.IsAny<CancellationToken>())).Callback(() => calls.Add("start")).Returns(Task.CompletedTask);
        docker.Setup(d => d.CopyFromContainerAsync("c1", "/work/out.AppImage", "/w/out.AppImage", It.IsAny<CancellationToken>())).Callback(() => calls.Add("cp out")).Returns(Task.CompletedTask);
        docker.Setup(d => d.RemoveContainerAsync("c1", It.IsAny<CancellationToken>())).Callback(() => calls.Add("rm")).Returns(Task.CompletedTask);
        var definitions = new Mock<IContainerDefinitionDao>();
        definitions.Setup(d => d.GetAppImageDockerfile()).Returns("FROM scratch\n");

        await new AppImageService(docker.Object, definitions.Object, NullLogger<AppImageService>.Instance).BuildAsync("/w/AppDir", arch, "/w/out.AppImage", CancellationToken.None);

        var expected = new List<string>();
        if (!imageExists)
        {
            expected.Add("build");
        }

        expected.AddRange([
            $"create /opt/appimage/appimagetool/AppRun --no-appstream --runtime-file /opt/appimage/runtime-{appImageArch} /work/AppDir /work/out.AppImage ARCH={appImageArch}",
            "cp in", "start", "cp out", "rm",
        ]);
        Assert.That(calls, Is.EqualTo(expected));
    }

    [Test]
    public void Container_is_removed_when_packing_fails()
    {
        var docker = new Mock<IDockerDao>();
        docker.Setup(d => d.ImageExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        docker.Setup(d => d.CreateContainerAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>())).ReturnsAsync("c1");
        docker.Setup(d => d.StartAsync("c1", It.IsAny<CancellationToken>())).ThrowsAsync(new BuildFailedException(ErrorCodes.ToolFailed, "appimagetool failed"));
        var definitions = new Mock<IContainerDefinitionDao>();
        definitions.Setup(d => d.GetAppImageDockerfile()).Returns("FROM scratch\n");

        Assert.ThrowsAsync<BuildFailedException>(() => new AppImageService(docker.Object, definitions.Object, NullLogger<AppImageService>.Instance).BuildAsync("/w/AppDir", TargetArch.X64, "/w/out.AppImage", CancellationToken.None));
        docker.Verify(d => d.RemoveContainerAsync("c1", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Format_preflight_needs_a_running_engine()
    {
        var docker = new Mock<IDockerDao>();
        var format = new LinuxFormatService(docker.Object, new Mock<IAppDirService>().Object, new Mock<IAppImageService>().Object);

        var problems = await format.PreflightAsync(Context().Target, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(problems.Single().Code, Is.EqualTo(ErrorCodes.DockerUnreachable));
            Assert.That(format.RequiredTools, Is.EqualTo(new[] { "docker" }));
            Assert.That(format.RequiredHost, Is.Null);
        });
    }

    [Test]
    public async Task Format_builds_the_app_dir_then_the_appimage()
    {
        var appDirs = new Mock<IAppDirService>();
        var appImages = new Mock<IAppImageService>();
        var format = new LinuxFormatService(new Mock<IDockerDao>().Object, appDirs.Object, appImages.Object);

        var produced = await format.BuildAsync(Context(TargetArch.Arm64), CancellationToken.None);

        appDirs.Verify(a => a.Build(It.IsAny<TargetContext>(), Path.Combine("/w", "AppDir")));
        appImages.Verify(a => a.BuildAsync(Path.Combine("/w", "AppDir"), TargetArch.Arm64, Path.Combine("/w", "ScreenRec-1.0.0-b12-linux-x64.AppImage"), It.IsAny<CancellationToken>()));
        Assert.That(produced.Single().Kind, Is.EqualTo(ArtifactKind.AppImage));
    }
}
