using System.Text.Json.Nodes;
using Installer.Dtos.Build;

namespace Installer.Services.Manifest;

/// <summary>Replaces <c>${env:NAME}</c> references with environment variable values. No other syntax is interpreted.</summary>
public interface IEnvironmentExpansionService
{
    /// <summary>Expands one string; an unset variable adds a problem naming it and <paramref name="jsonPath"/>.</summary>
    string Expand(string value, string jsonPath, ICollection<Problem> problems);

    /// <summary>Expands every string value in the tree in place. Property names are left alone.</summary>
    void ExpandAll(JsonNode? root, ICollection<Problem> problems);
}
