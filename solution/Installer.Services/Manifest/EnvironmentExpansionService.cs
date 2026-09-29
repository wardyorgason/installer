using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Installer.Dao.Host;
using Installer.Dtos.Build;

namespace Installer.Services.Manifest;

internal sealed partial class EnvironmentExpansionService(IEnvironmentDao environment) : IEnvironmentExpansionService
{
    public string Expand(string value, string jsonPath, ICollection<Problem> problems)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(problems);
        return Reference().Replace(value, match =>
        {
            var name = match.Groups["name"].Value;
            var resolved = environment.GetVariable(name);
            if (resolved is null)
            {
                problems.Add(Problem.Error(ErrorCodes.ManifestEnvUnset, $"{jsonPath}: environment variable {name} is not set."));
                return match.Value;
            }

            return resolved;
        });
    }

    public void ExpandAll(JsonNode? root, ICollection<Problem> problems)
    {
        switch (root)
        {
            case JsonObject obj:
                foreach (var (key, child) in obj.ToList())
                {
                    if (IsString(child, out var text))
                    {
                        obj[key] = Expand(text, child!.GetPath(), problems);
                    }
                    else
                    {
                        ExpandAll(child, problems);
                    }
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (IsString(array[i], out var text))
                    {
                        array[i] = Expand(text, array[i]!.GetPath(), problems);
                    }
                    else
                    {
                        ExpandAll(array[i], problems);
                    }
                }

                break;
        }
    }

    private static bool IsString(JsonNode? node, out string text)
    {
        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
        {
            text = value.GetValue<string>();
            return true;
        }

        text = string.Empty;
        return false;
    }

    [GeneratedRegex(@"\$\{env:(?<name>[A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex Reference();
}
