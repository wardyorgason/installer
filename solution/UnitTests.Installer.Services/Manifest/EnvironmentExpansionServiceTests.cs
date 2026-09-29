using System.Text.Json.Nodes;
using Installer.Dao.Host;
using Installer.Dtos.Build;
using Installer.Services.Manifest;

namespace UnitTests.Installer.Services.Manifest;

public class EnvironmentExpansionServiceTests
{
    private readonly Dictionary<string, string> _variables = new() { ["APP_VERSION"] = "1.0.0.42", ["APP_DISPLAY_VERSION"] = "1.0.0-b42" };
    private EnvironmentExpansionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var environment = new Mock<IEnvironmentDao>();
        environment.Setup(e => e.GetVariable(It.IsAny<string>())).Returns((string name) => _variables.GetValueOrDefault(name));
        _service = new EnvironmentExpansionService(environment.Object);
    }

    [Test]
    public void Version_from_the_environment()
    {
        var problems = new List<Problem>();

        Assert.That(_service.Expand("${env:APP_VERSION}", "$.version", problems), Is.EqualTo("1.0.0.42"));
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void Reference_inside_a_longer_string()
    {
        var problems = new List<Problem>();

        Assert.That(
            _service.Expand("dist/App-${env:APP_DISPLAY_VERSION}-win-x64.zip", "$.targets[0].payload", problems),
            Is.EqualTo("dist/App-1.0.0-b42-win-x64.zip"));
    }

    [Test]
    public void Several_references_in_one_string()
    {
        Assert.That(_service.Expand("${env:APP_VERSION}/${env:APP_DISPLAY_VERSION}", "$.x", []), Is.EqualTo("1.0.0.42/1.0.0-b42"));
    }

    [Test]
    public void Unset_variable_is_an_error_naming_it()
    {
        var problems = new List<Problem>();

        _service.Expand("${env:NOTARY_PROFILE}", "$.macos.notarize.keychainProfile", problems);

        Assert.That(problems, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(problems[0].Code, Is.EqualTo(ErrorCodes.ManifestEnvUnset));
            Assert.That(problems[0].Message, Does.Contain("NOTARY_PROFILE").And.Contain("$.macos.notarize.keychainProfile"));
        });
    }

    [TestCase("$HOME/app")]
    [TestCase("${APP_VERSION}")]
    [TestCase("%APP_VERSION%")]
    [TestCase("${env:}")]
    [TestCase("${ENV:APP_VERSION}")]
    public void Other_syntax_is_left_alone(string value)
    {
        var problems = new List<Problem>();

        Assert.That(_service.Expand(value, "$.x", problems), Is.EqualTo(value));
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void Expands_every_string_in_the_tree_but_not_property_names()
    {
        var root = JsonNode.Parse("""
            { "version": "${env:APP_VERSION}", "${env:APP_VERSION}": 1,
              "targets": [ { "payload": "a-${env:APP_DISPLAY_VERSION}.zip" } ], "tags": [ "${env:APP_VERSION}" ] }
            """)!;

        _service.ExpandAll(root, []);

        Assert.Multiple(() =>
        {
            Assert.That(root["version"]!.GetValue<string>(), Is.EqualTo("1.0.0.42"));
            Assert.That(root.AsObject().ContainsKey("${env:APP_VERSION}"), Is.True);
            Assert.That(root["targets"]![0]!["payload"]!.GetValue<string>(), Is.EqualTo("a-1.0.0-b42.zip"));
            Assert.That(root["tags"]![0]!.GetValue<string>(), Is.EqualTo("1.0.0.42"));
        });
    }
}
