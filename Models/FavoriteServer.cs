// TODO: [LAUNCH-126] Phase 3 Week 6 - FavoriteServer Model
// Component: Launcher
// Module: UI Redesign - Server Details & Favorites
// Description: LiteDB model for tracking favorite servers

using System;

namespace OPLauncher.Models;

/// <summary>
/// Represents a favorite server stored locally in LiteDB.
/// Supports both API-synced servers (WorldDto) and manual servers (ManualServer).
/// </summary>
public class FavoriteServer
{
    /// <summary>
    /// Unique identifier for this favorite entry.
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
    /// Server name (cached for display without loading full server data).
    /// </summary>
    public string ServerName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a manual server (true) or API server (false).
    /// </summary>
    public bool IsManualServer { get; set; }

    /// <summary>
    /// When this server was added to favorites.
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Sort order for custom favorite ordering (future feature).
    /// </summary>
    public int SortOrder { get; set; } = 0;
}
