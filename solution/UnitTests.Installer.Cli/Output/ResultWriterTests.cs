using System.Text.Json;
using Installer.Cli.Output;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;

namespace UnitTests.Installer.Cli.Output;

public class ResultWriterTests
{
    [Test]
    public void Writes_exactly_one_json_document_with_contract_names()
    {
        var console = new StringConsole();
        var result = new BuildResult(1, false, [], [
            new TargetResult("macos-arm64", TargetOs.MacOS, TargetArch.Arm64, TargetStatus.Succeeded,
                [new BuiltArtifact(ArtifactKind.Dmg, "/dist/App-1.0.0-macos-arm64.dmg", 71234567, "ab12")],
                [Problem.Warning(ErrorCodes.DotnetRuntimeRequired, "needs the runtime")], [], null),
            new TargetResult("windows-x64", TargetOs.Windows, TargetArch.X64, TargetStatus.NotSelected, [], [], [], null),
            new TargetResult("linux-x64", TargetOs.Linux, TargetArch.X64, TargetStatus.Failed, [],
                [], [Problem.Error(ErrorCodes.ToolMissing, "docker was not found")], "/tmp/installer/run/linux-x64"),
        ]);

        new ResultWriter(console).Write(result);

        using var json = JsonDocument.Parse(console.OutWriter.ToString());
        var root = json.RootElement;
        var targets = root.GetProperty("targets");
        var artifact = targets[0].GetProperty("artifacts")[0];
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(root.GetProperty("succeeded").GetBoolean(), Is.False);
            Assert.That(targets[0].GetProperty("os").GetString(), Is.EqualTo("macos"));
            Assert.That(targets[0].GetProperty("arch").GetString(), Is.EqualTo("arm64"));
            Assert.That(targets[0].GetProperty("status").GetString(), Is.EqualTo("succeeded"));
            Assert.That(artifact.GetProperty("kind").GetString(), Is.EqualTo("dmg"));
            Assert.That(artifact.GetProperty("size").GetInt64(), Is.EqualTo(71234567));
            Assert.That(artifact.GetProperty("sha256").GetString(), Is.EqualTo("ab12"));
            Assert.That(targets[0].GetProperty("warnings")[0].GetProperty("code").GetString(), Is.EqualTo("dotnet.runtime-required"));
            Assert.That(targets[0].TryGetProperty("workDir", out _), Is.False);
            Assert.That(targets[1].GetProperty("status").GetString(), Is.EqualTo("not-selected"));
            Assert.That(targets[2].GetProperty("errors")[0].GetProperty("code").GetString(), Is.EqualTo("tool.missing"));
            Assert.That(targets[2].GetProperty("workDir").GetString(), Is.EqualTo("/tmp/installer/run/linux-x64"));
            Assert.That(console.ErrorWriter.ToString(), Is.Empty);
        });
    }

    [TestCase(ArtifactKind.SetupExe, "setup-exe")]
    [TestCase(ArtifactKind.AppImage, "appimage")]
    [TestCase(ArtifactKind.Zip, "zip")]
    public void Artifact_kinds(ArtifactKind kind, string expected)
    {
        var console = new StringConsole();
        new ResultWriter(console).Write(new BuildResult(1, true, [], [
            new TargetResult("t", TargetOs.Windows, TargetArch.X64, TargetStatus.Succeeded, [new BuiltArtifact(kind, "/a", 1, "x")], [], [], null),
        ]));

        using var json = JsonDocument.Parse(console.OutWriter.ToString());
        Assert.That(json.RootElement.GetProperty("targets")[0].GetProperty("artifacts")[0].GetProperty("kind").GetString(), Is.EqualTo(expected));
    }
}
