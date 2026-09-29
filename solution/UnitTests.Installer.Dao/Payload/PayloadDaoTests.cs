using System.IO.Compression;
using Installer.Dao.Payload;
using Installer.Dao.Wrappers;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace UnitTests.Installer.Dao.Payload;

public class PayloadDaoTests
{
    private Mock<IFileSystemWrapper> _fileSystem = null!;
    private Mock<IZipFileWrapper> _zip = null!;
    private PayloadDao _dao = null!;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new Mock<IFileSystemWrapper>();
        _zip = new Mock<IZipFileWrapper>();
        _dao = new PayloadDao(_fileSystem.Object, _zip.Object);
    }

    [Test]
    public void Probe_distinguishes_zip_directory_and_missing()
    {
        _fileSystem.Setup(f => f.FileExists("/a.zip")).Returns(true);
        _fileSystem.Setup(f => f.FileExists("/a.tar")).Returns(true);
        _fileSystem.Setup(f => f.DirectoryExists("/dir")).Returns(true);

        Assert.Multiple(() =>
        {
            Assert.That(_dao.Probe("/a.zip"), Is.EqualTo(PayloadKind.Zip));
            Assert.That(_dao.Probe("/dir"), Is.EqualTo(PayloadKind.Directory));
            Assert.That(_dao.Probe("/a.tar"), Is.Null);
            Assert.That(_dao.Probe("/missing"), Is.Null);
        });
    }

    [Test]
    public void Extract_maps_relative_paths_under_the_destination()
    {
        IReadOnlyDictionary<string, string>? captured = null;
        _zip.Setup(z => z.ExtractEntries("/p.zip", It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Callback<string, IReadOnlyDictionary<string, string>>((_, d) => captured = d);

        _dao.ExtractZipEntries("/p.zip", "/w/payload", new Dictionary<string, string> { ["App/wwwroot/i.html"] = "wwwroot/i.html" });

        Assert.That(captured!["App/wwwroot/i.html"], Is.EqualTo(Path.Combine("/w/payload", "wwwroot", "i.html")));
    }

    [Test]
    public void Copy_recreates_links_and_keeps_modes()
    {
        _fileSystem.Setup(f => f.GetEntries("/src")).Returns([
            new FileSystemEntry("/src/app", FileSystemEntryKind.File),
            new FileSystemEntry("/src/current", FileSystemEntryKind.SymbolicLink),
            new FileSystemEntry("/src/lib", FileSystemEntryKind.Directory),
        ]);
        _fileSystem.Setup(f => f.GetEntries("/src/lib")).Returns([]);
        _fileSystem.Setup(f => f.GetLinkTarget("/src/current")).Returns("lib");
        _fileSystem.Setup(f => f.GetUnixFileMode("/src/app")).Returns(UnixFileMode.UserRead | UnixFileMode.UserExecute);

        _dao.CopyDirectory("/src", "/dst");

        _fileSystem.Verify(f => f.CopyFile("/src/app", Path.Combine("/dst", "app"), true));
        _fileSystem.Verify(f => f.SetUnixFileMode(Path.Combine("/dst", "app"), UnixFileMode.UserRead | UnixFileMode.UserExecute));
        _fileSystem.Verify(f => f.CreateSymbolicLink(Path.Combine("/dst", "current"), "lib"));
        _fileSystem.Verify(f => f.CreateDirectory(Path.Combine("/dst", "lib")));
    }

    [Test]
    public void List_files_returns_only_regular_files()
    {
        _fileSystem.Setup(f => f.GetEntries("/p")).Returns([
            new FileSystemEntry("/p/a", FileSystemEntryKind.File),
            new FileSystemEntry("/p/l", FileSystemEntryKind.SymbolicLink),
            new FileSystemEntry("/p/d", FileSystemEntryKind.Directory),
        ]);
        _fileSystem.Setup(f => f.GetEntries("/p/d")).Returns([new FileSystemEntry("/p/d/b", FileSystemEntryKind.File)]);

        Assert.That(_dao.ListFiles("/p"), Is.EqualTo(new[] { "a", "d/b" }));
    }

    [Test]
    [Category("Integration")]
    public void Extracts_a_real_zip_keeping_unix_permissions()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Unix permissions need a Unix host.");
        }

        var dir = Path.Combine(Path.GetTempPath(), "installer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var zipPath = Path.Combine(dir, "p.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("App/tool");
                entry.ExternalAttributes = (int)(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute) << 16;
                using var writer = new StreamWriter(entry.Open());
                writer.Write("#!/bin/sh\n");
            }

            var dao = new PayloadDao(new FileSystemWrapper(), new ZipFileWrapper());
            var entries = dao.ListZipEntries(zipPath);
            dao.ExtractZipEntries(zipPath, Path.Combine(dir, "out"), new Dictionary<string, string> { ["App/tool"] = "tool" });

            var extracted = Path.Combine(dir, "out", "tool");
            Assert.Multiple(() =>
            {
                Assert.That(entries.Select(e => e.FullName), Is.EqualTo(new[] { "App/tool" }));
                Assert.That(File.ReadAllText(extracted), Is.EqualTo("#!/bin/sh\n"));
                Assert.That(new FileSystemWrapper().GetUnixFileMode(extracted).HasFlag(UnixFileMode.UserExecute), Is.True);
            });
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
