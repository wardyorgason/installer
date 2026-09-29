using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnitTests.Installer.Cli.Integration;

namespace UnitTests.Installer.Cli.EndToEnd;

/// <summary>
/// The whole app packaging the samples. Each test builds every target this host can build (macOS needs a Mac and
/// INSTALLER_TEST_IDENTITY, Windows needs makensis, Linux needs Docker); on the Mac agent that is all of them.
/// </summary>
[Category("EndToEnd")]
public class EndToEndTests
{
    private static (bool Mac, bool Windows, bool Linux) Capabilities(InstallerHost host) => (
        OperatingSystem.IsMacOS() && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INSTALLER_TEST_IDENTITY")),
        host.FindTool("makensis") is not null,
        host.FindTool("docker") is not null);

    [Test]
    public async Task Dotnet_sample_framework_dependent_and_self_contained_for_every_platform()
    {
        using var host = new InstallerHost();
        var (mac, windows, linux) = Capabilities(host);
        var targets = new JsonArray();
        foreach (var selfContained in new[] { false, true })
        {
            var suffix = selfContained ? "-selfcontained" : string.Empty;
            if (windows)
            {
                targets.Add(Target($"windows-x64{suffix}", "windows", "x64", SampleApps.Dotnet("win-x64", selfContained)));
            }

            if (mac)
            {
                targets.Add(Target($"macos-arm64{suffix}", "macos", "arm64", SampleApps.Dotnet("osx-arm64", selfContained)));
            }

            if (linux)
            {
                targets.Add(Target($"linux-x64{suffix}", "linux", "x64", SampleApps.Dotnet("linux-x64", selfContained)));
            }
        }

        if (targets.Count == 0)
        {
            Assert.Ignore("This host can build none of the platforms (no Mac identity, makensis or Docker).");
        }

        var extra = mac ? new JsonObject { ["macos"] = new JsonObject { ["identity"] = Environment.GetEnvironmentVariable("INSTALLER_TEST_IDENTITY"), ["outputs"] = new JsonArray("dmg", "zip") } } : null;
        var (exit, result) = await host.BuildAsync(host.WriteManifest("dotnet", targets, extra: extra));

        AssertEveryTargetSucceededWithHashedArtifacts(exit, result, host, targets.Count);
        var warnings = result.RootElement.GetProperty("targets").EnumerateArray()
            .ToDictionary(t => t.GetProperty("name").GetString()!, t => t.GetProperty("warnings").GetArrayLength());
        Assert.That(warnings.Where(w => w.Key.EndsWith("-selfcontained", StringComparison.Ordinal)).Select(w => w.Value), Is.All.Zero);
        Assert.That(warnings.Where(w => !w.Key.EndsWith("-selfcontained", StringComparison.Ordinal)).Select(w => w.Value), Is.All.EqualTo(1), "framework-dependent targets warn about the runtime");
    }

    [Test]
    public async Task Go_sample_with_the_generic_profile()
    {
        using var host = new InstallerHost();
        var (mac, windows, linux) = Capabilities(host);
        if (!mac && !windows && !linux)
        {
            Assert.Ignore("This host can build none of the platforms (no Mac identity, makensis or Docker).");
        }

        // executable is one value per manifest, and only the Windows binary has .exe, so Windows gets its own manifest.
        if (windows)
        {
            using var windowsHost = new InstallerHost();
            var (exit, result) = await windowsHost.BuildAsync(windowsHost.WriteManifest(
                "generic", [Target("windows-x64", "windows", "x64", SampleApps.Generic("windows-x64"))], executable: "generic-sample.exe"));
            AssertEveryTargetSucceededWithHashedArtifacts(exit, result, windowsHost, 1);
        }

        var unixTargets = new JsonArray();
        if (mac)
        {
            unixTargets.Add(Target("macos-arm64", "macos", "arm64", SampleApps.Generic("macos-arm64")));
        }

        if (linux)
        {
            unixTargets.Add(Target("linux-x64", "linux", "x64", SampleApps.Generic("linux-x64")));
        }

        if (unixTargets.Count > 0)
        {
            var extra = mac ? new JsonObject { ["macos"] = new JsonObject { ["identity"] = Environment.GetEnvironmentVariable("INSTALLER_TEST_IDENTITY") } } : null;
            var (exit, result) = await host.BuildAsync(host.WriteManifest("generic", unixTargets, executable: "generic-sample", extra: extra));
            AssertEveryTargetSucceededWithHashedArtifacts(exit, result, host, unixTargets.Count);
        }
    }

    private static JsonObject Target(string name, string os, string arch, string payload) =>
        new() { ["name"] = name, ["os"] = os, ["arch"] = arch, ["payload"] = payload };

    private static void AssertEveryTargetSucceededWithHashedArtifacts(int exit, JsonDocument result, InstallerHost host, int targetCount)
    {
        Assert.That(exit, Is.Zero, host.Console.OutWriter.ToString());
        var targets = result.RootElement.GetProperty("targets").EnumerateArray().ToList();
        Assert.That(targets, Has.Count.EqualTo(targetCount));
        foreach (var target in targets)
        {
            Assert.That(target.GetProperty("status").GetString(), Is.EqualTo("succeeded"), target.GetProperty("name").GetString());
            var artifacts = target.GetProperty("artifacts").EnumerateArray().ToList();
            Assert.That(artifacts, Is.Not.Empty);
            foreach (var artifact in artifacts)
            {
                var path = artifact.GetProperty("path").GetString()!;
                Assert.Multiple(() =>
                {
                    Assert.That(File.Exists(path), Is.True, path);
                    Assert.That(artifact.GetProperty("size").GetInt64(), Is.EqualTo(new FileInfo(path).Length));
                    Assert.That(artifact.GetProperty("sha256").GetString(), Is.EqualTo(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))));
                });
            }
        }
    }
}
