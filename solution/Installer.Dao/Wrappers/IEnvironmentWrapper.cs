using System.Runtime.InteropServices;

namespace Installer.Dao.Wrappers;

/// <summary>Wraps <see cref="Environment"/>, <see cref="OperatingSystem"/> and <see cref="RuntimeInformation"/>.</summary>
public interface IEnvironmentWrapper
{
    string? GetEnvironmentVariable(string name);

    bool IsMacOS();

    bool IsLinux();

    bool IsWindows();

    Architecture OSArchitecture { get; }

    string TempPath { get; }

    /// <summary>The running executable (the dotnet host, or an apphost).</summary>
    string? ProcessPath { get; }

    /// <summary>The entry assembly's file, e.g. Installer.Cli.dll.</summary>
    string? EntryAssemblyLocation { get; }
}
