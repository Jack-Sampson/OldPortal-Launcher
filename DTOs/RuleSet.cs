// Component: OPLauncher
// TODO: [LAUNCH-Migration] Create DTOs matching SharedAPI structure
// Description: Server ruleset enum (for filtering in UI)

namespace OPLauncher.DTOs;

/// <summary>
/// Server ruleset type
/// </summary>
public enum RuleSet
{
    /// <summary>
    /// All rulesets
    /// </summary>
    All = 0,

    /// <summary>
    /// PvE (Player vs Environment) only
    /// </summary>
    PvE = 1,

    /// <summary>
    /// PvP (Player vs Player) enabled
    /// </summary>
    PvP = 2,

    /// <summary>
    /// Custom/modified ruleset
    /// </summary>
    Custom = 3,

    /// <summary>
    /// Roleplay (RP) ruleset
    /// </summary>
    RP = 4,

    /// <summary>
    /// Retail-like ruleset (mimicking original AC retail)
    /// </summary>
    Retail = 5,

    /// <summary>
    /// Hardcore ruleset - challenging difficulty, permadeath or other hardcore mechanics
    /// </summary>
    Hardcore = 6
}
