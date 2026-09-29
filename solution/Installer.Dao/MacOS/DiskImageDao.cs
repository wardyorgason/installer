using Installer.Dao.Tools;
using Installer.Dtos.Tools;

namespace Installer.Dao.MacOS;

internal sealed class DiskImageDao(IToolDao tools) : IDiskImageDao
{
    public Task CreateAsync(string volumeName, string sourceFolder, string output, CancellationToken cancellationToken) =>
        tools.RunAsync(
            new ToolCommand("hdiutil", ["create", "-volname", volumeName, "-srcfolder", sourceFolder, "-ov", "-format", "UDZO", "-fs", "HFS+", output]),
            cancellationToken);
}
