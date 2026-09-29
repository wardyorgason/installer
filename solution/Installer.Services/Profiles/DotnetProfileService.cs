using System.Text.Json;
using Installer.Dao.Payload;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace Installer.Services.Profiles;

/// <summary>
/// .NET <c>dotnet publish</c> output, framework-dependent or self-contained: finds the apphost next to
/// &lt;app&gt;.runtimeconfig.json, warns when users need a shared runtime, checks the runtime identifier, and supplies the
/// entitlements the .NET runtime needs under the hardened runtime.
/// </summary>
internal sealed class DotnetProfileService(IPayloadDao payloads) : IRuntimeProfileService
{
    private const string RuntimeConfigSuffix = ".runtimeconfig.json";

    internal static readonly IReadOnlyDictionary<string, bool> DefaultEntitlements = new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["com.apple.security.cs.allow-jit"] = true,
        ["com.apple.security.cs.allow-unsigned-executable-memory"] = true,
        ["com.apple.security.cs.disable-library-validation"] = true,
    };

    public string Name => "dotnet";

    public bool RequiresExecutable => false;

    public ProfileAnalysis Analyze(AppInfo app, TargetSpec target, PreparedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(payload);
        var appName = FindAppName(app, target, payload);
        var apphost = target.Os == TargetOs.Windows ? appName + ".exe" : appName;
        if (!payload.Files.Contains(apphost, StringComparer.Ordinal))
        {
            throw new BuildFailedException(
                ErrorCodes.ProfileNoAppHost,
                $"Target '{target.Name}': {appName}{RuntimeConfigSuffix} exists but the apphost {apphost} does not; publish with an apphost (UseAppHost=true, the default) for the target's runtime.");
        }

        var warnings = RuntimeWarnings(payload, appName, target);
        CheckRuntimeIdentifier(payload, appName, target);
        return new ProfileAnalysis(apphost, warnings, DefaultEntitlements);
    }

    private static string FindAppName(AppInfo app, TargetSpec target, PreparedPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(app.Executable))
        {
            var name = app.Executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? app.Executable[..^4] : app.Executable;
            if (!payload.Files.Contains(name + RuntimeConfigSuffix, StringComparer.Ordinal))
            {
                throw new BuildFailedException(
                    ErrorCodes.ProfileNoRuntimeConfig,
                    $"Target '{target.Name}': executable '{app.Executable}' was named but the payload has no {name}{RuntimeConfigSuffix}.");
            }

            return name;
        }

        var candidates = payload.Files
            .Where(file => !file.Contains('/', StringComparison.Ordinal) && file.EndsWith(RuntimeConfigSuffix, StringComparison.Ordinal))
            .Select(file => file[..^RuntimeConfigSuffix.Length])
            .Order(StringComparer.Ordinal)
            .ToList();
        return candidates.Count switch
        {
            0 => throw new BuildFailedException(
                ErrorCodes.ProfileNoRuntimeConfig,
                $"Target '{target.Name}': the payload has no *{RuntimeConfigSuffix} at its root. Single-file and NativeAOT output are not detected; package them with the generic profile."),
            1 => candidates[0],
            _ => throw new BuildFailedException(
                ErrorCodes.ProfileAmbiguousApp,
                $"Target '{target.Name}': the payload contains several apps ({string.Join(", ", candidates)}); name the main one with executable."),
        };
    }

    private List<Problem> RuntimeWarnings(PreparedPayload payload, string appName, TargetSpec target)
    {
        using var config = JsonDocument.Parse(payloads.ReadText(Path.Combine(payload.Root, appName + RuntimeConfigSuffix)));
        if (!config.RootElement.TryGetProperty("runtimeOptions", out var options) || options.TryGetProperty("includedFrameworks", out _))
        {
            return []; // self-contained
        }

        var frameworks = new List<JsonElement>();
        if (options.TryGetProperty("framework", out var single))
        {
            frameworks.Add(single);
        }

        if (options.TryGetProperty("frameworks", out var several) && several.ValueKind == JsonValueKind.Array)
        {
            frameworks.AddRange(several.EnumerateArray());
        }

        return frameworks
            .Select(framework => $"{framework.GetProperty("name").GetString()} {MajorMinor(framework.GetProperty("version").GetString())}")
            .Select(runtime => Problem.Warning(
                ErrorCodes.DotnetRuntimeRequired,
                $"Target '{target.Name}' is framework-dependent: users need the {runtime} runtime installed."))
            .ToList();
    }

    private void CheckRuntimeIdentifier(PreparedPayload payload, string appName, TargetSpec target)
    {
        var depsFile = appName + ".deps.json";
        if (!payload.Files.Contains(depsFile, StringComparer.Ordinal))
        {
            return;
        }

        using var deps = JsonDocument.Parse(payloads.ReadText(Path.Combine(payload.Root, depsFile)));
        var runtimeTarget = deps.RootElement.TryGetProperty("runtimeTarget", out var element) && element.TryGetProperty("name", out var name)
            ? name.GetString() ?? string.Empty
            : string.Empty;
        var slash = runtimeTarget.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0)
        {
            return; // portable publish: no runtime identifier
        }

        var rid = runtimeTarget[(slash + 1)..];
        var (os, arch) = ParseRid(rid);
        if (os is null)
        {
            return; // an identifier this profile doesn't know (e.g. freebsd-x64) is left to the main executable check
        }

        if (os != target.Os || arch != target.Arch)
        {
            throw new BuildFailedException(
                ErrorCodes.ProfileRidMismatch,
                $"Target '{target.Name}': the payload was published for runtime identifier {rid}, but the target is {target.Os} {target.Arch}.");
        }
    }

    private static (TargetOs? Os, TargetArch? Arch) ParseRid(string rid)
    {
        var dash = rid.LastIndexOf('-');
        if (dash < 0)
        {
            return (null, null);
        }

        var osPart = rid[..dash];
        TargetOs? os = osPart switch
        {
            "win" => TargetOs.Windows,
            "osx" or "macos" => TargetOs.MacOS,
            "linux" or "linux-musl" or "linux-bionic" => TargetOs.Linux,
            _ => null,
        };
        TargetArch? arch = rid[(dash + 1)..] switch
        {
            "x64" => TargetArch.X64,
            "arm64" => TargetArch.Arm64,
            _ => null,
        };
        return (os, arch);
    }

    private static string MajorMinor(string? version)
    {
        var parts = (version ?? string.Empty).Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : version ?? string.Empty;
    }
}
