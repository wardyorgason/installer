using System.Text.Json;
using System.Text.Json.Nodes;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;

namespace Installer.Services.Manifest;

internal sealed class ManifestReader : IManifestReader
{
    private static readonly string[] RootKeys =
        ["schemaVersion", "id", "name", "version", "displayVersion", "publisher", "description", "icon", "profile", "executable", "macos", "windows", "linux", "targets"];

    private static readonly string[] TargetKeys = ["name", "os", "arch", "payload", "macos", "windows", "linux"];
    private static readonly string[] MacKeys = ["identity", "entitlements", "infoPlist", "outputs", "notarize"];
    private static readonly string[] NotarizeKeys = ["keychainProfile"];
    private static readonly string[] WindowsKeys = ["signCommand"];
    private static readonly string[] LinuxKeys = ["categories"];

    public ManifestDocument Read(JsonNode root, ICollection<Problem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        var reader = new Reader(problems);
        if (root is not JsonObject obj)
        {
            problems.Add(Problem.Error(ErrorCodes.ManifestWrongType, "$: the manifest must be a JSON object."));
            return new ManifestDocument();
        }

        reader.CheckKeys(obj, RootKeys);
        return new ManifestDocument
        {
            SchemaVersion = reader.Integer(obj, "schemaVersion"),
            Id = reader.String(obj, "id"),
            Name = reader.String(obj, "name"),
            Version = reader.String(obj, "version"),
            DisplayVersion = reader.String(obj, "displayVersion"),
            Publisher = reader.String(obj, "publisher"),
            Description = reader.String(obj, "description"),
            Icon = reader.String(obj, "icon"),
            Profile = reader.String(obj, "profile"),
            Executable = reader.String(obj, "executable"),
            MacOS = ReadMac(reader, reader.Object(obj, "macos")),
            Windows = ReadWindows(reader, reader.Object(obj, "windows")),
            Linux = ReadLinux(reader, reader.Object(obj, "linux")),
            Targets = reader.Array(obj, "targets")?.Select((node, index) => ReadTarget(reader, node, index)).OfType<TargetDocument>().ToList(),
        };
    }

    private static TargetDocument? ReadTarget(Reader reader, JsonNode? node, int index)
    {
        if (node is not JsonObject obj)
        {
            reader.WrongType($"$.targets[{index}]", "an object");
            return null;
        }

        reader.CheckKeys(obj, TargetKeys);
        return new TargetDocument
        {
            Path = $"$.targets[{index}]",
            Name = reader.String(obj, "name"),
            Os = reader.String(obj, "os"),
            Arch = reader.String(obj, "arch"),
            Payload = reader.String(obj, "payload"),
            MacOS = ReadMac(reader, reader.Object(obj, "macos")),
            Windows = ReadWindows(reader, reader.Object(obj, "windows")),
            Linux = ReadLinux(reader, reader.Object(obj, "linux")),
        };
    }

    private static MacOptionsDocument? ReadMac(Reader reader, JsonObject? obj)
    {
        if (obj is null)
        {
            return null;
        }

        reader.CheckKeys(obj, MacKeys);
        var notarize = reader.Object(obj, "notarize");
        if (notarize is not null)
        {
            reader.CheckKeys(notarize, NotarizeKeys);
        }

        return new MacOptionsDocument
        {
            Identity = reader.String(obj, "identity"),
            Entitlements = reader.BooleanMap(obj, "entitlements"),
            InfoPlist = reader.Plist(obj, "infoPlist"),
            Outputs = reader.StringArray(obj, "outputs"),
            Notarize = notarize is null ? null : new NotarizeDocument { KeychainProfile = reader.String(notarize, "keychainProfile") },
        };
    }

    private static WindowsOptionsDocument? ReadWindows(Reader reader, JsonObject? obj)
    {
        if (obj is null)
        {
            return null;
        }

        reader.CheckKeys(obj, WindowsKeys);
        return new WindowsOptionsDocument { SignCommand = reader.StringArray(obj, "signCommand") };
    }

    private static LinuxOptionsDocument? ReadLinux(Reader reader, JsonObject? obj)
    {
        if (obj is null)
        {
            return null;
        }

        reader.CheckKeys(obj, LinuxKeys);
        return new LinuxOptionsDocument { Categories = reader.StringArray(obj, "categories") };
    }

    /// <summary>Typed accessors that treat JSON null as absent and record a problem for any other type mismatch.</summary>
    private sealed class Reader(ICollection<Problem> problems)
    {
        public void CheckKeys(JsonObject obj, string[] allowed)
        {
            foreach (var (key, value) in obj)
            {
                if (!allowed.Contains(key, StringComparer.Ordinal))
                {
                    problems.Add(Problem.Error(ErrorCodes.ManifestUnknownProperty, $"Unknown property {Child(obj, key)}."));
                }
            }
        }

        public void WrongType(string path, string expected) =>
            problems.Add(Problem.Error(ErrorCodes.ManifestWrongType, $"{path} must be {expected}."));

        public string? String(JsonObject obj, string key)
        {
            var node = obj[key];
            if (node is null)
            {
                return null;
            }

            if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
            {
                return value.GetValue<string>();
            }

            WrongType(Child(obj, key), "a string");
            return null;
        }

        public int? Integer(JsonObject obj, string key)
        {
            var node = obj[key];
            if (node is null)
            {
                return null;
            }

            if (node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<int>(out var number))
            {
                return number;
            }

            WrongType(Child(obj, key), "an integer");
            return null;
        }

        public JsonObject? Object(JsonObject obj, string key)
        {
            var node = obj[key];
            if (node is null or JsonObject)
            {
                return (JsonObject?)node;
            }

            WrongType(Child(obj, key), "an object");
            return null;
        }

        public JsonArray? Array(JsonObject obj, string key)
        {
            var node = obj[key];
            if (node is null or JsonArray)
            {
                return (JsonArray?)node;
            }

            WrongType(Child(obj, key), "an array");
            return null;
        }

        public IReadOnlyList<string>? StringArray(JsonObject obj, string key)
        {
            var array = Array(obj, key);
            if (array is null)
            {
                return null;
            }

            var items = new List<string>();
            for (var i = 0; i < array.Count; i++)
            {
                if (array[i] is JsonValue value && value.GetValueKind() == JsonValueKind.String)
                {
                    items.Add(value.GetValue<string>());
                }
                else
                {
                    WrongType($"{Child(obj, key)}[{i}]", "a string");
                }
            }

            return items;
        }

        public IReadOnlyDictionary<string, bool>? BooleanMap(JsonObject obj, string key)
        {
            var map = Object(obj, key);
            if (map is null)
            {
                return null;
            }

            var result = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var (name, node) in map)
            {
                if (node is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
                {
                    result[name] = value.GetValue<bool>();
                }
                else
                {
                    WrongType(Child(map, name), "true or false");
                }
            }

            return result;
        }

        public PlistDictionary? Plist(JsonObject obj, string key)
        {
            var map = Object(obj, key);
            return map is null ? null : PlistDictionaryOf(map);
        }

        private PlistDictionary PlistDictionaryOf(JsonObject obj) =>
            new(obj.Select(pair => new KeyValuePair<string, PlistValue?>(pair.Key, PlistValueOf(pair.Value, Child(obj, pair.Key))))
                .Where(pair => pair.Value is not null)
                .Select(pair => new KeyValuePair<string, PlistValue>(pair.Key, pair.Value!))
                .ToList());

        private PlistValue? PlistValueOf(JsonNode? node, string path)
        {
            switch (node)
            {
                case JsonObject obj:
                    return PlistDictionaryOf(obj);
                case JsonArray array:
                    return new PlistArray(array.Select((item, i) => PlistValueOf(item, $"{path}[{i}]")).OfType<PlistValue>().ToList());
                case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                    return new PlistString(value.GetValue<string>());
                case JsonValue value when value.GetValueKind() is JsonValueKind.True or JsonValueKind.False:
                    return new PlistBoolean(value.GetValue<bool>());
                case JsonValue value when value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<long>(out var number):
                    return new PlistInteger(number);
                default:
                    WrongType(path, "a string, boolean, integer, array or object (plist values)");
                    return null;
            }
        }

        /// <summary>
        /// A child's JSON path built from its parent's, so keys missing from the tree (and keys with dots) still read well.
        /// </summary>
        private static string Child(JsonObject parent, string key) =>
            key.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ? $"{parent.GetPath()}.{key}" : $"{parent.GetPath()}['{key}']";
    }
}
