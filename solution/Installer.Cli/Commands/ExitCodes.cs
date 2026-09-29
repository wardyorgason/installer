using Installer.Dtos.Build;

namespace Installer.Cli.Commands;

public static class ExitCodes
{
    public const int Success = 0;
    public const int TargetFailed = 1;
    public const int InvalidInput = 2;

    public static int For(BuildResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Errors.Count > 0 && result.Targets.Count == 0)
        {
            return InvalidInput;
        }

        return result.Succeeded ? Success : TargetFailed;
    }
}
