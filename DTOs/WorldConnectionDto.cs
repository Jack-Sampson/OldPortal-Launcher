// Component: OPLauncher
// TODO: [LAUNCH-Migration] Create DTOs matching SharedAPI structure
// Description: World connection information DTO

namespace OPLauncher.DTOs;

/// <summary>
/// Connection information for a world/server
/// </summary>
public class WorldConnectionDto
{
    /// <summary>
    /// Server hostname or IP address
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Server port number
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Emulator type (ACE or GDLE)
    /// </summary>
    public ServerType ServerType { get; set; }

    /// <summary>
    /// Server display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Alias for Name for compatibility with reference code
    /// </summary>
    public string WorldName => Name;

    /// <summary>
    /// World/Server ID (0 for manual servers)
    /// </summary>
    public int WorldId { get; set; }

    /// <summary>
    /// Optional server description
    /// </summary>
    public string? Description { get; set; }
}
