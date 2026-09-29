using System.Buffers.Binary;
using Installer.Dao.Payload;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;
using Installer.Dtos.Payload;

namespace Installer.Services.Payload;

internal sealed class BinaryInspectionService(IPayloadDao payloads) : IBinaryInspectionService
{
    public int HeaderLength => 4096;

    public BinaryInfo Inspect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 4 && header[0] == 0x7F && header[1] == (byte)'E' && header[2] == (byte)'L' && header[3] == (byte)'F')
        {
            return InspectElf(header);
        }

        if (header.Length >= 8 && IsMachO(header))
        {
            return InspectMachO(header);
        }

        if (header.Length >= 0x40 && header[0] == (byte)'M' && header[1] == (byte)'Z')
        {
            return InspectPe(header);
        }

        return new BinaryInfo(BinaryFormat.Unknown, []);
    }

    public bool NeedsExecutePermission(ReadOnlySpan<byte> header) =>
        (header.Length >= 2 && header[0] == (byte)'#' && header[1] == (byte)'!')
        || Inspect(header).Format is BinaryFormat.MachO or BinaryFormat.Elf;

    public void CheckMainExecutable(PreparedPayload payload, string mainExecutable, TargetSpec target)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(target);
        var header = payloads.ReadHeader(Path.Combine(payload.Root, mainExecutable), HeaderLength);
        if (target.Os != TargetOs.Windows && header.Length >= 2 && header[0] == (byte)'#' && header[1] == (byte)'!')
        {
            return; // a script runs on any Unix; there is no header to check
        }

        var info = Inspect(header);
        var expectedFormat = target.Os switch
        {
            TargetOs.Windows => BinaryFormat.Pe,
            TargetOs.MacOS => BinaryFormat.MachO,
            _ => BinaryFormat.Elf,
        };
        var expectedArch = target.Arch == TargetArch.Arm64 ? CpuArch.Arm64 : CpuArch.X64;
        if (info.Format == expectedFormat && info.Architectures.Contains(expectedArch))
        {
            return;
        }

        var actual = info.Format == BinaryFormat.Unknown
            ? "not a recognized executable"
            : $"a {FormatName(info.Format)} {string.Join('/', info.Architectures.Select(ArchName))} binary";
        throw new BuildFailedException(
            ErrorCodes.PayloadPlatformMismatch,
            $"Target '{target.Name}': the main executable {mainExecutable} is {actual}, but the target is {OsName(target.Os)} {ArchName(expectedArch)}.");
    }

    private static bool IsMachO(ReadOnlySpan<byte> header)
    {
        var magic = BinaryPrimitives.ReadUInt32BigEndian(header);
        return magic switch
        {
            0xFEEDFACE or 0xFEEDFACF or 0xCEFAEDFE or 0xCFFAEDFE => true,
            // Universal binaries share 0xCAFEBABE with Java class files, whose "count" (their version) is 45 or more.
            0xCAFEBABE or 0xCAFEBABF => BinaryPrimitives.ReadUInt32BigEndian(header[4..]) is > 0 and < 20,
            _ => false,
        };
    }

    private static BinaryInfo InspectMachO(ReadOnlySpan<byte> header)
    {
        var magic = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (magic is 0xCAFEBABE or 0xCAFEBABF)
        {
            var count = (int)BinaryPrimitives.ReadUInt32BigEndian(header[4..]);
            var entrySize = magic == 0xCAFEBABE ? 20 : 32;
            var archs = new List<CpuArch>();
            for (var i = 0; i < count && 8 + ((i + 1) * entrySize) <= header.Length; i++)
            {
                archs.Add(MachOCpu(BinaryPrimitives.ReadInt32BigEndian(header[(8 + (i * entrySize))..])));
            }

            return new BinaryInfo(BinaryFormat.MachO, archs);
        }

        var littleEndian = magic is 0xCEFAEDFE or 0xCFFAEDFE;
        var cpu = littleEndian ? BinaryPrimitives.ReadInt32LittleEndian(header[4..]) : BinaryPrimitives.ReadInt32BigEndian(header[4..]);
        return new BinaryInfo(BinaryFormat.MachO, [MachOCpu(cpu)]);
    }

    private static CpuArch MachOCpu(int cpuType) => cpuType switch
    {
        0x01000007 => CpuArch.X64,
        0x0100000C => CpuArch.Arm64,
        7 => CpuArch.X86,
        12 => CpuArch.Arm,
        _ => CpuArch.Other,
    };

    private static BinaryInfo InspectElf(ReadOnlySpan<byte> header)
    {
        if (header.Length < 20)
        {
            return new BinaryInfo(BinaryFormat.Elf, [CpuArch.Other]);
        }

        var machine = header[5] == 2 ? BinaryPrimitives.ReadUInt16BigEndian(header[18..]) : BinaryPrimitives.ReadUInt16LittleEndian(header[18..]);
        var arch = machine switch
        {
            0x3E => CpuArch.X64,
            0xB7 => CpuArch.Arm64,
            0x03 => CpuArch.X86,
            0x28 => CpuArch.Arm,
            _ => CpuArch.Other,
        };
        return new BinaryInfo(BinaryFormat.Elf, [arch]);
    }

    private static BinaryInfo InspectPe(ReadOnlySpan<byte> header)
    {
        var offset = BinaryPrimitives.ReadInt32LittleEndian(header[0x3C..]);
        if (offset < 0 || offset + 6 > header.Length || !header.Slice(offset, 4).SequenceEqual("PE\0\0"u8))
        {
            return new BinaryInfo(BinaryFormat.Unknown, []);
        }

        var arch = BinaryPrimitives.ReadUInt16LittleEndian(header[(offset + 4)..]) switch
        {
            0x8664 => CpuArch.X64,
            0xAA64 => CpuArch.Arm64,
            0x014C => CpuArch.X86,
            0x01C4 => CpuArch.Arm,
            _ => CpuArch.Other,
        };
        return new BinaryInfo(BinaryFormat.Pe, [arch]);
    }

    private static string FormatName(BinaryFormat format) => format switch
    {
        BinaryFormat.Pe => "Windows",
        BinaryFormat.MachO => "macOS",
        _ => "Linux",
    };

    private static string OsName(TargetOs os) => os switch
    {
        TargetOs.Windows => "Windows",
        TargetOs.MacOS => "macOS",
        _ => "Linux",
    };

    private static string ArchName(CpuArch arch) => arch switch
    {
        CpuArch.X64 => "x64",
        CpuArch.Arm64 => "arm64",
        CpuArch.X86 => "x86",
        CpuArch.Arm => "arm",
        _ => "unknown-architecture",
    };
}
