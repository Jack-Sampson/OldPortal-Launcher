// Component: OPLauncher
// TODO: [LAUNCH-Migration] Create DTOs matching SharedAPI structure
// Description: World/server status enum

namespace OPLauncher.DTOs;

/// <summary>
/// World/server online status
/// </summary>
public enum WorldStatus
{
    /// <summary>
    /// Server is offline
    /// </summary>
    Offline = 0,

    /// <summary>
    /// Server is online and accepting connections
    /// </summary>
    Online = 1,

    /// <summary>
    /// Server status is unknown
    /// </summary>
    Unknown = 2,

    /// <summary>
    /// Server is under maintenance
    /// </summary>
    Maintenance = 3
}
