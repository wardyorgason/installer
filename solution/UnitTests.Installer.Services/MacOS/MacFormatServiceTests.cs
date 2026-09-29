using Installer.Dao.MacOS;
using Installer.Dao.Workspace;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;
using Installer.Dtos.Tools;
using Installer.Services.MacOS;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Installer.Services.MacOS;

public class MacFormatServiceTests
{
    private readonly List<string> _calls = [];
    private Mock<IKeychainDao> _keychain = null!;
    private Mock<INotarizationService> _notarization = null!;
    private MacFormatService _format = null!;

    [SetUp]
    public void SetUp()
    {
        _calls.Clear();
        _keychain = new Mock<IKeychainDao>();
        var bundles = new Mock<IAppBundleService>();
        bundles.Setup(b => b.Build(It.IsAny<TargetContext>(), It.IsAny<string>())).Callback(() => _calls.Add("bundle"));
        var entitlements = new Mock<IEntitlementService>();
        entitlements.Setup(e => e.Resolve(It.IsAny<ProfileAnalysis>(), It.IsAny<MacOptions>())).Returns(["e"]);
        var plists = new Mock<IPlistService>();
        plists.Setup(p => p.Entitlements(It.IsAny<IReadOnlyList<string>>())).Returns("ent");
        var signing = new Mock<IMacSigningService>();
        signing.Setup(s => s.SignBundleAsync(It.IsAny<TargetContext>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("sign app")).Returns(Task.CompletedTask);
        _notarization = new Mock<INotarizationService>();
        _notarization.Setup(n => n.NotarizeAsync(It.IsAny<TargetContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TargetContext, string, CancellationToken>((_, file, _) => _calls.Add("notarize " + Path.GetFileName(file))).Returns(Task.CompletedTask);
        var outputs = new Mock<IMacOutputService>();
        outputs.Setup(o => o.CreateDmgAsync(It.IsAny<TargetContext>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TargetContext, string, string, CancellationToken>((_, _, dmg, _) => _calls.Add("dmg " + Path.GetFileName(dmg))).Returns(Task.CompletedTask);
        outputs.Setup(o => o.CreateZipAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, zip, _) => _calls.Add("zip " + Path.GetFileName(zip))).Returns(Task.CompletedTask);
        var notary = new Mock<INotaryDao>();
        notary.Setup(n => n.StapleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((path, _) => _calls.Add("staple " + Path.GetFileName(path))).Returns(Task.CompletedTask);
        _format = new MacFormatService(_keychain.Object, bundles.Object, entitlements.Object, plists.Object, signing.Object, _notarization.Object, outputs.Object, notary.Object, new Mock<IWorkspaceDao>().Object);
    }

    [Test]
    public void Requires_a_mac_host() => Assert.That(_format.RequiredHost, Is.EqualTo(TargetOs.MacOS));

    [Test]
    public async Task Identity_missing_lists_the_available_ones()
    {
        _keychain.Setup(k => k.FindCodeSigningIdentitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new SigningIdentity("AB", "Other Identity")]);

        var problems = await _format.PreflightAsync(MacBundleAndSigningTests.Context().Target, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(problems.Single().Code, Is.EqualTo(ErrorCodes.IdentityMissing));
            Assert.That(problems.Single().Message, Does.Contain("\"ScreenRec Dev\"").And.Contain("\"Other Identity\""));
        });
    }

    [Test]
    public async Task Installed_identity_passes()
    {
        _keychain.Setup(k => k.FindCodeSigningIdentitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new SigningIdentity("AB", "ScreenRec Dev")]);

        Assert.That(await _format.PreflightAsync(MacBundleAndSigningTests.Context().Target, CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task Default_output_is_a_signed_dmg()
    {
        var produced = await _format.BuildAsync(MacBundleAndSigningTests.Context(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_calls, Is.EqualTo(new[] { "bundle", "sign app", "dmg ScreenRec-1.0.0-macos-arm64.dmg" }));
            Assert.That(produced, Is.EqualTo(new[] { new ProducedFile(ArtifactKind.Dmg, Path.Combine("/w", "ScreenRec-1.0.0-macos-arm64.dmg")) }));
        });
    }

    [Test]
    public async Task Dmg_and_zip_without_notarization()
    {
        var produced = await _format.BuildAsync(MacBundleAndSigningTests.Context(outputs: [MacOutput.Dmg, MacOutput.Zip]), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_calls, Is.EqualTo(new[] { "bundle", "sign app", "dmg ScreenRec-1.0.0-macos-arm64.dmg", "zip ScreenRec-1.0.0-macos-arm64.zip" }));
            Assert.That(produced.Select(p => p.Kind), Is.EqualTo(new[] { ArtifactKind.Dmg, ArtifactKind.Zip }));
        });
    }

    [Test]
    public async Task Notarized_dmg_staples_both_and_zips_the_stapled_app()
    {
        await _format.BuildAsync(MacBundleAndSigningTests.Context("Developer ID Application: X (T)", "notary", MacOutput.Dmg, MacOutput.Zip), CancellationToken.None);

        Assert.That(_calls, Is.EqualTo(new[]
        {
            "bundle", "sign app", "dmg ScreenRec-1.0.0-macos-arm64.dmg",
            "notarize ScreenRec-1.0.0-macos-arm64.dmg",
            "staple ScreenRec-1.0.0-macos-arm64.dmg", "staple ScreenRec.app",
            "zip ScreenRec-1.0.0-macos-arm64.zip",
        }));
    }

    [Test]
    public async Task Notarized_zip_only_submits_the_zip_then_rezips_the_stapled_app()
    {
        var produced = await _format.BuildAsync(MacBundleAndSigningTests.Context("Developer ID Application: X (T)", "notary", MacOutput.Zip), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_calls, Is.EqualTo(new[]
            {
                "bundle", "sign app", "zip ScreenRec-1.0.0-macos-arm64.zip", "notarize ScreenRec-1.0.0-macos-arm64.zip", "staple ScreenRec.app", "zip ScreenRec-1.0.0-macos-arm64.zip",
            }));
            Assert.That(produced.Single().Kind, Is.EqualTo(ArtifactKind.Zip));
        });
    }

    [Test]
    public void Rejected_notarization_fails_the_target()
    {
        _notarization.Setup(n => n.NotarizeAsync(It.IsAny<TargetContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BuildFailedException(ErrorCodes.NotarizeRejected, "Invalid"));

        Assert.ThrowsAsync<BuildFailedException>(() => _format.BuildAsync(MacBundleAndSigningTests.Context("Developer ID Application: X (T)", "notary"), CancellationToken.None));
        Assert.That(_calls, Does.Not.Contain("staple ScreenRec.app"));
    }
}

public class NotarizationAndOutputServiceTests
{
    [Test]
    public void Rejected_submission_saves_the_log_and_names_it()
    {
        var notary = new Mock<INotaryDao>();
        notary.Setup(n => n.SubmitAsync("/w/App.dmg", "notary", It.IsAny<CancellationToken>())).ReturnsAsync(new NotaryResult("1234", "Invalid", "Processing complete"));
        var context = MacBundleAndSigningTests.Context("Developer ID Application: X (T)", "notary");

        var ex = Assert.ThrowsAsync<BuildFailedException>(() => new NotarizationService(notary.Object, NullLogger<NotarizationService>.Instance).NotarizeAsync(context, "/w/App.dmg", CancellationToken.None));

        var log = Path.Combine("/w", "notarization-log.json");
        notary.Verify(n => n.SaveLogAsync("1234", "notary", log, It.IsAny<CancellationToken>()));
        Assert.Multiple(() =>
        {
            Assert.That(ex!.Problem.Code, Is.EqualTo(ErrorCodes.NotarizeRejected));
            Assert.That(ex.Message, Does.Contain("Invalid").And.Contain(log));
        });
    }

    [Test]
    public async Task Accepted_submission_passes()
    {
        var notary = new Mock<INotaryDao>();
        notary.Setup(n => n.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NotaryResult("1234", "Accepted", null));

        await new NotarizationService(notary.Object, NullLogger<NotarizationService>.Instance).NotarizeAsync(MacBundleAndSigningTests.Context("Developer ID Application: X (T)", "notary"), "/w/App.dmg", CancellationToken.None);

        notary.Verify(n => n.SaveLogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Dmg_gets_an_applications_link_and_is_signed()
    {
        var calls = new List<string>();
        var diskImages = new Mock<IDiskImageDao>();
        diskImages.Setup(d => d.CreateAsync("ScreenRec", "/w/stage", "/w/App.dmg", It.IsAny<CancellationToken>())).Callback(() => calls.Add("hdiutil")).Returns(Task.CompletedTask);
        var signing = new Mock<IMacSigningService>();
        signing.Setup(s => s.SignArtifactAsync(It.IsAny<TargetContext>(), "/w/App.dmg", It.IsAny<CancellationToken>())).Callback(() => calls.Add("sign")).Returns(Task.CompletedTask);
        var workspace = new Mock<IWorkspaceDao>();
        workspace.Setup(w => w.CreateSymlink(Path.Combine("/w/stage", "Applications"), "/Applications")).Callback(() => calls.Add("link"));

        await new MacOutputService(diskImages.Object, new Mock<IMacArchiveDao>().Object, signing.Object, workspace.Object, NullLogger<MacOutputService>.Instance)
            .CreateDmgAsync(MacBundleAndSigningTests.Context(), "/w/stage", "/w/App.dmg", CancellationToken.None);

        Assert.That(calls, Is.EqualTo(new[] { "link", "hdiutil", "sign" }));
    }
}
