using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.Models;
using OPLauncher.Services;
using OPLauncher.Views;

namespace OPLauncher.ViewModels;

/// <summary>
/// Main window ViewModel responsible for navigation and application-level state.
/// Manages navigation between different views without requiring launcher-level authentication.
///
/// AUTHENTICATION ARCHITECTURE DECISION:
/// This launcher does NOT implement its own authentication system. Previous versions included
/// LoginViewModel, MfaViewModel, and AuthService for launcher-level authentication against
/// the OldPortal API. This approach was removed for the following reasons:
///
/// 1. REDUNDANT AUTHENTICATION: Players already authenticate at the game server level using
///    their AC account credentials. Adding launcher authentication created a confusing double
///    authentication flow (launcher login + per-character game credentials).
///
/// 2. SIMPLIFIED USER EXPERIENCE: The launcher now provides direct access to world browsing
///    and server connection without requiring users to create/manage separate launcher accounts.
///
/// 3. OFFLINE CAPABILITY: Users can browse cached world lists and launch games even when
///    OldPortal API is unreachable. Previous auth system blocked all functionality when offline.
///
/// 4. CREDENTIAL MANAGEMENT PATTERN: Instead of launcher-level authentication, credentials are
///    managed inline within WorldDetailView and ManualServerDetailView. This associates credentials
///    directly with specific servers/characters, which matches player mental models better.
///
/// REMOVED COMPONENTS (as of v1.0.18):
/// - AuthService.cs (21KB) - API authentication service
/// - LoginViewModel.cs + LoginView - Launcher login screen
/// - MfaViewModel.cs + MfaView - Multi-factor authentication screen
/// - CredentialPickerDialog/CredentialEditDialog - Separate credential dialogs
///
/// CURRENT FLOW:
/// App Launch -> WorldsBrowseView (no login required) -> Select World -> WorldDetailView ->
/// Manage credentials inline -> Launch game with selected credentials
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ConfigService _configService;
    private readonly WorldsService _worldsService;
    private readonly UpdateService _updateService;
    private readonly CredentialVaultService _credentialVaultService;
    private readonly GameLaunchService _gameLaunchService;
    private readonly ManualServersService _manualServersService;
    private readonly IFileDialogService _fileDialogService;
    private readonly ThemeManager _themeManager;
    private readonly IViewModelFactory _viewModelFactory;
    private readonly LoggingService _logger;
    private bool _disposed;
    private bool _isOfflineBannerDismissed;

    /// <summary>
    /// The currently active view being displayed in the content area.
    /// </summary>
    [ObservableProperty]
    private Control? _currentView;

    /// <summary>
    /// The title displayed in the window title bar.
    /// </summary>
    [ObservableProperty]
    private string _windowTitle = "Old Portal Launcher";

    /// <summary>
    /// Whether the launcher is currently in offline mode.
    /// </summary>
    [ObservableProperty]
    private bool _isOfflineMode;

    /// <summary>
    /// The connection status text to display in the navigation bar.
    /// </summary>
    [ObservableProperty]
    private string _connectionStatusText = "Online";

    /// <summary>
    /// Initializes a new instance of the MainWindowViewModel.
    /// </summary>
    /// <param name="configService">The configuration service.</param>
    /// <param name="worldsService">The worlds service.</param>
    /// <param name="updateService">The update service.</param>
    /// <param name="credentialVaultService">The credential vault service.</param>
    /// <param name="gameLaunchService">The game launch service.</param>
    /// <param name="manualServersService">The manual servers service.</param>
    /// <param name="fileDialogService">The file dialog service.</param>
    /// <param name="themeManager">The theme manager service.</param>
    /// <param name="viewModelFactory">The ViewModel factory for creating child ViewModels.</param>
    /// <param name="logger">The logging service.</param>
    public MainWindowViewModel(
        ConfigService configService,
        WorldsService worldsService,
        UpdateService updateService,
        CredentialVaultService credentialVaultService,
        GameLaunchService gameLaunchService,
        ManualServersService manualServersService,
        IFileDialogService fileDialogService,
        ThemeManager themeManager,
        IViewModelFactory viewModelFactory,
        LoggingService logger)
    {
        _configService = configService;
        _worldsService = worldsService;
        _updateService = updateService;
        _credentialVaultService = credentialVaultService;
        _gameLaunchService = gameLaunchService;
        _manualServersService = manualServersService;
        _fileDialogService = fileDialogService;
        _themeManager = themeManager;
        _viewModelFactory = viewModelFactory;
        _logger = logger;

        _logger.Debug("MainWindowViewModel initialized. No launcher authentication - navigating directly to worlds view.");

        // Navigate directly to worlds browse view on startup (no login screen)
        // See class-level documentation for authentication architecture decision
        NavigateToWorlds();
    }

    /// <summary>
    /// Navigates to the worlds browse view.
    /// </summary>
    [RelayCommand]
    private void NavigateToWorlds()
    {
        _logger.Debug("Navigating to Worlds view");
        var viewModel = _viewModelFactory.CreateWorldsBrowseViewModel(this);
        CurrentView = new WorldsBrowseView { DataContext = viewModel };
    }

    /// <summary>
    /// Navigates to the Browse Worlds page (Favorites section visible if you have favorites).
    /// </summary>
    [RelayCommand]
    private void NavigateToFavorites()
    {
        _logger.Debug("Navigating to Browse Worlds (Favorites section)");
        var viewModel = _viewModelFactory.CreateWorldsBrowseViewModel(this);
        CurrentView = new WorldsBrowseView { DataContext = viewModel };
        // Favorites section will be visible on the Browse Worlds page
    }

    /// <summary>
    /// Navigates to the Browse Worlds page (Recent section visible if you've played servers).
    /// </summary>
    [RelayCommand]
    private void NavigateToRecent()
    {
        _logger.Debug("Navigating to Browse Worlds (Recent section)");
        var viewModel = _viewModelFactory.CreateWorldsBrowseViewModel(this);
        CurrentView = new WorldsBrowseView { DataContext = viewModel };
        // Recent section will be visible on the Browse Worlds page
    }

    /// <summary>
    /// Navigates to the settings view.
    /// </summary>
    [RelayCommand]
    private void NavigateToSettings()
    {
        _logger.Debug("Navigating to Settings view");
        var viewModel = _viewModelFactory.CreateSettingsViewModel(this);
        CurrentView = new SettingsView { DataContext = viewModel };
    }

    /// <summary>
    /// Navigates to the manual servers view.
    /// </summary>
    [RelayCommand]
    private void NavigateToManualServers()
    {
        _logger.Debug("Navigating to Manual Servers view");
        var viewModel = _viewModelFactory.CreateManualServersViewModel(this);
        CurrentView = new ManualServersView { DataContext = viewModel };
    }

    /// <summary>
    /// Opens the OldPortal website in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenWebsite()
    {
        try
        {
            _logger.Information("User opening OldPortal website");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://oldportal.com",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening website");
        }
    }

    #region Deep Link Handling

    /// <summary>
    /// Handles a deep link by navigating to the specified world.
    /// Deep links use the format: oldportal://launch/{serverId}
    /// DEPRECATED: Use MainShellViewModel.HandleDeepLink instead (new navigation architecture).
    /// </summary>
    /// <param name="deepLink">The deep link information.</param>
    public void HandleDeepLink(DeepLinkInfo deepLink)
    {
        _logger.Information("Handling deep link: {Uri} -> Server {ServerId}", deepLink.OriginalUri, deepLink.ServerId);
        _logger.Warning("MainWindowViewModel.HandleDeepLink is deprecated. Use MainShellViewModel.HandleDeepLink instead.");

        // This method is part of the old navigation architecture
        // Deep link handling is now done in MainShellViewModel
    }

    /// <summary>
    /// Navigates to the world detail view for the specified world ID.
    /// NOTE: This method is part of the old navigation system.
    /// New code should use MainShellViewModel.HandleDeepLink() instead.
    /// </summary>
    /// <param name="worldId">The world ID to display.</param>
    private void NavigateToWorldDetail(int worldId)
    {
        _logger.Debug("Navigating to World Detail view for world {WorldId}", worldId);
        _logger.Warning("MainWindowViewModel.NavigateToWorldDetail is deprecated. Use MainShellViewModel.HandleDeepLink instead.");
        // This method is part of the old navigation architecture
        // For migration to new system, see MainShellViewModel
    }

    #endregion

    #region Offline Status Management

    /// <summary>
    /// Updates the offline mode status indicator.
    /// Called by child views to notify of connection status changes.
    /// </summary>
    /// <param name="isOffline">Whether the application is in offline mode.</param>
    public void UpdateOfflineStatus(bool isOffline)
    {
        IsOfflineMode = isOffline;
        ConnectionStatusText = isOffline ? "Offline" : "Online";
        _logger.Debug("Connection status updated: {Status}", ConnectionStatusText);
    }

    /// <summary>
    /// Gets whether the offline banner has been dismissed during this session.
    /// </summary>
    /// <returns>True if dismissed, false otherwise.</returns>
    public bool GetOfflineBannerDismissed()
    {
        return _isOfflineBannerDismissed;
    }

    /// <summary>
    /// Sets the offline banner dismissal state for this session.
    /// </summary>
    /// <param name="isDismissed">Whether the banner has been dismissed.</param>
    public void SetOfflineBannerDismissed(bool isDismissed)
    {
        _isOfflineBannerDismissed = isDismissed;
        _logger.Debug("Offline banner dismissal state updated: {IsDismissed}", isDismissed);
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Disposes resources used by the MainWindowViewModel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        // Dispose current view if it implements IDisposable
        if (CurrentView is IDisposable disposableView)
        {
            disposableView.Dispose();
        }

        _disposed = true;
        _logger.Debug("MainWindowViewModel disposed");
    }

    #endregion
}
