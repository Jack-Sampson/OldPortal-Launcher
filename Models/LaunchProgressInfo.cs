namespace OPLauncher.Models;

/// <summary>
/// Progress information during game launch verification
/// </summary>
public class LaunchProgressInfo
{
    /// <summary>
    /// Number of seconds elapsed since launch started
    /// </summary>
    public int ElapsedSeconds { get; set; }

    /// <summary>
    /// Total timeout in seconds (typically 30)
    /// </summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// Current status message (e.g., "Starting game... (5s / 30s)")
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Whether the launch can be cancelled at this point
    /// </summary>
    public bool CanCancel { get; set; }
}
