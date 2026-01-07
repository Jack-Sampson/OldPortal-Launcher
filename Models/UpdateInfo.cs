namespace OPLauncher.Models;

/// <summary>
/// Represents information about an available launcher update.
/// </summary>
public class LauncherUpdateInfo
{
    /// <summary>
    /// Gets or sets the current version of the launcher.
    /// </summary>
    public Version CurrentVersion { get; set; } = new Version("1.0.0");

    /// <summary>
    /// Gets or sets the latest available version.
    /// </summary>
    public Version LatestVersion { get; set; } = new Version("1.0.0");

    /// <summary>
    /// Gets or sets the download URL for the update package.
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 hash of the update package.
    /// Used for verifying the integrity of the downloaded file.
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release notes for this update.
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release date of this update.
    /// </summary>
    public DateTime ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets the file size of the update package in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets whether this update is mandatory.
    /// Mandatory updates cannot be skipped.
    /// </summary>
    public bool IsMandatory { get; set; }

    /// <summary>
    /// Gets whether an update is available (latest version is newer than current).
    /// </summary>
    public bool IsUpdateAvailable => LatestVersion > CurrentVersion;

    /// <summary>
    /// Gets a user-friendly display string for the update.
    /// </summary>
    public string DisplayText => $"Version {LatestVersion} is available (current: {CurrentVersion})";

    /// <summary>
    /// Gets a formatted file size string (e.g., "15.2 MB").
    /// </summary>
    public string FileSizeFormatted
    {
        get
        {
            if (FileSize < 1024)
                return $"{FileSize} B";
            if (FileSize < 1024 * 1024)
                return $"{FileSize / 1024.0:F1} KB";
            if (FileSize < 1024 * 1024 * 1024)
                return $"{FileSize / (1024.0 * 1024.0):F1} MB";
            return $"{FileSize / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }

    /// <summary>
    /// Creates a new UpdateInfo from current and latest versions.
    /// </summary>
    /// <param name="currentVersion">The current version.</param>
    /// <param name="latestVersion">The latest available version.</param>
    /// <param name="downloadUrl">The download URL.</param>
    /// <param name="hash">The SHA-256 hash.</param>
    /// <param name="releaseNotes">The release notes.</param>
    /// <param name="releaseDate">The release date.</param>
    /// <param name="fileSize">The file size in bytes.</param>
    /// <param name="isMandatory">Whether the update is mandatory.</param>
    /// <returns>A new LauncherUpdateInfo instance.</returns>
    public static LauncherUpdateInfo Create(
        Version currentVersion,
        Version latestVersion,
        string downloadUrl,
        string hash,
        string releaseNotes,
        DateTime releaseDate,
        long fileSize = 0,
        bool isMandatory = false)
    {
        return new LauncherUpdateInfo
        {
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            DownloadUrl = downloadUrl,
            Hash = hash,
            ReleaseNotes = releaseNotes,
            ReleaseDate = releaseDate,
            FileSize = fileSize,
            IsMandatory = isMandatory
        };
    }

    /// <summary>
    /// Creates a LauncherUpdateInfo indicating no update is available.
    /// </summary>
    /// <param name="currentVersion">The current version.</param>
    /// <returns>A LauncherUpdateInfo with current and latest versions equal.</returns>
    public static LauncherUpdateInfo NoUpdate(Version currentVersion)
    {
        return new LauncherUpdateInfo
        {
            CurrentVersion = currentVersion,
            LatestVersion = currentVersion,
            ReleaseNotes = "You are running the latest version."
        };
    }
}
