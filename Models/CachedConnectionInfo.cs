using OPLauncher.DTOs;

namespace OPLauncher.Models;

/// <summary>
/// Represents cached connection information for a world server.
/// Used to enable offline mode - allows launching games even when API is unreachable.
/// </summary>
public class CachedConnectionInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for this cache entry (LiteDB auto-increment).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the server ID (Guid) this connection info belongs to.
    /// </summary>
    public Guid ServerId { get; set; }

    /// <summary>
    /// Gets or sets the connection information (host, port, server type, etc.).
    /// </summary>
    public WorldConnectionDto ConnectionInfo { get; set; } = null!;

    /// <summary>
    /// Gets or sets when this connection info was cached (UTC).
    /// </summary>
    public DateTime CachedAt { get; set; }

    /// <summary>
    /// Gets or sets the cache time-to-live in hours.
    /// Connection info changes rarely, so we use a longer TTL (24 hours default).
    /// </summary>
    public int CacheTtlHours { get; set; } = 24;

    /// <summary>
    /// Checks if the cached connection info has expired based on the TTL.
    /// </summary>
    /// <returns>True if the cache is older than the TTL, false otherwise.</returns>
    public bool IsExpired()
    {
        return GetAge().TotalHours > CacheTtlHours;
    }

    /// <summary>
    /// Gets the age of the cached connection info.
    /// </summary>
    /// <returns>A TimeSpan representing how old the cached data is.</returns>
    public TimeSpan GetAge()
    {
        return DateTime.UtcNow - CachedAt;
    }

    /// <summary>
    /// Gets the time remaining until the cache expires.
    /// </summary>
    /// <returns>A TimeSpan representing time until expiration, or TimeSpan.Zero if already expired.</returns>
    public TimeSpan GetTimeUntilExpiration()
    {
        var expiresAt = CachedAt.AddHours(CacheTtlHours);
        var remaining = expiresAt - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Creates a new CachedConnectionInfo instance from connection data.
    /// </summary>
    /// <param name="serverId">The server ID (Guid).</param>
    /// <param name="connectionInfo">The connection information to cache.</param>
    /// <returns>A new CachedConnectionInfo instance with the current timestamp.</returns>
    public static CachedConnectionInfo FromConnectionInfo(Guid serverId, WorldConnectionDto connectionInfo)
    {
        return new CachedConnectionInfo
        {
            ServerId = serverId,
            ConnectionInfo = connectionInfo,
            CachedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new CachedConnectionInfo instance with a custom cache TTL.
    /// </summary>
    /// <param name="serverId">The server ID (Guid).</param>
    /// <param name="connectionInfo">The connection information to cache.</param>
    /// <param name="cacheTtlHours">The cache time-to-live in hours.</param>
    /// <returns>A new CachedConnectionInfo instance with the current timestamp and custom TTL.</returns>
    public static CachedConnectionInfo FromConnectionInfo(Guid serverId, WorldConnectionDto connectionInfo, int cacheTtlHours)
    {
        return new CachedConnectionInfo
        {
            ServerId = serverId,
            ConnectionInfo = connectionInfo,
            CachedAt = DateTime.UtcNow,
            CacheTtlHours = cacheTtlHours
        };
    }

    /// <summary>
    /// Gets a formatted string showing the cache age (e.g., "2 hours ago", "30 minutes ago").
    /// </summary>
    /// <returns>A human-readable cache age string.</returns>
    public string GetFormattedAge()
    {
        var age = GetAge();

        if (age.TotalMinutes < 1)
            return "just now";
        if (age.TotalMinutes < 60)
            return $"{(int)age.TotalMinutes} minute{((int)age.TotalMinutes == 1 ? "" : "s")} ago";
        if (age.TotalHours < 24)
            return $"{(int)age.TotalHours} hour{((int)age.TotalHours == 1 ? "" : "s")} ago";

        return $"{(int)age.TotalDays} day{((int)age.TotalDays == 1 ? "" : "s")} ago";
    }
}
