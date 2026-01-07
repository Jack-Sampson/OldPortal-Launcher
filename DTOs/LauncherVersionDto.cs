// Component: OPLauncher
// TODO: [LAUNCH-Migration] Create DTOs matching SharedAPI structure
// Description: Launcher version check response DTO

namespace OPLauncher.DTOs;

/// <summary>
/// Launcher version check response data transfer object
/// </summary>
public class LauncherVersionDto
{
    /// <summary>
    /// Latest available launcher version
    /// </summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether the update is mandatory
    /// </summary>
    public bool IsMandatory { get; set; }

    /// <summary>
    /// Update manifest containing download URL and metadata
    /// </summary>
    public UpdateManifestDto? UpdateManifest { get; set; }
}

/// <summary>
/// Update manifest containing download information
/// </summary>
public class UpdateManifestDto
{
    /// <summary>
    /// Direct download URL for the update
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the update file
    /// </summary>
    public string Sha256Hash { get; set; } = string.Empty;

    /// <summary>
    /// Release notes for the update
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// Release date (UTC)
    /// </summary>
    public DateTime ReleaseDate { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }
}
