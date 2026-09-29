namespace Installer.Dtos.Manifest;

public enum TargetOs
{
    Windows,
    MacOS,
    Linux,
}

public enum TargetArch
{
    X64,
    Arm64,
}

public enum PayloadKind
{
    Zip,
    Directory,
}

public enum MacOutput
{
    Dmg,
    Zip,
}
