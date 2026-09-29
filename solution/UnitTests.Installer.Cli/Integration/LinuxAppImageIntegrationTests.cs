using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace UnitTests.Installer.Cli.Integration;

[Category("Integration")]
public class LinuxAppImageIntegrationTests
{
    private static void RequireDocker(InstallerHost host)
    {
        if (host.FindTool("docker") is null || Docker("info").ExitCode != 0)
        {
            Assert.Ignore("No reachable Docker engine; start one to run this test.");
        }
    }

    [Test]
    public async Task X64_appimage_runs_and_the_image_is_reused()
    {
        using var host = new InstallerHost();
        RequireDocker(host);
        var manifest = host.WriteManifest("dotnet", [new JsonObject { ["os"] = "linux", ["arch"] = "x64", ["payload"] = SampleApps.Dotnet("linux-x64", selfContained: true) }]);

        var (exit, result) = await host.BuildAsync(manifest);

        Assert.That(exit, Is.Zero, host.Console.OutWriter.ToString());
        var appImage = result.RootElement.GetProperty("targets")[0].GetProperty("artifacts")[0].GetProperty("path").GetString()!;
        Assert.That(ElfMachine(appImage), Is.EqualTo(0x3E), "x86-64");

        var run = RunInContainer(appImage, "linux/amd64");
        Assert.That(run.Output, Does.Contain("args: --help"), run.Output);
        Assert.That(run.Output, Does.Contain("Name=Samples").And.Contain("X-AppImage-Version=1.2.3-b4"));

        using var second = new InstallerHost();
        var (secondExit, _) = await second.BuildAsync(second.WriteManifest("dotnet", [new JsonObject { ["os"] = "linux", ["arch"] = "x64", ["payload"] = SampleApps.Dotnet("linux-x64", selfContained: true) }]));
        Assert.Multiple(() =>
        {
            Assert.That(secondExit, Is.Zero);
            Assert.That(second.Logs, Has.None.Contain("Building the AppImage image"), "the cached image should be reused");
        });
    }

    [Test]
    public async Task Arm64_appimage_is_an_aarch64_executable()
    {
        using var host = new InstallerHost();
        RequireDocker(host);
        var manifest = host.WriteManifest("dotnet", [new JsonObject { ["os"] = "linux", ["arch"] = "arm64", ["payload"] = SampleApps.Dotnet("linux-arm64", selfContained: false) }]);

        var (exit, result) = await host.BuildAsync(manifest);

        Assert.That(exit, Is.Zero, host.Console.OutWriter.ToString());
        var appImage = result.RootElement.GetProperty("targets")[0].GetProperty("artifacts")[0].GetProperty("path").GetString()!;
        Assert.That(ElfMachine(appImage), Is.EqualTo(0xB7), "AArch64");
    }

    [Test]
    public async Task Arm64_appimage_runs()
    {
        using var host = new InstallerHost();
        RequireDocker(host);
        if (Docker("run", "--rm", "--platform", "linux/arm64", "debian:bookworm-slim", "true").ExitCode != 0)
        {
            Assert.Ignore("This host can't run arm64 containers (no native arm64 or emulation).");
        }

        var manifest = host.WriteManifest("dotnet", [new JsonObject { ["os"] = "linux", ["arch"] = "arm64", ["payload"] = SampleApps.Dotnet("linux-arm64", selfContained: true) }]);
        var (exit, result) = await host.BuildAsync(manifest);

        Assert.That(exit, Is.Zero, host.Console.OutWriter.ToString());
        var run = RunInContainer(result.RootElement.GetProperty("targets")[0].GetProperty("artifacts")[0].GetProperty("path").GetString()!, "linux/arm64");
        Assert.That(run.Output, Does.Contain("args: --help"), run.Output);
    }

    private static int ElfMachine(string path)
    {
        var header = new byte[20];
        using var stream = File.OpenRead(path);
        stream.ReadExactly(header);
        Assert.That(header.AsSpan(0, 4).SequenceEqual("\u007fELF"u8), Is.True);
        return BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(18));
    }

    /// <summary>Extracts the AppImage in a clean Debian container (no FUSE needed) and runs it with --help.</summary>
    private static (int ExitCode, string Output) RunInContainer(string appImage, string platform)
    {
        var directory = Path.GetDirectoryName(appImage)!;
        var name = Path.GetFileName(appImage);
        var script = $"cd /tmp && '/a/{name}' --appimage-extract > /dev/null && cat squashfs-root/*.desktop && ./squashfs-root/AppRun --help";
        return Docker("run", "--rm", "--platform", platform, "-e", "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1", "-v", $"{directory}:/a:ro", "debian:bookworm-slim", "sh", "-c", script);
    }

    private static (int ExitCode, string Output) Docker(params string[] arguments)
    {
        var info = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(info)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            return (process.ExitCode, stdout.Result + stderr.Result);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (-1, "docker not found");
        }
    }
}
