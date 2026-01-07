namespace OPLauncher.Models;

/// <summary>
/// Simple model for storing database version information.
/// </summary>
public class DatabaseVersion
{
    /// <summary>
    /// The database version number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// When the version was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
