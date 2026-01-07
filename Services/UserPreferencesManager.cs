using System;
using System.IO;
using System.Text;

namespace OPLauncher.Services;

/// <summary>
/// Manages the Asheron's Call UserPreferences.ini file for multi-client configuration.
/// Handles reading, writing, and backing up the UserPreferences.ini file located in
/// the user's Documents\Asheron's Call directory.
///
/// Primary responsibility: Enable/disable ComputeUniquePort setting in [Net] section,
/// which is required for multi-client support (allows each client to use a unique network port).
/// </summary>
public class UserPreferencesManager
{
    private readonly LoggingService _logger;
    private readonly object _fileLock = new();

    /// <summary>
    /// Gets the full path to the UserPreferences.ini file.
    /// Location: %USERPROFILE%\Documents\Asheron's Call\UserPreferences.ini
    /// </summary>
    public string UserPreferencesPath { get; }

    /// <summary>
    /// Initializes a new instance of the UserPreferencesManager.
    /// </summary>
    /// <param name="logger">The logging service for diagnostic output.</param>
    public UserPreferencesManager(LoggingService logger)
    {
        _logger = logger;

        // Construct path: Documents\Asheron's Call\UserPreferences.ini
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var acDirectory = Path.Combine(documentsPath, "Asheron's Call");
        UserPreferencesPath = Path.Combine(acDirectory, "UserPreferences.ini");

        _logger.Debug("UserPreferencesManager initialized with path: {Path}", UserPreferencesPath);
    }

    /// <summary>
    /// Gets the full path to the UserPreferences.ini file.
    /// </summary>
    /// <returns>The full path to UserPreferences.ini</returns>
    public string GetUserPreferencesPath()
    {
        return UserPreferencesPath;
    }

    /// <summary>
    /// Checks if the UserPreferences.ini file exists.
    /// </summary>
    /// <returns>True if the file exists, false otherwise.</returns>
    public bool FileExists()
    {
        var exists = File.Exists(UserPreferencesPath);
        _logger.Debug("UserPreferences.ini exists: {Exists}", exists);
        return exists;
    }

    /// <summary>
    /// Checks if ComputeUniquePort is enabled in the [Net] section of UserPreferences.ini.
    /// </summary>
    /// <returns>True if ComputeUniquePort=True, false otherwise (including if file doesn't exist).</returns>
    public bool IsComputeUniquePortEnabled()
    {
        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(UserPreferencesPath))
                {
                    _logger.Debug("UserPreferences.ini does not exist, ComputeUniquePort is not enabled");
                    return false;
                }

                _logger.Debug("Reading UserPreferences.ini to check ComputeUniquePort status");

                var lines = File.ReadAllLines(UserPreferencesPath);
                bool inNetSection = false;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    // Check for [Net] section header
                    if (trimmedLine.Equals("[Net]", StringComparison.OrdinalIgnoreCase))
                    {
                        inNetSection = true;
                        _logger.Debug("Found [Net] section");
                        continue;
                    }

                    // If we encounter another section, we've left [Net]
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        if (inNetSection)
                        {
                            _logger.Debug("Left [Net] section, ComputeUniquePort not found");
                            return false; // We were in [Net] but didn't find the setting
                        }
                        continue;
                    }

                    // Look for ComputeUniquePort in [Net] section
                    if (inNetSection && trimmedLine.StartsWith("ComputeUniquePort", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = trimmedLine.Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            var value = parts[1].Trim();
                            bool isEnabled = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                            _logger.Information("ComputeUniquePort found in [Net] section: {Value} (Enabled: {IsEnabled})", value, isEnabled);
                            return isEnabled;
                        }
                    }
                }

                _logger.Debug("ComputeUniquePort not found in UserPreferences.ini");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error reading UserPreferences.ini to check ComputeUniquePort");
                return false;
            }
        }
    }

    /// <summary>
    /// Enables ComputeUniquePort in the [Net] section of UserPreferences.ini.
    /// Creates the file and section if they don't exist.
    /// Creates a backup before modifying.
    /// </summary>
    /// <returns>True if successful, false otherwise.</returns>
    public bool EnableComputeUniquePort()
    {
        lock (_fileLock)
        {
            try
            {
                _logger.Information("Enabling ComputeUniquePort in UserPreferences.ini");

                // Create backup if file exists
                if (File.Exists(UserPreferencesPath))
                {
                    if (!BackupUserPreferences())
                    {
                        _logger.Warning("Failed to create backup, proceeding anyway");
                    }
                }

                // Ensure directory exists
                var directory = Path.GetDirectoryName(UserPreferencesPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    _logger.Information("Creating AC directory: {Directory}", directory);
                    Directory.CreateDirectory(directory);
                }

                // Read existing file or create new content
                string[] lines;
                if (File.Exists(UserPreferencesPath))
                {
                    lines = File.ReadAllLines(UserPreferencesPath);
                }
                else
                {
                    _logger.Information("UserPreferences.ini does not exist, creating new file");
                    lines = Array.Empty<string>();
                }

                // Process the file
                var newLines = new System.Collections.Generic.List<string>();
                bool inNetSection = false;
                bool foundComputeUniquePort = false;
                bool netSectionExists = false;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    // Check for [Net] section
                    if (trimmedLine.Equals("[Net]", StringComparison.OrdinalIgnoreCase))
                    {
                        inNetSection = true;
                        netSectionExists = true;
                        newLines.Add(line);
                        continue;
                    }

                    // If we encounter another section, exit [Net] section
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        // If we were in [Net] and didn't find ComputeUniquePort, add it now
                        if (inNetSection && !foundComputeUniquePort)
                        {
                            newLines.Add("ComputeUniquePort=True");
                            _logger.Information("Added ComputeUniquePort=True to [Net] section");
                            foundComputeUniquePort = true;
                        }
                        inNetSection = false;
                        newLines.Add(line);
                        continue;
                    }

                    // Update ComputeUniquePort if found in [Net] section
                    if (inNetSection && trimmedLine.StartsWith("ComputeUniquePort", StringComparison.OrdinalIgnoreCase))
                    {
                        newLines.Add("ComputeUniquePort=True");
                        _logger.Information("Updated existing ComputeUniquePort to True");
                        foundComputeUniquePort = true;
                        continue;
                    }

                    // Keep all other lines unchanged
                    newLines.Add(line);
                }

                // If we were still in [Net] at end of file and didn't find the setting, add it
                if (inNetSection && !foundComputeUniquePort)
                {
                    newLines.Add("ComputeUniquePort=True");
                    _logger.Information("Added ComputeUniquePort=True at end of [Net] section");
                    foundComputeUniquePort = true;
                }

                // If [Net] section doesn't exist, create it at the end
                if (!netSectionExists)
                {
                    _logger.Information("Creating [Net] section with ComputeUniquePort=True");
                    if (newLines.Count > 0)
                    {
                        newLines.Add(""); // Blank line for readability
                    }
                    newLines.Add("[Net]");
                    newLines.Add("ComputeUniquePort=True");
                }

                // Write the modified content
                File.WriteAllLines(UserPreferencesPath, newLines, Encoding.UTF8);

                _logger.Information("Successfully enabled ComputeUniquePort in UserPreferences.ini");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error enabling ComputeUniquePort in UserPreferences.ini");
                return false;
            }
        }
    }

    /// <summary>
    /// Creates a timestamped backup of the UserPreferences.ini file.
    /// Backup file format: UserPreferences.ini.backup_YYYYMMDD_HHMMSS
    /// </summary>
    /// <returns>True if backup was successful, false otherwise.</returns>
    public bool BackupUserPreferences()
    {
        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(UserPreferencesPath))
                {
                    _logger.Debug("UserPreferences.ini does not exist, no backup needed");
                    return true; // Not an error - nothing to backup
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = $"{UserPreferencesPath}.backup_{timestamp}";

                _logger.Information("Creating backup: {BackupPath}", backupPath);

                File.Copy(UserPreferencesPath, backupPath, overwrite: false);

                _logger.Information("Backup created successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating backup of UserPreferences.ini");
                return false;
            }
        }
    }

    /// <summary>
    /// Restores UserPreferences.ini from the most recent backup file.
    /// Searches for backup files matching the pattern UserPreferences.ini.backup_*
    /// </summary>
    /// <returns>True if restore was successful, false otherwise.</returns>
    public bool RestoreUserPreferences()
    {
        lock (_fileLock)
        {
            try
            {
                var directory = Path.GetDirectoryName(UserPreferencesPath);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    _logger.Error("AC directory does not exist, cannot restore backup");
                    return false;
                }

                // Find all backup files
                var backupFiles = Directory.GetFiles(directory, "UserPreferences.ini.backup_*");

                if (backupFiles.Length == 0)
                {
                    _logger.Warning("No backup files found for UserPreferences.ini");
                    return false;
                }

                // Sort by filename (timestamp is in filename) to get most recent
                Array.Sort(backupFiles);
                var mostRecentBackup = backupFiles[^1]; // Last item (most recent)

                _logger.Information("Restoring from backup: {BackupPath}", mostRecentBackup);

                // Create backup of current file before restoring (just in case)
                if (File.Exists(UserPreferencesPath))
                {
                    var preRestoreBackup = $"{UserPreferencesPath}.pre_restore_{DateTime.Now:yyyyMMdd_HHmmss}";
                    File.Copy(UserPreferencesPath, preRestoreBackup, overwrite: false);
                    _logger.Debug("Created pre-restore backup: {Path}", preRestoreBackup);
                }

                // Restore the backup
                File.Copy(mostRecentBackup, UserPreferencesPath, overwrite: true);

                _logger.Information("Successfully restored UserPreferences.ini from backup");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error restoring UserPreferences.ini from backup");
                return false;
            }
        }
    }
}
