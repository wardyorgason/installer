using System.Text.Json.Nodes;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Services.Manifest;

namespace UnitTests.Installer.Services.Manifest;

public class ManifestReaderTests
{
    private readonly ManifestReader _reader = new();

    private (ManifestDocument Document, List<Problem> Problems) Read(string json)
    {
        var problems = new List<Problem>();
        return (_reader.Read(JsonNode.Parse(json)!, problems), problems);
    }

    [Test]
    public void Misspelled_property_is_reported_with_its_path()
    {
        var (_, problems) = Read("""{ "macos": { "identiy": "ScreenRec Dev" } }""");

        Assert.That(problems, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(problems[0].Code, Is.EqualTo(ErrorCodes.ManifestUnknownProperty));
            Assert.That(problems[0].Message, Does.Contain("$.macos.identiy"));
        });
    }

    [Test]
    public void Unknown_properties_in_targets_are_reported_with_their_index()
    {
        var (_, problems) = Read("""{ "targets": [ { "os": "linux" }, { "platform": "linux" } ] }""");

        Assert.That(problems.Single().Message, Does.Contain("$.targets[1].platform"));
    }

    [Test]
    public void Wrong_types_are_reported_and_reading_continues()
    {
        var (document, problems) = Read("""{ "schemaVersion": "1", "id": 5, "name": "App", "targets": {} }""");

        Assert.Multiple(() =>
        {
            Assert.That(problems.Select(p => p.Code), Is.All.EqualTo(ErrorCodes.ManifestWrongType));
            Assert.That(problems.Select(p => p.Message), Has.Some.Contain("$.schemaVersion").And.Some.Contain("$.id").And.Some.Contain("$.targets"));
            Assert.That(document.Name, Is.EqualTo("App"));
        });
    }

    [Test]
    public void Null_is_treated_as_absent()
    {
        var (document, problems) = Read("""{ "executable": null, "windows": { "signCommand": null } }""");

        Assert.Multiple(() =>
        {
            Assert.That(problems, Is.Empty);
            Assert.That(document.Executable, Is.Null);
            Assert.That(document.Windows!.SignCommand, Is.Null);
        });
    }

    [Test]
    public void Reads_every_field()
    {
        var (document, problems) = Read("""
            {
              "schemaVersion": 1, "id": "a.b", "name": "N", "version": "1.0.0", "displayVersion": "1.0.0-x",
              "publisher": "P", "description": "D", "icon": "i.png", "profile": "generic", "executable": "bin/tool",
              "macos": { "identity": "I", "entitlements": { "e": true }, "outputs": ["dmg", "zip"], "notarize": { "keychainProfile": "k" } },
              "windows": { "signCommand": ["sign", "{file}"] },
              "linux": { "categories": ["AudioVideo"] },
              "targets": [ { "name": "t", "os": "linux", "arch": "arm64", "payload": "p", "linux": { "categories": ["Video"] } } ]
            }
            """);

        Assert.That(problems, Is.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(document.SchemaVersion, Is.EqualTo(1));
            Assert.That(document.Executable, Is.EqualTo("bin/tool"));
            Assert.That(document.MacOS!.Entitlements!["e"], Is.True);
            Assert.That(document.MacOS.Outputs, Is.EqualTo(new[] { "dmg", "zip" }));
            Assert.That(document.MacOS.Notarize!.KeychainProfile, Is.EqualTo("k"));
            Assert.That(document.Windows!.SignCommand, Is.EqualTo(new[] { "sign", "{file}" }));
            Assert.That(document.Targets!.Single().Path, Is.EqualTo("$.targets[0]"));
            Assert.That(document.Targets!.Single().Linux!.Categories, Is.EqualTo(new[] { "Video" }));
        });
    }

    [Test]
    public void Info_plist_values_map_to_plist_types_in_order()
    {
        var (document, problems) = Read("""
            { "macos": { "infoPlist": { "S": "text", "B": true, "I": 14, "A": ["x", 1], "D": { "NSAllowsLocalNetworking": true } } } }
            """);

        Assert.That(problems, Is.Empty);
        var entries = document.MacOS!.InfoPlist!.Entries;
        Assert.Multiple(() =>
        {
            Assert.That(entries.Select(e => e.Key), Is.EqualTo(new[] { "S", "B", "I", "A", "D" }));
            Assert.That(entries[0].Value, Is.EqualTo(new PlistString("text")));
            Assert.That(entries[1].Value, Is.EqualTo(new PlistBoolean(true)));
            Assert.That(entries[2].Value, Is.EqualTo(new PlistInteger(14)));
            Assert.That(((PlistArray)entries[3].Value).Items, Is.EqualTo(new PlistValue[] { new PlistString("x"), new PlistInteger(1) }));
            Assert.That(((PlistDictionary)entries[4].Value).Entries.Single().Value, Is.EqualTo(new PlistBoolean(true)));
        });
    }

    [TestCase("""{ "macos": { "infoPlist": { "X": 1.5 } } }""", "$.macos.infoPlist.X")]
    [TestCase("""{ "macos": { "infoPlist": { "X": null } } }""", "$.macos.infoPlist.X")]
    [TestCase("""{ "macos": { "entitlements": { "e": "yes" } } }""", "$.macos.entitlements.e")]
    [TestCase("""{ "macos": { "outputs": ["dmg", 2] } }""", "$.macos.outputs[1]")]
    public void Invalid_nested_values_are_reported(string json, string path)
    {
        var (_, problems) = Read(json);

        Assert.That(problems.Single().Message, Does.Contain(path));
    }

    [Test]
    public void Root_must_be_an_object()
    {
        var (_, problems) = Read("[]");

        Assert.That(problems.Single().Code, Is.EqualTo(ErrorCodes.ManifestWrongType));
    }
}
