using Installer.Dao.Payload;
using Installer.Dao.Signing;
using Installer.Dtos.Signing;
using Installer.Services.Signing;
using Installer.Services.Windows;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Installer.Services.Signing;

public class CommandFileSignerTests
{
    [Test]
    public async Task Replaces_every_file_placeholder()
    {
        var commands = new Mock<ISignCommandDao>();
        var signer = new CommandFileSigner(commands.Object, NullLogger<CommandFileSigner>.Instance);

        await signer.SignAsync("/w/App.exe", new CommandSigningOptions(["osslsigncode", "sign", "-in", "{file}", "-out", "{file}.signed"]), CancellationToken.None);

        commands.Verify(c => c.RunAsync(
            It.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "osslsigncode", "sign", "-in", "/w/App.exe", "-out", "/w/App.exe.signed" })),
            It.IsAny<CancellationToken>()));
    }

    [Test]
    public void Rejects_other_signing_options()
    {
        var signer = new CommandFileSigner(new Mock<ISignCommandDao>().Object, NullLogger<CommandFileSigner>.Instance);

        Assert.That(() => signer.SignAsync("/a", new CodesignSigningOptions("X", true, false), CancellationToken.None), Throws.ArgumentException);
    }

    [Test]
    public async Task Sign_file_reads_the_command_written_for_the_nsis_hooks()
    {
        var files = new Mock<IPayloadDao>();
        files.Setup(f => f.ReadText("/w/sign-command.json")).Returns("""["sign","{file}"]""");
        var signer = new Mock<ICommandFileSigner>();

        await new WindowsSigningService(signer.Object, files.Object).SignFileFromCommandFileAsync("/w/uninstall.exe", "/w/sign-command.json", CancellationToken.None);

        signer.Verify(s => s.SignAsync("/w/uninstall.exe", It.Is<CommandSigningOptions>(o => o.Command.SequenceEqual(new[] { "sign", "{file}" })), It.IsAny<CancellationToken>()));
    }
}
