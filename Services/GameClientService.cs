// TODO: [LAUNCH-138] Phase 4 Week 8 - GameClientService
// Component: Launcher
// Module: First-Run Experience - Game Client Detection
// Description: Detects, validates, and optionally installs Asheron's Call client

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OPLauncher.Services;

/// <summary>
/// Service for detecting and managing the Asheron's Call client installation.
/// Handles automatic detection via registry and common paths, validation, and optional installation.
/// </summary>
public class GameClientService
{
    private readonly LoggingService _logger;
    private readonly ConfigService _configService;

    /// <summary>
    /// Common installation paths to check for acclient.exe.
    /// Ordered by likelihood (most common first).
    /// </summary>
    private readonly string[] _commonPaths = new[]
    {
        @"C:\Turbine\Asheron's Call\acclient.exe",
        @"C:\Program Files (x86)\Microsoft Games\Asheron's Call\acclient.exe",
        @"C:\Program Files\Microsoft Games\Asheron's Call\acclient.exe",
        @"C:\Games\Asheron's Call\acclient.exe",
        @"C:\AC\acclient.exe"
    };

    /// <summary>
    /// Registry paths to check for AC client installation.
    /// </summary>
    private readonly string[] _registryPaths = new[]
    {
        @"SOFTWARE\Microsoft\Microsoft Games\Asheron's Call",
        @"SOFTWARE\WOW6432Node\Microsoft\Microsoft Games\Asheron's Call",
        @"SOFTWARE\Turbine\Asheron's Call"
    };

    /// <summary>
    /// Initializes a new instance of the GameClientService.
    /// </summary>
    public GameClientService(
        LoggingService logger,
        ConfigService configService)
    {
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// Detects the AC client installation by checking registry and common paths.
    /// Returns the path to acclient.exe if found, null otherwise.
    /// </summary>
    /// <returns>Path to acclient.exe, or null if not found</returns>
    public async Task<string?> DetectAcClientAsync()
    {
        _logger.Information("Starting AC client detection");

        // 1. Check if already configured
        if (!string.IsNullOrEmpty(_configService.Current.AcClientPath))
        {
            var configuredPath = _configService.Current.AcClientPath;
            _logger.Information("AC client path already configured: {Path}", configuredPath);

            if (await ValidateAcClientAsync(configuredPath))
            {
                _logger.Information("Configured AC client path is valid");
                return configuredPath;
            }
            else
            {
                _logger.Warning("Configured AC client path is invalid, will attempt auto-detection");
            }
        }

        // 2. Try registry detection (Windows only)
        if (OperatingSystem.IsWindows())
        {
            var registryPath = await TryDetectFromRegistryAsync();
            if (registryPath != null)
            {
                _logger.Information("AC client detected from registry: {Path}", registryPath);
                return registryPath;
            }
        }

        // 3. Try common installation paths
        var commonPath = await TryDetectFromCommonPathsAsync();
        if (commonPath != null)
        {
            _logger.Information("AC client detected from common path: {Path}", commonPath);
            return commonPath;
        }

        _logger.Warning("AC client not detected automatically");
        return null;
    }

    /// <summary>
    /// Attempts to detect AC client from Windows registry.
    /// </summary>
    private async Task<string?> TryDetectFromRegistryAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        await Task.CompletedTask; // Make async for consistency

        foreach (var regPath in _registryPaths)
        {
            try
            {
                // Try both HKEY_LOCAL_MACHINE and HKEY_CURRENT_USER
                foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    using var key = hive.OpenSubKey(regPath);
                    if (key == null) continue;

                    // Common registry value names for installation path
                    var valueNames = new[] { "InstallDir", "Path", "InstallPath", "Location" };

                    foreach (var valueName in valueNames)
                    {
                        var installDir = key.GetValue(valueName) as string;
                        if (string.IsNullOrEmpty(installDir)) continue;

                        var acClientPath = Path.Combine(installDir, "acclient.exe");
                        if (File.Exists(acClientPath))
                        {
                            _logger.Debug("Found AC client in registry: {Registry} -> {Path}", regPath, acClientPath);
                            return acClientPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug("Failed to read registry path {Path}: {Error}", regPath, ex.Message);
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to detect AC client from common installation paths.
    /// </summary>
    private async Task<string?> TryDetectFromCommonPathsAsync()
    {
        await Task.CompletedTask; // Make async for consistency

        foreach (var path in _commonPaths)
        {
            if (File.Exists(path))
            {
                _logger.Debug("Found AC client at common path: {Path}", path);
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Validates that the given path points to a valid acclient.exe.
    /// Checks file existence, name, and optionally file size/hash.
    /// </summary>
    /// <param name="acClientPath">Path to validate</param>
    /// <returns>True if valid AC client executable</returns>
    public async Task<bool> ValidateAcClientAsync(string? acClientPath)
    {
        if (string.IsNullOrWhiteSpace(acClientPath))
        {
            _logger.Debug("AC client path is null or empty");
            return false;
        }

        await Task.CompletedTask; // Make async for consistency

        // 1. Check file exists
        if (!File.Exists(acClientPath))
        {
            _logger.Debug("AC client file does not exist: {Path}", acClientPath);
            return false;
        }

        // 2. Check filename is acclient.exe
        var fileName = Path.GetFileName(acClientPath);
        if (!fileName.Equals("acclient.exe", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Debug("File is not acclient.exe: {FileName}", fileName);
            return false;
        }

        // 3. Check file size is reasonable (AC client is ~5-15 MB)
        var fileInfo = new FileInfo(acClientPath);
        const long minSize = 1 * 1024 * 1024;  // 1 MB min
        const long maxSize = 50 * 1024 * 1024; // 50 MB max (generous)

        if (fileInfo.Length < minSize || fileInfo.Length > maxSize)
        {
            _logger.Warning("AC client file size is suspicious: {Size} bytes", fileInfo.Length);
            // Still return true, just warn (might be a modified client)
        }

        // 4. Check for portal.dat in same directory (validates it's an AC installation)
        var installDir = Path.GetDirectoryName(acClientPath);
        if (!string.IsNullOrEmpty(installDir))
        {
            var portalDatPath = Path.Combine(installDir, "portal.dat");
            if (!File.Exists(portalDatPath))
            {
                _logger.Warning("portal.dat not found in AC client directory, might not be a complete installation");
                // Still return true, user might have custom setup
            }
        }

        _logger.Debug("AC client validation passed: {Path}", acClientPath);
        return true;
    }

    /// <summary>
    /// Gets the installation directory for the AC client.
    /// </summary>
    /// <param name="acClientPath">Path to acclient.exe</param>
    /// <returns>Installation directory path, or null if invalid</returns>
    public string? GetInstallDirectory(string? acClientPath)
    {
        if (string.IsNullOrWhiteSpace(acClientPath))
        {
            return null;
        }

        return Path.GetDirectoryName(acClientPath);
    }

    /// <summary>
    /// Checks if the End of Retail patch has been applied.
    /// This checks for the presence of a marker file or modified portal.dat.
    /// </summary>
    /// <param name="acClientPath">Path to acclient.exe</param>
    /// <returns>True if patch is applied</returns>
    public async Task<bool> IsPatchAppliedAsync(string? acClientPath)
    {
        if (string.IsNullOrWhiteSpace(acClientPath))
        {
            return false;
        }

        await Task.CompletedTask;

        var installDir = GetInstallDirectory(acClientPath);
        if (string.IsNullOrEmpty(installDir))
        {
            return false;
        }

        // Check for patch marker file (PatchService will create this after applying patch)
        var patchMarkerPath = Path.Combine(installDir, ".oldportal_eor_patch_applied");
        if (File.Exists(patchMarkerPath))
        {
            _logger.Debug("End of Retail patch marker found");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets a list of all AC client installations found on the system.
    /// Useful for showing the user multiple detected installations.
    /// </summary>
    /// <returns>List of detected AC client paths</returns>
    public async Task<List<string>> FindAllInstallationsAsync()
    {
        _logger.Information("Searching for all AC client installations");
        var installations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Add registry detections
        if (OperatingSystem.IsWindows())
        {
            var registryPath = await TryDetectFromRegistryAsync();
            if (registryPath != null)
            {
                installations.Add(registryPath);
            }
        }

        // 2. Add common path detections
        var commonPath = await TryDetectFromCommonPathsAsync();
        if (commonPath != null)
        {
            installations.Add(commonPath);
        }

        // 3. Add configured path if valid
        if (!string.IsNullOrEmpty(_configService.Current.AcClientPath))
        {
            if (await ValidateAcClientAsync(_configService.Current.AcClientPath))
            {
                installations.Add(_configService.Current.AcClientPath);
            }
        }

        var result = installations.ToList();
        _logger.Information("Found {Count} AC client installation(s)", result.Count);

        return result;
    }

    /// <summary>
    /// Opens the AC client installer download URL in the user's default browser.
    /// Archive.org requires browser behavior (cookies, JavaScript) to download properly.
    /// The browser will handle the download to the user's Downloads folder.
    /// </summary>
    /// <returns>True if browser opened successfully</returns>
    public bool OpenAcClientDownloadInBrowser()
    {
        const string installerUrl = "https://web.archive.org/web/20201121104423/http://content.turbine.com/sites/clientdl/ac1/ac1install.exe";

        _logger.Information("Opening AC client installer download in browser: {Url}", installerUrl);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = installerUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            _logger.Information("Browser opened successfully for AC client download");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to open browser for AC client download: {Error}", ex.Message);
            return false;
        }
    }
}
