using System.Reflection;

var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
Console.WriteLine($"Samples.DotnetApp {version}");
Console.WriteLine($"args: {string.Join(' ', args)}");
