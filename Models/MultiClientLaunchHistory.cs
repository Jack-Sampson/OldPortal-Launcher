using System;

namespace OPLauncher.Models;

/// <summary>
/// Represents a historical record of a multi-client batch launch.
/// Used for tracking launch history and enabling "Launch Again" functionality.
/// </summary>
public class MultiClientLaunchHistory
{
    /// <summary>
    /// Gets or sets the unique identifier for this history entry.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the ID of the batch group that was launched.
    /// </summary>
    public Guid BatchGroupId { get; set; }

    /// <summary>
    /// Gets or sets the name of the batch at the time of launch.
    /// Stored separately in case batch is later renamed or deleted.
    /// </summary>
    public string BatchName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the world/server ID where the batch was launched.
    /// </summary>
    public int WorldId { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the batch was launched.
    /// </summary>
    public DateTime LaunchDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the total number of clients that were attempted to launch.
    /// </summary>
    public int TotalClients { get; set; }

    /// <summary>
    /// Gets or sets the number of clients that launched successfully.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets the number of clients that failed to launch.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Gets or sets the total duration of the launch sequence.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets whether all clients launched successfully.
    /// </summary>
    public bool IsFullSuccess => FailureCount == 0 && SuccessCount == TotalClients;

    /// <summary>
    /// Gets a display string showing the time elapsed since launch.
    /// </summary>
    public string TimeAgoDisplay
    {
        get
        {
            var timeAgo = DateTime.UtcNow - LaunchDateTime;
            if (timeAgo.TotalMinutes < 1)
                return "Just now";
            if (timeAgo.TotalMinutes < 60)
                return $"{(int)timeAgo.TotalMinutes}m ago";
            if (timeAgo.TotalHours < 24)
                return $"{(int)timeAgo.TotalHours}h ago";
            if (timeAgo.TotalDays < 7)
                return $"{(int)timeAgo.TotalDays}d ago";
            return LaunchDateTime.ToLocalTime().ToString("MMM dd");
        }
    }

    /// <summary>
    /// Gets a display string showing the launch result.
    /// </summary>
    public string ResultDisplay => IsFullSuccess
        ? $"✓ {SuccessCount}/{TotalClients} clients"
        : $"⚠ {SuccessCount}/{TotalClients} clients";
}
