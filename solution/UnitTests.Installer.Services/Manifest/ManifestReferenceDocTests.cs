using Installer.Dtos.Manifest;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.Manifest;

/// <summary>Keeps docs/manifest-reference.md honest: its full example must load without problems.</summary>
public class ManifestReferenceDocTests
{
    [Test]
    public void Full_example_in_the_reference_validates()
    {
        var doc = File.ReadAllText(Path.Combine(RepoPaths.Root, "docs", "manifest-reference.md"));
        var start = doc.IndexOf("```jsonc", doc.IndexOf("## Full example", StringComparison.Ordinal), StringComparison.Ordinal) + "```jsonc".Length;
        var example = doc[start..doc.IndexOf("```", start, StringComparison.Ordinal)];

        var fixture = new ManifestFixture();
        fixture.Variables["APP_VERSION"] = "1.0.0.12";
        fixture.Variables["APP_DISPLAY_VERSION"] = "1.0.0-b12";
        fixture.Payloads.Setup(p => p.Probe(It.Is<string>(path => path.EndsWith(".zip", StringComparison.Ordinal)))).Returns(PayloadKind.Zip);

        var result = fixture.Load(example);

        Assert.That(result.Problems, Is.Empty, ManifestFixture.Messages(result));
        Assert.Multiple(() =>
        {
            Assert.That(result.Manifest!.Targets.Select(t => t.Name), Is.EqualTo(new[] { "windows-x64", "windows-x64-selfcontained", "macos-arm64", "linux-x64" }));
            Assert.That(result.Manifest.Targets[0].PayloadPath, Does.EndWith("ScreenRec-1.0.0-b12-win-x64.zip"));
            Assert.That(result.Manifest.Targets[0].PayloadKind, Is.EqualTo(PayloadKind.Zip));
        });
    }
}
