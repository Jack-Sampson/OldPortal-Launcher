// TODO: [LAUNCH-101] Phase 1 Week 2 - MainShellViewModel
// Component: Launcher
// Module: UI Redesign - Navigation Architecture
// Description: Main shell view model with sidebar navigation and content area

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.Models;
using OPLauncher.Services;

namespace OPLauncher.ViewModels;

/// <summary>
/// View model for the main application shell with sidebar navigation.
/// Manages navigation between different views and displays the current view in the content area.
/// </summary>
public partial class MainShellViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly ConfigService _configService;
    private readonly UpdateService _updateService;
    private readonly GameLaunchService _gameLaunchService;
    private readonly WorldsService _worldsService;
    private readonly LoggingService _logger;

    /// <summary>
    /// The currently displayed content view model.
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? _currentContent;

    /// <summary>
    /// The currently active navigation item (for visual feedback).
    /// </summary>
    [ObservableProperty]
    private string _activeNavigationItem = "Home";

    /// <summary>
    /// Whether the sidebar can navigate back.
    /// </summary>
    [ObservableProperty]
    private bool _canGoBack;

    /// <summary>
    /// The onboarding view model (shown on first run).
    /// </summary>
    [ObservableProperty]
    private OnboardingViewModel? _onboardingViewModel;

    /// <summary>
    /// Whether to show the onboarding modal overlay.
    /// </summary>
    [ObservableProperty]
    private bool _showOnboarding;

    /// <summary>
    /// Whether to show the update prompt dialog.
    /// </summary>
    [ObservableProperty]
    private bool _showUpdatePrompt;

    /// <summary>
    /// The update information for the available update.
    /// </summary>
    [ObservableProperty]
    private LauncherUpdateInfo? _availableUpdate;

    /// <summary>
    /// Whether a game is currently being launched (shows loading overlay).
    /// </summary>
    [ObservableProperty]
    private bool _isLaunchingGame;

    /// <summary>
    /// The message to display in the loading overlay (e.g., "Launching Frostfell...").
    /// </summary>
    [ObservableProperty]
    private string _launchingGameMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the MainShellViewModel.
    /// </summary>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="configService">The configuration service.</param>
    /// <param name="updateService">The update service.</param>
    /// <param name="gameLaunchService">The game launch service.</param>
    /// <param name="worldsService">The worlds service for prefetching data.</param>
    /// <param name="onboardingViewModel">The onboarding view model.</param>
    /// <param name="logger">The logging service.</param>
    public MainShellViewModel(
        INavigationService navigationService,
        ConfigService configService,
        UpdateService updateService,
        GameLaunchService gameLaunchService,
        WorldsService worldsService,
        OnboardingViewModel onboardingViewModel,
        LoggingService logger)
    {
        _navigationService = navigationService;
        _configService = configService;
        _updateService = updateService;
        _gameLaunchService = gameLaunchService;
        _worldsService = worldsService;
        _logger = logger;

        // Subscribe to navigation events
        _navigationService.Navigated += OnNavigated;

        // Subscribe to update events
        _updateService.UpdateCheckCompleted += OnUpdateCheckCompleted;

        // Subscribe to game launch events for loading overlay
        _gameLaunchService.LaunchProgress += OnGameLaunchProgress;

        _logger.Information("MainShellViewModel initialized");

        // Check if onboarding should be shown
        if (!_configService.Current.IsOnboardingComplete)
        {
            _logger.Information("Onboarding not complete, showing onboarding modal");
            OnboardingViewModel = onboardingViewModel;
            ShowOnboarding = true;

            // Subscribe to onboarding events
            OnboardingViewModel.OnOnboardingCompleted += OnOnboardingCompleted;
            OnboardingViewModel.OnOnboardingSkipped += OnOnboardingSkipped;
        }
        else
        {
            _logger.Information("Onboarding already complete, skipping");
            ShowOnboarding = false;
        }

        // Navigate to Home view by default
        NavigateToHomeCommand.Execute(null);

        // Prefetch common data in the background for faster navigation
        _ = PrefetchCommonDataAsync();

        // Check for updates in the background (after a delay to avoid startup slowdown)
        _ = _updateService.CheckForUpdatesInBackgroundAsync();
    }

    /// <summary>
    /// Handles the onboarding completed event.
    /// Hides the onboarding modal and allows user to proceed to main app.
    /// </summary>
    private void OnOnboardingCompleted()
    {
        _logger.Information("Onboarding completed, hiding modal");
        ShowOnboarding = false;

        // Unsubscribe from events
        if (OnboardingViewModel != null)
        {
            OnboardingViewModel.OnOnboardingCompleted -= OnOnboardingCompleted;
            OnboardingViewModel.OnOnboardingSkipped -= OnOnboardingSkipped;
        }
    }

    /// <summary>
    /// Handles the onboarding skipped event.
    /// Hides the onboarding modal and allows user to proceed to main app.
    /// </summary>
    private void OnOnboardingSkipped()
    {
        _logger.Information("Onboarding skipped, hiding modal");
        ShowOnboarding = false;

        // Unsubscribe from events
        if (OnboardingViewModel != null)
        {
            OnboardingViewModel.OnOnboardingCompleted -= OnOnboardingCompleted;
            OnboardingViewModel.OnOnboardingSkipped -= OnOnboardingSkipped;
        }
    }

    /// <summary>
    /// Handles navigation events from the navigation service.
    /// </summary>
    private void OnNavigated(object? sender, ViewModelBase viewModel)
    {
        CurrentContent = viewModel;
        CanGoBack = _navigationService.CanGoBack;
        _logger.Debug("Current content set to {ViewModelType}", viewModel.GetType().Name);
    }

    /// <summary>
    /// Navigates to the Home view.
    /// </summary>
    [RelayCommand]
    private void NavigateToHome()
    {
        _logger.Information("Navigate to Home requested");
        ActiveNavigationItem = "Home";
        _navigationService.NavigateTo<HomeViewModel>();
    }

    /// <summary>
    /// Navigates to the News view.
    /// </summary>
    [RelayCommand]
    private void NavigateToNews()
    {
        _logger.Information("Navigate to News requested");
        ActiveNavigationItem = "News";
        _navigationService.NavigateTo<NewsViewModel>();
    }

    /// <summary>
    /// Navigates to the Browse Worlds view.
    /// </summary>
    [RelayCommand]
    private void NavigateToBrowse()
    {
        _logger.Information("Navigate to Browse requested");
        ActiveNavigationItem = "Browse";
        _navigationService.NavigateTo<WorldsBrowseViewModel>();
    }

    /// <summary>
    /// Navigates to the Manual Servers (My Servers) view.
    /// </summary>
    [RelayCommand]
    private void NavigateToManualServers()
    {
        _logger.Information("Navigate to Manual Servers requested");
        ActiveNavigationItem = "My Servers";
        _navigationService.NavigateTo<ManualServersViewModel>();
    }

    /// <summary>
    /// Navigates to the Help view.
    /// </summary>
    [RelayCommand]
    private void NavigateToHelp()
    {
        _logger.Information("Navigate to Help requested");
        ActiveNavigationItem = "Help";
        _navigationService.NavigateTo<GeneralHelpViewModel>();
    }

    /// <summary>
    /// Navigates to the Settings view.
    /// </summary>
    [RelayCommand]
    private void NavigateToSettings()
    {
        _logger.Information("Navigate to Settings requested");
        ActiveNavigationItem = "Settings";
        _navigationService.NavigateTo<SettingsViewModel>();
    }

    /// <summary>
    /// Navigates back to the previous view.
    /// </summary>
    [RelayCommand]
    private void GoBack()
    {
        _logger.Information("Go back requested");
        _navigationService.GoBack();
    }

    /// <summary>
    /// Handles deep link navigation (e.g., oldportal://launch/{serverId}).
    /// </summary>
    /// <param name="deepLink">The deep link information.</param>
    public async Task HandleDeepLink(Models.DeepLinkInfo deepLink)
    {
        _logger.Information("Handling deep link for server {ServerId}", deepLink.ServerId);

        try
        {
            // Fetch world from API/cache using the Guid
            var world = await _worldsService.GetWorldByIdAsync(deepLink.ServerId);

            if (world == null)
            {
                _logger.Warning("Server {ServerId} not found via deep link", deepLink.ServerId);
                // TODO: Show error message to user (could add a property for error dialogs)
                return;
            }

            // Navigate to WorldDetailView with the WorldDto object
            ActiveNavigationItem = "Browse";
            _navigationService.NavigateTo<WorldDetailViewModel>(world);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to handle deep link for server {ServerId}", deepLink.ServerId);
        }
    }

    /// <summary>
    /// Handles update check completed event from UpdateService.
    /// Shows update prompt if an update is available.
    /// </summary>
    private void OnUpdateCheckCompleted(object? sender, LauncherUpdateInfo updateInfo)
    {
        _logger.Debug("Update check completed event received in MainShellViewModel");

        if (updateInfo.IsUpdateAvailable)
        {
            _logger.Information("Update available: {LatestVersion} (current: {CurrentVersion})",
                updateInfo.LatestVersion, updateInfo.CurrentVersion);

            // Show update prompt on UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AvailableUpdate = updateInfo;
                ShowUpdatePrompt = true;
            });
        }
        else
        {
            _logger.Debug("No update available, not showing update prompt");
        }
    }

    /// <summary>
    /// Starts the update download and installation process.
    /// </summary>
    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (AvailableUpdate == null)
        {
            _logger.Warning("InstallUpdate called but no update available");
            return;
        }

        try
        {
            _logger.Information("User requested to install update to version {Version}", AvailableUpdate.LatestVersion);

            // Open download URL in browser (manual installation from oldportal.com/downloads)
            if (!string.IsNullOrEmpty(AvailableUpdate.DownloadUrl))
            {
                _logger.Information("Opening download URL: {Url}", AvailableUpdate.DownloadUrl);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = AvailableUpdate.DownloadUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);

                _logger.Information("Download page opened - user will manually install update");
            }
            else
            {
                _logger.Warning("No download URL available for update");
            }

            // Hide the prompt after opening the download page
            ShowUpdatePrompt = false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening download URL");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Dismisses the update prompt.
    /// </summary>
    [RelayCommand]
    private void SkipUpdate()
    {
        _logger.Information("User skipped update to version {Version}", AvailableUpdate?.LatestVersion);
        ShowUpdatePrompt = false;
        AvailableUpdate = null;
    }

    /// <summary>
    /// Handles game launch progress events from GameLaunchService.
    /// Shows/hides the loading overlay based on launch progress.
    /// </summary>
    private void OnGameLaunchProgress(object? sender, LaunchProgressInfo progressInfo)
    {
        // Don't show overlay during multi-client launches (MultiLaunchDialog has its own progress UI)
        if (_gameLaunchService.SuppressProgressEvents)
            return;

        // Update on UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Double-check suppression flag inside UI thread action (in case it changed while queued)
            if (_gameLaunchService.SuppressProgressEvents)
                return;

            if (progressInfo.ElapsedSeconds == 0)
            {
                // Launch just started - show overlay
                IsLaunchingGame = true;
                LaunchingGameMessage = $"Launching game...";
                _logger.Debug("Showing launch overlay");
            }
            else if (progressInfo.StatusMessage.Contains("completed", StringComparison.OrdinalIgnoreCase))
            {
                // Launch completed successfully - hide overlay
                IsLaunchingGame = false;
                LaunchingGameMessage = string.Empty;
                _logger.Debug("Hiding launch overlay (launch completed successfully)");
            }
            else if (progressInfo.ElapsedSeconds >= progressInfo.TimeoutSeconds)
            {
                // Launch timeout reached - hide overlay
                IsLaunchingGame = false;
                LaunchingGameMessage = string.Empty;
                _logger.Debug("Hiding launch overlay (timeout)");
            }
        });
    }

    /// <summary>
    /// Prefetches common data in the background to improve perceived performance.
    /// Loads worlds/servers data and caches images that are likely to be viewed.
    /// Runs asynchronously without blocking UI.
    /// </summary>
    private Task PrefetchCommonDataAsync()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.Information("[Prefetch] Starting background data prefetch");

                // Prefetch featured worlds (will be shown on Home screen)
                _logger.Debug("[Prefetch] Loading featured worlds");
                var featuredWorlds = await _worldsService.GetFeaturedWorldsAsync();
                _logger.Information("[Prefetch] Loaded {Count} featured worlds", featuredWorlds.Count);

                // Prefetch all worlds (for Browse Worlds screen)
                _logger.Debug("[Prefetch] Loading all worlds");
                var allWorlds = await _worldsService.GetAllWorldsAsync();
                _logger.Information("[Prefetch] Loaded {Count} total worlds", allWorlds.Count);

                _logger.Information("[Prefetch] Background data prefetch completed successfully");
            }
            catch (Exception ex)
            {
                // Silent fail - prefetch is optional, don't disrupt user experience
                _logger.Warning(ex, "[Prefetch] Background data prefetch failed (non-critical)");
            }
        });

        return Task.CompletedTask;
    }
}
