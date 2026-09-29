using System.Diagnostics;
using System.Text.Json.Nodes;

namespace UnitTests.Installer.Cli.Integration;

/// <summary>
/// Real codesign, hdiutil and ditto. Needs a Mac and a code-signing identity named by INSTALLER_TEST_IDENTITY; the
/// notarization test also needs INSTALLER_TEST_NOTARIZE=1, INSTALLER_TEST_DEVELOPER_ID and INSTALLER_TEST_NOTARY_PROFILE.
/// </summary>
[Category("Integration")]
public class MacPackageIntegrationTests
{
    private static string RequireIdentity(string variable = "INSTALLER_TEST_IDENTITY")
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("macOS packages can only be built on a Mac.");
        }

        var identity = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(identity))
        {
            Assert.Ignore($"{variable} is not set; see docs/ci-jenkins.md.");
        }

        return identity!;
    }

    [Test]
    public async Task Signs_and_packages_the_dotnet_sample()
    {
        var identity = RequireIdentity();
        using var host = new InstallerHost();
        var manifest = host.WriteManifest(
            "dotnet",
            [new JsonObject { ["os"] = "macos", ["arch"] = "arm64", ["payload"] = SampleApps.Dotnet("osx-arm64", selfContained: true) }],
            extra: new JsonObject { ["macos"] = new JsonObject { ["identity"] = identity, ["outputs"] = new JsonArray("dmg", "zip") } });

        var (exit, result) = await host.BuildAsync(manifest);

        Assert.That(exit, Is.Zero, host.Console.OutWriter.ToString());
        var artifacts = result.RootElement.GetProperty("targets")[0].GetProperty("artifacts");
        var dmg = artifacts[0].GetProperty("path").GetString()!;
        var zip = artifacts[1].GetProperty("path").GetString()!;

        var mount = Path.Combine(host.Directory, "mnt");
        Run("hdiutil", "attach", dmg, "-nobrowse", "-readonly", "-mountpoint", mount);
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(Path.Combine(mount, "Samples.app")), Is.True);
                Assert.That(new FileInfo(Path.Combine(mount, "Applications")).LinkTarget, Is.EqualTo("/Applications"));
                Assert.That(Run("codesign", "--verify", "--strict", "--deep", Path.Combine(mount, "Samples.app")).ExitCode, Is.Zero);
            });
        }
        finally
        {
            Run("hdiutil", "detach", mount);
        }

        var unzipped = Path.Combine(host.Directory, "unzipped");
        Run("ditto", "-x", "-k", zip, unzipped);
        var app = Path.Combine(unzipped, "Samples.app");
        var details = Run("codesign", "-d", "--entitlements", "-", "--verbose=2", app).Output;
        Assert.Multiple(() =>
        {
            Assert.That(Run("codesign", "--verify", "--strict", "--deep", app).ExitCode, Is.Zero, "the zip keeps the signature");
            Assert.That(details, Does.Contain("runtime"), "hardened runtime");
            Assert.That(details, Does.Contain("com.apple.security.cs.allow-jit"));
            Assert.That(Run(Path.Combine(app, "Contents", "MacOS", "Samples.DotnetApp"), "--help").Output, Does.Contain("args: --help"));
        });
    }

    [Test]
    public async Task Notarizes_and_staples()
    {
        if (Environment.GetEnvironmentVariable("INSTALLER_TEST_NOTARIZE") != "1")
        {
            Assert.Ignore("Set INSTALLER_TEST_NOTARIZE=1 (the test job's NOTARIZE parameter) to run notarization.");
        }

        var identity = RequireIdentity("INSTALLER_TEST_DEVELOPER_ID");
        var profile = Environment.GetEnvironmentVariable("INSTALLER_TEST_NOTARY_PROFILE");
        Assert.That(profile, Is.Not.Null.And.Not.Empty, "INSTALLER_TEST_NOTARY_PROFILE");
        using var host = new InstallerHost();
        var manifest = host.WriteManifest(
            "dotnet",
            [new JsonObject { ["os"] = "macos", ["arch"] = "arm64", ["payload"] = SampleApps.Dotnet("osx-arm64", selfContained: true) }],
            extra: new JsonObject { ["macos"] = new JsonObject { ["identity"] = identity, ["notarize"] = new JsonObject { ["keychainProfile"] = profile } } });

        var (exit, result) = await host.BuildAsync(manifest);

        Assert.That(exit, Is.Zero, host.Console.OutWriter.ToString());
        var dmg = result.RootElement.GetProperty("targets")[0].GetProperty("artifacts")[0].GetProperty("path").GetString()!;
        Assert.That(Run("xcrun", "stapler", "validate", dmg).ExitCode, Is.Zero);
    }

    private static (int ExitCode, string Output) Run(string file, params string[] arguments)
    {
        var info = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
