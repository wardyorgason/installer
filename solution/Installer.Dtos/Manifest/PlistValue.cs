namespace Installer.Dtos.Manifest;

/// <summary>A property list value from the manifest's <c>infoPlist</c>: the JSON types a plist can hold.</summary>
public abstract record PlistValue;

public sealed record PlistString(string Value) : PlistValue;

public sealed record PlistBoolean(bool Value) : PlistValue;

public sealed record PlistInteger(long Value) : PlistValue;

public sealed record PlistArray(IReadOnlyList<PlistValue> Items) : PlistValue;

/// <summary>Keys keep the order they were written in, so generated files are stable.</summary>
public sealed record PlistDictionary(IReadOnlyList<KeyValuePair<string, PlistValue>> Entries) : PlistValue
{
    public static PlistDictionary Empty { get; } = new([]);
}
