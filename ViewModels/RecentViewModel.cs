// TODO: [LAUNCH-129] Phase 3 Week 6 - RecentViewModel
// Component: Launcher
// Module: UI Redesign - Server Details & Favorites
// Description: ViewModel for Recent servers view displaying recently played servers

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.Models;
using OPLauncher.DTOs;
using OPLauncher.Services;

namespace OPLauncher.ViewModels;

/// <summary>
/// ViewModel for the Recent servers view.
/// Displays recently played servers (both API and manual servers) in a card grid,
/// ordered by most recently played first.
/// </summary>
public partial class RecentViewModel : ViewModelBase
{
    private readonly RecentServersService _recentServersService;
    private readonly FavoritesService _favoritesService;
    private readonly WorldsService _worldsService;
    private readonly ManualServersService _manualServersService;
    private readonly INavigationService _navigationService;
    private readonly GameLaunchService _gameLaunchService;
    private readonly CredentialVaultService _credentialVaultService;
    private readonly ConfigService _configService;
    private readonly LoggingService _logger;
    private readonly MainWindowViewModel _mainWindow;

    /// <summary>
    /// Collection of recent server cards for display.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ServerCardViewModel> _recentServerCards = new();

    /// <summary>
    /// Whether recent servers are currently being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Message to display when there are no recent servers.
    /// </summary>
    [ObservableProperty]
    private string _emptyMessage = "No recently played servers yet. Launch a server to see it appear here.";

    /// <summary>
    /// Whether to show the empty state message.
    /// </summary>
    [ObservableProperty]
    private bool _showEmptyState;

    public RecentViewModel(
        RecentServersService recentServersService,
        FavoritesService favoritesService,
        WorldsService worldsService,
        ManualServersService manualServersService,
        INavigationService navigationService,
        GameLaunchService gameLaunchService,
        CredentialVaultService credentialVaultService,
        ConfigService configService,
        LoggingService logger,
        MainWindowViewModel mainWindow)
    {
        _recentServersService = recentServersService;
        _favoritesService = favoritesService;
        _worldsService = worldsService;
        _manualServersService = manualServersService;
        _navigationService = navigationService;
        _gameLaunchService = gameLaunchService;
        _credentialVaultService = credentialVaultService;
        _configService = configService;
        _logger = logger;
        _mainWindow = mainWindow;
    }

    /// <summary>
    /// Loads all recent servers and creates card view models.
    /// </summary>
    [RelayCommand]
    private async Task LoadRecentServersAsync()
    {
        IsLoading = true;
        ShowEmptyState = false;

        try
        {
            _logger.Information("Loading recent servers");

            // Get recent servers from database (ordered by LastPlayedAt DESC)
            var recentServers = _recentServersService.GetRecentServers();

            _logger.Debug("Found {Count} recent servers", recentServers.Count);

            // Clear existing cards
            RecentServerCards.Clear();

            if (recentServers.Count == 0)
            {
                ShowEmptyState = true;
                _logger.Information("No recent servers to display");
                return;
            }

            // Load all worlds and manual servers for efficient lookups
            var allWorlds = await _worldsService.GetAllWorldsAsync();
            var allManualServers = await _manualServersService.GetAllServersAsync();

            // Load server data for each recent entry
            foreach (var recent in recentServers)
            {
                try
                {
                    ServerCardViewModel? card = null;

                    if (recent.IsManualServer && recent.ManualServerId.HasValue)
                    {
                        // Find manual server
                        var manualServer = allManualServers.FirstOrDefault(s => s.Id == recent.ManualServerId.Value);
                        if (manualServer != null)
                        {
                            card = new ServerCardViewModel(manualServer, _navigationService, _favoritesService, _logger);
                        }
                        else
                        {
                            _logger.Warning("Manual server {ServerId} not found in recent servers", recent.ManualServerId);
                        }
                    }
                    else if (!recent.IsManualServer && recent.WorldServerId.HasValue)
                    {
                        // Find world server by ServerId (Guid)
                        var worldServer = allWorlds.FirstOrDefault(w => w.ServerId == recent.WorldServerId.Value);
                        if (worldServer != null)
                        {
                            card = new ServerCardViewModel(worldServer, _navigationService, _favoritesService, _logger);
                        }
                        else
                        {
                            _logger.Warning("World server {ServerId} not found in recent servers", recent.WorldServerId);
                        }
                    }

                    if (card != null)
                    {
                        RecentServerCards.Add(card);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error loading recent server {ServerName}", recent.ServerName);
                }
            }

            _logger.Information("Loaded {Count} recent server cards", RecentServerCards.Count);

            ShowEmptyState = RecentServerCards.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading recent servers");
            ShowEmptyState = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Refreshes the recent servers list.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadRecentServersAsync();
    }

    /// <summary>
    /// Clears all recent server history after user confirmation.
    /// </summary>
    [RelayCommand]
    private void ClearHistory()
    {
        try
        {
            _logger.Information("User clearing recent server history");

            _recentServersService.ClearRecentServers();
            RecentServerCards.Clear();
            ShowEmptyState = true;

            _logger.Information("Recent server history cleared");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error clearing recent server history");
        }
    }

    /// <summary>
    /// Called when the view is activated.
    /// </summary>
    public async Task OnActivatedAsync()
    {
        await LoadRecentServersAsync();
    }
}
