using System.Text.Json;
using Installer.Dao.Host;
using Installer.Dao.Windows;
using Installer.Dao.Workspace;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Services.Formats;
using Installer.Services.Imaging;
using Installer.Services.Manifest;
using Microsoft.Extensions.Logging;

namespace Installer.Services.Windows;

/// <summary>A per-user setup .exe built with NSIS, which runs on any host (Homebrew on the Mac agent).</summary>
internal sealed class WindowsFormatService(
    INsisDao nsis,
    INsisScriptService scripts,
    IWindowsSigningService signing,
    IIconService icons,
    IVersionService versions,
    IWorkspaceDao workspace,
    IEnvironmentDao environment,
    ILogger<WindowsFormatService> logger) : IPackageFormatService
{
    internal static readonly Version MinimumNsisVersion = new(3, 8);

    public TargetOs Os => TargetOs.Windows;

    public TargetOs? RequiredHost => null;

    public IReadOnlyList<string> RequiredTools => ["makensis"];

    public async Task<IReadOnlyList<Problem>> PreflightAsync(TargetSpec target, CancellationToken cancellationToken)
    {
        var version = await nsis.GetVersionAsync(cancellationToken).ConfigureAwait(false);
        return version is not null && version >= MinimumNsisVersion
            ? []
            : [Problem.Error(ErrorCodes.ToolVersion, $"makensis {MinimumNsisVersion.Major}.{MinimumNsisVersion.Minor:00} or later is required; found {version?.ToString() ?? "an unknown version"}. `brew upgrade makensis`.")];
    }

    public async Task<IReadOnlyList<ProducedFile>> BuildAsync(TargetContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var app = context.App;
        var signCommand = context.Target.Windows?.SignCommand;

        var iconPath = Path.Combine(context.WorkDir, "app.ico");
        workspace.WriteBytes(iconPath, icons.CreateIco(app.IconPath));

        string? signHook = null;
        if (signCommand is not null)
        {
            await signing.SignFileAsync(Path.Combine(context.Payload.Root, context.Profile.MainExecutable), signCommand, cancellationToken).ConfigureAwait(false);
            var commandJson = Path.Combine(context.WorkDir, "sign-command.json");
            workspace.WriteText(commandJson, JsonSerializer.Serialize(signCommand));
            signHook = scripts.SignHook(environment.GetSelfInvocation(), commandJson);
        }

        var output = Path.Combine(context.WorkDir, context.ArtifactBaseName + "-setup.exe");
        var script = scripts.Generate(new NsisScriptRequest(
            app.Id,
            app.Name,
            app.Publisher,
            app.Description,
            app.DisplayVersion,
            versions.FourPartVersion(app.Version),
            context.Payload.Root,
            context.Profile.MainExecutable,
            iconPath,
            output,
            (workspace.GetDirectorySize(context.Payload.Root) + 1023) / 1024,
            signHook));
        var scriptPath = Path.Combine(context.WorkDir, "installer.nsi");
        workspace.WriteText(scriptPath, script);

        logger.LogInformation("Compiling {Setup}", Path.GetFileName(output));
        await nsis.CompileAsync(scriptPath, cancellationToken).ConfigureAwait(false);
        return [new ProducedFile(ArtifactKind.SetupExe, output)];
    }
}
