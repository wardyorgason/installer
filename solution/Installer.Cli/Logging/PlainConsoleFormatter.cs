using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Installer.Cli.Logging;

/// <summary>Writes just the message (prefixed with "warning:" or "error:"), without categories or event ids.</summary>
internal sealed class PlainConsoleFormatter() : ConsoleFormatter(Name)
{
    public new const string Name = "plain";

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (message is null)
        {
            return;
        }

        var prefix = logEntry.LogLevel switch
        {
            LogLevel.Warning => "warning: ",
            LogLevel.Error or LogLevel.Critical => "error: ",
            _ => string.Empty,
        };
        textWriter.WriteLine(prefix + message);
        if (logEntry.Exception is not null)
        {
            textWriter.WriteLine(logEntry.Exception.ToString());
        }
    }
}
