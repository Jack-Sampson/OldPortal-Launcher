using System.Net.Http.Json;
using System.Security.Cryptography;
using OPLauncher.Models;
using OPLauncher.DTOs;
using OPLauncher.Utilities;

namespace OPLauncher.Services;

/// <summary>
/// Service for checking launcher updates.
/// Update installation is manual - users download the new installer from oldportal.com/downloads
/// </summary>
public class UpdateService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ConfigService _configService;
    private readonly LoggingService _logger;
    private bool _disposed;

    /// <summary>
    /// Event raised when update check completes.
    /// </summary>
    public event EventHandler<LauncherUpdateInfo>? UpdateCheckCompleted;

    /// <summary>
    /// Initializes a new instance of the UpdateService.
    /// </summary>
    /// <param name="httpClient">The HTTP client for API communication.</param>
    /// <param name="configService">The configuration service.</param>
    /// <param name="logger">The logging service for diagnostics.</param>
    public UpdateService(
        HttpClient httpClient,
        ConfigService configService,
        LoggingService logger)
    {
        _httpClient = httpClient;
        _configService = configService;
        _logger = logger;

        _logger.Debug("UpdateService initialized");
    }

    /// <summary>
    /// Checks for available updates from the API.
    /// This method does not download or apply updates, only checks if they're available.
    /// </summary>
    /// <returns>LauncherUpdateInfo containing information about available updates, or null if check failed.</returns>
    public async Task<LauncherUpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            _logger.Information("Checking for launcher updates...");

            var currentVersion = GetCurrentVersion();
            _logger.Debug("Current launcher version: {Version}", currentVersion);

            // Fetch update manifest from API
            var endpoint = ApiEndpoints.Launcher.CheckVersion;
            _logger.Debug("Fetching update manifest from: {Endpoint}", endpoint);

            var response = await _httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("Failed to fetch update manifest: {StatusCode}", response.StatusCode);
                return null;
            }

            var versionDto = await response.Content.ReadFromJsonAsync<LauncherVersionDto>();

            if (versionDto == null || versionDto.UpdateManifest == null)
            {
                _logger.Information("No update information available from server");
                return LauncherUpdateInfo.NoUpdate(currentVersion);
            }

            // Parse latest version
            if (!Version.TryParse(versionDto.LatestVersion, out var latestVersion))
            {
                _logger.Warning("Invalid version format from server: {Version}", versionDto.LatestVersion);
                return LauncherUpdateInfo.NoUpdate(currentVersion);
            }

            // Create LauncherUpdateInfo
            var updateInfo = LauncherUpdateInfo.Create(
                currentVersion,
                latestVersion,
                versionDto.UpdateManifest.DownloadUrl,
                versionDto.UpdateManifest.Sha256Hash,
                versionDto.UpdateManifest.ReleaseNotes,
                versionDto.UpdateManifest.ReleaseDate,
                versionDto.UpdateManifest.FileSize,
                versionDto.IsMandatory);

            if (updateInfo.IsUpdateAvailable)
            {
                _logger.Information("Update available: {LatestVersion} (current: {CurrentVersion})",
                    latestVersion, currentVersion);
                _logger.Information("Release notes: {ReleaseNotes}",
                    updateInfo.ReleaseNotes.Length > 100
                        ? updateInfo.ReleaseNotes.Substring(0, 100) + "..."
                        : updateInfo.ReleaseNotes);
            }
            else
            {
                _logger.Information("Launcher is up to date (version {Version})", currentVersion);
            }

            // Raise event
            UpdateCheckCompleted?.Invoke(this, updateInfo);

            return updateInfo;
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "Network error while checking for updates");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error while checking for updates");
            return null;
        }
    }


    /// <summary>
    /// Verifies the SHA-256 hash of a downloaded file.
    /// </summary>
    /// <param name="filePath">The path to the file to verify.</param>
    /// <param name="expectedHash">The expected SHA-256 hash (hex string).</param>
    /// <returns>True if hash matches, false otherwise.</returns>
    public async Task<bool> VerifyFileHashAsync(string filePath, string expectedHash)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.Error("File not found for hash verification: {FilePath}", filePath);
                return false;
            }

            _logger.Debug("Verifying SHA-256 hash for: {FilePath}", filePath);

            using var sha256 = SHA256.Create();
            using var fileStream = File.OpenRead(filePath);
            var hashBytes = await sha256.ComputeHashAsync(fileStream);
            var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            var expectedHashNormalized = expectedHash.Replace("-", "").ToLowerInvariant();

            var isMatch = actualHash.Equals(expectedHashNormalized, StringComparison.OrdinalIgnoreCase);

            if (isMatch)
            {
                _logger.Information("Hash verification successful");
            }
            else
            {
                _logger.Error("Hash mismatch! Expected: {Expected}, Actual: {Actual}",
                    expectedHashNormalized, actualHash);
            }

            return isMatch;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error verifying file hash");
            return false;
        }
    }

    /// <summary>
    /// Performs update check in the background on application startup.
    /// Logs results but does not notify the user.
    /// </summary>
    /// <returns>A task representing the background check operation.</returns>
    public async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            if (!_configService.Current.AutoCheckUpdates)
            {
                _logger.Debug("Auto-check for updates is disabled");
                return;
            }

            _logger.Information("Performing background update check...");

            await Task.Delay(TimeSpan.FromSeconds(5)); // Delay to avoid startup slowdown

            var updateInfo = await CheckForUpdatesAsync();

            if (updateInfo != null && updateInfo.IsUpdateAvailable)
            {
                _logger.Information("Background check found update: {Version}", updateInfo.LatestVersion);
                // UI layer should listen to UpdateCheckCompleted event to show notification
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during background update check");
        }
    }

    /// <summary>
    /// Gets the current version of the launcher from assembly metadata.
    /// </summary>
    /// <returns>The current version.</returns>
    public Version GetCurrentVersion()
    {
        try
        {
            // Read version from assembly (single source of truth from .csproj)
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            if (version != null)
            {
                return version;
            }

            _logger.Warning("Assembly version not available, using default 1.0.0");
            return new Version("1.0.0");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading assembly version, using default 1.0.0");
            return new Version("1.0.0");
        }
    }

    /// <summary>
    /// Disposes of the UpdateService resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logger.Debug("UpdateService disposed");
    }
}
