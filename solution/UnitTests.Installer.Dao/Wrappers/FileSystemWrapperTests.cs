using Installer.Dao.Wrappers;
using Installer.Dtos.Payload;

namespace UnitTests.Installer.Dao.Wrappers;

[Category("Integration")]
public class FileSystemWrapperTests
{
    private readonly FileSystemWrapper _wrapper = new();
    private string _dir = null!;

    [SetUp]
    public void CreateTempDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Unix modes and symlinks need a Unix host.");
        }

        _dir = Path.Combine(Path.GetTempPath(), "installer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void DeleteTempDirectory()
    {
        if (_dir is not null && Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Test]
    public void Relative_symlink_is_reported_as_a_link_and_resolves()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Resources", "wwwroot"));
        File.WriteAllText(Path.Combine(_dir, "Resources", "wwwroot", "index.html"), "hi");
        Directory.CreateDirectory(Path.Combine(_dir, "MacOS"));

        _wrapper.CreateSymbolicLink(Path.Combine(_dir, "MacOS", "wwwroot"), "../Resources/wwwroot");

        var entries = _wrapper.GetEntries(Path.Combine(_dir, "MacOS"));
        Assert.Multiple(() =>
        {
            Assert.That(entries.Single().Kind, Is.EqualTo(FileSystemEntryKind.SymbolicLink));
            Assert.That(new FileInfo(Path.Combine(_dir, "MacOS", "wwwroot")).LinkTarget, Is.EqualTo("../Resources/wwwroot"));
            Assert.That(File.ReadAllText(Path.Combine(_dir, "MacOS", "wwwroot", "index.html")), Is.EqualTo("hi"));
        });
    }

    [Test]
    public void Unix_mode_round_trips()
    {
        var file = Path.Combine(_dir, "tool");
        File.WriteAllText(file, "#!/bin/sh\n");
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

        _wrapper.SetUnixFileMode(file, mode);

        Assert.That(_wrapper.GetUnixFileMode(file), Is.EqualTo(mode));
    }
}
