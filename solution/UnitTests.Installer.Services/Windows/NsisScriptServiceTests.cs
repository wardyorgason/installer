using Installer.Services.Windows;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.Windows;

public class NsisScriptServiceTests
{
    private readonly NsisScriptService _service = new();

    private static NsisScriptRequest Request(string? signHook = null) => new(
        "dev.screenrec.app",
        "ScreenRec",
        "Ward Yorgason",
        "Screen recorder",
        "1.0.0-b12",
        "1.0.0.12",
        "/w/payload",
        "ScreenRec.exe",
        "/w/app.ico",
        "/w/ScreenRec-1.0.0-b12-windows-x64-setup.exe",
        2048,
        signHook);

    [Test]
    public void Script_without_signing() => Golden.AssertMatches("installer.nsi", _service.Generate(Request()));

    [Test]
    public void Script_with_signing_hooks()
    {
        var hook = _service.SignHook(["/usr/local/share/dotnet/dotnet", "/opt/installer/Installer.Cli.dll"], "/w/sign-command.json");

        Golden.AssertMatches("installer-signed.nsi", _service.Generate(Request(hook)));
    }

    [Test]
    public void Sign_hook_calls_back_into_sign_file_with_the_nsis_placeholder()
    {
        var hook = _service.SignHook(["/opt/installer/Installer.Cli"], "/w/sign command.json");

        Assert.That(hook, Is.EqualTo("\"/opt/installer/Installer.Cli\" sign-file \"%1\" --command-json \"/w/sign command.json\""));
    }

    [Test]
    public void Runtime_strings_escape_dollar_and_quote_and_version_info_escapes_only_the_quote()
    {
        var script = _service.Generate(Request() with { Publisher = "Ward's $Test \"Co\"" });

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("\"Publisher\" \"Ward's $$Test $\\\"Co$\\\"\""));
            Assert.That(script, Does.Contain("\"CompanyName\" \"Ward's $Test $\\\"Co$\\\"\""));
        });
    }

    [Test]
    public void Nested_main_executable_uses_backslashes()
    {
        var script = _service.Generate(Request() with { MainExecutable = "bin/tool.exe" });

        Assert.That(script, Does.Contain("CreateShortcut \"$SMPROGRAMS\\ScreenRec.lnk\" \"$INSTDIR\\bin\\tool.exe\""));
    }
}
