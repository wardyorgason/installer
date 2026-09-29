using System.Globalization;
using Installer.Dao.Host;
using Installer.Dao.Workspace;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Services.Formats;
using Installer.Services.Manifest;
using Installer.Services.Payload;
using Installer.Services.Profiles;
using Microsoft.Extensions.Logging;

namespace Installer.Services.Build;

internal sealed class BuildService(
    IManifestService manifests,
    IPreflightService preflight,
    IPackageFormatResolver formats,
    IRuntimeProfileResolver profiles,
    IPayloadService payloads,
    IBinaryInspectionService binaries,
    IWorkspaceDao workspace,
    IEnvironmentDao environment,
    TimeProvider time,
    ILogger<BuildService> logger) : IBuildService
{
    public async Task<BuildResult> RunAsync(BuildRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var load = manifests.Load(request.ManifestPath);
        if (load.Manifest is null)
        {
            foreach (var problem in load.Problems)
            {
                logger.LogError("{Message}", problem.Message);
            }

            return Invalid(load.Problems.Where(problem => problem.Severity == Severity.Error).ToList());
        }

        var manifest = load.Manifest;
        var unknown = request.SelectedTargets.Where(name => manifest.Targets.All(target => target.Name != name)).ToList();
        if (unknown.Count > 0)
        {
            var message = $"Unknown target {string.Join(", ", unknown)}; the manifest's targets are {string.Join(", ", manifest.Targets.Select(target => target.Name))}.";
            logger.LogError("{Message}", message);
            return Invalid([Problem.Error(ErrorCodes.CliUnknownTarget, message)]);
        }

        var selected = manifest.Targets
            .Where(target => request.SelectedTargets.Count == 0 || request.SelectedTargets.Contains(target.Name))
            .ToList();
        var outputDir = Path.GetFullPath(request.OutputDirectory ?? Path.Combine(Path.GetDirectoryName(manifest.ManifestPath)!, "dist"));
        var runRoot = Path.Combine(environment.GetHost().TempDirectory, "installer", RunId());
        var preflightProblems = await preflight.CheckAsync(selected, cancellationToken).ConfigureAwait(false);

        var results = new List<TargetResult>();
        foreach (var target in manifest.Targets)
        {
            if (!selected.Contains(target))
            {
                results.Add(new TargetResult(target.Name, target.Os, target.Arch, TargetStatus.NotSelected, [], [], [], null));
            }
            else if (preflightProblems.TryGetValue(target.Name, out var problems))
            {
                results.Add(new TargetResult(target.Name, target.Os, target.Arch, TargetStatus.Failed, [], [], problems, null));
            }
            else
            {
                results.Add(await BuildTargetAsync(manifest.App, target, Path.Combine(runRoot, target.Name), outputDir, cancellationToken).ConfigureAwait(false));
            }
        }

        if (workspace.DirectoryExists(runRoot) && workspace.ListTree(runRoot).Count == 0)
        {
            workspace.DeleteDirectory(runRoot);
        }

        var succeeded = results.Where(result => result.Status != TargetStatus.NotSelected).All(result => result.Status == TargetStatus.Succeeded);
        if (succeeded)
        {
            logger.LogInformation("Build succeeded.");
        }
        else
        {
            logger.LogError("Build failed: {Failed}", string.Join(", ", results.Where(r => r.Status == TargetStatus.Failed).Select(r => r.Name)));
        }

        return new BuildResult(BuildResult.CurrentSchemaVersion, succeeded, [], results);
    }

    private async Task<TargetResult> BuildTargetAsync(AppInfo app, TargetSpec target, string workDir, string outputDir, CancellationToken cancellationToken)
    {
        logger.LogInformation("==> Build {Target}", target.Name);
        var warnings = new List<Problem>();
        try
        {
            workspace.ResetDirectory(workDir);
            var payload = payloads.Prepare(target, Path.Combine(workDir, "payload"));
            var profile = profiles.Find(app.Profile) ?? throw new BuildFailedException(ErrorCodes.Unexpected, $"Profile {app.Profile} is not registered.");
            var analysis = profile.Analyze(app, target, payload);
            warnings.AddRange(analysis.Warnings);
            foreach (var warning in analysis.Warnings)
            {
                logger.LogWarning("{Target}: {Message}", target.Name, warning.Message);
            }

            binaries.CheckMainExecutable(payload, analysis.MainExecutable, target);
            var format = formats.Find(target.Os) ?? throw new BuildFailedException(ErrorCodes.Unexpected, $"No output format is registered for {target.Os} targets.");
            var context = new TargetContext(app, target, payload, analysis, workDir, outputDir, $"{app.Name}-{app.DisplayVersion}-{target.Name}");
            var produced = await format.BuildAsync(context, cancellationToken).ConfigureAwait(false);

            var artifacts = new List<BuiltArtifact>();
            foreach (var file in produced)
            {
                var destination = Path.Combine(outputDir, Path.GetFileName(file.Path));
                var fingerprint = workspace.Publish(file.Path, destination);
                artifacts.Add(new BuiltArtifact(file.Kind, destination, fingerprint.Size, fingerprint.Sha256));
                logger.LogInformation("Wrote {Artifact}", destination);
            }

            workspace.DeleteDirectory(workDir);
            return new TargetResult(target.Name, target.Os, target.Arch, TargetStatus.Succeeded, artifacts, warnings, [], null);
        }
        catch (BuildFailedException ex)
        {
            return Failed(target, warnings, ex.Problem, workDir);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(target, warnings, Problem.Error(ErrorCodes.Unexpected, $"Target '{target.Name}': {ex.GetType().Name}: {ex.Message}"), workDir);
        }
    }

    private TargetResult Failed(TargetSpec target, List<Problem> warnings, Problem problem, string workDir)
    {
        logger.LogError("{Target} failed: {Message} (work directory kept: {WorkDir})", target.Name, problem.Message, workDir);
        return new TargetResult(target.Name, target.Os, target.Arch, TargetStatus.Failed, [], warnings, [problem], workDir);
    }

    private string RunId() =>
        time.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..6];

    private static BuildResult Invalid(IReadOnlyList<Problem> errors) =>
        new(BuildResult.CurrentSchemaVersion, false, errors, []);
}
