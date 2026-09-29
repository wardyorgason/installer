using Installer.Dtos.Tools;

namespace Installer.Dao.MacOS;

public interface INotaryDao
{
    /// <summary>Submits the file with <c>notarytool submit --wait</c> and returns Apple's verdict (Accepted, Invalid, …).</summary>
    Task<NotaryResult> SubmitAsync(string file, string keychainProfile, CancellationToken cancellationToken);

    /// <summary>Saves the submission's notarization log to <paramref name="output"/>.</summary>
    Task SaveLogAsync(string submissionId, string keychainProfile, string output, CancellationToken cancellationToken);

    /// <summary>Staples the notarization ticket to an .app or .dmg.</summary>
    Task StapleAsync(string path, CancellationToken cancellationToken);
}
