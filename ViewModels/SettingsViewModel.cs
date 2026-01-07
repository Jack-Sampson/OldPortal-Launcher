using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.Models;
using OPLauncher.Services;

namespace OPLauncher.ViewModels;

/// <summary>
/// ViewModel for the settings screen.
/// Allows configuration of AC client path, theme, updates, and other preferences.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly UpdateService _updateService;
    private readonly WorldsService _worldsService;
    private readonly LoggingService _logger;
    private readonly IFileDialogService _fileDialogService;
    private readonly MainWindowViewModel? _mainWindow;
    private readonly ThemeManager _themeManager;
    private readonly DecalService _decalService;
    private readonly UserPreferencesManager _userPreferencesManager;
    private readonly INavigationService _navigationService;

    /// <summary>
    /// The path to the Asheron's Call client executable (acclient.exe).
    /// </summary>
    [ObservableProperty]
    private string _acClientPath = string.Empty;

    /// <summary>
    /// Whether to use Decal when launching the game.
    /// </summary>
    [ObservableProperty]
    private bool _useDecal;

    /// <summary>
    /// The detected Decal installation path (read-only, for display purposes).
    /// </summary>
    [ObservableProperty]
    private string? _decalPath;

    /// <summary>
    /// The selected UI theme (Dark or Light).
    /// </summary>
    [ObservableProperty]
    private AppTheme _selectedTheme = AppTheme.Dark;

    /// <summary>
    /// Whether to automatically check for updates on startup.
    /// </summary>
    [ObservableProperty]
    private bool _autoCheckUpdates;

    /// <summary>
    /// The current launcher version (read-only).
    /// </summary>
    [ObservableProperty]
    private string _launcherVersion = "1.0.0";

    /// <summary>
    /// Whether settings are currently being saved.
    /// </summary>
    [ObservableProperty]
    private bool _isSaving;

    /// <summary>
    /// Whether an update check is in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isCheckingUpdates;

    /// <summary>
    /// Whether cache clearing is in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isClearingCache;

    /// <summary>
    /// Success message to display.
    /// </summary>
    [ObservableProperty]
    private string? _successMessage;

    /// <summary>
    /// Error message to display.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Validation message for AC client path.
    /// </summary>
    [ObservableProperty]
    private string? _acClientValidationMessage;

    /// <summary>
    /// Whether the AC client path is valid.
    /// </summary>
    [ObservableProperty]
    private bool _isAcClientPathValid;

    /// <summary>
    /// Validation message for Decal installation.
    /// </summary>
    [ObservableProperty]
    private string? _decalValidationMessage;

    /// <summary>
    /// Status message for update checks.
    /// </summary>
    [ObservableProperty]
    private string? _updateStatusMessage;

    /// <summary>
    /// Status message for cache operations.
    /// </summary>
    [ObservableProperty]
    private string? _cacheStatusMessage;

    /// <summary>
    /// Whether multi-client support is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _enableMultiClient;

    /// <summary>
    /// Whether to automatically configure UserPreferences.ini for multi-client support.
    /// </summary>
    [ObservableProperty]
    private bool _autoConfigureUniquePort;

    /// <summary>
    /// The default delay in seconds between sequential client launches.
    /// Valid range: 0-30 seconds.
    /// </summary>
    [ObservableProperty]
    private int _defaultLaunchDelay;

    /// <summary>
    /// The maximum number of simultaneous clients that can be launched.
    /// Valid range: 1-50 clients.
    /// </summary>
    [ObservableProperty]
    private int _maxSimultaneousClients;

    /// <summary>
    /// Status message for UserPreferences.ini configuration.
    /// </summary>
    [ObservableProperty]
    private string? _userPreferencesStatusMessage;

    /// <summary>
    /// Available theme options (Dark and Light).
    /// </summary>
    public List<AppTheme> ThemeOptions { get; } = new()
    {
        AppTheme.Dark,
        AppTheme.Light  // Light theme in Phase 5
    };

    /// <summary>
    /// Initializes a new instance of the SettingsViewModel.
    /// </summary>
    /// <param name="configService">The configuration service.</param>
    /// <param name="updateService">The update service.</param>
    /// <param name="worldsService">The worlds service for cache management.</param>
    /// <param name="logger">The logging service.</param>
    /// <param name="fileDialogService">The file dialog service.</param>
    /// <param name="themeManager">The theme manager service.</param>
    /// <param name="decalService">The Decal service.</param>
    /// <param name="userPreferencesManager">The UserPreferences.ini manager for multi-client configuration.</param>
    /// <param name="mainWindow">The main window view model for navigation (optional for standalone usage).</param>
    public SettingsViewModel(
        ConfigService configService,
        UpdateService updateService,
        WorldsService worldsService,
        LoggingService logger,
        IFileDialogService fileDialogService,
        ThemeManager themeManager,
        DecalService decalService,
        UserPreferencesManager userPreferencesManager,
        INavigationService navigationService,
        MainWindowViewModel? mainWindow = null)
    {
        _configService = configService;
        _updateService = updateService;
        _worldsService = worldsService;
        _logger = logger;
        _fileDialogService = fileDialogService;
        _themeManager = themeManager;
        _decalService = decalService;
        _userPreferencesManager = userPreferencesManager;
        _navigationService = navigationService;
        _mainWindow = mainWindow;

        _logger.Debug("SettingsViewModel initialized");

        // Load current theme
        _selectedTheme = _themeManager.CurrentTheme;

        // Load current configuration
        LoadSettings();
    }

    /// <summary>
    /// Called when the SelectedTheme property changes. Applies the new theme.
    /// </summary>
    partial void OnSelectedThemeChanged(AppTheme value)
    {
        _logger.Information("Theme changed to: {Theme}", value);

        // Use SetTheme instead of ApplyTheme to ensure ThemeManager's internal state is updated
        _themeManager.SetTheme(value);

        // Note: SetTheme already saves the preference, no need to call UpdateTheme
    }

    /// <summary>
    /// Called when the UseDecal property changes. Validates Decal installation.
    /// </summary>
    partial void OnUseDecalChanged(bool value)
    {
        _logger.Information("UseDecal changed to: {Value}", value);

        // Validate Decal installation if enabled
        if (value)
        {
            ValidateDecalInstallation();
        }
        else
        {
            DecalValidationMessage = null;
        }

        // Save preference to config
        _configService.UpdateConfiguration(config => config.UseDecal = value);
    }

    /// <summary>
    /// Loads current settings from the configuration service.
    /// </summary>
    private void LoadSettings()
    {
        var config = _configService.Current;

        AcClientPath = config.AcClientPath ?? string.Empty;
        UseDecal = config.UseDecal;
        AutoCheckUpdates = config.AutoCheckUpdates;

        // Load multi-client settings
        EnableMultiClient = config.EnableMultiClient;
        AutoConfigureUniquePort = config.AutoConfigureUniquePort;
        DefaultLaunchDelay = config.DefaultLaunchDelay;
        MaxSimultaneousClients = config.MaxSimultaneousClients;

        // Auto-detect Decal installation on settings load
        ValidateDecalInstallation();

        // Read version from assembly (single source of truth from .csproj)
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            LauncherVersion = version != null
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : "1.0.0";
        }
        catch
        {
            LauncherVersion = "1.0.0";
        }

        _logger.Debug("Settings loaded from configuration");

        // Validate AC client path on load
        ValidateAcClientPath();

        // Check UserPreferences.ini status
        CheckUserPreferences();
    }

    /// <summary>
    /// Saves the current settings to the configuration service.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveSettingsAsync()
    {
        try
        {
            SuccessMessage = null;
            ErrorMessage = null;
            IsSaving = true;

            _logger.Information("User saving settings");

            // Validate AC client path before saving
            if (!string.IsNullOrWhiteSpace(AcClientPath))
            {
                var (isValid, errorMessage) = _configService.ValidateAcClientPath(AcClientPath);
                if (!isValid)
                {
                    ErrorMessage = errorMessage ?? "Invalid AC client path";
                    _logger.Warning("Settings save aborted due to invalid AC client path");
                    return;
                }
            }

            // Update configuration
            var success = _configService.UpdateConfiguration(config =>
            {
                config.AcClientPath = string.IsNullOrWhiteSpace(AcClientPath) ? null : AcClientPath.Trim();
                config.Theme = SelectedTheme;
                config.AutoCheckUpdates = AutoCheckUpdates;
                config.UseDecal = UseDecal;

                // Multi-client settings
                config.EnableMultiClient = EnableMultiClient;
                config.AutoConfigureUniquePort = AutoConfigureUniquePort;
                config.DefaultLaunchDelay = DefaultLaunchDelay;
                config.MaxSimultaneousClients = MaxSimultaneousClients;
            });

            if (success)
            {
                // If multi-client is enabled and auto-configure is enabled, update UserPreferences.ini
                if (EnableMultiClient && AutoConfigureUniquePort)
                {
                    _logger.Information("Auto-configuring UserPreferences.ini for multi-client support");

                    if (!_userPreferencesManager.IsComputeUniquePortEnabled())
                    {
                        var result = _userPreferencesManager.EnableComputeUniquePort();
                        if (result)
                        {
                            _logger.Information("UserPreferences.ini configured successfully");
                            UserPreferencesStatusMessage = "✓ UserPreferences.ini configured for multi-client";
                        }
                        else
                        {
                            _logger.Warning("Failed to configure UserPreferences.ini");
                            UserPreferencesStatusMessage = "⚠ Failed to configure UserPreferences.ini - check logs";
                        }
                    }
                    else
                    {
                        UserPreferencesStatusMessage = "✓ UserPreferences.ini already configured";
                    }
                }

                SuccessMessage = "Settings saved successfully!";
                _logger.Information("Settings saved successfully");

                // Apply theme change immediately if needed
                // TODO: Trigger theme change in the app

                // Navigate back to worlds screen after successful save
                if (_mainWindow != null)
                {
                    await Task.Delay(500); // Brief delay to show success message
                    _mainWindow.NavigateToWorldsCommand.Execute(null);
                }
            }
            else
            {
                ErrorMessage = "Failed to save settings. Please try again.";
                _logger.Error("Failed to save settings");
            }

            await Task.Delay(100); // Ensure async pattern
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving settings");
            ErrorMessage = $"Error saving settings: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Determines whether settings can be saved.
    /// </summary>
    private bool CanSave()
    {
        return !IsSaving;
    }

    /// <summary>
    /// Opens a file browser to select the AC client executable.
    /// </summary>
    [RelayCommand]
    private async Task BrowseAcClientAsync()
    {
        try
        {
            _logger.Debug("User initiated AC client path browse");

            // Clear any existing messages
            ErrorMessage = null;
            SuccessMessage = null;

            // Determine suggested start location
            string? suggestedLocation = null;
            if (!string.IsNullOrWhiteSpace(AcClientPath))
            {
                suggestedLocation = AcClientPath;
            }
            else if (System.OperatingSystem.IsWindows())
            {
                // Common installation paths for AC
                var commonPaths = new[]
                {
                    @"C:\Program Files (x86)\Microsoft Games\Asheron's Call",
                    @"C:\Program Files\Microsoft Games\Asheron's Call",
                    @"C:\Turbine\Asheron's Call"
                };

                foreach (var path in commonPaths)
                {
                    if (System.IO.Directory.Exists(path))
                    {
                        suggestedLocation = System.IO.Path.Combine(path, "acclient.exe");
                        break;
                    }
                }
            }

            // Show file dialog
            var selectedPath = await _fileDialogService.ShowOpenFileDialogAsync(
                title: "Select Asheron's Call Client (acclient.exe)",
                fileTypeFilters: new[] { "*.exe" },
                suggestedStartLocation: suggestedLocation
            );

            if (selectedPath != null)
            {
                _logger.Information("AC client path selected: {Path}", selectedPath);
                AcClientPath = selectedPath;

                // Validation will happen automatically via OnAcClientPathChanged
                if (IsAcClientPathValid)
                {
                    SuccessMessage = "✓ Valid AC client path selected!";
                }
            }
            else
            {
                _logger.Debug("File dialog cancelled by user");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error browsing for AC client");
            ErrorMessage = $"Error opening file browser: {ex.Message}";
        }
    }

    /// <summary>
    /// Validates the AC client path.
    /// </summary>
    [RelayCommand]
    private void ValidateAcClientPath()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(AcClientPath))
            {
                AcClientValidationMessage = "AC client path is not configured.";
                IsAcClientPathValid = false;
                return;
            }

            var (isValid, errorMessage) = _configService.ValidateAcClientPath(AcClientPath);

            if (isValid)
            {
                AcClientValidationMessage = "✓ AC client path is valid";
                IsAcClientPathValid = true;
                _logger.Debug("AC client path validation passed");
            }
            else
            {
                AcClientValidationMessage = $"✗ {errorMessage}";
                IsAcClientPathValid = false;
                _logger.Warning("AC client path validation failed: {Error}", errorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error validating AC client path");
            AcClientValidationMessage = $"✗ Error: {ex.Message}";
            IsAcClientPathValid = false;
        }
    }

    /// <summary>
    /// Opens the OldPortal downloads page in the default browser.
    /// Users can manually download the latest launcher version.
    /// </summary>
    [RelayCommand]
    private void OpenDownloadsPage()
    {
        try
        {
            _logger.Information("User opened downloads page for manual update check");
            var url = "https://oldportal.com/downloads";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open downloads page");
            ErrorMessage = "Failed to open browser. Please visit https://oldportal.com/downloads";
        }
    }

    /// <summary>
    /// Clears the world cache.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClearCache))]
    private async Task ClearCacheAsync()
    {
        try
        {
            CacheStatusMessage = null;
            IsClearingCache = true;

            _logger.Information("User clearing world cache");

            await Task.Run(() => _worldsService.ClearCache());

            CacheStatusMessage = "✓ Cache cleared successfully!";
            _logger.Information("World cache cleared");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error clearing cache");
            CacheStatusMessage = $"✗ Error clearing cache: {ex.Message}";
        }
        finally
        {
            IsClearingCache = false;
        }
    }

    /// <summary>
    /// Determines whether cache can be cleared.
    /// </summary>
    private bool CanClearCache()
    {
        return !IsClearingCache;
    }

    /// <summary>
    /// Opens the logs folder in Windows Explorer.
    /// </summary>
    [RelayCommand]
    private void ViewLogs()
    {
        try
        {
            var logsPath = Path.Combine(_configService.GetConfigDirectory(), "logs");
            _logger.Information("Opening logs folder: {Path}", logsPath);

            if (!Directory.Exists(logsPath))
            {
                Directory.CreateDirectory(logsPath);
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = logsPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening logs folder");
            ErrorMessage = "Failed to open logs folder.";
        }
    }

    /// <summary>
    /// Resets settings to default values.
    /// </summary>
    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        try
        {
            _logger.Information("User resetting settings to defaults");

            var success = _configService.ResetToDefaults();

            if (success)
            {
                LoadSettings(); // Reload settings from config
                SuccessMessage = "Settings reset to defaults.";
                _logger.Information("Settings reset to defaults successfully");
            }
            else
            {
                ErrorMessage = "Failed to reset settings.";
                _logger.Error("Failed to reset settings");
            }

            await Task.Delay(100); // Ensure async pattern
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error resetting settings");
            ErrorMessage = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Navigates back to the previous view (typically the worlds screen).
    /// </summary>
    [RelayCommand]
    private void GoBack()
    {
        if (_mainWindow != null)
        {
            _logger.Debug("Navigating back from settings");
            _mainWindow.NavigateToWorldsCommand.Execute(null);
        }
    }

    /// <summary>
    /// Opens the Getting Started guide in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenGettingStarted()
    {
        try
        {
            _logger.Information("User opened Getting Started guide");
            var url = "https://oldportal.com/getting-started";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open Getting Started guide");
            ErrorMessage = "Failed to open browser. Please visit https://oldportal.com/getting-started";
        }
    }

    /// <summary>
    /// Opens the Decal website in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenDecalWebsite()
    {
        try
        {
            _logger.Information("User opened Decal website");
            var url = "https://www.decaldev.com/";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open Decal website");
            ErrorMessage = "Failed to open browser. Please visit https://www.decaldev.com/";
        }
    }

    /// <summary>
    /// Validates that Decal is installed on the system using the registry.
    /// </summary>
    [RelayCommand]
    private void ValidateDecalInstallation()
    {
        try
        {
            _logger.Debug("Validating Decal installation via registry");

            if (_decalService.IsDecalInstalled())
            {
                // Get and display the Decal path
                var decalInjectPath = _decalService.GetDecalInjectPath();
                if (!string.IsNullOrEmpty(decalInjectPath))
                {
                    DecalPath = decalInjectPath;
                    DecalValidationMessage = $"✓ Decal is installed: {decalInjectPath}";
                    _logger.Debug("Decal installation validated via registry at: {Path}", decalInjectPath);
                }
                else
                {
                    DecalValidationMessage = "✓ Decal is installed";
                    _logger.Debug("Decal installation validated via registry");
                }
            }
            else
            {
                DecalPath = null;
                DecalValidationMessage = "⚠ Decal not found. Click 'Need Decal?' to download and install it.";
                _logger.Warning("Decal not found in registry");
            }
        }
        catch (Exception ex)
        {
            DecalPath = null;
            _logger.Error(ex, "Error validating Decal installation");
            DecalValidationMessage = $"✗ Error checking Decal: {ex.Message}";
        }
    }

    /// <summary>
    /// Called when AC client path changes.
    /// Triggers validation.
    /// </summary>
    partial void OnAcClientPathChanged(string value)
    {
        ValidateAcClientPath();
        SuccessMessage = null;
        ErrorMessage = null;
    }


    /// <summary>
    /// Called when AutoCheckUpdates changes.
    /// </summary>
    partial void OnAutoCheckUpdatesChanged(bool value)
    {
        _logger.Debug("AutoCheckUpdates changed to: {Value}", value);
        SuccessMessage = null;
        ErrorMessage = null;
    }

    /// <summary>
    /// Called when IsClearingCache changes.
    /// </summary>
    partial void OnIsClearingCacheChanged(bool value)
    {
        ClearCacheCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Called when IsSaving changes.
    /// </summary>
    partial void OnIsSavingChanged(bool value)
    {
        SaveSettingsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Re-runs the first-run onboarding wizard by resetting IsOnboardingComplete flag.
    /// The wizard will show on next application restart.
    /// </summary>
    [RelayCommand]
    private void RerunOnboarding()
    {
        try
        {
            _logger.Information("User requested to re-run onboarding wizard");

            // Reset the onboarding complete flag
            _configService.Current.IsOnboardingComplete = false;
            _configService.SaveConfiguration();

            SuccessMessage = "✅ Onboarding wizard reset successfully! The setup wizard will appear when you restart the launcher.";
            ErrorMessage = null;

            _logger.Information("Onboarding wizard reset successfully");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to reset onboarding wizard: {Error}", ex.Message);
            ErrorMessage = $"Failed to reset onboarding: {ex.Message}";
            SuccessMessage = null;
        }
    }

    /// <summary>
    /// Checks the status of UserPreferences.ini and displays the result.
    /// </summary>
    [RelayCommand]
    private void CheckUserPreferences()
    {
        try
        {
            _logger.Debug("Checking UserPreferences.ini status");

            if (!_userPreferencesManager.FileExists())
            {
                UserPreferencesStatusMessage = "⚠ UserPreferences.ini not found - will be created when needed";
                _logger.Debug("UserPreferences.ini does not exist");
                return;
            }

            var isEnabled = _userPreferencesManager.IsComputeUniquePortEnabled();
            if (isEnabled)
            {
                UserPreferencesStatusMessage = "✓ ComputeUniquePort is enabled in UserPreferences.ini";
                _logger.Debug("ComputeUniquePort is enabled");
            }
            else
            {
                UserPreferencesStatusMessage = "⚠ ComputeUniquePort is not enabled - click 'Configure' to enable it";
                _logger.Debug("ComputeUniquePort is not enabled");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking UserPreferences.ini");
            UserPreferencesStatusMessage = $"✗ Error checking UserPreferences.ini: {ex.Message}";
        }
    }

    /// <summary>
    /// Enables ComputeUniquePort in UserPreferences.ini for multi-client support.
    /// </summary>
    [RelayCommand]
    private void EnableUniquePort()
    {
        try
        {
            _logger.Information("User manually enabling ComputeUniquePort in UserPreferences.ini");

            var result = _userPreferencesManager.EnableComputeUniquePort();
            if (result)
            {
                UserPreferencesStatusMessage = "✓ ComputeUniquePort enabled successfully!";
                _logger.Information("ComputeUniquePort enabled successfully");
            }
            else
            {
                UserPreferencesStatusMessage = "✗ Failed to enable ComputeUniquePort - check logs";
                _logger.Error("Failed to enable ComputeUniquePort");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error enabling ComputeUniquePort");
            UserPreferencesStatusMessage = $"✗ Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens the UserPreferences.ini file location in Windows Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenUserPreferencesLocation()
    {
        try
        {
            var userPrefsPath = _userPreferencesManager.GetUserPreferencesPath();
            var directory = Path.GetDirectoryName(userPrefsPath);

            if (string.IsNullOrEmpty(directory))
            {
                ErrorMessage = "Could not determine UserPreferences.ini location";
                return;
            }

            _logger.Information("Opening UserPreferences.ini location: {Path}", directory);

            // Create directory if it doesn't exist
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening UserPreferences.ini location");
            ErrorMessage = "Failed to open UserPreferences.ini location.";
        }
    }

    /// <summary>
    /// Opens the multi-client help documentation view.
    /// </summary>
    [RelayCommand]
    private void OpenMultiClientHelp()
    {
        try
        {
            _logger.Information("Opening multi-client help documentation");
            _navigationService.NavigateTo<MultiClientHelpViewModel>();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening multi-client help");
            ErrorMessage = "Failed to open help documentation.";
        }
    }

    /// <summary>
    /// Called when EnableMultiClient changes.
    /// </summary>
    partial void OnEnableMultiClientChanged(bool value)
    {
        _logger.Debug("EnableMultiClient changed to: {Value}", value);

        // If disabling multi-client, clear the status message
        if (!value)
        {
            UserPreferencesStatusMessage = null;
        }
        else
        {
            // Check UserPreferences.ini status when enabling
            CheckUserPreferences();
        }

        SuccessMessage = null;
        ErrorMessage = null;
    }

    /// <summary>
    /// Called when AutoConfigureUniquePort changes.
    /// </summary>
    partial void OnAutoConfigureUniquePortChanged(bool value)
    {
        _logger.Debug("AutoConfigureUniquePort changed to: {Value}", value);
        SuccessMessage = null;
        ErrorMessage = null;
    }

    /// <summary>
    /// Called when DefaultLaunchDelay changes.
    /// </summary>
    partial void OnDefaultLaunchDelayChanged(int value)
    {
        _logger.Debug("DefaultLaunchDelay changed to: {Value}", value);
        SuccessMessage = null;
        ErrorMessage = null;
    }

    /// <summary>
    /// Called when MaxSimultaneousClients changes.
    /// </summary>
    partial void OnMaxSimultaneousClientsChanged(int value)
    {
        _logger.Debug("MaxSimultaneousClients changed to: {Value}", value);
        SuccessMessage = null;
        ErrorMessage = null;
    }
}
