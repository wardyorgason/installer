using System.Text.Json.Nodes;
using Installer.Dtos.Build;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.Manifest;

/// <summary>One test per package-definition scenario, run through the real manifest pipeline.</summary>
public class ManifestValidationServiceTests
{
    private ManifestFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new ManifestFixture();

    private global::Installer.Services.Manifest.ManifestLoadResult Load(Action<JsonObject> change)
    {
        var manifest = ManifestFixture.Valid();
        change(manifest);
        return _fixture.Load(manifest);
    }

    private static void AssertSingleError(global::Installer.Services.Manifest.ManifestLoadResult result, string code, params string[] fragments)
    {
        var errors = ManifestFixture.Errors(result);
        Assert.That(errors, Has.Count.EqualTo(1), ManifestFixture.Messages(result));
        Assert.That(errors[0].Code, Is.EqualTo(code));
        foreach (var fragment in fragments)
        {
            Assert.That(errors[0].Message, Does.Contain(fragment));
        }

        Assert.That(result.Manifest, Is.Null);
    }

    [Test]
    public void Unsupported_schema_version_names_the_supported_one() =>
        AssertSingleError(Load(m => m["schemaVersion"] = 2), ErrorCodes.ManifestUnsupportedSchema, "supports schema version 1");

    [Test]
    public void Missing_schema_version() =>
        AssertSingleError(Load(m => m.Remove("schemaVersion")), ErrorCodes.ManifestMissingField, "$.schemaVersion");

    [TestCase("ScreenRec")]
    [TestCase("dev..app")]
    [TestCase("dev.screen rec")]
    public void Invalid_app_id(string id) =>
        AssertSingleError(Load(m => m["id"] = id), ErrorCodes.ManifestInvalidValue, "reverse-DNS");

    [TestCase("publisher")]
    [TestCase("description")]
    [TestCase("name")]
    [TestCase("version")]
    [TestCase("icon")]
    [TestCase("profile")]
    public void Required_metadata(string field) =>
        AssertSingleError(Load(m => m.Remove(field)), ErrorCodes.ManifestMissingField, "$." + field);

    [Test]
    public void Name_must_be_file_name_safe() =>
        AssertSingleError(Load(m => m["name"] = "Screen/Rec"), ErrorCodes.ManifestInvalidValue, "$.name");

    [TestCase(256, 256)]
    [TestCase(1024, 512)]
    public void Icon_too_small_or_not_square(int width, int height)
    {
        _fixture.Payloads.Setup(p => p.ReadHeader(It.IsAny<string>(), 24)).Returns(Png.Header(width, height));

        AssertSingleError(Load(_ => { }), ErrorCodes.ManifestInvalidValue, "square PNG of at least 512×512");
    }

    [Test]
    public void Icon_that_is_not_a_png()
    {
        _fixture.Payloads.Setup(p => p.ReadHeader(It.IsAny<string>(), 24)).Returns(new byte[24]);

        AssertSingleError(Load(_ => { }), ErrorCodes.ManifestInvalidValue, "is not a PNG");
    }

    [Test]
    public void Unknown_profile_lists_the_known_ones() =>
        AssertSingleError(Load(m => m["profile"] = "node"), ErrorCodes.ManifestInvalidValue, "generic, dotnet");

    [Test]
    public void Generic_profile_requires_executable() =>
        AssertSingleError(Load(m => m["profile"] = "generic"), ErrorCodes.ManifestMissingField, "requires executable");

    [Test]
    public void Semantic_version_is_rejected() =>
        AssertSingleError(Load(m => m["version"] = "1.2.0-beta.1"), ErrorCodes.ManifestInvalidValue, "displayVersion");

    [Test]
    public void Display_version_defaults_to_version()
    {
        var result = Load(m =>
        {
            m["version"] = "1.0.0";
            m.Remove("displayVersion");
        });

        Assert.That(result.Manifest!.App.DisplayVersion, Is.EqualTo("1.0.0"));
    }

    [Test]
    public void Display_version_must_not_contain_path_separators() =>
        AssertSingleError(Load(m => m["displayVersion"] = "1.0/2"), ErrorCodes.ManifestInvalidValue, "$.displayVersion");

    [Test]
    public void Two_flavors_for_one_platform()
    {
        var result = Load(m => m["targets"]!.AsArray().Add(new JsonObject
        {
            ["name"] = "win-x64-selfcontained", ["os"] = "windows", ["arch"] = "x64", ["payload"] = "dist/sc.zip",
        }));

        Assert.That(result.Manifest!.Targets.Select(t => t.Name), Has.Member("win-x64-selfcontained").And.Member("windows-x64"));
    }

    [Test]
    public void Duplicate_target_names() =>
        AssertSingleError(Load(m => m["targets"]![1]!["name"] = "windows-x64"), ErrorCodes.ManifestDuplicateTarget, "windows-x64");

    [Test]
    public void Empty_targets_list() =>
        AssertSingleError(Load(m => m["targets"] = new JsonArray()), ErrorCodes.ManifestMissingField, "at least one target");

    [Test]
    public void Absent_targets_list() =>
        AssertSingleError(Load(m => m.Remove("targets")), ErrorCodes.ManifestMissingField, "at least one target");

    [Test]
    public void Payload_not_found()
    {
        _fixture.Payloads.Setup(p => p.Probe(Path.GetFullPath("/repo/dist/linux-x64"))).Returns((global::Installer.Dtos.Manifest.PayloadKind?)null);

        AssertSingleError(Load(_ => { }), ErrorCodes.PayloadNotFound, "linux-x64", "$.targets[2]");
    }

    [TestCase("os", "bsd", "windows, macos or linux")]
    [TestCase("arch", "x86", "x64 or arm64")]
    [TestCase("name", "has space", "letters, digits")]
    public void Invalid_target_fields(string field, string value, string fragment) =>
        AssertSingleError(Load(m => m["targets"]![2]![field] = value), ErrorCodes.ManifestInvalidValue, fragment);

    [Test]
    public void Section_for_the_wrong_os() =>
        AssertSingleError(
            Load(m => m["targets"]![2]!["macos"] = new JsonObject { ["identity"] = "x" }),
            ErrorCodes.ManifestInvalidValue,
            "linux-x64",
            "macos");

    [Test]
    public void Mac_target_requires_identity() =>
        AssertSingleError(Load(m => m.Remove("macos")), ErrorCodes.ManifestMissingField, "signing identity");

    [Test]
    public void Mac_outputs_must_not_be_empty() =>
        AssertSingleError(Load(m => m["macos"]!["outputs"] = new JsonArray()), ErrorCodes.ManifestInvalidValue, "at least one");

    [Test]
    public void Mac_outputs_must_be_known() =>
        AssertSingleError(Load(m => m["macos"]!["outputs"] = new JsonArray("pkg")), ErrorCodes.ManifestInvalidValue, "dmg or zip");

    [Test]
    public void Notarization_with_a_self_signed_identity() =>
        AssertSingleError(
            Load(m => m["macos"]!["notarize"] = new JsonObject { ["keychainProfile"] = "notary" }),
            ErrorCodes.ManifestInvalidValue,
            "requires a Developer ID Application identity");

    [Test]
    public void Notarization_with_a_developer_id_identity()
    {
        var result = Load(m =>
        {
            m["macos"]!["identity"] = "Developer ID Application: Example (TEAMID)";
            m["macos"]!["notarize"] = new JsonObject { ["keychainProfile"] = "notary" };
        });

        Assert.That(result.Manifest!.Targets[1].MacOS!.NotaryKeychainProfile, Is.EqualTo("notary"));
    }

    [TestCase("CFBundleIdentifier")]
    [TestCase("CFBundleExecutable")]
    [TestCase("CFBundleVersion")]
    [TestCase("CFBundleShortVersionString")]
    public void Reserved_info_plist_keys(string key) =>
        AssertSingleError(
            Load(m => m["macos"]!["infoPlist"] = new JsonObject { [key] = "x" }),
            ErrorCodes.ManifestInvalidValue,
            key,
            "set from the manifest");

    [Test]
    public void Sign_command_needs_a_file_placeholder() =>
        AssertSingleError(
            Load(m => m["windows"] = new JsonObject { ["signCommand"] = new JsonArray("osslsigncode", "sign") }),
            ErrorCodes.ManifestInvalidValue,
            "{file}");

    [Test]
    public void Sign_command_with_placeholder_is_kept()
    {
        var result = Load(m => m["windows"] = new JsonObject { ["signCommand"] = new JsonArray("sign", "-in", "{file}") });

        Assert.That(result.Manifest!.Targets[0].Windows!.SignCommand, Is.EqualTo(new[] { "sign", "-in", "{file}" }));
    }
}
