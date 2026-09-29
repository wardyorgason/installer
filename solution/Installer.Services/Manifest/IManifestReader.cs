using System.Text.Json.Nodes;
using Installer.Dtos.Build;
using Installer.Dtos.Manifest;

namespace Installer.Services.Manifest;

/// <summary>
/// Turns the (expanded) JSON tree into a <see cref="ManifestDocument"/>. Every unknown property and wrong type is
/// reported with its JSON path; reading continues so one pass reports every problem.
/// </summary>
public interface IManifestReader
{
    ManifestDocument Read(JsonNode root, ICollection<Problem> problems);
}
