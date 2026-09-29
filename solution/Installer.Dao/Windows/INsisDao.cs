namespace Installer.Dao.Windows;

public interface INsisDao
{
    /// <summary>The installed makensis version (from <c>makensis -VERSION</c>), or null when it can't be parsed.</summary>
    Task<Version?> GetVersionAsync(CancellationToken cancellationToken);

    /// <summary>Compiles the script; the output path is set inside the script (OutFile).</summary>
    Task CompileAsync(string scriptPath, CancellationToken cancellationToken);
}
