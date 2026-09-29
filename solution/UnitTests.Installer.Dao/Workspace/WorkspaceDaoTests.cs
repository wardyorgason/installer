using System.Security.Cryptography;
using System.Text;
using Installer.Dao.Workspace;
using Installer.Dao.Wrappers;
using Installer.Dtos.Payload;

namespace UnitTests.Installer.Dao.Workspace;

public class WorkspaceDaoTests
{
    private Mock<IFileSystemWrapper> _fileSystem = null!;
    private WorkspaceDao _dao = null!;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new Mock<IFileSystemWrapper>();
        _dao = new WorkspaceDao(_fileSystem.Object);
    }

    [Test]
    public void Reset_deletes_an_existing_directory_then_creates_it()
    {
        _fileSystem.Setup(f => f.DirectoryExists("/w")).Returns(true);
        var calls = new List<string>();
        _fileSystem.Setup(f => f.DeleteDirectory("/w", true)).Callback(() => calls.Add("delete"));
        _fileSystem.Setup(f => f.CreateDirectory("/w")).Callback(() => calls.Add("create"));

        _dao.ResetDirectory("/w");

        Assert.That(calls, Is.EqualTo(new[] { "delete", "create" }));
    }

    [Test]
    public void Write_creates_the_parent_directory()
    {
        _dao.WriteText("/w/a/b.txt", "x");

        _fileSystem.Verify(f => f.CreateDirectory("/w/a"));
        _fileSystem.Verify(f => f.WriteAllText("/w/a/b.txt", "x"));
    }

    [Test]
    public void Symlink_target_must_be_relative()
    {
        Assert.That(() => _dao.CreateRelativeSymlink("/w/link", "/abs"), Throws.ArgumentException);
        _fileSystem.Verify(f => f.CreateSymbolicLink(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void Publish_copies_replacing_and_returns_size_and_sha256()
    {
        var bytes = Encoding.UTF8.GetBytes("artifact");
        _fileSystem.Setup(f => f.OpenRead("/out/a.dmg")).Returns(() => new MemoryStream(bytes));
        _fileSystem.Setup(f => f.GetFileLength("/out/a.dmg")).Returns(bytes.Length);

        var fingerprint = _dao.Publish("/w/a.dmg", "/out/a.dmg");

        _fileSystem.Verify(f => f.CopyFile("/w/a.dmg", "/out/a.dmg", true));
        Assert.That(fingerprint, Is.EqualTo(new FileFingerprint(bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)))));
    }

    [Test]
    public void Tree_walk_lists_links_without_following_them()
    {
        _fileSystem.Setup(f => f.GetEntries("/b")).Returns([
            new FileSystemEntry("/b/MacOS", FileSystemEntryKind.Directory),
            new FileSystemEntry("/b/Info.plist", FileSystemEntryKind.File),
        ]);
        _fileSystem.Setup(f => f.GetEntries("/b/MacOS")).Returns([
            new FileSystemEntry("/b/MacOS/App", FileSystemEntryKind.File),
            new FileSystemEntry("/b/MacOS/wwwroot", FileSystemEntryKind.SymbolicLink),
        ]);

        var tree = _dao.ListTree("/b");

        Assert.That(tree, Is.EqualTo(new[]
        {
            ("Info.plist", FileSystemEntryKind.File),
            ("MacOS", FileSystemEntryKind.Directory),
            ("MacOS/App", FileSystemEntryKind.File),
            ("MacOS/wwwroot", FileSystemEntryKind.SymbolicLink),
        }));
        _fileSystem.Verify(f => f.GetEntries("/b/MacOS/wwwroot"), Times.Never);
    }

    [Test]
    public void Header_returns_at_most_the_file_length()
    {
        _fileSystem.Setup(f => f.OpenRead("/f")).Returns(new MemoryStream([1, 2, 3]));

        Assert.That(_dao.ReadHeader("/f", 16), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public void Directory_size_sums_regular_files()
    {
        _fileSystem.Setup(f => f.GetEntries("/p")).Returns([
            new FileSystemEntry("/p/a", FileSystemEntryKind.File),
            new FileSystemEntry("/p/l", FileSystemEntryKind.SymbolicLink),
        ]);
        _fileSystem.Setup(f => f.GetFileLength("/p/a")).Returns(10);

        Assert.That(_dao.GetDirectorySize("/p"), Is.EqualTo(10));
    }
}
