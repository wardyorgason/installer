namespace Installer.Dtos.Build;

public enum Severity
{
    Warning,
    Error,
}

/// <summary>A warning or error. <see cref="Code"/> is a stable identifier from <see cref="ErrorCodes"/>; the message may change.</summary>
public sealed record Problem(string Code, string Message, Severity Severity = Severity.Error)
{
    public static Problem Error(string code, string message) => new(code, message, Severity.Error);

    public static Problem Warning(string code, string message) => new(code, message, Severity.Warning);
}
