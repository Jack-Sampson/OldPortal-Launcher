using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.Services;

namespace OPLauncher.ViewModels;

/// <summary>
/// ViewModel for the multi-client help and documentation view.
/// Provides commands for testing configuration and accessing UserPreferences.ini.
/// </summary>
public partial class MultiClientHelpViewModel : ViewModelBase
{
    private readonly UserPreferencesManager _userPreferencesManager;
    private readonly ConfigService _configService;
    private readonly LoggingService _logger;

    [ObservableProperty]
    private string? _testStatusMessage;

    [ObservableProperty]
    private bool _isTestingConfiguration;

    public MultiClientHelpViewModel(
        UserPreferencesManager userPreferencesManager,
        ConfigService configService,
        LoggingService logger)
    {
        _userPreferencesManager = userPreferencesManager;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// Tests the UserPreferences.ini configuration.
    /// Checks if ComputeUniquePort is enabled and displays the result.
    /// </summary>
    [RelayCommand]
    private async Task TestConfigurationAsync()
    {
        IsTestingConfiguration = true;
        TestStatusMessage = "Testing configuration...";

        try
        {
            await Task.Run(() =>
            {
                var isConfigured = _userPreferencesManager.IsComputeUniquePortEnabled();

                if (isConfigured)
                {
                    TestStatusMessage = "✓ Configuration is correct! ComputeUniquePort=True is set.";
                    _logger.Information("UserPreferences.ini configuration test: PASS");
                }
                else
                {
                    TestStatusMessage = "✗ Configuration issue: ComputeUniquePort is not enabled. Use Settings to configure.";
                    _logger.Warning("UserPreferences.ini configuration test: FAIL - ComputeUniquePort not enabled");
                }
            });
        }
        catch (FileNotFoundException)
        {
            TestStatusMessage = "✗ UserPreferences.ini not found. Launch AC at least once to create it.";
            _logger.Warning("UserPreferences.ini not found during configuration test");
        }
        catch (Exception ex)
        {
            TestStatusMessage = $"✗ Error testing configuration: {ex.Message}";
            _logger.Error(ex, "Error testing UserPreferences.ini configuration");
        }
        finally
        {
            IsTestingConfiguration = false;
        }
    }

    /// <summary>
    /// Opens Windows Explorer to the folder containing UserPreferences.ini.
    /// </summary>
    [RelayCommand]
    private void OpenUserPreferencesLocation()
    {
        try
        {
            var acPath = _configService.Current.AcClientPath;
            if (string.IsNullOrWhiteSpace(acPath) || !File.Exists(acPath))
            {
                TestStatusMessage = "✗ AC client path not configured. Set it in Settings first.";
                _logger.Warning("Cannot open UserPreferences location - AC path not configured");
                return;
            }

            var acDirectory = Path.GetDirectoryName(acPath);
            if (acDirectory == null || !Directory.Exists(acDirectory))
            {
                TestStatusMessage = "✗ AC directory not found.";
                _logger.Warning("Cannot open UserPreferences location - directory not found: {Path}", acDirectory ?? "null");
                return;
            }

            // Open the directory in Windows Explorer
            Process.Start(new ProcessStartInfo
            {
                FileName = acDirectory,
                UseShellExecute = true,
                Verb = "open"
            });

            _logger.Information("Opened UserPreferences.ini location: {Path}", acDirectory);
            TestStatusMessage = "✓ Opened folder in Windows Explorer.";
        }
        catch (Exception ex)
        {
            TestStatusMessage = $"✗ Error opening folder: {ex.Message}";
            _logger.Error(ex, "Error opening UserPreferences.ini location");
        }
    }
}
