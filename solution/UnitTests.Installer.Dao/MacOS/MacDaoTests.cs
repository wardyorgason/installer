using Installer.Dao.MacOS;
using Installer.Dao.Tools;
using Installer.Dtos.Build;
using Installer.Dtos.Signing;
using Installer.Dtos.Tools;

namespace UnitTests.Installer.Dao.MacOS;

public class MacDaoTests
{
    private readonly List<ToolCommand> _commands = [];
    private Mock<IToolDao> _tools = null!;
    private ProcessResult _result = new(0, string.Empty, string.Empty);

    [SetUp]
    public void SetUp()
    {
        _commands.Clear();
        _result = new ProcessResult(0, string.Empty, string.Empty);
        _tools = new Mock<IToolDao>();
        _tools.Setup(t => t.RunAsync(It.IsAny<ToolCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ToolCommand, CancellationToken>((c, _) => _commands.Add(c))
            .ReturnsAsync(() => _result);
    }

    private string Line(int index = -1) => string.Join(' ', new[] { _commands[index < 0 ? _commands.Count + index : index].Tool }.Concat(_commands[index < 0 ? _commands.Count + index : index].Arguments));

    [Test]
    public async Task Keychain_lists_valid_code_signing_identities()
    {
        _result = new ProcessResult(0, """
              1) 0123456789ABCDEF0123456789ABCDEF01234567 "ScreenRec Dev"
              2) 89ABCDEF0123456789ABCDEF0123456789ABCDEF "Developer ID Application: Example (TEAMID)"
                 2 valid identities found
            """, string.Empty);

        var identities = await new KeychainDao(_tools.Object).FindCodeSigningIdentitiesAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(Line(), Is.EqualTo("security find-identity -v -p codesigning"));
            Assert.That(identities.Select(i => i.Name), Is.EqualTo(new[] { "ScreenRec Dev", "Developer ID Application: Example (TEAMID)" }));
            Assert.That(identities[0].Hash, Is.EqualTo("0123456789ABCDEF0123456789ABCDEF01234567"));
        });
    }

    [Test]
    public void Keychain_with_no_identities() => Assert.That(KeychainDao.Parse("     0 valid identities found\n"), Is.Empty);

    [Test]
    public async Task Codesign_self_signed_file()
    {
        await new CodesignDao(_tools.Object).SignAsync("/a/App.dll", new CodesignSigningOptions("ScreenRec Dev", HardenedRuntime: true, SecureTimestamp: false), CancellationToken.None);

        Assert.That(Line(), Is.EqualTo("codesign --force --sign ScreenRec Dev --options runtime --timestamp=none /a/App.dll"));
    }

    [Test]
    public async Task Codesign_developer_id_bundle_with_identifier_and_entitlements()
    {
        var options = new CodesignSigningOptions("Developer ID Application: X (T)", true, true, "dev.app", "/w/entitlements.plist");

        await new CodesignDao(_tools.Object).SignAsync("/w/App.app", options, CancellationToken.None);

        Assert.That(_commands[0].Arguments, Is.EqualTo(new[]
        {
            "--force", "--sign", "Developer ID Application: X (T)", "--options", "runtime", "--timestamp",
            "--identifier", "dev.app", "--entitlements", "/w/entitlements.plist", "/w/App.app",
        }));
    }

    [Test]
    public void Codesign_failure_is_a_signing_failure_with_its_output()
    {
        _result = new ProcessResult(1, string.Empty, "errSecInternalComponent");

        var ex = Assert.ThrowsAsync<BuildFailedException>(() => new CodesignDao(_tools.Object).SignAsync("/a", new CodesignSigningOptions("X", true, false), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Problem.Code, Is.EqualTo(ErrorCodes.SignFailed));
            Assert.That(ex.Message, Does.Contain("errSecInternalComponent"));
        });
    }

    [Test]
    public async Task Verify_clear_attributes_disk_image_and_zip()
    {
        await new CodesignDao(_tools.Object).VerifyAsync("/w/App.app", CancellationToken.None);
        await new CodesignDao(_tools.Object).ClearExtendedAttributesAsync("/w/App.app", CancellationToken.None);
        await new DiskImageDao(_tools.Object).CreateAsync("My App", "/w/stage", "/w/App.dmg", CancellationToken.None);
        await new MacArchiveDao(_tools.Object).ZipAsync("/w/stage/App.app", "/w/App.zip", CancellationToken.None);

        Assert.That(_commands.Select((_, i) => Line(i)), Is.EqualTo(new[]
        {
            "codesign --verify --strict --deep --verbose=2 /w/App.app",
            "xattr -cr /w/App.app",
            "hdiutil create -volname My App -srcfolder /w/stage -ov -format UDZO -fs HFS+ /w/App.dmg",
            "ditto -c -k --sequesterRsrc --keepParent /w/stage/App.app /w/App.zip",
        }));
    }

    [TestCase("Accepted")]
    [TestCase("Invalid")]
    public async Task Notary_submit_waits_and_reads_the_verdict(string status)
    {
        _result = new ProcessResult(status == "Accepted" ? 0 : 1, $$"""{"id":"1234-abcd","status":"{{status}}","message":"Processing complete"}""", string.Empty);

        var result = await new NotaryDao(_tools.Object).SubmitAsync("/w/App.dmg", "notary", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(Line(), Is.EqualTo("xcrun notarytool submit /w/App.dmg --keychain-profile notary --wait --timeout 30m --output-format json"));
            Assert.That(result, Is.EqualTo(new NotaryResult("1234-abcd", status, "Processing complete")));
        });
    }

    [Test]
    public void Notary_submit_without_a_verdict_fails_with_the_output()
    {
        _result = new ProcessResult(69, string.Empty, "Error: No Keychain password item found for profile: notary");

        var ex = Assert.ThrowsAsync<BuildFailedException>(() => new NotaryDao(_tools.Object).SubmitAsync("/w/App.dmg", "notary", CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("No Keychain password item"));
    }

    [Test]
    public async Task Notary_log_and_staple()
    {
        await new NotaryDao(_tools.Object).SaveLogAsync("1234", "notary", "/w/log.json", CancellationToken.None);
        await new NotaryDao(_tools.Object).StapleAsync("/w/App.dmg", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(Line(0), Is.EqualTo("xcrun notarytool log 1234 --keychain-profile notary /w/log.json"));
            Assert.That(Line(1), Is.EqualTo("xcrun stapler staple /w/App.dmg"));
        });
    }

    [Test]
    public void Codesign_timeout_explains_the_keychain_dialog()
    {
        _tools.Setup(t => t.RunAsync(It.Is<ToolCommand>(c => c.Tool == "codesign"), It.IsAny<CancellationToken>()))
            .Callback<ToolCommand, CancellationToken>((c, _) => _commands.Add(c))
            .ThrowsAsync(new BuildFailedException(ErrorCodes.ToolTimeout, "codesign did not finish"));

        var ex = Assert.ThrowsAsync<BuildFailedException>(() => new CodesignDao(_tools.Object).SignAsync("/a", new CodesignSigningOptions("X", true, false), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(_commands.Single().Timeout, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(ex!.Problem.Code, Is.EqualTo(ErrorCodes.SignFailed));
            Assert.That(ex.Message, Does.Contain("keychain access dialog").And.Contain("set-key-partition-list"));
        });
    }
}
