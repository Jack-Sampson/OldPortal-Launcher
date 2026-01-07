namespace OPLauncher.Models;

/// <summary>
/// Represents the launcher configuration with user preferences and settings.
/// This configuration is stored in %ProgramData%\OldPortal\launcher\config.json
/// </summary>
public class LauncherConfig
{
    /// <summary>
    /// Gets or sets the API base URL for OldPortal.com endpoints.
    /// Default: https://oldportal.com/api
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://oldportal.com/api";

    /// <summary>
    /// Gets or sets the path to the Asheron's Call client executable (acclient.exe).
    /// This is required to launch the game.
    /// </summary>
    public string? AcClientPath { get; set; }

    /// <summary>
    /// Gets or sets whether to use Decal when launching the game.
    /// Default: false
    /// </summary>
    public bool UseDecal { get; set; } = false;

    /// <summary>
    /// Gets or sets the UI theme preference.
    /// Supported values: Dark, Light (Light theme in Phase 5)
    /// </summary>
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>
    /// Gets or sets whether the launcher should remember the user's login session.
    /// When true, the refresh token is stored encrypted on disk for persistent login.
    /// </summary>
    public bool RememberMe { get; set; } = true;

    // LauncherVersion removed - now read from assembly metadata (.csproj) at runtime
    // This ensures version is always synchronized with the actual build version

    /// <summary>
    /// Gets or sets whether the launcher should automatically check for updates on startup.
    /// Default: false (update checking disabled - users directed to website)
    /// </summary>
    public bool AutoCheckUpdates { get; set; } = false;

    /// <summary>
    /// Gets or sets the last time the launcher checked for updates (UTC).
    /// Used to prevent excessive update checks.
    /// </summary>
    public DateTime? LastUpdateCheck { get; set; }

    /// <summary>
    /// Gets or sets whether the launcher should minimize to system tray instead of closing.
    /// Default: false
    /// </summary>
    public bool MinimizeToTray { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the launcher should start with Windows.
    /// Default: false
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Gets or sets the telemetry consent flag.
    /// When true, anonymous usage statistics are sent to OldPortal.com.
    /// Default: false (opt-in)
    /// </summary>
    public bool TelemetryEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the logging level for the launcher.
    /// Supported values: Verbose, Debug, Information, Warning, Error, Fatal
    /// Default: Information
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Gets or sets whether the website promo banner has been permanently dismissed.
    /// Default: false
    /// </summary>
    public bool IsWebsiteBannerDismissed { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the first-run onboarding wizard has been completed.
    /// When false, the onboarding modal will be shown on startup.
    /// Default: false
    /// </summary>
    public bool IsOnboardingComplete { get; set; } = false;

    /// <summary>
    /// Gets or sets the optional password for database encryption.
    /// When set, the LiteDB database will be encrypted using this password.
    /// Leave null or empty to disable encryption.
    /// </summary>
    public string? DatabasePassword { get; set; }

    /// <summary>
    /// Gets or sets the preferred sort option for the worlds list.
    /// Default: Name
    /// </summary>
    public ViewModels.WorldSortOption WorldSortOption { get; set; } = ViewModels.WorldSortOption.Name;

    /// <summary>
    /// Gets or sets the preferred sort direction for the worlds list.
    /// Default: Ascending
    /// </summary>
    public ViewModels.SortDirection WorldSortDirection { get; set; } = ViewModels.SortDirection.Ascending;

    /// <summary>
    /// Gets or sets the timestamp when this configuration was last saved (UTC).
    /// </summary>
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether multi-client support is enabled.
    /// When true, uses suspended process launch to bypass AC's single-instance mutex.
    /// Default: false
    /// </summary>
    public bool EnableMultiClient { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to automatically configure UserPreferences.ini for multi-client support.
    /// When true, the launcher will set ComputeUniquePort=True automatically.
    /// Default: true
    /// </summary>
    public bool AutoConfigureUniquePort { get; set; } = true;

    /// <summary>
    /// Gets or sets the default delay in seconds between sequential client launches.
    /// Valid range: 0-30 seconds. 0 = simultaneous launch.
    /// Default: 3 seconds
    /// </summary>
    public int DefaultLaunchDelay { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of simultaneous clients that can be launched.
    /// Valid range: 1-50 clients.
    /// Default: 12 clients
    /// </summary>
    public int MaxSimultaneousClients { get; set; } = 12;

    /// <summary>
    /// Validates the current configuration and returns any validation errors.
    /// </summary>
    /// <returns>A list of validation error messages. Empty if valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        // Validate API base URL
        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            errors.Add("API base URL cannot be empty");
        }
        else if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("API base URL must be a valid HTTP or HTTPS URL");
        }

        // Validate theme (AppTheme is an enum, so it's always valid)

        // Validate log level
        var validLogLevels = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" };
        if (!validLogLevels.Contains(LogLevel, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Log level must be one of: {string.Join(", ", validLogLevels)}");
        }

        // Validate multi-client settings
        if (DefaultLaunchDelay < 0 || DefaultLaunchDelay > 30)
        {
            errors.Add("Default launch delay must be between 0 and 30 seconds");
        }

        if (MaxSimultaneousClients < 1 || MaxSimultaneousClients > 50)
        {
            errors.Add("Max simultaneous clients must be between 1 and 50");
        }

        // LauncherVersion validation removed - version now comes from assembly

        return errors;
    }

    /// <summary>
    /// Creates a default configuration with sensible defaults.
    /// Auto-detects AC client path if found at the default installation location.
    /// </summary>
    /// <returns>A new LauncherConfig instance with default values.</returns>
    public static LauncherConfig CreateDefault()
    {
        // Try to auto-detect AC client at the default Turbine installation path
        string? acClientPath = null;
        var defaultPath = @"C:\Turbine\Asheron's Call\acclient.exe";
        if (File.Exists(defaultPath))
        {
            acClientPath = defaultPath;
        }
        else
        {
            // Try other common installation paths
            var commonPaths = new[]
            {
                @"C:\Program Files (x86)\Microsoft Games\Asheron's Call\acclient.exe",
                @"C:\Program Files\Microsoft Games\Asheron's Call\acclient.exe"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    acClientPath = path;
                    break;
                }
            }
        }

        return new LauncherConfig
        {
            ApiBaseUrl = "https://oldportal.com/api",
            AcClientPath = acClientPath,  // Auto-detected if found
            Theme = AppTheme.Dark,  // Default to Dark theme (new design system)
            RememberMe = true,
            AutoCheckUpdates = false,  // Update checking disabled
            MinimizeToTray = false,
            StartWithWindows = false,
            TelemetryEnabled = false,
            LogLevel = "Information",
            LastSaved = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a shallow copy of this configuration.
    /// </summary>
    /// <returns>A new LauncherConfig instance with copied values.</returns>
    public LauncherConfig Clone()
    {
        return new LauncherConfig
        {
            ApiBaseUrl = ApiBaseUrl,
            AcClientPath = AcClientPath,
            UseDecal = UseDecal,
            Theme = Theme,
            RememberMe = RememberMe,
            AutoCheckUpdates = AutoCheckUpdates,
            LastUpdateCheck = LastUpdateCheck,
            MinimizeToTray = MinimizeToTray,
            StartWithWindows = StartWithWindows,
            TelemetryEnabled = TelemetryEnabled,
            LogLevel = LogLevel,
            IsWebsiteBannerDismissed = IsWebsiteBannerDismissed,
            IsOnboardingComplete = IsOnboardingComplete,
            DatabasePassword = DatabasePassword,
            WorldSortOption = WorldSortOption,
            WorldSortDirection = WorldSortDirection,
            LastSaved = LastSaved,
            EnableMultiClient = EnableMultiClient,
            AutoConfigureUniquePort = AutoConfigureUniquePort,
            DefaultLaunchDelay = DefaultLaunchDelay,
            MaxSimultaneousClients = MaxSimultaneousClients
        };
    }
}
