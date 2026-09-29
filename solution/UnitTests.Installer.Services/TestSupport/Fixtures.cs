using Installer.Dao.Payload;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace UnitTests.Installer.Services.TestSupport;

public static class Fixtures
{
    public static string TestData => Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");

    public static byte[] Header(string name) => File.ReadAllBytes(Path.Combine(TestData, "Headers", name + ".bin"));

    /// <summary>
    /// A payload fixture from TestData/Payloads as a prepared payload, with <paramref name="payloads"/> set up to read its
    /// real files.
    /// </summary>
    public static PreparedPayload Payload(string name, Mock<IPayloadDao> payloads)
    {
        var root = Path.Combine(TestData, "Payloads", name);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        payloads.Setup(p => p.ReadText(It.Is<string>(path => path.StartsWith(root, StringComparison.Ordinal)))).Returns((string path) => File.ReadAllText(path));
        return new PreparedPayload(root, files);
    }

    public static AppInfo App(string profile = "dotnet", string? executable = null) =>
        new("dev.example.app", "Example", new AppVersion("1.0.0", [1, 0, 0]), "1.0.0", "Publisher", "Description", "/icon.png", profile, executable);

    public static TargetSpec Target(TargetOs os, TargetArch arch, string? name = null) =>
        new(name ?? $"{os.ToString().ToLowerInvariant()}-{arch.ToString().ToLowerInvariant()}", os, arch, "/payload", PayloadKind.Directory, null, null, null);
}
