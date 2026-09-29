using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;
using Installer.Services.MacOS;

namespace UnitTests.Installer.Services.MacOS;

public class EntitlementServiceTests
{
    private static readonly ProfileAnalysis Dotnet = new("App", [], new Dictionary<string, bool>
    {
        ["com.apple.security.cs.allow-jit"] = true,
        ["com.apple.security.cs.allow-unsigned-executable-memory"] = true,
        ["com.apple.security.cs.disable-library-validation"] = true,
    });

    private static MacOptions Options(Dictionary<string, bool> entitlements) =>
        new("ScreenRec Dev", entitlements, PlistDictionary.Empty, [MacOutput.Dmg], null);

    [Test]
    public void Defaults_plus_caller_entitlement()
    {
        var resolved = new EntitlementService().Resolve(Dotnet, Options(new() { ["com.apple.security.device.audio-input"] = true }));

        Assert.That(resolved, Is.EqualTo(new[]
        {
            "com.apple.security.cs.allow-jit",
            "com.apple.security.cs.allow-unsigned-executable-memory",
            "com.apple.security.cs.disable-library-validation",
            "com.apple.security.device.audio-input",
        }));
    }

    [Test]
    public void Caller_removes_a_default()
    {
        var resolved = new EntitlementService().Resolve(Dotnet, Options(new() { ["com.apple.security.cs.disable-library-validation"] = false }));

        Assert.That(resolved, Does.Not.Contain("com.apple.security.cs.disable-library-validation"));
        Assert.That(resolved, Has.Count.EqualTo(2));
    }
}
