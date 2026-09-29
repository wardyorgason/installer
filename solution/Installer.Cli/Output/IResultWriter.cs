using Installer.Dtos.Build;

namespace Installer.Cli.Output;

public interface IResultWriter
{
    /// <summary>Writes the result to stdout as a single JSON document.</summary>
    void Write(BuildResult result);
}
