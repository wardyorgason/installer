namespace Installer.Dtos.Build;

/// <summary>Thrown by any build step to fail the current target; the pipeline catches it and records <see cref="Problem"/>.</summary>
public sealed class BuildFailedException : Exception
{
    public BuildFailedException(Problem problem)
        : base(problem?.Message)
    {
        ArgumentNullException.ThrowIfNull(problem);
        Problem = problem;
    }

    public BuildFailedException(string code, string message)
        : this(Problem.Error(code, message))
    {
    }

    public Problem Problem { get; }
}
