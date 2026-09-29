using System.Diagnostics.CodeAnalysis;

namespace Installer.Dao.Wrappers;

[ExcludeFromCodeCoverage(Justification = "Thin wrapper; covered by integration tests.")]
internal sealed class EmbeddedResourceWrapper : IEmbeddedResourceWrapper
{
    public string ReadText(string resourceName)
    {
        using var stream = typeof(EmbeddedResourceWrapper).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
