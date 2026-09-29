using Installer.Cli;
using Installer.Cli.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Installer.Cli;

public class ServiceRegistrationTests
{
    [Test]
    public void Composition_root_validates_on_build()
    {
        using var provider = ServiceRegistration.BuildProvider(new CliOptions(Verbose: false));

        Assert.That(provider.GetRequiredService<TimeProvider>(), Is.SameAs(TimeProvider.System));
    }

    [Test]
    public void Composition_root_resolves_every_command_handler()
    {
        using var provider = ServiceRegistration.BuildProvider(new CliOptions(Verbose: false));

        Assert.That(provider.GetRequiredService<IBuildCommandHandler>(), Is.Not.Null);
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public void Tool_output_is_logged_only_when_verbose(bool verbose, bool debugEnabled)
    {
        using var provider = ServiceRegistration.BuildProvider(new CliOptions(verbose));

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("test");
        Assert.Multiple(() =>
        {
            Assert.That(logger.IsEnabled(LogLevel.Debug), Is.EqualTo(debugEnabled));
            Assert.That(logger.IsEnabled(LogLevel.Information), Is.True);
        });
    }

    [Test]
    public void Every_log_level_goes_to_stderr()
    {
        using var provider = ServiceRegistration.BuildProvider(new CliOptions(Verbose: false));

        Assert.That(provider.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value.LogToStandardErrorThreshold, Is.EqualTo(LogLevel.Trace));
    }
}
