// TODO: [LAUNCH-137] Phase 4 Week 8 - OnboardingViewModel
// Component: Launcher
// Module: First-Run Experience - Onboarding Infrastructure
// Description: ViewModel for 6-step onboarding wizard with state machine

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.Services;

namespace OPLauncher.ViewModels;

/// <summary>
/// Manages the 6-step first-run onboarding experience.
/// Uses a state machine pattern to track progress through setup steps.
/// </summary>
public partial class OnboardingViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly LoggingService _logger;
    private readonly GameClientService _gameClientService;
    private readonly PatchService _patchService;
    private readonly DecalService _decalService;
    private readonly IFileDialogService _fileDialogService;

    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Enum representing the 6 onboarding steps.
    /// </summary>
    public enum OnboardingStep
    {
        Welcome = 1,
        GameClient = 2,
        Patch = 3,
        Decal = 4,
        Addons = 5,  // Marketing step for Old Portal addons (only shown if Decal installed)
        Complete = 6
    }

    /// <summary>
    /// Current step in the onboarding process.
    /// </summary>
    [ObservableProperty]
    private OnboardingStep _currentStep = OnboardingStep.Welcome;

    /// <summary>
    /// Total number of steps (always 6).
    /// </summary>
    public int TotalSteps => 6;

    /// <summary>
    /// Current step number (1-6) for display.
    /// </summary>
    public int CurrentStepNumber => (int)CurrentStep;

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int ProgressPercentage => (CurrentStepNumber - 1) * 100 / (TotalSteps - 1);

    /// <summary>
    /// Whether the Next button should be enabled.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private bool _canGoNext = true;

    /// <summary>
    /// Whether the Back button should be enabled.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackCommand))]
    private bool _canGoBack = false;

    /// <summary>
    /// Whether the Skip button should be visible.
    /// </summary>
    [ObservableProperty]
    private bool _canSkip = true;

    /// <summary>
    /// Whether a step is currently in progress (disables navigation).
    /// </summary>
    [ObservableProperty]
    private bool _isStepInProgress;

    /// <summary>
    /// Status message for current step.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Error message if step fails.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Path to AC client installation (detected or selected by user).
    /// </summary>
    [ObservableProperty]
    private string? _acClientPath;

    /// <summary>
    /// Whether AC client is installed and valid.
    /// </summary>
    [ObservableProperty]
    private bool _isAcClientInstalled;

    /// <summary>
    /// Whether End of Retail patch is applied.
    /// </summary>
    [ObservableProperty]
    private bool _isPatchApplied;

    /// <summary>
    /// Whether user has manually validated patch files are correct.
    /// Used as alternative to downloading patch.
    /// </summary>
    [ObservableProperty]
    private bool _patchValidatedManually;

    /// <summary>
    /// Whether Decal is installed.
    /// </summary>
    [ObservableProperty]
    private bool _isDecalInstalled;

    /// <summary>
    /// Whether onboarding is complete.
    /// </summary>
    [ObservableProperty]
    private bool _isOnboardingComplete;

    public OnboardingViewModel(
        ConfigService configService,
        LoggingService logger,
        GameClientService gameClientService,
        PatchService patchService,
        DecalService decalService,
        IFileDialogService fileDialogService)
    {
        _configService = configService;
        _logger = logger;
        _gameClientService = gameClientService;
        _patchService = patchService;
        _decalService = decalService;
        _fileDialogService = fileDialogService;

        _logger.Information("OnboardingViewModel initialized");

        // Set initial state
        UpdateNavigationState();

        // Initialize with auto-detection on startup
        _ = InitializeDetectionAsync();
    }

    /// <summary>
    /// Initializes the onboarding by auto-detecting existing installations.
    /// Runs in background to avoid blocking UI startup.
    /// </summary>
    private async Task InitializeDetectionAsync()
    {
        try
        {
            // Auto-detect AC client
            await DetectClientAsync();

            // Auto-detect Decal
            await DetectDecalAsync();

            // Note: Agent detection is NOT run here to avoid confusing status messages
            // Agent detection will run when user reaches the Agent step
        }
        catch (Exception ex)
        {
            _logger.Error("Error during initialization detection: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Called when CurrentStep changes to update navigation state.
    /// </summary>
    partial void OnCurrentStepChanged(OnboardingStep value)
    {
        _logger.Information("Onboarding step changed to: {Step}", value);

        // Clear status message when changing steps to avoid confusion
        StatusMessage = string.Empty;
        ErrorMessage = null;

        // Run step-specific initialization
        InitializeCurrentStep();

        UpdateNavigationState();
        OnPropertyChanged(nameof(CurrentStepNumber));
        OnPropertyChanged(nameof(ProgressPercentage));
    }

    /// <summary>
    /// Initializes the current step with appropriate status messages.
    /// </summary>
    private void InitializeCurrentStep()
    {
        switch (CurrentStep)
        {
            case OnboardingStep.GameClient:
                // Show detected client status if available
                if (IsAcClientInstalled && !string.IsNullOrEmpty(AcClientPath))
                {
                    StatusMessage = $"✅ AC client found: {AcClientPath}";
                    if (IsPatchApplied)
                    {
                        StatusMessage += "\n✅ End of Retail patch is already applied!";
                    }
                }
                else
                {
                    // Show initial instructions
                    StatusMessage = "👆 Click the 'Auto-Detect Game Client' button below to search for an existing installation.";
                }
                break;

            case OnboardingStep.Patch:
                // Show patch status if available
                if (IsPatchApplied)
                {
                    StatusMessage = "✅ End of Retail patch is already applied!";
                }
                break;

            case OnboardingStep.Decal:
                // Show Decal status if available
                if (IsDecalInstalled)
                {
                    var decalPath = _decalService.GetDecalInjectPath();
                    StatusMessage = $"✅ Decal is installed: {decalPath}";
                }
                else
                {
                    StatusMessage = "Decal is optional. You can skip this step if you prefer not to use plugins.";
                }
                break;

            case OnboardingStep.Addons:
                // Marketing step - no status message needed
                StatusMessage = string.Empty;
                break;
        }
    }

    /// <summary>
    /// Updates navigation button states based on current step.
    /// </summary>
    private void UpdateNavigationState()
    {
        // Back button: disabled on first step, enabled on others
        CanGoBack = CurrentStep != OnboardingStep.Welcome && !IsStepInProgress;

        // Next button: enabled unless step is in progress or validation fails
        CanGoNext = !IsStepInProgress && CanProceedToNextStep();

        // Skip button: visible until completion step, but hidden on Addons (marketing-only step)
        CanSkip = CurrentStep != OnboardingStep.Complete && CurrentStep != OnboardingStep.Addons;
    }

    /// <summary>
    /// Checks if current step allows proceeding to next step.
    /// </summary>
    private bool CanProceedToNextStep()
    {
        return CurrentStep switch
        {
            OnboardingStep.Welcome => true, // Always can proceed from welcome
            OnboardingStep.GameClient => IsAcClientInstalled,
            OnboardingStep.Patch => IsPatchApplied || PatchValidatedManually, // Allow manual validation
            OnboardingStep.Decal => true, // Decal is optional - users can skip
            OnboardingStep.Addons => true, // Marketing step - always can proceed
            OnboardingStep.Complete => true,
            _ => false
        };
    }

    /// <summary>
    /// Called when PatchValidatedManually changes to update navigation state.
    /// </summary>
    partial void OnPatchValidatedManuallyChanged(bool value)
    {
        UpdateNavigationState();
    }

    /// <summary>
    /// Called when IsAcClientInstalled changes to update navigation state.
    /// </summary>
    partial void OnIsAcClientInstalledChanged(bool value)
    {
        UpdateNavigationState();
    }

    /// <summary>
    /// Called when IsPatchApplied changes to update navigation state.
    /// </summary>
    partial void OnIsPatchAppliedChanged(bool value)
    {
        UpdateNavigationState();
    }

    /// <summary>
    /// Called when IsDecalInstalled changes to update navigation state.
    /// Decal is now optional, so Next button should always be enabled on Decal step.
    /// </summary>
    partial void OnIsDecalInstalledChanged(bool value)
    {
        UpdateNavigationState();
    }

    /// <summary>
    /// Called when IsStepInProgress changes to update navigation state.
    /// </summary>
    partial void OnIsStepInProgressChanged(bool value)
    {
        UpdateNavigationState();
    }

    /// <summary>
    /// Navigates to the next step.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextAsync()
    {
        if (CurrentStep == OnboardingStep.Complete)
        {
            await CompleteOnboardingAsync();
            return;
        }

        // Special logic: If leaving Decal step without installing, skip Addons step
        // (Addons are Decal plugins, so no point showing them if user doesn't use Decal)
        if (CurrentStep == OnboardingStep.Decal && !IsDecalInstalled)
        {
            _logger.Information("User skipped Decal, skipping Addons step (requires Decal)");
            CurrentStep = OnboardingStep.Complete;
            ErrorMessage = null;
            return;
        }

        var nextStep = (OnboardingStep)((int)CurrentStep + 1);
        CurrentStep = nextStep;
        ErrorMessage = null;

        _logger.Information("Advanced to step {Step}", nextStep);
    }

    /// <summary>
    /// Navigates to the previous step.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        if (CurrentStep == OnboardingStep.Welcome)
            return;

        var previousStep = (OnboardingStep)((int)CurrentStep - 1);
        CurrentStep = previousStep;
        ErrorMessage = null;

        _logger.Information("Went back to step {Step}", previousStep);
    }

    /// <summary>
    /// Skips the onboarding process.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSkip))]
    private void Skip()
    {
        _logger.Warning("User skipped onboarding at step {Step}", CurrentStep);

        // Mark onboarding as incomplete but allow launcher to proceed
        _configService.Current.IsOnboardingComplete = false;
        _configService.SaveConfiguration();

        // Close onboarding (handled by parent)
        OnOnboardingSkipped?.Invoke();
    }

    /// <summary>
    /// Completes the onboarding process.
    /// </summary>
    private async Task CompleteOnboardingAsync()
    {
        try
        {
            _logger.Information("Completing onboarding");

            IsOnboardingComplete = true;

            // Save AC client path to config
            if (!string.IsNullOrEmpty(AcClientPath))
            {
                _configService.Current.AcClientPath = AcClientPath;
            }

            // Save Decal preference if it was detected during onboarding
            if (IsDecalInstalled)
            {
                _configService.Current.UseDecal = true;
                _logger.Information("Decal was detected during onboarding, enabling UseDecal setting");
            }

            // Mark onboarding as complete
            _configService.Current.IsOnboardingComplete = true;
            _configService.SaveConfiguration();

            _logger.Information("Onboarding completed successfully");

            // Wait a moment to show completion screen
            await Task.Delay(1500);

            // Close onboarding and launch main app
            OnOnboardingCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error completing onboarding");
            ErrorMessage = "Failed to save configuration";
        }
    }

    /// <summary>
    /// Download progress properties for Steps 3 and 5.
    /// </summary>
    [ObservableProperty]
    private long _downloadBytesDownloaded;

    [ObservableProperty]
    private long _downloadTotalBytes;

    /// <summary>
    /// Download progress percentage (0-100).
    /// </summary>
    public int DownloadProgressPercentage =>
        DownloadTotalBytes > 0 ? (int)((DownloadBytesDownloaded * 100) / DownloadTotalBytes) : 0;

    // ========================================
    // STEP 2: GAME CLIENT DETECTION/BROWSE
    // ========================================

    /// <summary>
    /// Detects the AC client installation automatically.
    /// </summary>
    [RelayCommand]
    private async Task DetectClientAsync()
    {
        try
        {
            _logger.Information("Starting AC client detection");
            StatusMessage = "Detecting Asheron's Call client...";
            ErrorMessage = null;
            IsStepInProgress = true;

            var detectedPath = await _gameClientService.DetectAcClientAsync();

            if (detectedPath != null)
            {
                AcClientPath = detectedPath;
                IsAcClientInstalled = true;
                StatusMessage = $"✅ AC client found: {detectedPath}";
                _logger.Information("AC client detected at: {Path}", detectedPath);

                // Auto-check if patch is already applied
                var isPatchApplied = await _gameClientService.IsPatchAppliedAsync(detectedPath);
                if (isPatchApplied)
                {
                    IsPatchApplied = true;
                    StatusMessage += "\n✅ End of Retail patch is already applied!";
                }
            }
            else
            {
                IsAcClientInstalled = false;
                StatusMessage = "❌ AC client not found.\n\n" +
                               "No worries! You can either:\n" +
                               "• Download the official game client (~500 MB)\n" +
                               "• Browse to an existing installation folder";
                _logger.Warning("AC client not detected");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error detecting AC client: {Error}", ex.Message);
            ErrorMessage = $"Detection failed: {ex.Message}";
            IsAcClientInstalled = false;
        }
        finally
        {
            IsStepInProgress = false;
            UpdateNavigationState();
        }
    }

    /// <summary>
    /// Opens a file browser to manually select acclient.exe.
    /// </summary>
    [RelayCommand]
    private async Task BrowseForClientAsync()
    {
        try
        {
            _logger.Information("User browsing for AC client");
            StatusMessage = "Please select acclient.exe...";
            ErrorMessage = null;

            var selectedPath = await _fileDialogService.ShowOpenFileDialogAsync(
                "Select Asheron's Call Client",
                new[] { "*.exe" });

            if (!string.IsNullOrEmpty(selectedPath))
            {
                // Validate the selected file
                if (await _gameClientService.ValidateAcClientAsync(selectedPath))
                {
                    AcClientPath = selectedPath;
                    IsAcClientInstalled = true;
                    StatusMessage = $"✅ AC client selected: {selectedPath}";
                    _logger.Information("User selected valid AC client: {Path}", selectedPath);

                    // Auto-check if patch is already applied
                    var isPatchApplied = await _gameClientService.IsPatchAppliedAsync(selectedPath);
                    if (isPatchApplied)
                    {
                        IsPatchApplied = true;
                        StatusMessage += "\n✅ End of Retail patch is already applied!";
                    }

                    UpdateNavigationState();
                }
                else
                {
                    ErrorMessage = "The selected file is not a valid acclient.exe";
                    IsAcClientInstalled = false;
                    StatusMessage = "❌ Invalid file selected.\n\n" +
                                  "Please select 'acclient.exe' from your Asheron's Call installation folder,\n" +
                                  "or download the game client using the button below.";
                    _logger.Warning("User selected invalid AC client: {Path}", selectedPath);
                }
            }
            else
            {
                StatusMessage = "No file selected.\n\n" +
                              "👆 Click 'Auto-Detect' to search automatically,\n" +
                              "or use the buttons below to download or browse.";
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error browsing for AC client: {Error}", ex.Message);
            ErrorMessage = $"Browse failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens the AC client installer download in the user's browser.
    /// Browser will download to the user's Downloads folder (~500 MB).
    /// </summary>
    [RelayCommand]
    private async Task DownloadAcClientAsync()
    {
        try
        {
            _logger.Information("Opening AC client download in browser");
            StatusMessage = "Opening download in your browser...";
            ErrorMessage = null;

            // Open browser to download the installer
            var success = _gameClientService.OpenAcClientDownloadInBrowser();

            if (success)
            {
                StatusMessage = "✅ Your browser is downloading the installer (~500 MB).\n" +
                               "Once downloaded, run ac1install.exe, then click 'Detect AC Client' below.";
                _logger.Information("Browser opened for AC client download");
            }
            else
            {
                ErrorMessage = "Failed to open browser. Please manually visit:\n" +
                              "https://web.archive.org/web/20201121104423/http://content.turbine.com/sites/clientdl/ac1/ac1install.exe";
                _logger.Error("Failed to open browser for AC client download");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error("Error opening AC client download: {Error}", ex.Message);
            ErrorMessage = $"Failed to open browser: {ex.Message}";
        }
    }

    // ========================================
    // STEP 3: PATCH APPLICATION
    // ========================================

    /// <summary>
    /// Downloads and applies the End of Retail patch.
    /// </summary>
    [RelayCommand]
    private async Task ApplyPatchAsync()
    {
        if (string.IsNullOrEmpty(AcClientPath))
        {
            ErrorMessage = "AC client path is not set";
            return;
        }

        try
        {
            _logger.Information("Starting End of Retail patch application");
            StatusMessage = "Downloading End of Retail patch from Mega.nz...";
            ErrorMessage = null;
            IsStepInProgress = true;

            _cancellationTokenSource = new CancellationTokenSource();

            // Progress callback
            void OnProgress(long downloaded, long total)
            {
                DownloadBytesDownloaded = downloaded;
                DownloadTotalBytes = total;
                OnPropertyChanged(nameof(DownloadProgressPercentage));

                StatusMessage = $"Downloading: {downloaded / 1024 / 1024} MB / {total / 1024 / 1024} MB ({DownloadProgressPercentage}%)";
            }

            var result = await _patchService.DownloadAndApplyPatchAsync(
                AcClientPath,
                OnProgress,
                _cancellationTokenSource.Token);

            if (result.Success)
            {
                IsPatchApplied = true;
                StatusMessage = "✅ End of Retail patch applied successfully!";
                _logger.Information("Patch applied successfully");
            }
            else
            {
                IsPatchApplied = false;
                _logger.Error("Patch application failed - see detailed logs above");

                // Build error message with manual installation instructions
                var errorMsg = "❌ Patch installation failed.\n\n";
                errorMsg += $"Error: {result.ErrorMessage}\n\n";

                if (!string.IsNullOrEmpty(result.ExtractedFilesPath))
                {
                    // Files were extracted but not installed - provide manual installation instructions
                    errorMsg += "═══════════════════════════════════════\n";
                    errorMsg += "MANUAL INSTALLATION REQUIRED\n";
                    errorMsg += "═══════════════════════════════════════\n\n";
                    errorMsg += "The patch files were downloaded but could not be\n";
                    errorMsg += "automatically installed. You can manually copy them:\n\n";
                    errorMsg += $"📁 Patch files are located at:\n";
                    errorMsg += $"   {result.ExtractedFilesPath}\n\n";
                    errorMsg += $"📁 Copy all files from the above folder to:\n";
                    errorMsg += $"   {System.IO.Path.GetDirectoryName(AcClientPath)}\n\n";
                    errorMsg += "⚠️  Make sure to close Asheron's Call before copying!\n\n";
                    errorMsg += "After copying manually, click 'Next' to continue.";

                    _logger.Error("Manual installation required - patch files at: {Path}", result.ExtractedFilesPath);
                }
                else
                {
                    // Generic error
                    errorMsg += "Common issues:\n";
                    errorMsg += "• Asheron's Call is still running (close it completely)\n";
                    errorMsg += "• Files are in use by another program\n";
                    errorMsg += "• Insufficient disk space\n";
                    errorMsg += "• Permission denied (try running launcher as administrator)\n\n";
                    errorMsg += "Check the logs for detailed error information.";
                }

                ErrorMessage = errorMsg;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Patch download cancelled by user");
            StatusMessage = "Patch download cancelled";
        }
        catch (Exception ex)
        {
            _logger.Error("Error applying patch: {Error}", ex.Message);
            ErrorMessage = $"Patch failed: {ex.Message}";
            IsPatchApplied = false;
        }
        finally
        {
            IsStepInProgress = false;
            DownloadBytesDownloaded = 0;
            DownloadTotalBytes = 0;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            UpdateNavigationState();
        }
    }

    // ========================================
    // STEP 4: DECAL DETECTION/INSTALLATION
    // ========================================

    /// <summary>
    /// Detects if Decal is installed.
    /// </summary>
    [RelayCommand]
    private async Task DetectDecalAsync()
    {
        try
        {
            _logger.Information("Detecting Decal installation");
            StatusMessage = "Checking for Decal installation...";
            ErrorMessage = null;

            await Task.Delay(100); // Small delay for UI responsiveness

            var isInstalled = _decalService.IsDecalInstalled();

            if (isInstalled)
            {
                IsDecalInstalled = true;
                var decalPath = _decalService.GetDecalInjectPath();
                StatusMessage = $"✅ Decal is installed: {decalPath}";
                _logger.Information("Decal detected at: {Path}", decalPath);
            }
            else
            {
                IsDecalInstalled = false;
                StatusMessage = "❌ Decal is not installed. Please download and install Decal.";
                _logger.Warning("Decal not detected");
            }

            UpdateNavigationState();
        }
        catch (Exception ex)
        {
            _logger.Error("Error detecting Decal: {Error}", ex.Message);
            ErrorMessage = $"Decal detection failed: {ex.Message}";
            IsDecalInstalled = false;
        }
    }

    /// <summary>
    /// Opens the Decal download page in the user's browser.
    /// </summary>
    [RelayCommand]
    private async Task OpenDecalDownloadAsync()
    {
        try
        {
            _logger.Information("Opening Decal download page");
            StatusMessage = "Downloading Decal installer...";

            // Direct download link for Decal 2.9.8.3 MSI installer
            const string decalUrl = "https://www.decaldev.com/releases/2983/Decal.msi";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = decalUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            StatusMessage = "Please run the Decal installer, then click 'Detect Decal' when complete.";

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error("Error opening Decal download page: {Error}", ex.Message);
            ErrorMessage = $"Failed to open browser: {ex.Message}";
        }
    }

    // ========================================
    // STEP 5: OLD PORTAL ADDONS (MARKETING)
    // ========================================

    /// <summary>
    /// Opens the OldPortal downloads page where users can browse and download addons.
    /// This is a marketing/informational step - no installation logic needed.
    /// </summary>
    [RelayCommand]
    private void OpenAddonsDownloadPage()
    {
        try
        {
            _logger.Information("User opened addons download page");
            var url = "https://oldportal.com/downloads";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open addons download page");
            ErrorMessage = "Failed to open browser. Please visit https://oldportal.com/downloads";
        }
    }

    /// <summary>
    /// Resets the onboarding to the beginning.
    /// </summary>
    [RelayCommand]
    private void Reset()
    {
        _logger.Information("Resetting onboarding to beginning");

        CurrentStep = OnboardingStep.Welcome;
        IsAcClientInstalled = false;
        IsPatchApplied = false;
        IsDecalInstalled = false;
        IsOnboardingComplete = false;
        AcClientPath = null;
        ErrorMessage = null;
        StatusMessage = string.Empty;
        DownloadBytesDownloaded = 0;
        DownloadTotalBytes = 0;
    }

    /// <summary>
    /// Event fired when onboarding is completed successfully.
    /// </summary>
    public event Action? OnOnboardingCompleted;

    /// <summary>
    /// Event fired when user skips onboarding.
    /// </summary>
    public event Action? OnOnboardingSkipped;
}
