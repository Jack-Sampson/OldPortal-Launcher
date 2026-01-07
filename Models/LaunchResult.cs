namespace OPLauncher.Models;

/// <summary>
/// Represents the result of a game launch attempt.
/// Contains success status, error information, and process details.
/// </summary>
public class LaunchResult
{
    /// <summary>
    /// Gets or sets whether the launch was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if the launch failed.
    /// Null if Success is true.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the process ID of the launched game instance.
    /// Null if the launch failed or process couldn't be tracked.
    /// </summary>
    public int? ProcessId { get; set; }

    /// <summary>
    /// Gets or sets the world ID that was launched.
    /// Useful for tracking and logging purposes.
    /// </summary>
    public int WorldId { get; set; }

    /// <summary>
    /// Gets or sets the world name that was launched.
    /// </summary>
    public string? WorldName { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the launch was attempted.
    /// </summary>
    public DateTime LaunchedAt { get; set; }

    /// <summary>
    /// Gets or sets whether the launch was cancelled by the user.
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Creates a successful launch result.
    /// </summary>
    /// <param name="processId">The process ID of the launched game.</param>
    /// <param name="worldId">The ID of the world being connected to.</param>
    /// <param name="worldName">The name of the world being connected to.</param>
    /// <returns>A successful LaunchResult.</returns>
    public static LaunchResult CreateSuccess(int processId, int worldId, string? worldName)
    {
        return new LaunchResult
        {
            Success = true,
            ProcessId = processId,
            WorldId = worldId,
            WorldName = worldName,
            LaunchedAt = DateTime.UtcNow,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Creates a failed launch result.
    /// </summary>
    /// <param name="errorMessage">The error message describing why the launch failed.</param>
    /// <param name="worldId">The ID of the world that failed to launch.</param>
    /// <param name="worldName">The name of the world that failed to launch.</param>
    /// <returns>A failed LaunchResult.</returns>
    public static LaunchResult CreateFailure(string errorMessage, int worldId, string? worldName)
    {
        return new LaunchResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ProcessId = null,
            WorldId = worldId,
            WorldName = worldName,
            LaunchedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a cancelled launch result.
    /// </summary>
    /// <param name="worldId">The ID of the world that was cancelled.</param>
    /// <param name="worldName">The name of the world that was cancelled.</param>
    /// <returns>A cancelled LaunchResult.</returns>
    public static LaunchResult CreateCancelled(int worldId, string? worldName)
    {
        return new LaunchResult
        {
            Success = false,
            IsCancelled = true,
            ErrorMessage = "Launch cancelled by user",
            ProcessId = null,
            WorldId = worldId,
            WorldName = worldName,
            LaunchedAt = DateTime.UtcNow
        };
    }
}
