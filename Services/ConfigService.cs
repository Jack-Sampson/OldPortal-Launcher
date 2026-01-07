using System.Text.Json;
using OPLauncher.Models;

namespace OPLauncher.Services;

/// <summary>
/// Service for managing launcher configuration and user preferences.
/// Handles loading, saving, and validating configuration stored in %LOCALAPPDATA%\OldPortal\launcher\config.json
/// </summary>
public class ConfigService
{
    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly LoggingService _logger;
    private LauncherConfig _currentConfig;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the current loaded configuration.
    /// </summary>
    public LauncherConfig Current
    {
        get
        {
            lock (_lock)
            {
                return _currentConfig;
            }
        }
    }

    /// <summary>
    /// Event raised when the configuration is changed and saved.
    /// </summary>
    public event EventHandler<LauncherConfig>? ConfigurationChanged;

    /// <summary>
    /// Initializes a new instance of the ConfigService.
    /// </summary>
    /// <param name="logger">The logging service for diagnostic output.</param>
    public ConfigService(LoggingService logger)
    {
        _logger = logger;

        // Set config path to %LOCALAPPDATA%\OldPortal\launcher\config.json
        var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _configDirectory = Path.Combine(localAppDataPath, "OldPortal", "launcher");
        _configFilePath = Path.Combine(_configDirectory, "config.json");

        _logger.Debug("ConfigService initialized with path: {ConfigPath}", _configFilePath);

        // Load configuration on initialization
        _currentConfig = LoadConfigurationInternal();
    }

    /// <summary>
    /// Loads the configuration from disk. If the file doesn't exist, creates a new default configuration.
    /// </summary>
    /// <returns>The loaded or default LauncherConfig.</returns>
    public LauncherConfig LoadConfiguration()
    {
        lock (_lock)
        {
            _currentConfig = LoadConfigurationInternal();
            return _currentConfig;
        }
    }

    private LauncherConfig LoadConfigurationInternal()
    {
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(_configDirectory))
            {
                _logger.Information("Configuration directory does not exist. Creating: {Directory}", _configDirectory);
                Directory.CreateDirectory(_configDirectory);
            }

            // Check if config file exists
            if (!File.Exists(_configFilePath))
            {
                _logger.Information("Configuration file not found. Creating default configuration at: {Path}", _configFilePath);
                var defaultConfig = LauncherConfig.CreateDefault();
                SaveConfigurationInternal(defaultConfig);
                return defaultConfig;
            }

            // Read and deserialize config file
            var jsonText = File.ReadAllText(_configFilePath);
            var config = JsonSerializer.Deserialize<LauncherConfig>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            });

            if (config == null)
            {
                _logger.Warning("Failed to deserialize configuration file. Using default configuration.");
                return LauncherConfig.CreateDefault();
            }

            // Validate configuration
            var validationErrors = config.Validate();
            if (validationErrors.Count > 0)
            {
                _logger.Warning("Configuration validation failed with {ErrorCount} errors:", validationErrors.Count);
                foreach (var error in validationErrors)
                {
                    _logger.Warning("  - {Error}", error);
                }
                _logger.Information("Using default configuration due to validation errors.");
                return LauncherConfig.CreateDefault();
            }

            _logger.Information("Configuration loaded successfully from: {Path}", _configFilePath);

            // Auto-detect AC client path if not configured
            if (string.IsNullOrWhiteSpace(config.AcClientPath))
            {
                _logger.Debug("AC client path not configured. Attempting auto-detection...");
                var autoDetectedPath = TryAutoDetectAcClientPath();
                if (autoDetectedPath != null)
                {
                    _logger.Information("Auto-detected AC client at: {Path}", autoDetectedPath);
                    config.AcClientPath = autoDetectedPath;
                    // Save the auto-detected path
                    SaveConfigurationInternal(config);
                }
                else
                {
                    _logger.Debug("AC client not found at default installation paths");
                }
            }

            return config;
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Failed to parse configuration file. Using default configuration.");
            return LauncherConfig.CreateDefault();
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "IO error while loading configuration. Using default configuration.");
            return LauncherConfig.CreateDefault();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error while loading configuration. Using default configuration.");
            return LauncherConfig.CreateDefault();
        }
    }

    /// <summary>
    /// Saves the current configuration to disk.
    /// </summary>
    /// <returns>True if save was successful, false otherwise.</returns>
    public bool SaveConfiguration()
    {
        lock (_lock)
        {
            return SaveConfigurationInternal(_currentConfig);
        }
    }

    /// <summary>
    /// Saves the specified configuration to disk and sets it as the current configuration.
    /// </summary>
    /// <param name="config">The configuration to save.</param>
    /// <returns>True if save was successful, false otherwise.</returns>
    public bool SaveConfiguration(LauncherConfig config)
    {
        lock (_lock)
        {
            // Validate before saving
            var validationErrors = config.Validate();
            if (validationErrors.Count > 0)
            {
                _logger.Warning("Cannot save configuration due to validation errors:");
                foreach (var error in validationErrors)
                {
                    _logger.Warning("  - {Error}", error);
                }
                return false;
            }

            if (SaveConfigurationInternal(config))
            {
                _currentConfig = config;
                return true;
            }

            return false;
        }
    }

    private bool SaveConfigurationInternal(LauncherConfig config)
    {
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(_configDirectory))
            {
                _logger.Debug("Creating configuration directory: {Directory}", _configDirectory);
                Directory.CreateDirectory(_configDirectory);
            }

            // Update last saved timestamp
            config.LastSaved = DateTime.UtcNow;

            // Serialize to JSON
            var jsonText = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Write to file
            File.WriteAllText(_configFilePath, jsonText);

            _logger.Information("Configuration saved successfully to: {Path}", _configFilePath);

            // Raise event
            ConfigurationChanged?.Invoke(this, config);

            return true;
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "IO error while saving configuration to: {Path}", _configFilePath);
            return false;
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Failed to serialize configuration");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, "Access denied while saving configuration to: {Path}", _configFilePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error while saving configuration");
            return false;
        }
    }

    /// <summary>
    /// Updates a specific configuration property and saves to disk.
    /// </summary>
    /// <param name="updateAction">Action to modify the configuration.</param>
    /// <returns>True if update and save were successful, false otherwise.</returns>
    public bool UpdateConfiguration(Action<LauncherConfig> updateAction)
    {
        lock (_lock)
        {
            try
            {
                // Clone current config to avoid partial updates
                var updatedConfig = _currentConfig.Clone();

                // Apply update
                updateAction(updatedConfig);

                // Validate and save
                return SaveConfiguration(updatedConfig);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while updating configuration");
                return false;
            }
        }
    }

    /// <summary>
    /// Tries to auto-detect the AC client path at common installation locations.
    /// </summary>
    /// <returns>The auto-detected path if found, otherwise null.</returns>
    private string? TryAutoDetectAcClientPath()
    {
        // Check default Turbine installation path first
        var defaultPath = @"C:\Turbine\Asheron's Call\acclient.exe";
        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        // Check other common installation paths
        var commonPaths = new[]
        {
            @"C:\Program Files (x86)\Microsoft Games\Asheron's Call\acclient.exe",
            @"C:\Program Files\Microsoft Games\Asheron's Call\acclient.exe"
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Validates the Asheron's Call client path.
    /// Checks if the path exists and points to acclient.exe.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <returns>Validation result with success flag and error message if applicable.</returns>
    public (bool IsValid, string? ErrorMessage) ValidateAcClientPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (false, "AC client path cannot be empty");
        }

        if (!File.Exists(path))
        {
            return (false, $"File not found: {path}");
        }

        var fileName = Path.GetFileName(path);
        if (!fileName.Equals("acclient.exe", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Path must point to acclient.exe");
        }

        try
        {
            // Additional check: try to read file info to ensure it's accessible
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length == 0)
            {
                return (false, "AC client executable appears to be empty or corrupted");
            }

            _logger.Debug("AC client path validation successful: {Path}", path);
            return (true, null);
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Access denied to AC client executable");
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error while validating AC client path: {Path}", path);
            return (false, $"Unable to access file: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets the AC client path after validation.
    /// </summary>
    /// <param name="path">The path to the AC client executable.</param>
    /// <returns>True if path is valid and configuration was saved, false otherwise.</returns>
    public bool SetAcClientPath(string? path)
    {
        var (isValid, errorMessage) = ValidateAcClientPath(path);

        if (!isValid)
        {
            _logger.Warning("Invalid AC client path: {Error}", errorMessage ?? "Unknown error");
            return false;
        }

        return UpdateConfiguration(config => config.AcClientPath = path);
    }


    /// <summary>
    /// Sets whether the launcher should remember the user's login session.
    /// </summary>
    /// <param name="rememberMe">True to remember login, false otherwise.</param>
    /// <returns>True if setting was saved successfully, false otherwise.</returns>
    public bool SetRememberMe(bool rememberMe)
    {
        return UpdateConfiguration(config => config.RememberMe = rememberMe);
    }

    /// <summary>
    /// Sets whether the launcher should automatically check for updates.
    /// </summary>
    /// <param name="autoCheck">True to enable auto-check, false otherwise.</param>
    /// <returns>True if setting was saved successfully, false otherwise.</returns>
    public bool SetAutoCheckUpdates(bool autoCheck)
    {
        return UpdateConfiguration(config => config.AutoCheckUpdates = autoCheck);
    }

    /// <summary>
    /// Updates the UI theme preference.
    /// </summary>
    /// <param name="theme">The theme to apply.</param>
    /// <returns>True if setting was saved successfully, false otherwise.</returns>
    public bool UpdateTheme(AppTheme theme)
    {
        return UpdateConfiguration(config => config.Theme = theme);
    }

    /// <summary>
    /// Resets the configuration to default values.
    /// </summary>
    /// <returns>True if reset was successful, false otherwise.</returns>
    public bool ResetToDefaults()
    {
        _logger.Information("Resetting configuration to defaults");
        var defaultConfig = LauncherConfig.CreateDefault();
        return SaveConfiguration(defaultConfig);
    }

    /// <summary>
    /// Gets the full path to the configuration file.
    /// </summary>
    /// <returns>The absolute path to config.json.</returns>
    public string GetConfigFilePath()
    {
        return _configFilePath;
    }

    /// <summary>
    /// Gets the configuration directory path.
    /// </summary>
    /// <returns>The absolute path to the configuration directory.</returns>
    public string GetConfigDirectory()
    {
        return _configDirectory;
    }

    /// <summary>
    /// Checks if the configuration file exists on disk.
    /// </summary>
    /// <returns>True if config file exists, false otherwise.</returns>
    public bool ConfigFileExists()
    {
        return File.Exists(_configFilePath);
    }

    /// <summary>
    /// Exports the current configuration to a specified file path.
    /// Useful for backup or sharing configurations.
    /// </summary>
    /// <param name="exportPath">The path where to export the configuration.</param>
    /// <returns>True if export was successful, false otherwise.</returns>
    public bool ExportConfiguration(string exportPath)
    {
        try
        {
            lock (_lock)
            {
                var jsonText = JsonSerializer.Serialize(_currentConfig, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.WriteAllText(exportPath, jsonText);
                _logger.Information("Configuration exported to: {Path}", exportPath);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to export configuration to: {Path}", exportPath);
            return false;
        }
    }

    /// <summary>
    /// Imports configuration from a specified file path.
    /// </summary>
    /// <param name="importPath">The path to the configuration file to import.</param>
    /// <returns>True if import was successful, false otherwise.</returns>
    public bool ImportConfiguration(string importPath)
    {
        try
        {
            if (!File.Exists(importPath))
            {
                _logger.Warning("Import file not found: {Path}", importPath);
                return false;
            }

            var jsonText = File.ReadAllText(importPath);
            var config = JsonSerializer.Deserialize<LauncherConfig>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
            {
                _logger.Warning("Failed to deserialize import file: {Path}", importPath);
                return false;
            }

            // Validate imported config
            var validationErrors = config.Validate();
            if (validationErrors.Count > 0)
            {
                _logger.Warning("Imported configuration is invalid:");
                foreach (var error in validationErrors)
                {
                    _logger.Warning("  - {Error}", error);
                }
                return false;
            }

            // Save imported config
            if (SaveConfiguration(config))
            {
                _logger.Information("Configuration imported successfully from: {Path}", importPath);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to import configuration from: {Path}", importPath);
            return false;
        }
    }
}
