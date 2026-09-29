using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;

namespace Installer.Cli.Output;

/// <summary>
/// Serializes the result with explicit names (<c>macos</c>, <c>not-selected</c>, <c>setup-exe</c>) so the JSON contract
/// doesn't depend on C# enum spellings.
/// </summary>
internal sealed class ResultWriter(IConsoleWrapper console) : IResultWriter
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public void Write(BuildResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var json = new JsonObject
        {
            ["schemaVersion"] = result.SchemaVersion,
            ["succeeded"] = result.Succeeded,
            ["errors"] = Problems(result.Errors),
            ["targets"] = new JsonArray(result.Targets.Select(Target).ToArray<JsonNode?>()),
        };
        console.Out.WriteLine(json.ToJsonString(Indented));
        console.Out.Flush();
    }

    private static JsonObject Target(TargetResult target)
    {
        var json = new JsonObject
        {
            ["name"] = target.Name,
            ["os"] = Os(target.Os),
            ["arch"] = target.Arch == TargetArch.Arm64 ? "arm64" : "x64",
            ["status"] = target.Status switch
            {
                TargetStatus.Succeeded => "succeeded",
                TargetStatus.Failed => "failed",
                _ => "not-selected",
            },
            ["artifacts"] = new JsonArray(target.Artifacts.Select(artifact => (JsonNode?)new JsonObject
            {
                ["kind"] = Kind(artifact.Kind),
                ["path"] = artifact.Path,
                ["size"] = artifact.Size,
                ["sha256"] = artifact.Sha256,
            }).ToArray()),
            ["warnings"] = Problems(target.Warnings),
            ["errors"] = Problems(target.Errors),
        };
        if (target.WorkDir is not null)
        {
            json["workDir"] = target.WorkDir;
        }

        return json;
    }

    private static JsonArray Problems(IEnumerable<Problem> problems) =>
        new(problems.Select(problem => (JsonNode?)new JsonObject { ["code"] = problem.Code, ["message"] = problem.Message }).ToArray());

    private static string Os(TargetOs os) => os switch
    {
        TargetOs.Windows => "windows",
        TargetOs.MacOS => "macos",
        _ => "linux",
    };

    private static string Kind(ArtifactKind kind) => kind switch
    {
        ArtifactKind.SetupExe => "setup-exe",
        ArtifactKind.Dmg => "dmg",
        ArtifactKind.Zip => "zip",
        _ => "appimage",
    };
}
