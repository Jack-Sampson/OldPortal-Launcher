// Component: OPLauncher
// TODO: [LAUNCH-Migration] Create DTOs matching SharedAPI structure
// Description: Server type enum matching SharedAPI.Data.ServerType

namespace OPLauncher.DTOs;

/// <summary>
/// Asheron's Call emulator server type
/// </summary>
public enum ServerType
{
    /// <summary>
    /// Asheron's Call Emulator (ACE)
    /// </summary>
    ACE = 0,

    /// <summary>
    /// GDLE (GDL Enhanced)
    /// </summary>
    GDLE = 1
}
