namespace Installer.Dtos.Tools;

/// <summary>
/// An external tool invocation by tool name (resolved on PATH by the tool Dao). A tool still running after
/// <see cref="Timeout"/> is killed and fails with <c>tool.timeout</c>.
/// </summary>
public sealed record ToolCommand(
    string Tool,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    string? StandardInput = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    bool AllowFailure = false,
    TimeSpan? Timeout = null);

/// <summary>A process to start, with an absolute <see cref="FileName"/>.</summary>
public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    string? StandardInput = null,
    IReadOnlyDictionary<string, string>? Environment = null);

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>A code-signing identity listed by <c>security find-identity</c>.</summary>
public sealed record SigningIdentity(string Hash, string Name);

/// <summary>The outcome of a <c>notarytool submit --wait</c>. <see cref="Status"/> is Apple's text, e.g. <c>Accepted</c>.</summary>
public sealed record NotaryResult(string SubmissionId, string Status, string? Message);
