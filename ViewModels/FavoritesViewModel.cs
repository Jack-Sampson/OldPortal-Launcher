// TODO: [LAUNCH-127] Phase 3 Week 6 - FavoritesViewModel
// Component: Launcher
// Module: UI Redesign - Server Details & Favorites
// Description: ViewModel for Favorites view displaying favorited servers

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
/// ViewModel for the Favorites view.
/// Displays all favorited servers (both API and manual servers) in a card grid.
/// </summary>
public partial class FavoritesViewModel : ViewModelBase
{
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
    /// Collection of favorite server cards for display.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ServerCardViewModel> _favoriteServerCards = new();

    /// <summary>
    /// Whether favorites are currently being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Message to display when there are no favorites.
    /// </summary>
    [ObservableProperty]
    private string _emptyMessage = "No favorite servers yet. Star servers from the Browse or Manual Servers views to add them here.";

    /// <summary>
    /// Whether to show the empty state message.
    /// </summary>
    [ObservableProperty]
    private bool _showEmptyState;

    public FavoritesViewModel(
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
    /// Loads all favorite servers and creates card view models.
    /// </summary>
    [RelayCommand]
    private async Task LoadFavoritesAsync()
    {
        IsLoading = true;
        ShowEmptyState = false;

        try
        {
            _logger.Information("Loading favorite servers");

            // Get all favorites from database
            var favorites = _favoritesService.GetAllFavorites();

            _logger.Debug("Found {Count} favorite servers", favorites.Count);

            // Clear existing cards
            FavoriteServerCards.Clear();

            if (favorites.Count == 0)
            {
                ShowEmptyState = true;
                _logger.Information("No favorites to display");
                return;
            }

            // Load all worlds and manual servers for efficient lookups
            var allWorlds = await _worldsService.GetAllWorldsAsync();
            var allManualServers = await _manualServersService.GetAllServersAsync();

            // Load server data for each favorite
            foreach (var favorite in favorites)
            {
                try
                {
                    ServerCardViewModel? card = null;

                    if (favorite.IsManualServer && favorite.ManualServerId.HasValue)
                    {
                        // Find manual server
                        var manualServer = allManualServers.FirstOrDefault(s => s.Id == favorite.ManualServerId.Value);
                        if (manualServer != null)
                        {
                            card = new ServerCardViewModel(manualServer, _navigationService, _favoritesService, _logger);
                        }
                        else
                        {
                            _logger.Warning("Manual server {ServerId} not found, removing from favorites", favorite.ManualServerId);
                            _favoritesService.RemoveFavorite(null, favorite.ManualServerId);
                        }
                    }
                    else if (!favorite.IsManualServer && favorite.WorldServerId.HasValue)
                    {
                        // Find world server by ServerId (Guid)
                        var worldServer = allWorlds.FirstOrDefault(w => w.ServerId == favorite.WorldServerId.Value);
                        if (worldServer != null)
                        {
                            card = new ServerCardViewModel(worldServer, _navigationService, _favoritesService, _logger);
                        }
                        else
                        {
                            _logger.Warning("World server {ServerId} not found, removing from favorites", favorite.WorldServerId);
                            _favoritesService.RemoveFavorite(favorite.WorldServerId, null);
                        }
                    }

                    if (card != null)
                    {
                        FavoriteServerCards.Add(card);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error loading favorite server {ServerName}", favorite.ServerName);
                }
            }

            _logger.Information("Loaded {Count} favorite server cards", FavoriteServerCards.Count);

            ShowEmptyState = FavoriteServerCards.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading favorites");
            ShowEmptyState = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Refreshes the favorites list.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadFavoritesAsync();
    }

    /// <summary>
    /// Called when the view is activated.
    /// </summary>
    public async Task OnActivatedAsync()
    {
        await LoadFavoritesAsync();
    }
}
