using Installer.Services.Manifest;

namespace UnitTests.Installer.Services.Manifest;

public class VersionServiceTests
{
    private readonly VersionService _service = new();

    [TestCase("1.0.0.123", new[] { 1, 0, 0, 123 })]
    [TestCase("2.1.0", new[] { 2, 1, 0 })]
    [TestCase("0.0.0.65535", new[] { 0, 0, 0, 65535 })]
    public void Parses_numeric_versions(string text, int[] parts)
    {
        var version = _service.Parse(text, out var error);

        Assert.That(error, Is.Null);
        Assert.That(version!.Parts, Is.EqualTo(parts));
    }

    [Test]
    public void Semantic_version_with_suffix_is_rejected_pointing_to_display_version()
    {
        Assert.That(_service.Parse("1.2.0-beta.1", out var error), Is.Null);
        Assert.That(error, Does.Contain("numbers").And.Contain("displayVersion"));
    }

    [Test]
    public void Part_out_of_range_is_rejected()
    {
        Assert.That(_service.Parse("1.0.0.70000", out var error), Is.Null);
        Assert.That(error, Does.Contain("between 0 and 65535"));
    }

    [TestCase("1.0")]
    [TestCase("1.0.0.0.0")]
    [TestCase("1..0")]
    [TestCase("1.0.-1")]
    [TestCase("v1.0.0")]
    [TestCase("1.0.0.99999999999")]
    public void Malformed_versions_are_rejected(string text)
    {
        Assert.That(_service.Parse(text, out var error), Is.Null);
        Assert.That(error, Is.Not.Null);
    }

    [TestCase("1.0.0.12", "1.0.0", "1.0.0.12", "1.0.0.12")]
    [TestCase("2.1.0", "2.1.0", "2.1.0", "2.1.0.0")]
    public void Maps_to_platform_versions(string text, string shortVersion, string full, string fourPart)
    {
        var version = _service.Parse(text, out _)!;

        Assert.Multiple(() =>
        {
            Assert.That(_service.ShortVersion(version), Is.EqualTo(shortVersion));
            Assert.That(_service.FullVersion(version), Is.EqualTo(full));
            Assert.That(_service.FourPartVersion(version), Is.EqualTo(fourPart));
        });
    }
}
