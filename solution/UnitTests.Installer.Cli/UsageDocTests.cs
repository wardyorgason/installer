using System.Reflection;
using Installer.Dtos.Build;

namespace UnitTests.Installer.Cli;

/// <summary>Keeps docs/usage.md in step with the error codes, which are part of the public result contract.</summary>
public class UsageDocTests
{
    [Test]
    public void Every_error_code_is_documented()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "usage.md")))
        {
            root = root.Parent;
        }

        var doc = File.ReadAllText(Path.Combine(root!.FullName, "docs", "usage.md"));
        var codes = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => (string)field.GetValue(null)!);

        Assert.That(codes.Where(code => !doc.Contains($"`{code}`", StringComparison.Ordinal)), Is.Empty);
    }
}
