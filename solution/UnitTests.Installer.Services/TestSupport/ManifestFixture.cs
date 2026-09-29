using System.Text.Json.Nodes;
using Installer.Dao.Host;
using Installer.Dao.Manifest;
using Installer.Dao.Payload;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Services.Manifest;
using Installer.Services.Profiles;

namespace UnitTests.Installer.Services.TestSupport;

/// <summary>
/// The real manifest pipeline (expansion, reader, validation, versions) over mocked Daos. The manifest lives at
/// /repo/installer.json; every payload path probes as a directory and the icon as a 1024×1024 PNG unless a test changes it.
/// </summary>
public sealed class ManifestFixture
{
    public const string ManifestPath = "/repo/installer.json";

    public Mock<IManifestDao> Manifests { get; } = new();
    public Mock<IEnvironmentDao> Environment { get; } = new();
    public Mock<IPayloadDao> Payloads { get; } = new();
    public Mock<IRuntimeProfileResolver> Profiles { get; } = new();
    public Dictionary<string, string> Variables { get; } = [];

    public ManifestFixture()
    {
        Manifests.Setup(m => m.Exists(ManifestPath)).Returns(true);
        Environment.Setup(e => e.GetVariable(It.IsAny<string>())).Returns((string name) => Variables.GetValueOrDefault(name));
        Payloads.Setup(p => p.Probe(It.IsAny<string>())).Returns(PayloadKind.Directory);
        Payloads.Setup(p => p.FileExists(It.IsAny<string>())).Returns(true);
        Payloads.Setup(p => p.ReadHeader(It.IsAny<string>(), 24)).Returns(Png.Header(1024, 1024));
        Profiles.Setup(p => p.Names).Returns(["generic", "dotnet"]);
        Profiles.Setup(p => p.Find("dotnet")).Returns(Profile("dotnet", requiresExecutable: false));
        Profiles.Setup(p => p.Find("generic")).Returns(Profile("generic", requiresExecutable: true));
    }

    public IManifestService Service =>
        new ManifestService(
            Manifests.Object,
            new EnvironmentExpansionService(Environment.Object),
            new ManifestReader(),
            new ManifestValidationService(new VersionService(), Payloads.Object, Profiles.Object));

    public ManifestLoadResult Load(JsonObject manifest)
    {
        Manifests.Setup(m => m.ReadText(ManifestPath)).Returns(manifest.ToJsonString());
        return Service.Load(ManifestPath);
    }

    public ManifestLoadResult Load(string json)
    {
        Manifests.Setup(m => m.ReadText(ManifestPath)).Returns(json);
        return Service.Load(ManifestPath);
    }

    /// <summary>A valid manifest with one target per OS; tests change what they exercise.</summary>
    public static JsonObject Valid() => new()
    {
        ["schemaVersion"] = 1,
        ["id"] = "dev.screenrec.app",
        ["name"] = "ScreenRec",
        ["version"] = "1.0.0.12",
        ["displayVersion"] = "1.0.0-b12",
        ["publisher"] = "Ward Yorgason",
        ["description"] = "Screen recorder",
        ["icon"] = "assets/icon.png",
        ["profile"] = "dotnet",
        ["macos"] = new JsonObject { ["identity"] = "ScreenRec Dev" },
        ["targets"] = new JsonArray
        {
            new JsonObject { ["os"] = "windows", ["arch"] = "x64", ["payload"] = "dist/win-x64.zip" },
            new JsonObject { ["os"] = "macos", ["arch"] = "arm64", ["payload"] = "dist/osx-arm64" },
            new JsonObject { ["os"] = "linux", ["arch"] = "x64", ["payload"] = "dist/linux-x64" },
        },
    };

    public static IReadOnlyList<string> Codes(ManifestLoadResult result) => result.Problems.Select(problem => problem.Code).ToList();

    public static string Messages(ManifestLoadResult result) => string.Join("\n", result.Problems.Select(problem => problem.Message));

    public static IReadOnlyList<Problem> Errors(ManifestLoadResult result) =>
        result.Problems.Where(problem => problem.Severity == Severity.Error).ToList();

    private static IRuntimeProfileService Profile(string name, bool requiresExecutable)
    {
        var profile = new Mock<IRuntimeProfileService>();
        profile.Setup(p => p.Name).Returns(name);
        profile.Setup(p => p.RequiresExecutable).Returns(requiresExecutable);
        return profile.Object;
    }
}
