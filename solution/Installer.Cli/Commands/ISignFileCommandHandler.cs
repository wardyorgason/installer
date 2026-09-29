namespace Installer.Cli.Commands;

/// <summary>The hidden <c>sign-file</c> command the NSIS signing hooks call back into.</summary>
public interface ISignFileCommandHandler
{
    /// <summary>Signs the file with the command stored in <paramref name="commandJsonPath"/>; 0 on success, 1 on failure.</summary>
    Task<int> HandleAsync(string file, string commandJsonPath, CancellationToken cancellationToken);
}
