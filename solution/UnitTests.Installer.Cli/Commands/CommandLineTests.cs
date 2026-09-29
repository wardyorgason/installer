using System.CommandLine;
using System.Text.Json;
using Installer.Cli;
using Installer.Cli.Commands;
using Installer.Cli.Output;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Installer.Cli.Commands;

public class CommandLineTests
{
    private Mock<IBuildCommandHandler> _handler = null!;
    private Mock<ISignFileCommandHandler> _signFile = null!;
    private StringConsole _console = null!;
    private CliOptions? _options;
    private BuildCommandArguments? _arguments;

    [SetUp]
    public void SetUp()
    {
        _options = null;
        _arguments = null;
        _console = new StringConsole();
        _signFile = new Mock<ISignFileCommandHandler>();
        _handler = new Mock<IBuildCommandHandler>();
        _handler.Setup(h => h.HandleAsync(It.IsAny<BuildCommandArguments>(), It.IsAny<CancellationToken>()))
            .Callback<BuildCommandArguments, CancellationToken>((a, _) => _arguments = a)
            .ReturnsAsync(1);
    }

    private ServiceProvider Provider(CliOptions options)
    {
        _options = options;
        return new ServiceCollection()
            .AddSingleton(_handler.Object)
            .AddSingleton(_signFile.Object)
            .AddSingleton<IConsoleWrapper>(_console)
            .AddSingleton<IResultWriter, ResultWriter>()
            .BuildServiceProvider();
    }

    private Task<int> Run(params string[] args) =>
        CommandLine.InvokeAsync(args, Provider, new InvocationConfiguration { Output = _console.Out, Error = _console.Error });

    [Test]
    public async Task Build_everything()
    {
        var exit = await Run("build", "installer.json");

        Assert.Multiple(() =>
        {
            Assert.That(exit, Is.EqualTo(1), "the handler's exit code is returned");
            Assert.That(_arguments, Is.EqualTo(new BuildCommandArguments("installer.json", null, _arguments!.Targets)));
            Assert.That(_arguments!.Targets, Is.Empty);
            Assert.That(_options!.Verbose, Is.False);
        });
    }

    [Test]
    public async Task Build_selected_targets_into_an_output_directory_verbosely()
    {
        await Run("build", "installer.json", "--target", "windows-x64", "-t", "linux-x64", "--output", "/tmp/out", "--verbose");

        Assert.Multiple(() =>
        {
            Assert.That(_arguments!.Targets, Is.EqualTo(new[] { "windows-x64", "linux-x64" }));
            Assert.That(_arguments.OutputDirectory, Is.EqualTo("/tmp/out"));
            Assert.That(_options!.Verbose, Is.True);
        });
    }

    [Test]
    public async Task Invalid_arguments_exit_2_with_a_result_on_stdout()
    {
        var exit = await Run("build");

        using var result = JsonDocument.Parse(_console.OutWriter.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(exit, Is.EqualTo(2));
            Assert.That(result.RootElement.GetProperty("errors")[0].GetProperty("code").GetString(), Is.EqualTo("cli.invalid-arguments"));
            Assert.That(result.RootElement.GetProperty("targets").GetArrayLength(), Is.Zero);
            Assert.That(_console.ErrorWriter.ToString(), Does.Contain("error:"));
        });
        _handler.Verify(h => h.HandleAsync(It.IsAny<BuildCommandArguments>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Version_prints_and_exits_0()
    {
        var exit = await Run("--version");

        Assert.Multiple(() =>
        {
            Assert.That(exit, Is.Zero);
            Assert.That(_console.OutWriter.ToString().Trim(), Is.Not.Empty.And.Match(@"^\d+\.\d+\.\d+"));
        });
    }

    [Test]
    public async Task Hidden_sign_file_command_reaches_its_handler()
    {
        _signFile.Setup(h => h.HandleAsync("/w/setup.exe", "/w/sign.json", It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var exit = await Run("sign-file", "/w/setup.exe", "--command-json", "/w/sign.json");

        Assert.That(exit, Is.Zero);
        _signFile.Verify(h => h.HandleAsync("/w/setup.exe", "/w/sign.json", It.IsAny<CancellationToken>()));
    }
}
