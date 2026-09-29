namespace Installer.Dao.Wrappers;

/// <summary>Wraps <see cref="System.Reflection.Assembly.GetManifestResourceStream(string)"/> for this assembly's resources.</summary>
public interface IEmbeddedResourceWrapper
{
    string ReadText(string resourceName);
}
