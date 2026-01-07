// TODO: [LAUNCH-128] Phase 3 Week 6 - RecentServer Model
// Component: Launcher
// Module: UI Redesign - Server Details & Favorites
// Description: LiteDB model for tracking recently played servers

using System;

namespace OPLauncher.Models;

/// <summary>
/// Represents a recently played server stored locally in LiteDB.
/// Automatically tracks when user connects to servers.
/// </summary>
public class RecentServer
{
    /// <summary>
    /// Unique identifier for this recent entry.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Server ID from API (for WorldDto servers). Null for manual servers.
    /// </summary>
    public Guid? WorldServerId { get; set; }

    /// <summary>
    /// Manual server ID (for ManualServer). Null for API servers.
    /// </summary>
    public int? ManualServerId { get; set; }

    /// <summary>
    /// Server name (cached for display).
    /// </summary>
    public string ServerName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a manual server (true) or API server (false).
    /// </summary>
    public bool IsManualServer { get; set; }

    /// <summary>
    /// When user last played on this server.
    /// </summary>
    public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of times user has connected to this server.
    /// </summary>
    public int PlayCount { get; set; } = 1;

    /// <summary>
    /// Total playtime duration in minutes (future feature).
    /// </summary>
    public int TotalPlaytimeMinutes { get; set; } = 0;
}
