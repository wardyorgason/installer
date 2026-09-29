using System.Runtime.InteropServices;
using Installer.Dao.Wrappers;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;

namespace Installer.Dao.Host;

internal sealed class EnvironmentDao(IEnvironmentWrapper environment) : IEnvironmentDao
{
    public string? GetVariable(string name) => environment.GetEnvironmentVariable(name);

    public HostInfo GetHost()
    {
        var os = environment.IsMacOS() ? TargetOs.MacOS
            : environment.IsWindows() ? TargetOs.Windows
            : TargetOs.Linux;
        var arch = environment.OSArchitecture == Architecture.Arm64 ? TargetArch.Arm64 : TargetArch.X64;
        return new HostInfo(os, arch, environment.TempPath);
    }

    public IReadOnlyList<string> GetSelfInvocation()
    {
        var process = environment.ProcessPath ?? throw new InvalidOperationException("The builder's process path is unknown.");
        var isDotnetHost = Path.GetFileNameWithoutExtension(process).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        return isDotnetHost && environment.EntryAssemblyLocation is { Length: > 0 } dll ? [process, dll] : [process];
    }
}
