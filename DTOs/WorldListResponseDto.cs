// Component: OPLauncher
// TODO: [LAUNCH-Migration] Create DTOs matching SharedAPI structure
// Description: World list response DTO

using System.Text.Json.Serialization;

namespace OPLauncher.DTOs;

/// <summary>
/// World list response data transfer object
/// Contains list of worlds/servers from API
/// </summary>
public class WorldListResponseDto
{
    /// <summary>
    /// List of worlds/servers (API property name: "servers")
    /// </summary>
    [JsonPropertyName("servers")]
    public List<WorldDto> Worlds { get; set; } = new();

    /// <summary>
    /// Total count of worlds (for pagination)
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}
