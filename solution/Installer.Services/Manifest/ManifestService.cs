using System.Text.Json;
using System.Text.Json.Nodes;
using Installer.Dao.Manifest;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;

namespace Installer.Services.Manifest;

internal sealed class ManifestService(
    IManifestDao manifests,
    IEnvironmentExpansionService expansion,
    IManifestReader reader,
    IManifestValidationService validation) : IManifestService
{
    private static readonly JsonDocumentOptions ParseOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public ManifestLoadResult Load(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        var fullPath = Path.GetFullPath(manifestPath);
        var problems = new List<Problem>();
        if (!manifests.Exists(fullPath))
        {
            problems.Add(Problem.Error(ErrorCodes.ManifestNotFound, $"Manifest {fullPath} was not found."));
            return new ManifestLoadResult(null, problems);
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(manifests.ReadText(fullPath), documentOptions: ParseOptions);
        }
        catch (JsonException ex)
        {
            problems.Add(Problem.Error(ErrorCodes.ManifestInvalidJson, $"{fullPath} is not valid JSON: {ex.Message}"));
            return new ManifestLoadResult(null, problems);
        }

        if (root is null)
        {
            problems.Add(Problem.Error(ErrorCodes.ManifestWrongType, "$: the manifest must be a JSON object."));
            return new ManifestLoadResult(null, problems);
        }

        expansion.ExpandAll(root, problems);
        var document = Resolve(reader.Read(root, problems), Path.GetDirectoryName(fullPath)!);
        var manifest = validation.Validate(fullPath, document, problems);
        return new ManifestLoadResult(problems.Any(problem => problem.Severity == Severity.Error) ? null : manifest, problems);
    }

    /// <summary>
    /// Resolves relative paths against the manifest's directory, defaults target names to &lt;os&gt;-&lt;arch&gt;, and merges each
    /// top-level platform section into the targets of that OS. A target's sections for other OSes are kept as written
    /// so validation can reject them.
    /// </summary>
    internal static ManifestDocument Resolve(ManifestDocument document, string manifestDirectory) =>
        document with
        {
            Icon = ResolvePath(document.Icon, manifestDirectory),
            Targets = document.Targets?.Select(target => target with
            {
                Name = target.Name ?? (target.Os is not null && target.Arch is not null ? $"{target.Os}-{target.Arch}" : null),
                Payload = ResolvePath(target.Payload, manifestDirectory),
                MacOS = target.Os == "macos" ? Merge(document.MacOS, target.MacOS) : target.MacOS,
                Windows = target.Os == "windows" ? Merge(document.Windows, target.Windows) : target.Windows,
                Linux = target.Os == "linux" ? Merge(document.Linux, target.Linux) : target.Linux,
            }).ToList(),
        };

    private static string? ResolvePath(string? path, string baseDirectory) =>
        path is null ? null : Path.GetFullPath(Path.Combine(baseDirectory, path));

    internal static MacOptionsDocument? Merge(MacOptionsDocument? top, MacOptionsDocument? target)
    {
        if (top is null || target is null)
        {
            return target ?? top;
        }

        return new MacOptionsDocument
        {
            Identity = target.Identity ?? top.Identity,
            Entitlements = MergeMaps(top.Entitlements, target.Entitlements),
            InfoPlist = MergePlist(top.InfoPlist, target.InfoPlist),
            Outputs = target.Outputs ?? top.Outputs,
            Notarize = top.Notarize is null || target.Notarize is null
                ? target.Notarize ?? top.Notarize
                : new NotarizeDocument { KeychainProfile = target.Notarize.KeychainProfile ?? top.Notarize.KeychainProfile },
        };
    }

    internal static WindowsOptionsDocument? Merge(WindowsOptionsDocument? top, WindowsOptionsDocument? target) =>
        top is null || target is null ? target ?? top : new WindowsOptionsDocument { SignCommand = target.SignCommand ?? top.SignCommand };

    internal static LinuxOptionsDocument? Merge(LinuxOptionsDocument? top, LinuxOptionsDocument? target) =>
        top is null || target is null ? target ?? top : new LinuxOptionsDocument { Categories = target.Categories ?? top.Categories };

    private static IReadOnlyDictionary<string, bool>? MergeMaps(IReadOnlyDictionary<string, bool>? top, IReadOnlyDictionary<string, bool>? target)
    {
        if (top is null || target is null)
        {
            return target ?? top;
        }

        var merged = new Dictionary<string, bool>(top, StringComparer.Ordinal);
        foreach (var (key, value) in target)
        {
            merged[key] = value;
        }

        return merged;
    }

    /// <summary>Merges dictionaries key by key, recursing into nested dictionaries; any other value from the target replaces.</summary>
    internal static PlistDictionary? MergePlist(PlistDictionary? top, PlistDictionary? target)
    {
        if (top is null || target is null)
        {
            return target ?? top;
        }

        var entries = top.Entries.ToList();
        foreach (var entry in target.Entries)
        {
            var index = entries.FindIndex(existing => existing.Key == entry.Key);
            if (index < 0)
            {
                entries.Add(entry);
            }
            else if (entries[index].Value is PlistDictionary existingDictionary && entry.Value is PlistDictionary targetDictionary)
            {
                entries[index] = new KeyValuePair<string, PlistValue>(entry.Key, MergePlist(existingDictionary, targetDictionary)!);
            }
            else
            {
                entries[index] = entry;
            }
        }

        return new PlistDictionary(entries);
    }
}
