namespace Installer.Services.Profiles;

public interface IRuntimeProfileResolver
{
    IReadOnlyList<string> Names { get; }

    /// <summary>The profile with this name, or null when there is none.</summary>
    IRuntimeProfileService? Find(string name);
}
