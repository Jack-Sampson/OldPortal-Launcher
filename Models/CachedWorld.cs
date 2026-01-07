using OPLauncher.DTOs;

namespace OPLauncher.Models;

/// <summary>
/// Represents a cached world entry with cache metadata for expiration tracking.
/// Used by WorldsService to implement 24-hour cache TTL for world data.
/// </summary>
public class CachedWorld
{
    /// <summary>
    /// Gets or sets the world data from the API.
    /// </summary>
    public WorldDto World { get; set; } = null!;

    /// <summary>
    /// Gets or sets when this world data was cached (UTC).
    /// </summary>
    public DateTime CachedAt { get; set; }

    /// <summary>
    /// Gets or sets the cache time-to-live in minutes.
    /// Default is 1440 minutes (24 hours) as worlds are added infrequently.
    /// </summary>
    public int CacheTtlMinutes { get; set; } = 1440;

    /// <summary>
    /// Checks if the cached data has expired based on the TTL.
    /// </summary>
    /// <returns>True if the cache is older than the TTL, false otherwise.</returns>
    public bool IsExpired()
    {
        return GetAge().TotalMinutes > CacheTtlMinutes;
    }

    /// <summary>
    /// Gets the age of the cached data.
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
        var expiresAt = CachedAt.AddMinutes(CacheTtlMinutes);
        var remaining = expiresAt - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Creates a new CachedWorld instance from a WorldDto.
    /// </summary>
    /// <param name="world">The world data to cache.</param>
    /// <returns>A new CachedWorld instance with the current timestamp.</returns>
    public static CachedWorld FromWorld(WorldDto world)
    {
        return new CachedWorld
        {
            World = world,
            CachedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new CachedWorld instance from a WorldDto with a custom cache TTL.
    /// </summary>
    /// <param name="world">The world data to cache.</param>
    /// <param name="cacheTtlMinutes">The cache time-to-live in minutes.</param>
    /// <returns>A new CachedWorld instance with the current timestamp and custom TTL.</returns>
    public static CachedWorld FromWorld(WorldDto world, int cacheTtlMinutes)
    {
        return new CachedWorld
        {
            World = world,
            CachedAt = DateTime.UtcNow,
            CacheTtlMinutes = cacheTtlMinutes
        };
    }
}
