namespace UnitTests.Installer.Services.TestSupport;

/// <summary>
/// Compares generated text with TestData/Golden/&lt;name&gt;. Run with INSTALLER_UPDATE_GOLDEN=1 to rewrite the golden files
/// in the source tree from the current output (then review the diff).
/// </summary>
public static class Golden
{
    public static void AssertMatches(string name, string actual)
    {
        if (Environment.GetEnvironmentVariable("INSTALLER_UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(Path.Combine(RepoPaths.Root, "solution", "UnitTests.Installer.Services", "TestData", "Golden", name), actual);
            return;
        }

        var expected = File.ReadAllText(Path.Combine(Fixtures.TestData, "Golden", name));
        Assert.That(actual, Is.EqualTo(expected), $"Generated {name} differs from TestData/Golden/{name}; if intended, rerun with INSTALLER_UPDATE_GOLDEN=1.");
    }
}
