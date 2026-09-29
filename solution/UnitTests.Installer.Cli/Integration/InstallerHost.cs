using System.Text.Json;
using System.Text.Json.Nodes;
using Installer.Cli;
using Installer.Cli.Commands;
using Installer.Cli.Output;
using Installer.Dao.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UnitTests.Installer.Cli.Integration;

/// <summary>The real composed builder (every service and Dao), with stdout captured, working in a temp directory.</summary>
public sealed class InstallerHost : IDisposable
{
    private readonly ServiceProvider _provider;

    public InstallerHost()
    {
        Directory = Path.Combine(Path.GetTempPath(), "installer-tests", "runs", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        var services = new ServiceCollection().AddInstaller(new CliOptions(Verbose: true));
        services.AddSingleton<IConsoleWrapper>(Console);
        services.AddLogging(builder => builder.AddProvider(new CollectingLoggerProvider(Logs)));
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        IconPath = WriteIcon(Path.Combine(Directory, "icon.png"));
    }

    public string Directory { get; }

    public string IconPath { get; }

    public StringConsole Console { get; } = new();

    /// <summary>Every log message of the run, for assertions about what the builder did.</summary>
    public List<string> Logs { get; } = [];

    public string? FindTool(string tool) => _provider.GetRequiredService<IToolDao>().FindTool(tool);

    /// <summary>Writes a manifest for the given targets into the run directory and returns its path.</summary>
    public string WriteManifest(string profile, JsonArray targets, string? executable = null, JsonObject? extra = null)
    {
        var manifest = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["id"] = "dev.installer.samples",
            ["name"] = "Samples",
            ["version"] = "1.2.3.4",
            ["displayVersion"] = "1.2.3-b4",
            ["publisher"] = "Installer Tests",
            ["description"] = "Integration test sample",
            ["icon"] = IconPath,
            ["profile"] = profile,
            ["targets"] = targets,
        };
        if (executable is not null)
        {
            manifest["executable"] = executable;
        }

        foreach (var (key, value) in extra ?? [])
        {
            manifest[key] = value?.DeepClone();
        }

        var path = Path.Combine(Directory, "installer.json");
        File.WriteAllText(path, manifest.ToJsonString());
        return path;
    }

    public async Task<(int ExitCode, JsonDocument Result)> BuildAsync(string manifestPath, params string[] targets)
    {
        var handler = _provider.GetRequiredService<IBuildCommandHandler>();
        var exit = await handler.HandleAsync(new BuildCommandArguments(manifestPath, Path.Combine(Directory, "dist"), targets), CancellationToken.None);
        return (exit, JsonDocument.Parse(Console.OutWriter.ToString()));
    }

    public void Dispose()
    {
        _provider.Dispose();
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }

    /// <summary>A 1024×1024 opaque PNG, written without an imaging library.</summary>
    private static string WriteIcon(string path)
    {
        const int size = 1024;
        var raw = new byte[size * ((size * 4) + 1)];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var i = (y * ((size * 4) + 1)) + 1 + (x * 4);
                raw[i] = 40;
                raw[i + 1] = 120;
                raw[i + 2] = 220;
                raw[i + 3] = 255;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        using var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, size);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), size);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        File.WriteAllBytes(path, png.ToArray());
        return path;

        static void Chunk(Stream stream, string type, byte[] data)
        {
            var length = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            stream.Write(length);
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes);
            stream.Write(data);
            var crc = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(crc, System.IO.Hashing.Crc32.HashToUInt32([.. typeBytes, .. data]));
            stream.Write(crc);
        }
    }

    private sealed class CollectingLoggerProvider(List<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CollectingLogger(logs);

        public void Dispose()
        {
        }
    }

    private sealed class CollectingLogger(List<string> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (logs)
            {
                logs.Add(formatter(state, exception));
            }
        }
    }
}
