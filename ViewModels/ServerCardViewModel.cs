// TODO: [LAUNCH-106] Phase 1 Week 3 - ServerCardViewModel
// Component: Launcher
// Module: UI Redesign - Card Grid Layout
// Description: Wrapper ViewModel for displaying WorldDto or ManualServer in card format

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.DTOs;
using OPLauncher.Models;
using OPLauncher.Services;
using OPLauncher.Utilities;

namespace OPLauncher.ViewModels;

/// <summary>
/// Event args for server deletion request.
/// </summary>
public class ServerDeleteRequestedEventArgs : EventArgs
{
    public ManualServer Server { get; }

    public ServerDeleteRequestedEventArgs(ManualServer server)
    {
        Server = server;
    }
}

/// <summary>
/// View model for a server card in the card grid layout.
/// Wraps either a WorldDto or ManualServer and provides a unified interface.
/// </summary>
public partial class ServerCardViewModel : ViewModelBase
{
    private readonly WorldDto? _worldServer;
    private readonly ManualServer? _manualServer;
    private readonly INavigationService _navigationService;
    private readonly FavoritesService _favoritesService;
    private readonly LoggingService _logger;

    /// <summary>
    /// Initializes a new instance for a WorldDto server.
    /// </summary>
    public ServerCardViewModel(
        WorldDto worldServer,
        INavigationService navigationService,
        FavoritesService favoritesService,
        LoggingService logger)
    {
        _worldServer = worldServer;
        _navigationService = navigationService;
        _favoritesService = favoritesService;
        _logger = logger;
        IsManualServer = false;

        // Initialize favorite status from database
        _isFavorite = _favoritesService.IsFavorite(worldServer.ServerId, null);
    }

    /// <summary>
    /// Initializes a new instance for a ManualServer.
    /// </summary>
    public ServerCardViewModel(
        ManualServer manualServer,
        INavigationService navigationService,
        FavoritesService favoritesService,
        LoggingService logger)
    {
        _manualServer = manualServer;
        _navigationService = navigationService;
        _favoritesService = favoritesService;
        _logger = logger;
        IsManualServer = true;

        // Initialize favorite status from database
        _isFavorite = _favoritesService.IsFavorite(null, manualServer.Id);

        // Subscribe to ManualServer property changes to update UI when status changes
        manualServer.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ManualServer.IsOnline))
            {
                OnPropertyChanged(nameof(IsOnline));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(PlayerCountText));
            }
            else if (e.PropertyName == nameof(ManualServer.PlayerCount))
            {
                OnPropertyChanged(nameof(PlayerCount));
                OnPropertyChanged(nameof(PlayerCountText));
            }
        };
    }

    /// <summary>
    /// Whether this is a manual server (vs a WorldDto from API).
    /// </summary>
    public bool IsManualServer { get; }

    /// <summary>
    /// Gets the underlying ManualServer (null if this is a WorldDto).
    /// </summary>
    public ManualServer? ManualServer => _manualServer;

    /// <summary>
    /// Gets the underlying WorldDto (null if this is a ManualServer).
    /// </summary>
    public WorldDto? WorldServer => _worldServer;

    /// <summary>
    /// Server display name.
    /// </summary>
    public string Name => _worldServer?.Name ?? _manualServer?.Name ?? "Unknown Server";

    /// <summary>
    /// Server description (nullable).
    /// </summary>
    public string? Description => _worldServer?.Description ?? _manualServer?.Description;

    /// <summary>
    /// Whether the server is currently online.
    /// </summary>
    public bool IsOnline => _worldServer?.IsOnline ?? _manualServer?.IsOnline ?? false;

    /// <summary>
    /// Current player count.
    /// </summary>
    public int PlayerCount => _worldServer?.PlayerCount ?? _manualServer?.PlayerCount ?? 0;

    /// <summary>
    /// Whether the server is verified (WorldDto only).
    /// </summary>
    public bool IsVerified => _worldServer?.IsVerified ?? false;

    /// <summary>
    /// Whether the server has sponsor badge (WorldDto only).
    /// </summary>
    public bool IsSponsor => _worldServer?.IsSponsor ?? false;

    /// <summary>
    /// Server ruleset (PvE, PvP, RP, Retail, Custom).
    /// </summary>
    public RuleSet RuleSet => _worldServer?.EffectiveRuleSet ?? DTOs.RuleSet.Custom;

    /// <summary>
    /// Server emulator type (ACE or GDLE).
    /// </summary>
    public ServerType ServerType => _worldServer?.ServerType ?? _manualServer?.ServerType ?? ServerType.ACE;

    /// <summary>
    /// Gets the banner image path for this server.
    /// Uses ImageUrlResolver for unified image resolution with ruleset-specific fallbacks.
    /// Custom server banners from API are used when available, otherwise falls back to ruleset-specific default images.
    /// </summary>
    public string BannerImagePath
    {
        get
        {
            // Determine fallback type based on server ruleset
            var fallbackType = RuleSet switch
            {
                DTOs.RuleSet.PvP => ImageFallbackType.ServerPvP,
                DTOs.RuleSet.PvE => ImageFallbackType.ServerPvE,
                DTOs.RuleSet.RP => ImageFallbackType.ServerRP,
                DTOs.RuleSet.Retail => ImageFallbackType.ServerRetail,
                DTOs.RuleSet.Hardcore => ImageFallbackType.ServerHardcore,
                DTOs.RuleSet.Custom => ImageFallbackType.ServerCustom,
                _ => ImageFallbackType.GenericServer
            };

            // Use unified image resolver (handles both API URLs and fallbacks)
            return ImageUrlResolver.ResolveImageUrl(_worldServer?.CardBannerImageUrl, fallbackType);
        }
    }

    /// <summary>
    /// Whether this server is favorited by the user.
    /// Backed by FavoritesService in LiteDB.
    /// </summary>
    [ObservableProperty]
    private bool _isFavorite;

    /// <summary>
    /// Status text for display (e.g., "Online", "Offline").
    /// </summary>
    public string StatusText => IsOnline ? "Online" : "Offline";

    /// <summary>
    /// Player count text for display (e.g., "45 players", "Offline").
    /// </summary>
    public string PlayerCountText
    {
        get
        {
            if (!IsOnline) return "Offline";
            if (PlayerCount == 1) return "1 player";
            return $"{PlayerCount} players";
        }
    }

    /// <summary>
    /// Ruleset display text (e.g., "PvP", "PvE").
    /// </summary>
    public string RuleSetText => RuleSet.ToString();

    /// <summary>
    /// Server type display text (e.g., "ACE", "GDLE").
    /// </summary>
    public string ServerTypeText => ServerType.ToString();

    /// <summary>
    /// Combined tags text for display (e.g., "PvP • ACE").
    /// </summary>
    public string TagsText => $"{RuleSetText} • {ServerTypeText}";

    /// <summary>
    /// Command to play/launch this server.
    /// </summary>
    [RelayCommand]
    private void Play()
    {
        _logger.Information("Play command invoked for server: {ServerName}", Name);

        // Navigate to the appropriate detail view
        if (_worldServer != null)
        {
            _navigationService.NavigateTo<WorldDetailViewModel>(_worldServer);
        }
        else if (_manualServer != null)
        {
            _navigationService.NavigateTo<ManualServerDetailViewModel>(_manualServer);
        }
    }

    /// <summary>
    /// Command to toggle favorite status.
    /// </summary>
    [RelayCommand]
    private void ToggleFavorite()
    {
        // Get server IDs
        Guid? worldServerId = _worldServer?.ServerId;
        int? manualServerId = _manualServer?.Id;

        // Toggle in database
        bool newStatus = _favoritesService.ToggleFavorite(
            worldServerId,
            manualServerId,
            Name,
            IsManualServer);

        // Update local property
        IsFavorite = newStatus;

        _logger.Information("Favorite toggled for server: {ServerName}, IsFavorite: {IsFavorite}", Name, IsFavorite);
    }

    /// <summary>
    /// Command to view server details (alternative to Play).
    /// </summary>
    [RelayCommand]
    private void ViewDetails()
    {
        _logger.Information("View details command invoked for server: {ServerName}", Name);
        Play(); // For now, same behavior as Play
    }

    /// <summary>
    /// Event raised when the user requests to delete this manual server.
    /// </summary>
    public event EventHandler<ServerDeleteRequestedEventArgs>? DeleteRequested;

    /// <summary>
    /// Command to request deletion of this manual server.
    /// Only available for manual servers.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsManualServer))]
    private void Delete()
    {
        if (_manualServer == null) return;

        _logger.Information("Delete command invoked for manual server: {ServerName}", Name);

        // Raise event for parent ViewModel to handle
        DeleteRequested?.Invoke(this, new ServerDeleteRequestedEventArgs(_manualServer));
    }
}
