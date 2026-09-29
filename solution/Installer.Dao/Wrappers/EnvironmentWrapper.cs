using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Installer.Dao.Wrappers;

[ExcludeFromCodeCoverage(Justification = "Thin wrapper; covered by integration tests.")]
internal sealed class EnvironmentWrapper : IEnvironmentWrapper
{
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    public bool IsMacOS() => OperatingSystem.IsMacOS();

    public bool IsLinux() => OperatingSystem.IsLinux();

    public bool IsWindows() => OperatingSystem.IsWindows();

    public Architecture OSArchitecture => RuntimeInformation.OSArchitecture;

    public string TempPath => Path.GetTempPath();

    public string? ProcessPath => Environment.ProcessPath;

    public string? EntryAssemblyLocation => System.Reflection.Assembly.GetEntryAssembly()?.Location;
}
