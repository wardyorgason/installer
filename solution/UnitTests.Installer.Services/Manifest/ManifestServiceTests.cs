using System.Text.Json.Nodes;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.Manifest;

public class ManifestServiceTests
{
    private ManifestFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new ManifestFixture();

    [Test]
    public void Valid_manifest_loads()
    {
        var result = _fixture.Load(ManifestFixture.Valid());

        Assert.That(result.Problems, Is.Empty, ManifestFixture.Messages(result));
        var manifest = result.Manifest!;
        Assert.Multiple(() =>
        {
            Assert.That(manifest.App.Id, Is.EqualTo("dev.screenrec.app"));
            Assert.That(manifest.App.Version.Parts, Is.EqualTo(new[] { 1, 0, 0, 12 }));
            Assert.That(manifest.Targets.Select(t => t.Name), Is.EqualTo(new[] { "windows-x64", "macos-arm64", "linux-x64" }));
            Assert.That(manifest.Targets[1].MacOS!.Identity, Is.EqualTo("ScreenRec Dev"));
            Assert.That(manifest.Targets[1].MacOS!.Outputs, Is.EqualTo(new[] { MacOutput.Dmg }));
            Assert.That(manifest.Targets[2].Linux!.Categories, Is.EqualTo(new[] { "Utility" }));
        });
    }

    [Test]
    public void Missing_manifest_file()
    {
        _fixture.Manifests.Setup(m => m.Exists(ManifestFixture.ManifestPath)).Returns(false);

        var result = _fixture.Service.Load(ManifestFixture.ManifestPath);

        Assert.That(ManifestFixture.Codes(result), Is.EqualTo(new[] { ErrorCodes.ManifestNotFound }));
        Assert.That(result.Manifest, Is.Null);
    }

    [Test]
    public void Invalid_json()
    {
        var result = _fixture.Load("{ \"id\": ");

        Assert.That(ManifestFixture.Codes(result), Is.EqualTo(new[] { ErrorCodes.ManifestInvalidJson }));
    }

    [Test]
    public void Comments_and_trailing_commas_are_allowed()
    {
        var json = ManifestFixture.Valid().ToJsonString().Replace("\"schemaVersion\":1,", "// comment\n\"schemaVersion\":1,", StringComparison.Ordinal);
        json = json[..^1] + ",}";

        Assert.That(_fixture.Load(json).Problems, Is.Empty);
    }

    [Test]
    public void Relative_paths_resolve_against_the_manifest_directory()
    {
        var result = _fixture.Load(ManifestFixture.Valid());

        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.App.IconPath, Is.EqualTo(Path.GetFullPath("/repo/assets/icon.png")));
            Assert.That(result.Manifest.Targets[0].PayloadPath, Is.EqualTo(Path.GetFullPath("/repo/dist/win-x64.zip")));
        });
    }

    [Test]
    public void Target_overrides_a_top_level_option()
    {
        var manifest = ManifestFixture.Valid();
        manifest["macos"]!["outputs"] = new JsonArray("dmg", "zip");
        manifest["targets"]![1]!["macos"] = new JsonObject { ["outputs"] = new JsonArray("zip") };

        var result = _fixture.Load(manifest);

        Assert.That(result.Manifest!.Targets[1].MacOS!.Outputs, Is.EqualTo(new[] { MacOutput.Zip }));
    }

    [Test]
    public void Nested_objects_merge_key_by_key()
    {
        var manifest = ManifestFixture.Valid();
        manifest["macos"]!["infoPlist"] = new JsonObject { ["A"] = "top", ["D"] = new JsonObject { ["x"] = 1, ["y"] = 1 } };
        manifest["macos"]!["entitlements"] = new JsonObject { ["e1"] = true };
        manifest["targets"]![1]!["macos"] = new JsonObject
        {
            ["infoPlist"] = new JsonObject { ["B"] = "target", ["D"] = new JsonObject { ["y"] = 2 } },
            ["entitlements"] = new JsonObject { ["e2"] = true },
        };

        var mac = _fixture.Load(manifest).Manifest!.Targets[1].MacOS!;

        var nested = (PlistDictionary)mac.InfoPlist.Entries.Single(e => e.Key == "D").Value;
        Assert.Multiple(() =>
        {
            Assert.That(mac.InfoPlist.Entries.Select(e => e.Key), Is.EqualTo(new[] { "A", "D", "B" }));
            Assert.That(nested.Entries, Is.EqualTo(new[]
            {
                new KeyValuePair<string, PlistValue>("x", new PlistInteger(1)),
                new KeyValuePair<string, PlistValue>("y", new PlistInteger(2)),
            }));
            Assert.That(mac.Entitlements.Keys, Is.EquivalentTo(new[] { "e1", "e2" }));
        });
    }

    [Test]
    public void Top_level_section_applies_only_to_its_os()
    {
        var manifest = ManifestFixture.Valid();
        manifest["linux"] = new JsonObject { ["categories"] = new JsonArray("AudioVideo") };

        var result = _fixture.Load(manifest);

        Assert.Multiple(() =>
        {
            Assert.That(result.Problems, Is.Empty, ManifestFixture.Messages(result));
            Assert.That(result.Manifest!.Targets[2].Linux!.Categories, Is.EqualTo(new[] { "AudioVideo" }));
            Assert.That(result.Manifest.Targets[0].Linux, Is.Null);
        });
    }

    [Test]
    public void Environment_references_are_expanded_before_validation()
    {
        _fixture.Variables["APP_VERSION"] = "2.0.0.7";
        var manifest = ManifestFixture.Valid();
        manifest["version"] = "${env:APP_VERSION}";

        Assert.That(_fixture.Load(manifest).Manifest!.App.Version.Text, Is.EqualTo("2.0.0.7"));
    }

    [Test]
    public void Several_problems_are_all_reported()
    {
        var manifest = ManifestFixture.Valid();
        manifest["id"] = "ScreenRec";
        manifest["targets"]![2]!["name"] = "windows-x64";
        manifest["macos"]!["identiy"] = "typo";

        var result = _fixture.Load(manifest);

        Assert.That(ManifestFixture.Codes(result), Is.EquivalentTo(new[]
        {
            ErrorCodes.ManifestUnknownProperty,
            ErrorCodes.ManifestInvalidValue,
            ErrorCodes.ManifestDuplicateTarget,
        }));
        Assert.That(result.Manifest, Is.Null);
    }
}
