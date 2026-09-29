using System.Collections.Concurrent;
using System.Diagnostics;

namespace UnitTests.Installer.Cli.Integration;

/// <summary>Publishes Samples.DotnetApp (once per runtime identifier and flavor per test run) and locates the Go sample.</summary>
public static class SampleApps
{
    private static readonly ConcurrentDictionary<string, Lazy<string>> Published = new();

    public static string RepoRoot
    {
        get
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "solution", "Samples.DotnetApp")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    /// <summary>The Go sample's directory for a target (windows-x64, macos-arm64, linux-x64).</summary>
    public static string Generic(string target) => Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "generic", target);

    public static string Dotnet(string rid, bool selfContained) =>
        Published.GetOrAdd($"{rid}-{selfContained}", key => new Lazy<string>(() => Publish(key, rid, selfContained))).Value;

    private static string Publish(string key, string rid, bool selfContained)
    {
        var output = Path.Combine(Path.GetTempPath(), "installer-tests", "samples", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), key);
        var project = Path.Combine(RepoRoot, "solution", "Samples.DotnetApp", "Samples.DotnetApp.csproj");
        var info = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { "publish", project, "-c", "Release", "-r", rid, "--self-contained", selfContained ? "true" : "false", "-o", output, "--nologo" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet publish {rid} failed:\n{stdout.Result}\n{stderr.Result}");
        }

        return output;
    }
}
