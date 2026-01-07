// TODO: [LAUNCH-095] Phase 1 Week 1 - Updated AppTheme Enum for New Design System
// Component: OPLauncher
// Module: UI Redesign - Design System
// Description: Application theme enumeration (Dark default, Light in Phase 5)

namespace OPLauncher.Models;

/// <summary>
/// Available application themes.
/// Part of the UI Redesign - Dark theme is default, Light theme deferred to Phase 5.
/// </summary>
public enum AppTheme
{
    /// <summary>
    /// Dark theme (default) - Portal Blue, Twilight Purple, dark backgrounds
    /// Background: #0D0D10, #1A1A1E, #2A2A2E
    /// Text: White, light gray
    /// </summary>
    Dark = 0,

    /// <summary>
    /// Light theme (Phase 5) - Same accent colors, light backgrounds
    /// Background: #FFFFFF, #F5F5F7, #E8E8ED
    /// Text: Black, dark gray
    /// </summary>
    Light = 1
}
