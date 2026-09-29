using Installer.Dao.MacOS;
using Installer.Dao.Workspace;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;
using Installer.Dtos.Signing;
using Installer.Services.Imaging;
using Installer.Services.MacOS;
using Installer.Services.Payload;
using Installer.Services.Signing;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.MacOS;

public class MacBundleAndSigningTests
{
    private const string App = "/w/stage/ScreenRec.app";

    internal static TargetContext Context(string identity = "ScreenRec Dev", string? notary = null, params MacOutput[] outputs)
    {
        var app = Fixtures.App() with { Id = "dev.screenrec.app", Name = "ScreenRec", IconPath = "/icon.png" };
        var mac = new MacOptions(identity, new Dictionary<string, bool>(), PlistDictionary.Empty, outputs.Length == 0 ? [MacOutput.Dmg] : outputs, notary);
        var target = Fixtures.Target(TargetOs.MacOS, TargetArch.Arm64, "macos-arm64") with { MacOS = mac };
        return new TargetContext(app, target, new PreparedPayload("/w/payload", ["ScreenRec"]), new ProfileAnalysis("ScreenRec", [], new Dictionary<string, bool>()), "/w", "/out", "ScreenRec-1.0.0-macos-arm64");
    }

    [Test]
    public void Payload_folders_move_to_resources_behind_relative_symlinks()
    {
        var workspace = new Mock<IWorkspaceDao>();
        workspace.Setup(w => w.GetEntries(Path.Combine(App, "Contents", "MacOS"))).Returns([
            new FileSystemEntry(Path.Combine(App, "Contents", "MacOS", "ScreenRec"), FileSystemEntryKind.File),
            new FileSystemEntry(Path.Combine(App, "Contents", "MacOS", "ScreenRec.dll"), FileSystemEntryKind.File),
            new FileSystemEntry(Path.Combine(App, "Contents", "MacOS", "wwwroot"), FileSystemEntryKind.Directory),
        ]);
        var icons = new Mock<IIconService>();
        icons.Setup(i => i.CreateIcns("/icon.png")).Returns([4]);
        var plists = new Mock<IPlistService>();
        plists.Setup(p => p.InfoPlist(It.IsAny<AppInfo>(), "ScreenRec", "ScreenRec", It.IsAny<PlistDictionary>())).Returns("plist");

        new AppBundleService(workspace.Object, plists.Object, icons.Object).Build(Context(), App);

        workspace.Verify(w => w.MoveDirectory("/w/payload", Path.Combine(App, "Contents", "MacOS")));
        workspace.Verify(w => w.MoveDirectory(Path.Combine(App, "Contents", "MacOS", "wwwroot"), Path.Combine(App, "Contents", "Resources", "wwwroot")));
        workspace.Verify(w => w.CreateRelativeSymlink(Path.Combine(App, "Contents", "MacOS", "wwwroot"), "../Resources/wwwroot"));
        workspace.Verify(w => w.MoveDirectory(Path.Combine(App, "Contents", "MacOS", "ScreenRec.dll"), It.IsAny<string>()), Times.Never);
        workspace.Verify(w => w.WriteBytes(Path.Combine(App, "Contents", "Resources", "ScreenRec.icns"), It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 4 }))));
        workspace.Verify(w => w.WriteText(Path.Combine(App, "Contents", "Info.plist"), "plist"));
    }

    [Test]
    public void Payload_folder_named_like_the_icon_file()
    {
        var workspace = new Mock<IWorkspaceDao>();
        workspace.Setup(w => w.GetEntries(It.IsAny<string>())).Returns([new FileSystemEntry(Path.Combine(App, "Contents", "MacOS", "ScreenRec.icns"), FileSystemEntryKind.Directory)]);

        var ex = Assert.Throws<BuildFailedException>(() => new AppBundleService(workspace.Object, new Mock<IPlistService>().Object, new Mock<IIconService>().Object).Build(Context(), App));

        Assert.That(ex!.Problem.Code, Is.EqualTo(ErrorCodes.BundleNameCollision));
    }

    private static (MacSigningService Service, List<string> Calls) Signing(params (string Path, FileSystemEntryKind Kind, bool MachO)[] tree)
    {
        var calls = new List<string>();
        var signer = new Mock<ICodesignFileSigner>();
        signer.Setup(s => s.SignAsync(It.IsAny<string>(), It.IsAny<SigningOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, SigningOptions, CancellationToken>((path, options, _) =>
            {
                var o = (CodesignSigningOptions)options;
                calls.Add($"sign {Path.GetRelativePath(App, path)} runtime={o.HardenedRuntime} timestamp={o.SecureTimestamp} id={o.Identifier} ent={o.EntitlementsPath}");
            })
            .Returns(Task.CompletedTask);
        var codesign = new Mock<ICodesignDao>();
        codesign.Setup(c => c.ClearExtendedAttributesAsync(App, It.IsAny<CancellationToken>())).Callback(() => calls.Add("xattr")).Returns(Task.CompletedTask);
        codesign.Setup(c => c.VerifyAsync(App, It.IsAny<CancellationToken>())).Callback(() => calls.Add("verify")).Returns(Task.CompletedTask);
        var workspace = new Mock<IWorkspaceDao>();
        workspace.Setup(w => w.ListTree(App)).Returns(tree.Select(t => (t.Path, t.Kind)).ToList());
        workspace.Setup(w => w.ReadHeader(It.IsAny<string>(), It.IsAny<int>()))
            .Returns((string path, int _) => tree.Any(t => path.EndsWith(t.Path, StringComparison.Ordinal) && t.MachO) ? Fixtures.Header("macho-arm64-apphost") : "text"u8.ToArray());
        return (new MacSigningService(signer.Object, codesign.Object, workspace.Object, new BinaryInspectionService(new Mock<global::Installer.Dao.Payload.IPayloadDao>().Object), NullLogger<MacSigningService>.Instance), calls);
    }

    private static readonly (string, FileSystemEntryKind, bool)[] Tree =
    [
        ("Contents", FileSystemEntryKind.Directory, false),
        ("Contents/Info.plist", FileSystemEntryKind.File, false),
        ("Contents/MacOS", FileSystemEntryKind.Directory, false),
        ("Contents/MacOS/ScreenRec", FileSystemEntryKind.File, true),
        ("Contents/MacOS/ScreenRec.dll", FileSystemEntryKind.File, false),
        ("Contents/MacOS/libSkiaSharp.dylib", FileSystemEntryKind.File, true),
        ("Contents/MacOS/wwwroot", FileSystemEntryKind.SymbolicLink, false),
        ("Contents/Resources", FileSystemEntryKind.Directory, false),
        ("Contents/Resources/ScreenRec.icns", FileSystemEntryKind.File, false),
        ("Contents/Resources/runtimes/osx-arm64/native/libe_sqlite3.dylib", FileSystemEntryKind.File, true),
        ("Contents/Resources/wwwroot/index.html", FileSystemEntryKind.File, false),
    ];

    [Test]
    public async Task Self_signed_identity_signs_inside_out_without_timestamp()
    {
        var (service, calls) = Signing(Tree);

        await service.SignBundleAsync(Context(), App, "/w/entitlements.plist", CancellationToken.None);

        Assert.That(calls, Is.EqualTo(new[]
        {
            "xattr",
            "sign Contents/MacOS/ScreenRec.dll runtime=True timestamp=False id= ent=",
            "sign Contents/MacOS/libSkiaSharp.dylib runtime=True timestamp=False id= ent=",
            "sign Contents/Resources/runtimes/osx-arm64/native/libe_sqlite3.dylib runtime=True timestamp=False id= ent=",
            "sign . runtime=True timestamp=False id=dev.screenrec.app ent=/w/entitlements.plist",
            "verify",
        }));
    }

    [Test]
    public async Task Developer_id_identity_adds_the_secure_timestamp()
    {
        var (service, calls) = Signing(Tree);

        await service.SignBundleAsync(Context("Developer ID Application: Example (TEAMID)"), App, "/w/e.plist", CancellationToken.None);

        Assert.That(calls.Where(c => c.StartsWith("sign", StringComparison.Ordinal)), Is.All.Contain("runtime=True timestamp=True"));
    }

    [Test]
    public async Task Artifact_signature_has_no_hardened_runtime()
    {
        var (service, calls) = Signing();

        await service.SignArtifactAsync(Context(), Path.Combine(App, "..", "..", "x.dmg"), CancellationToken.None);

        Assert.That(calls.Single(), Does.Contain("runtime=False timestamp=False"));
    }
}
