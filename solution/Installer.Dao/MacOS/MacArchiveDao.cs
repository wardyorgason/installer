using Installer.Dao.Tools;
using Installer.Dtos.Tools;

namespace Installer.Dao.MacOS;

internal sealed class MacArchiveDao(IToolDao tools) : IMacArchiveDao
{
    public Task ZipAsync(string appPath, string output, CancellationToken cancellationToken) =>
        tools.RunAsync(new ToolCommand("ditto", ["-c", "-k", "--sequesterRsrc", "--keepParent", appPath, output]), cancellationToken);
}
