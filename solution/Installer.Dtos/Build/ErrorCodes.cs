namespace Installer.Dtos.Build;

/// <summary>Stable codes for every warning and error. They are part of the public result contract: never rename one.</summary>
public static class ErrorCodes
{
    public const string ManifestNotFound = "manifest.not-found";
    public const string ManifestInvalidJson = "manifest.invalid-json";
    public const string ManifestUnknownProperty = "manifest.unknown-property";
    public const string ManifestWrongType = "manifest.wrong-type";
    public const string ManifestMissingField = "manifest.missing-field";
    public const string ManifestInvalidValue = "manifest.invalid-value";
    public const string ManifestUnsupportedSchema = "manifest.unsupported-schema";
    public const string ManifestEnvUnset = "manifest.env-unset";
    public const string ManifestDuplicateTarget = "manifest.duplicate-target";

    public const string CliUnknownTarget = "cli.unknown-target";
    public const string CliInvalidArguments = "cli.invalid-arguments";

    public const string HostUnsupported = "host.unsupported";
    public const string ToolMissing = "tool.missing";
    public const string ToolVersion = "tool.version";
    public const string ToolFailed = "tool.failed";
    public const string ToolTimeout = "tool.timeout";
    public const string DockerUnreachable = "docker.unreachable";
    public const string IdentityMissing = "identity.missing";

    public const string PayloadNotFound = "payload.not-found";
    public const string PayloadPathEscape = "payload.path-escape";
    public const string PayloadExecutableMissing = "payload.executable-missing";
    public const string PayloadPlatformMismatch = "payload.platform-mismatch";

    public const string ProfileNoRuntimeConfig = "profile.no-runtimeconfig";
    public const string ProfileAmbiguousApp = "profile.ambiguous-app";
    public const string ProfileNoAppHost = "profile.no-apphost";
    public const string ProfileRidMismatch = "profile.rid-mismatch";
    public const string DotnetRuntimeRequired = "dotnet.runtime-required";

    public const string BundleNameCollision = "bundle.name-collision";
    public const string SignFailed = "sign.failed";
    public const string NotarizeRejected = "notarize.rejected";

    public const string Unexpected = "internal.unexpected";
}
