using System.Diagnostics;
using Installer.Dao.Wrappers;
using Installer.Dtos.Tools;

namespace UnitTests.Installer.Dao.Wrappers;

[Category("Integration")]
public class ProcessWrapperTests
{
    private readonly ProcessWrapper _wrapper = new();

    [SetUp]
    public void RequireUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Uses /bin/sh.");
        }
    }

    [Test]
    public async Task Captures_stdout_stderr_and_exit_code()
    {
        var lines = new List<string>();
        var result = await _wrapper.RunAsync(
            new ProcessRequest("/bin/sh", ["-c", "echo out; echo err >&2; exit 3"]),
            line => { lock (lines) { lines.Add(line); } },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(3));
            Assert.That(result.StandardOutput.Trim(), Is.EqualTo("out"));
            Assert.That(result.StandardError.Trim(), Is.EqualTo("err"));
            Assert.That(lines, Is.EquivalentTo(new[] { "out", "err" }));
        });
    }

    [Test]
    public async Task Writes_standard_input_and_passes_environment_and_working_directory()
    {
        var dir = Path.GetTempPath();
        var result = await _wrapper.RunAsync(
            new ProcessRequest("/bin/sh", ["-c", "cat; echo \"$GREETING\"; pwd"], dir, "from stdin\n", new Dictionary<string, string> { ["GREETING"] = "hello" }),
            null,
            CancellationToken.None);

        var output = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Multiple(() =>
        {
            Assert.That(output[0], Is.EqualTo("from stdin"));
            Assert.That(output[1], Is.EqualTo("hello"));
            Assert.That(Path.GetFullPath(output[2]).TrimEnd('/'), Does.EndWith(Path.GetFileName(dir.TrimEnd('/'))));
        });
    }

    [Test]
    public void Cancellation_kills_the_process_tree()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var stopwatch = Stopwatch.StartNew();

        Assert.That(
            async () => await _wrapper.RunAsync(new ProcessRequest("/bin/sh", ["-c", "sleep 30 & wait"]), null, cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
    }
}
