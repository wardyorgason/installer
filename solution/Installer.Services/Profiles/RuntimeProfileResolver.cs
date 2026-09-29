namespace Installer.Services.Profiles;

internal sealed class RuntimeProfileResolver(IEnumerable<IRuntimeProfileService> profiles) : IRuntimeProfileResolver
{
    private readonly IReadOnlyList<IRuntimeProfileService> _profiles = profiles.ToList();

    public IReadOnlyList<string> Names => _profiles.Select(profile => profile.Name).ToList();

    public IRuntimeProfileService? Find(string name) =>
        _profiles.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.Ordinal));
}
