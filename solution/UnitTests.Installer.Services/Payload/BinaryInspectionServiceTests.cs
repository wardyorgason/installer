using System.Buffers.Binary;
using Installer.Dao.Payload;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;
using Installer.Services.Payload;
using UnitTests.Installer.Services.TestSupport;

namespace UnitTests.Installer.Services.Payload;

public class BinaryInspectionServiceTests
{
    private Mock<IPayloadDao> _payloads = null!;
    private BinaryInspectionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _payloads = new Mock<IPayloadDao>();
        _service = new BinaryInspectionService(_payloads.Object);
    }

    [TestCase("pe-x64-apphost", BinaryFormat.Pe, CpuArch.X64)]
    [TestCase("macho-arm64-apphost", BinaryFormat.MachO, CpuArch.Arm64)]
    [TestCase("elf-x64-apphost", BinaryFormat.Elf, CpuArch.X64)]
    public void Reads_real_apphost_headers(string fixture, BinaryFormat format, CpuArch arch)
    {
        Assert.That(_service.Inspect(Fixtures.Header(fixture)), Is.EqualTo(new BinaryInfo(format, [arch])).Using<BinaryInfo>(SameInfo));
    }

    [Test]
    public void Reads_arm64_elf_and_pe()
    {
        var elf = new byte[64];
        "\u007fELF"u8.CopyTo(elf);
        elf[5] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(18), 0xB7);
        var pe = new byte[256];
        pe[0] = (byte)'M';
        pe[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(0x3C), 0x80);
        "PE\0\0"u8.CopyTo(pe.AsSpan(0x80));
        BinaryPrimitives.WriteUInt16LittleEndian(pe.AsSpan(0x84), 0xAA64);

        Assert.Multiple(() =>
        {
            Assert.That(_service.Inspect(elf).Architectures, Is.EqualTo(new[] { CpuArch.Arm64 }));
            Assert.That(_service.Inspect(pe).Architectures, Is.EqualTo(new[] { CpuArch.Arm64 }));
        });
    }

    [Test]
    public void Universal_binary_lists_every_slice()
    {
        Assert.That(_service.Inspect(Universal()), Is.EqualTo(new BinaryInfo(BinaryFormat.MachO, [CpuArch.X64, CpuArch.Arm64])).Using<BinaryInfo>(SameInfo));
    }

    [Test]
    public void Java_class_file_is_not_a_universal_binary()
    {
        var java = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE, 0x00, 0x00, 0x00, 0x41 }; // class file version 65

        Assert.Multiple(() =>
        {
            Assert.That(_service.Inspect(java).Format, Is.EqualTo(BinaryFormat.Unknown));
            Assert.That(_service.NeedsExecutePermission(java), Is.False);
        });
    }

    [Test]
    public void Execute_permission_is_for_native_binaries_and_scripts_only()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_service.NeedsExecutePermission(Fixtures.Header("macho-arm64-apphost")), Is.True);
            Assert.That(_service.NeedsExecutePermission(Fixtures.Header("elf-x64-apphost")), Is.True);
            Assert.That(_service.NeedsExecutePermission("#!/bin/sh\n"u8), Is.True);
            Assert.That(_service.NeedsExecutePermission(Fixtures.Header("pe-x64-apphost")), Is.False);
            Assert.That(_service.NeedsExecutePermission("{ \"json\": 1 }"u8), Is.False);
            Assert.That(_service.NeedsExecutePermission([]), Is.False);
        });
    }

    [Test]
    public void Windows_payload_given_to_a_mac_target()
    {
        _payloads.Setup(p => p.ReadHeader(It.IsAny<string>(), 4096)).Returns(Fixtures.Header("pe-x64-apphost"));

        var ex = Assert.Throws<BuildFailedException>(() => _service.CheckMainExecutable(
            new PreparedPayload("/p", ["App.exe"]), "App.exe", Fixtures.Target(TargetOs.MacOS, TargetArch.Arm64)));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Problem.Code, Is.EqualTo(ErrorCodes.PayloadPlatformMismatch));
            Assert.That(ex.Message, Does.Contain("is a Windows x64 binary, but the target is macOS arm64"));
        });
    }

    [Test]
    public void Wrong_architecture_names_both()
    {
        _payloads.Setup(p => p.ReadHeader(It.IsAny<string>(), 4096)).Returns(Fixtures.Header("elf-x64-apphost"));

        var ex = Assert.Throws<BuildFailedException>(() => _service.CheckMainExecutable(
            new PreparedPayload("/p", ["app"]), "app", Fixtures.Target(TargetOs.Linux, TargetArch.Arm64)));

        Assert.That(ex!.Message, Does.Contain("Linux x64 binary, but the target is Linux arm64"));
    }

    [Test]
    public void Universal_binary_matches_either_architecture()
    {
        _payloads.Setup(p => p.ReadHeader(It.IsAny<string>(), 4096)).Returns(Universal());

        Assert.Multiple(() =>
        {
            Assert.That(() => _service.CheckMainExecutable(new PreparedPayload("/p", ["a"]), "a", Fixtures.Target(TargetOs.MacOS, TargetArch.Arm64)), Throws.Nothing);
            Assert.That(() => _service.CheckMainExecutable(new PreparedPayload("/p", ["a"]), "a", Fixtures.Target(TargetOs.MacOS, TargetArch.X64)), Throws.Nothing);
        });
    }

    [Test]
    public void Matching_executable_passes_and_scripts_pass_on_unix()
    {
        _payloads.Setup(p => p.ReadHeader("/p/app", 4096)).Returns(Fixtures.Header("elf-x64-apphost"));
        _payloads.Setup(p => p.ReadHeader("/p/run.sh", 4096)).Returns("#!/bin/sh\nexec ./app"u8.ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(() => _service.CheckMainExecutable(new PreparedPayload("/p", ["app"]), "app", Fixtures.Target(TargetOs.Linux, TargetArch.X64)), Throws.Nothing);
            Assert.That(() => _service.CheckMainExecutable(new PreparedPayload("/p", ["run.sh"]), "run.sh", Fixtures.Target(TargetOs.Linux, TargetArch.Arm64)), Throws.Nothing);
        });
    }

    [Test]
    public void Unrecognized_file_fails()
    {
        _payloads.Setup(p => p.ReadHeader(It.IsAny<string>(), 4096)).Returns("hello"u8.ToArray());

        var ex = Assert.Throws<BuildFailedException>(() => _service.CheckMainExecutable(
            new PreparedPayload("/p", ["App.exe"]), "App.exe", Fixtures.Target(TargetOs.Windows, TargetArch.X64)));

        Assert.That(ex!.Message, Does.Contain("not a recognized executable"));
    }

    private static byte[] Universal()
    {
        var bytes = new byte[64];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 0xCAFEBABE);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 0x01000007);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(28), 0x0100000C);
        return bytes;
    }

    private static bool SameInfo(BinaryInfo a, BinaryInfo b) => a.Format == b.Format && a.Architectures.SequenceEqual(b.Architectures);
}
