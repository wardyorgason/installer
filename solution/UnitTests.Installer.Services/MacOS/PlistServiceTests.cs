using Installer.Dtos.Manifest;
using Installer.Services.MacOS;
using Installer.Services.Manifest;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.MacOS;

public class PlistServiceTests
{
    private readonly PlistService _service = new(new VersionService());

    private static AppInfo App => Fixtures.App() with { Id = "dev.screenrec.app", Name = "ScreenRec", Version = new AppVersion("1.0.0.12", [1, 0, 0, 12]) };

    [Test]
    public void Info_plist_with_caller_keys()
    {
        var extra = new PlistDictionary([
            new("LSMinimumSystemVersion", new PlistString("14.0")),
            new("NSMicrophoneUsageDescription", new PlistString("ScreenRec records your microphone <when> you turn it on & more.")),
            new("NSAppTransportSecurity", new PlistDictionary([new("NSAllowsLocalNetworking", new PlistBoolean(true))])),
            new("LSApplicationCategoryType", new PlistString("public.app-category.productivity")),
            new("Numbers", new PlistArray([new PlistInteger(1), new PlistBoolean(false)])),
            new("CFBundleDisplayName", new PlistString("Screen Rec")),
        ]);

        Golden.AssertMatches("Info.plist", _service.InfoPlist(App, "ScreenRec", "ScreenRec", extra));
    }

    [Test]
    public void Versions_are_mapped()
    {
        var plist = _service.InfoPlist(App, "ScreenRec", "ScreenRec", PlistDictionary.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(plist, Does.Contain("<key>CFBundleShortVersionString</key>\n  <string>1.0.0</string>"));
            Assert.That(plist, Does.Contain("<key>CFBundleVersion</key>\n  <string>1.0.0.12</string>"));
        });
    }

    [Test]
    public void Entitlements_plist() =>
        Golden.AssertMatches("entitlements.plist", _service.Entitlements([
            "com.apple.security.cs.allow-jit",
            "com.apple.security.cs.allow-unsigned-executable-memory",
            "com.apple.security.cs.disable-library-validation",
            "com.apple.security.device.audio-input",
        ]));
}
