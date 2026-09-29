namespace UnitTests.Installer.Services.TestSupport;

public static class RepoPaths
{
    /// <summary>The repository root: the nearest ancestor of the test output that contains docs/ and solution/.</summary>
    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory is not null && !(Directory.Exists(Path.Combine(directory.FullName, "docs")) && Directory.Exists(Path.Combine(directory.FullName, "solution"))))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }
}
