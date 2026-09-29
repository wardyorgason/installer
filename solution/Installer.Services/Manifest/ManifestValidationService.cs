using System.Buffers.Binary;
using System.Text.RegularExpressions;
using Installer.Dao.Payload;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Services.MacOS;
using Installer.Services.Profiles;

namespace Installer.Services.Manifest;

internal sealed partial class ManifestValidationService(IVersionService versions, IPayloadDao payloads, IRuntimeProfileResolver profiles)
    : IManifestValidationService
{
    public const int SupportedSchemaVersion = 1;
    public const int MinimumIconSize = 512;

    /// <summary>Keys the builder sets from the manifest; a caller override would contradict it.</summary>
    internal static readonly IReadOnlyDictionary<string, string> ReservedPlistKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["CFBundleIdentifier"] = "id",
        ["CFBundleExecutable"] = "the main executable",
        ["CFBundleShortVersionString"] = "version",
        ["CFBundleVersion"] = "version",
    };

    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly char[] UnsafeNameChars = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public PackageManifest? Validate(string manifestPath, ManifestDocument document, ICollection<Problem> problems)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(problems);
        var errorsBefore = problems.Count(problem => problem.Severity == Severity.Error);

        CheckSchemaVersion(document, problems);
        var id = Required(document.Id, "$.id", problems);
        if (id is not null && !AppId().IsMatch(id))
        {
            Invalid(problems, $"$.id: '{id}' must be a reverse-DNS identifier such as com.example.app.");
        }

        var name = Required(document.Name, "$.name", problems);
        if (name is not null && name.IndexOfAny(UnsafeNameChars) >= 0)
        {
            Invalid(problems, $"$.name: '{name}' must not contain any of {string.Join(' ', UnsafeNameChars)} (it is used in file and folder names).");
        }

        var version = CheckVersion(document, problems);
        var displayVersion = document.DisplayVersion ?? document.Version;
        if (document.DisplayVersion is not null && (document.DisplayVersion.Length == 0 || document.DisplayVersion.IndexOfAny(['/', '\\']) >= 0))
        {
            Invalid(problems, "$.displayVersion must be non-empty and must not contain path separators.");
        }

        var publisher = Required(document.Publisher, "$.publisher", problems);
        var description = Required(document.Description, "$.description", problems);
        var icon = Required(document.Icon, "$.icon", problems);
        if (icon is not null)
        {
            CheckIcon(icon, problems);
        }

        var profile = CheckProfile(document, problems);
        var targets = CheckTargets(document, problems);

        if (problems.Count(problem => problem.Severity == Severity.Error) > errorsBefore)
        {
            return null;
        }

        var app = new AppInfo(id!, name!, version!, displayVersion!, publisher!, description!, icon!, profile!, document.Executable);
        return new PackageManifest(manifestPath, app, targets);
    }

    private static void CheckSchemaVersion(ManifestDocument document, ICollection<Problem> problems)
    {
        if (document.SchemaVersion is null)
        {
            Missing(problems, "$.schemaVersion");
        }
        else if (document.SchemaVersion != SupportedSchemaVersion)
        {
            problems.Add(Problem.Error(
                ErrorCodes.ManifestUnsupportedSchema,
                $"$.schemaVersion {document.SchemaVersion} is not supported; this builder supports schema version {SupportedSchemaVersion}."));
        }
    }

    private AppVersion? CheckVersion(ManifestDocument document, ICollection<Problem> problems)
    {
        var text = Required(document.Version, "$.version", problems);
        if (text is null)
        {
            return null;
        }

        var version = versions.Parse(text, out var error);
        if (version is null)
        {
            Invalid(problems, $"$.version: {error}");
        }

        return version;
    }

    private void CheckIcon(string icon, ICollection<Problem> problems)
    {
        if (!payloads.FileExists(icon))
        {
            Invalid(problems, $"$.icon: {icon} was not found.");
            return;
        }

        var header = payloads.ReadHeader(icon, 24);
        if (header.Length < 24 || !header.AsSpan(0, 8).SequenceEqual(PngSignature) || header[12..16] is not [(byte)'I', (byte)'H', (byte)'D', (byte)'R'])
        {
            Invalid(problems, $"$.icon: {icon} is not a PNG file.");
            return;
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4));
        if (width != height || width < MinimumIconSize)
        {
            Invalid(problems, $"$.icon must be a square PNG of at least {MinimumIconSize}×{MinimumIconSize} pixels; {icon} is {width}×{height}.");
        }
    }

    private string? CheckProfile(ManifestDocument document, ICollection<Problem> problems)
    {
        var name = Required(document.Profile, "$.profile", problems);
        if (name is null)
        {
            return null;
        }

        var profile = profiles.Find(name);
        if (profile is null)
        {
            Invalid(problems, $"$.profile: unknown profile '{name}'; known profiles: {string.Join(", ", profiles.Names)}.");
        }
        else if (profile.RequiresExecutable && string.IsNullOrWhiteSpace(document.Executable))
        {
            Missing(problems, "$.executable", $"the '{name}' profile requires executable (the main executable's path relative to the payload)");
        }

        return name;
    }

    private List<TargetSpec> CheckTargets(ManifestDocument document, ICollection<Problem> problems)
    {
        var specs = new List<TargetSpec>();
        if (document.Targets is null || document.Targets.Count == 0)
        {
            Missing(problems, "$.targets", "at least one target is required");
            return specs;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in document.Targets)
        {
            var spec = CheckTarget(target, problems);
            if (target.Name is not null && !seen.Add(target.Name))
            {
                problems.Add(Problem.Error(ErrorCodes.ManifestDuplicateTarget, $"{target.Path}: target name '{target.Name}' is used more than once."));
            }

            if (spec is not null)
            {
                specs.Add(spec);
            }
        }

        return specs;
    }

    private TargetSpec? CheckTarget(TargetDocument target, ICollection<Problem> problems)
    {
        var path = target.Path;
        TargetOs? os = target.Os switch
        {
            "windows" => TargetOs.Windows,
            "macos" => TargetOs.MacOS,
            "linux" => TargetOs.Linux,
            null => null,
            _ => null,
        };
        if (target.Os is null)
        {
            Missing(problems, $"{path}.os");
        }
        else if (os is null)
        {
            Invalid(problems, $"{path}.os: '{target.Os}' must be windows, macos or linux.");
        }

        TargetArch? arch = target.Arch switch
        {
            "x64" => TargetArch.X64,
            "arm64" => TargetArch.Arm64,
            _ => null,
        };
        if (target.Arch is null)
        {
            Missing(problems, $"{path}.arch");
        }
        else if (arch is null)
        {
            Invalid(problems, $"{path}.arch: '{target.Arch}' must be x64 or arm64.");
        }

        if (target.Name is not null && !TargetName().IsMatch(target.Name))
        {
            Invalid(problems, $"{path}.name: '{target.Name}' may contain only letters, digits, '.', '_' and '-'.");
        }

        PayloadKind? kind = null;
        if (target.Payload is null)
        {
            Missing(problems, $"{path}.payload");
        }
        else
        {
            kind = payloads.Probe(target.Payload);
            if (kind is null)
            {
                problems.Add(Problem.Error(
                    ErrorCodes.PayloadNotFound,
                    $"{path}.payload: target '{target.Name}': {target.Payload} is not an existing .zip file or directory."));
            }
        }

        CheckForeignSections(target, os, problems);
        var mac = os == TargetOs.MacOS ? CheckMac(target.MacOS, path, problems) : null;
        var windows = os == TargetOs.Windows ? CheckWindows(target.Windows, path, problems) : null;
        var linux = os == TargetOs.Linux ? CheckLinux(target.Linux, path, problems) : null;

        if (os is null || arch is null || kind is null || target.Name is null)
        {
            return null;
        }

        return new TargetSpec(target.Name, os.Value, arch.Value, target.Payload!, kind.Value, mac, windows, linux);
    }

    private static void CheckForeignSections(TargetDocument target, TargetOs? os, ICollection<Problem> problems)
    {
        if (os is null)
        {
            return;
        }

        foreach (var (section, sectionOs, present) in new[]
                 {
                     ("macos", TargetOs.MacOS, target.MacOS is not null),
                     ("windows", TargetOs.Windows, target.Windows is not null),
                     ("linux", TargetOs.Linux, target.Linux is not null),
                 })
        {
            if (present && sectionOs != os)
            {
                Invalid(problems, $"{target.Path}.{section}: target '{target.Name}' is a {target.Os} target and cannot have a {section} section.");
            }
        }
    }

    private static MacOptions? CheckMac(MacOptionsDocument? mac, string path, ICollection<Problem> problems)
    {
        var identity = mac?.Identity;
        if (string.IsNullOrWhiteSpace(identity))
        {
            Missing(problems, $"{path}.macos.identity", "macOS targets require a signing identity (macos.identity)");
        }

        var outputs = new List<MacOutput>();
        var requested = mac?.Outputs ?? ["dmg"];
        if (requested.Count == 0)
        {
            Invalid(problems, $"{path}.macos.outputs must list at least one of dmg, zip.");
        }

        foreach (var output in requested)
        {
            MacOutput? parsed = output switch { "dmg" => MacOutput.Dmg, "zip" => MacOutput.Zip, _ => null };
            if (parsed is null)
            {
                Invalid(problems, $"{path}.macos.outputs: '{output}' must be dmg or zip.");
            }
            else if (!outputs.Contains(parsed.Value))
            {
                outputs.Add(parsed.Value);
            }
        }

        string? notaryProfile = null;
        if (mac?.Notarize is not null)
        {
            notaryProfile = mac.Notarize.KeychainProfile;
            if (string.IsNullOrWhiteSpace(notaryProfile))
            {
                Missing(problems, $"{path}.macos.notarize.keychainProfile");
            }

            if (identity is not null && !MacIdentity.IsDeveloperId(identity))
            {
                Invalid(problems, $"{path}.macos.notarize: notarization requires a Developer ID Application identity; '{identity}' is not one.");
            }
        }

        var infoPlist = mac?.InfoPlist ?? PlistDictionary.Empty;
        foreach (var entry in infoPlist.Entries)
        {
            if (ReservedPlistKeys.TryGetValue(entry.Key, out var source))
            {
                Invalid(problems, $"{path}.macos.infoPlist.{entry.Key} cannot be set: it is set from the manifest's {source}.");
            }
        }

        return identity is null ? null : new MacOptions(identity, mac?.Entitlements ?? new Dictionary<string, bool>(), infoPlist, outputs, notaryProfile);
    }

    private static WindowsOptions CheckWindows(WindowsOptionsDocument? windows, string path, ICollection<Problem> problems)
    {
        var command = windows?.SignCommand;
        if (command is not null && (command.Count == 0 || !command.Any(argument => argument.Contains("{file}", StringComparison.Ordinal))))
        {
            Invalid(problems, $"{path}.windows.signCommand must contain {{file}}, which stands for the file to sign.");
        }

        return new WindowsOptions(command);
    }

    private static LinuxOptions CheckLinux(LinuxOptionsDocument? linux, string path, ICollection<Problem> problems)
    {
        var categories = linux?.Categories ?? ["Utility"];
        foreach (var category in categories)
        {
            if (category.Length == 0 || category.Contains(';', StringComparison.Ordinal))
            {
                Invalid(problems, $"{path}.linux.categories: '{category}' must be a non-empty desktop category without ';'.");
            }
        }

        return new LinuxOptions(categories);
    }

    private static string? Required(string? value, string path, ICollection<Problem> problems)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Missing(problems, path);
            return null;
        }

        return value;
    }

    private static void Missing(ICollection<Problem> problems, string path, string? detail = null) =>
        problems.Add(Problem.Error(ErrorCodes.ManifestMissingField, detail is null ? $"{path} is required." : $"{path}: {detail}."));

    private static void Invalid(ICollection<Problem> problems, string message) =>
        problems.Add(Problem.Error(ErrorCodes.ManifestInvalidValue, message));

    [GeneratedRegex(@"^[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+$")]
    private static partial Regex AppId();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
    private static partial Regex TargetName();
}
