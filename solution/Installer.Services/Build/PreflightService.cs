using Installer.Dao.Host;
using Installer.Dao.Tools;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Services.Formats;
using Microsoft.Extensions.Logging;

namespace Installer.Services.Build;

internal sealed class PreflightService(
    IPackageFormatResolver formats,
    IEnvironmentDao environment,
    IToolDao tools,
    ILogger<PreflightService> logger) : IPreflightService
{
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<Problem>>> CheckAsync(IReadOnlyList<TargetSpec> targets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var host = environment.GetHost();
        var results = new Dictionary<string, IReadOnlyList<Problem>>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            logger.LogInformation("==> Preflight {Target}", target.Name);
            var problems = await CheckTargetAsync(target, host, cancellationToken).ConfigureAwait(false);
            foreach (var problem in problems)
            {
                logger.LogError("{Target}: {Message}", target.Name, problem.Message);
            }

            if (problems.Count > 0)
            {
                results[target.Name] = problems;
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<Problem>> CheckTargetAsync(TargetSpec target, HostInfo host, CancellationToken cancellationToken)
    {
        var format = formats.Find(target.Os);
        if (format is null)
        {
            return [Problem.Error(ErrorCodes.Unexpected, $"No output format is registered for {target.Os} targets.")];
        }

        if (format.RequiredHost is { } requiredHost && requiredHost != host.Os)
        {
            return [Problem.Error(ErrorCodes.HostUnsupported, $"{Describe(target.Os)} packages require a {Describe(requiredHost)} host; this host is {Describe(host.Os)}.")];
        }

        var missing = format.RequiredTools
            .Where(tool => tools.FindTool(tool) is null)
            .Select(tool => Problem.Error(ErrorCodes.ToolMissing, tools.DescribeMissing(tool)))
            .ToList();
        if (missing.Count > 0)
        {
            return missing;
        }

        try
        {
            return await format.PreflightAsync(target, cancellationToken).ConfigureAwait(false);
        }
        catch (BuildFailedException ex)
        {
            return [ex.Problem];
        }
    }

    private static string Describe(TargetOs os) => os switch
    {
        TargetOs.MacOS => "macOS",
        TargetOs.Windows => "Windows",
        _ => "Linux",
    };
}
