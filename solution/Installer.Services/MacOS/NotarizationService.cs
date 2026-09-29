using Installer.Dao.MacOS;
using Installer.Dtos.Build;
using Microsoft.Extensions.Logging;

namespace Installer.Services.MacOS;

internal sealed class NotarizationService(INotaryDao notary, ILogger<NotarizationService> logger) : INotarizationService
{
    public async Task NotarizeAsync(TargetContext context, string file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var profile = context.Target.MacOS?.NotaryKeychainProfile ?? throw new InvalidOperationException("Notarization isn't configured for this target.");
        logger.LogInformation("Notarizing {File} (this can take several minutes)", Path.GetFileName(file));
        var result = await notary.SubmitAsync(file, profile, cancellationToken).ConfigureAwait(false);
        if (string.Equals(result.Status, "Accepted", StringComparison.Ordinal))
        {
            logger.LogInformation("Notarization accepted ({Submission})", result.SubmissionId);
            return;
        }

        var log = Path.Combine(context.WorkDir, "notarization-log.json");
        await notary.SaveLogAsync(result.SubmissionId, profile, log, cancellationToken).ConfigureAwait(false);
        throw new BuildFailedException(
            ErrorCodes.NotarizeRejected,
            $"Target '{context.Target.Name}': Apple returned {result.Status} for {Path.GetFileName(file)}{(result.Message is null ? string.Empty : ": " + result.Message)}. Notarization log: {log}");
    }
}
