using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.Services;
using OPLauncher.Utilities;
using OPLauncher.DTOs;

namespace OPLauncher.ViewModels;

/// <summary>
/// ViewModel for the worlds browse screen.
/// Displays featured worlds and allows searching/filtering of all available worlds.
/// </summary>
public partial class WorldsBrowseViewModel : ViewModelBase, IDisposable
{
    private readonly WorldsService _worldsService;
    private readonly ConfigService _configService;
    private readonly CredentialVaultService _credentialVaultService;
    private readonly GameLaunchService _gameLaunchService;
    private readonly INavigationService _navigationService;
    private readonly FavoritesService _favoritesService;
    private readonly RecentServersService _recentServersService;
    private readonly ServerMonitorService _serverMonitorService;
    private readonly LoggingService _logger;
    private readonly MainWindowViewModel _mainWindow;
    private System.Timers.Timer? _searchDebounceTimer;
    private System.Threading.Timer? _statusCheckTimer;
    private bool _disposed;
    private bool _suppressSearchDebounce;
    private System.Threading.CancellationTokenSource? _loadCancellationTokenSource;

    // Singleton lifetime state tracking
    private DateTime? _lastLoadTime;
    private bool _isInitialized;
    private const int StaleDataThresholdMinutes = 5;

    /// <summary>
    /// Collection of all worlds matching the current search/filter criteria.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<WorldDto> _worlds = new();

    /// <summary>
    /// Collection of server cards for display in card grid.
    /// Wraps WorldDto objects in ServerCardViewModel.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ServerCardViewModel> _serverCards = new();

    /// <summary>
    /// Collection of featured worlds displayed prominently.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<WorldDto> _featuredWorlds = new();

    /// <summary>
    /// Single featured world for the hero card (rotates daily).
    /// </summary>
    [ObservableProperty]
    private WorldDto? _featuredWorld;

    /// <summary>
    /// Whether the featured world is favorited.
    /// </summary>
    [ObservableProperty]
    private bool _isFeaturedWorldFavorite;

    /// <summary>
    /// Resolved banner URL for the featured world.
    /// Uses ImageUrlResolver to handle API-relative paths and provide ruleset-specific fallbacks.
    /// </summary>
    public string FeaturedWorldBannerUrl
    {
        get
        {
            if (FeaturedWorld == null)
                return ImageUrlResolver.ResolveImageUrl(null, ImageFallbackType.GenericServer);

            var fallbackType = FeaturedWorld.EffectiveRuleSet switch
            {
                DTOs.RuleSet.PvP => ImageFallbackType.ServerPvP,
                DTOs.RuleSet.PvE => ImageFallbackType.ServerPvE,
                DTOs.RuleSet.RP => ImageFallbackType.ServerRP,
                DTOs.RuleSet.Retail => ImageFallbackType.ServerRetail,
                DTOs.RuleSet.Hardcore => ImageFallbackType.ServerHardcore,
                DTOs.RuleSet.Custom => ImageFallbackType.ServerCustom,
                _ => ImageFallbackType.GenericServer
            };

            return ImageUrlResolver.ResolveImageUrl(FeaturedWorld.CardBannerImageUrl, fallbackType);
        }
    }

    /// <summary>
    /// Called when FeaturedWorld property changes.
    /// Notifies dependent computed properties.
    /// </summary>
    partial void OnFeaturedWorldChanged(WorldDto? value)
    {
        OnPropertyChanged(nameof(FeaturedWorldBannerUrl));
    }

    /// <summary>
    /// Collection of recent servers (last 3 played).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ServerCardViewModel> _recentServers = new();

    /// <summary>
    /// Collection of favorited servers.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ServerCardViewModel> _favoriteServers = new();

    /// <summary>
    /// Collection of all other worlds (excluding featured, recent, favorites).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ServerCardViewModel> _allOtherWorlds = new();

    /// <summary>
    /// The currently selected world in the list.
    /// </summary>
    [ObservableProperty]
    private WorldDto? _selectedWorld;

    /// <summary>
    /// The search text entered by the user.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// The selected ruleset filter (null = all rulesets).
    /// </summary>
    [ObservableProperty]
    private RuleSet? _selectedRuleSet;

    /// <summary>
    /// Whether to show only online servers (filters out offline servers).
    /// </summary>
    [ObservableProperty]
    private bool _showOnlineOnly;

    /// <summary>
    /// The selected sort option for the world list.
    /// </summary>
    [ObservableProperty]
    private WorldSortOption _selectedSortOption = WorldSortOption.Name;

    /// <summary>
    /// The selected sort direction (ascending or descending).
    /// </summary>
    [ObservableProperty]
    private SortDirection _selectedSortDirection = SortDirection.Ascending;

    /// <summary>
    /// The active view filter (all, favorites, or recent).
    /// </summary>
    [ObservableProperty]
    private string _activeViewFilter = "all";

    /// <summary>
    /// Whether worlds are currently being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Whether featured worlds are currently being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingFeatured;

    /// <summary>
    /// Error message to display if loading fails.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Whether the launcher is in offline mode (using cached data).
    /// </summary>
    [ObservableProperty]
    private bool _isOfflineMode;

    /// <summary>
    /// Whether the offline mode banner has been dismissed by the user.
    /// </summary>
    [ObservableProperty]
    private bool _isOfflineBannerDismissed;

    /// <summary>
    /// Whether the website promo banner has been permanently dismissed.
    /// </summary>
    [ObservableProperty]
    private bool _isWebsiteBannerDismissed;

    /// <summary>
    /// Status message to display (e.g., "Showing cached results").
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// The age of the cached data (e.g., "Last updated 2 hours ago").
    /// Only displayed when in offline mode.
    /// </summary>
    [ObservableProperty]
    private string? _cacheAgeText;

    /// <summary>
    /// Gets the "Last updated X ago" text for the refresh indicator.
    /// </summary>
    public string? LastUpdateText => _lastLoadTime.HasValue
        ? $"Updated {GetTimeAgoText(_lastLoadTime.Value)}"
        : null;

    /// <summary>
    /// Gets whether to show the last update text.
    /// </summary>
    public bool ShowLastUpdateText => _lastLoadTime.HasValue;

    /// <summary>
    /// Available sort options for the dropdown.
    /// </summary>
    public ObservableCollection<SortOptionItem> SortOptions { get; } = new()
    {
        new SortOptionItem(WorldSortOption.Name, "Name"),
        new SortOptionItem(WorldSortOption.PlayerCount, "Player Count"),
        new SortOptionItem(WorldSortOption.Uptime, "Uptime %"),
        new SortOptionItem(WorldSortOption.ServerType, "Server Type")
    };

    /// <summary>
    /// Available sort directions for the toggle button.
    /// </summary>
    public ObservableCollection<SortDirectionItem> SortDirections { get; } = new()
    {
        new SortDirectionItem(SortDirection.Ascending, "↑ Ascending"),
        new SortDirectionItem(SortDirection.Descending, "↓ Descending")
    };

    /// <summary>
    /// Available ruleset filters for the dropdown (null means "All").
    /// </summary>
    public ObservableCollection<RuleSetFilterOption> RuleSetFilters { get; } = new()
    {
        new RuleSetFilterOption(null, "All RuleSets"),
        new RuleSetFilterOption(RuleSet.PvE, "PvE"),
        new RuleSetFilterOption(RuleSet.PvP, "PvP"),
        new RuleSetFilterOption(RuleSet.RP, "Roleplay"),
        new RuleSetFilterOption(RuleSet.Custom, "Custom"),
        new RuleSetFilterOption(RuleSet.Retail, "Retail"),
        new RuleSetFilterOption(RuleSet.Hardcore, "Hardcore")
    };

    /// <summary>
    /// The selected ruleset filter option from the dropdown.
    /// </summary>
    [ObservableProperty]
    private RuleSetFilterOption? _selectedRuleSetFilter;

    /// <summary>
    /// Initializes a new instance of the WorldsBrowseViewModel.
    /// </summary>
    /// <param name="worldsService">The worlds service.</param>
    /// <param name="configService">The configuration service.</param>
    /// <param name="credentialVaultService">The credential vault service.</param>
    /// <param name="gameLaunchService">The game launch service.</param>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="favoritesService">The favorites service.</param>
    /// <param name="recentServersService">The recent servers service.</param>
    /// <param name="serverMonitorService">The server monitoring service for UDP status checks.</param>
    /// <param name="logger">The logging service.</param>
    /// <param name="mainWindow">The main window view model for navigation (legacy).</param>
    public WorldsBrowseViewModel(
        WorldsService worldsService,
        ConfigService configService,
        CredentialVaultService credentialVaultService,
        GameLaunchService gameLaunchService,
        INavigationService navigationService,
        FavoritesService favoritesService,
        RecentServersService recentServersService,
        ServerMonitorService serverMonitorService,
        LoggingService logger,
        MainWindowViewModel mainWindow)
    {
        _worldsService = worldsService;
        _configService = configService;
        _credentialVaultService = credentialVaultService;
        _gameLaunchService = gameLaunchService;
        _navigationService = navigationService;
        _favoritesService = favoritesService;
        _recentServersService = recentServersService;
        _serverMonitorService = serverMonitorService;
        _logger = logger;
        _mainWindow = mainWindow;

        _logger.Debug("WorldsBrowseViewModel initialized");

        // Load saved sort preferences from config
        SelectedSortOption = _configService.Current.WorldSortOption;
        SelectedSortDirection = _configService.Current.WorldSortDirection;
        _logger.Debug("Loaded sort preferences: {SortOption} {SortDirection}",
            SelectedSortOption, SelectedSortDirection);

        // Subscribe to server status change events for real-time UI updates
        _serverMonitorService.ServerStatusChanged += OnServerStatusChanged;

        // Load offline banner dismissal state from session
        IsOfflineBannerDismissed = _mainWindow.GetOfflineBannerDismissed();

        // Load website banner dismissal state from config (permanent)
        IsWebsiteBannerDismissed = _configService.Current.IsWebsiteBannerDismissed;

        // Initialize debounce timer for search (500ms delay)
        _searchDebounceTimer = new System.Timers.Timer(500);
        _searchDebounceTimer.Elapsed += OnSearchDebounceElapsed;
        _searchDebounceTimer.AutoReset = false;

        // Initialize status checking timer (30 second interval)
        // Uses ThwargLauncher approach: parallel checks with adaptive intervals
        _statusCheckTimer = new System.Threading.Timer(
            _ =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await CheckAllWorldStatusAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error in periodic world status check");
                    }
                });
            },
            null,
            TimeSpan.FromSeconds(10),  // Initial delay (after worlds load)
            TimeSpan.FromSeconds(30)); // Check every 30 seconds

        // Don't auto-load in constructor for Singleton - load on first navigation
        _logger.Debug("WorldsBrowseViewModel constructed (Singleton - data will load on first navigation)");
    }

    /// <summary>
    /// Called when navigated to this view. Can receive a filter parameter.
    /// Implements smart caching for Singleton lifetime:
    /// - First navigation: Full load from API/cache
    /// - Subsequent navigations: Always use in-memory cache (no auto-refresh)
    /// - Manual refresh: User clicks refresh button or presses F5
    /// </summary>
    /// <param name="parameter">Optional filter parameter: "favorites", "recent", or null for all.</param>
    public override void OnNavigatedTo(object? parameter)
    {
        _logger.Information("WorldsBrowseView navigated to (Initialized: {IsInitialized}, LastLoad: {LastLoad})",
            _isInitialized, _lastLoadTime?.ToString("HH:mm:ss") ?? "Never");

        // Handle filter parameter if provided
        if (parameter is string filter)
        {
            var newFilter = filter.ToLowerInvariant();
            if (ActiveViewFilter != newFilter)
            {
                ActiveViewFilter = newFilter;
                _logger.Information("Filter changed to: {Filter}", ActiveViewFilter);
            }
        }

        // First-time navigation - full load required
        if (!_isInitialized)
        {
            _logger.Information("First navigation - performing initial data load");
            _ = InitialLoadAndMarkInitializedAsync();
            return;
        }

        // Data already loaded - use in-memory cache (no auto-refresh on navigation)
        var dataAge = _lastLoadTime.HasValue
            ? DateTime.UtcNow - _lastLoadTime.Value
            : TimeSpan.MaxValue;

        _logger.Information("Using cached data ({Age:F1} minutes old) - no auto-refresh",
            dataAge.TotalMinutes);

        // Rebuild sections in case favorites/recent changed
        BuildSectionedLayout();

        // Update offline status banner
        CheckOfflineMode(Worlds.Count);
    }

    /// <summary>
    /// Performs initial load and marks the ViewModel as initialized.
    /// </summary>
    private async Task InitialLoadAndMarkInitializedAsync()
    {
        try
        {
            await InitialLoadAsync();
            _isInitialized = true;
            _lastLoadTime = DateTime.UtcNow;
            _logger.Information("Initial data load complete");

            // Notify UI that timestamp changed
            OnPropertyChanged(nameof(LastUpdateText));
            OnPropertyChanged(nameof(ShowLastUpdateText));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during initial data load");
            ErrorMessage = "Failed to load worlds. Please try refreshing.";
        }
    }

    /// <summary>
    /// Refreshes data from API/cache and updates timestamp.
    /// </summary>
    private async Task RefreshDataAsync()
    {
        try
        {
            await LoadFeaturedWorldsAsync();
            await LoadWorldsAsync();
            BuildSectionedLayout();
            _lastLoadTime = DateTime.UtcNow;
            _logger.Information("Data refresh complete");

            // Notify UI that timestamp changed
            OnPropertyChanged(nameof(LastUpdateText));
            OnPropertyChanged(nameof(ShowLastUpdateText));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during data refresh");
            ErrorMessage = "Failed to refresh worlds.";
        }
    }

    /// <summary>
    /// Performs initial data load, ensuring Featured Worlds loads before All Worlds
    /// to prevent race conditions in the exclusion logic.
    /// </summary>
    private async Task InitialLoadAsync()
    {
        await LoadFeaturedWorldsAsync();
        await LoadWorldsAsync();
        BuildSectionedLayout();
    }

    /// <summary>
    /// Builds the sectioned layout with Featured, Recent, Favorites, and All Worlds sections.
    /// This method organizes all loaded worlds into distinct sections for the new UI design.
    /// </summary>
    private void BuildSectionedLayout()
    {
        try
        {
            _logger.Debug("Building sectioned layout for Browse Worlds");

            // 1. Select daily featured world (rotates based on day of year)
            if (FeaturedWorlds.Count > 0)
            {
                var dayOfYear = DateTime.UtcNow.DayOfYear;
                var featuredIndex = dayOfYear % FeaturedWorlds.Count;
                FeaturedWorld = FeaturedWorlds[featuredIndex];
                _logger.Information("Selected featured world for day {Day}: {WorldName}", dayOfYear, FeaturedWorld.Name);

                // Update favorite status
                IsFeaturedWorldFavorite = _favoritesService.IsFavorite(FeaturedWorld.ServerId, null);
            }
            else
            {
                FeaturedWorld = null;
                IsFeaturedWorldFavorite = false;
            }

            // 2. Build Recent Servers section (last 3 played)
            RecentServers.Clear();
            var recentServersList = _recentServersService.GetRecentServers()
                .Where(r => r.WorldServerId.HasValue)
                .Take(3)
                .ToList();

            foreach (var recent in recentServersList)
            {
                // Find the world in our loaded worlds
                var world = Worlds.FirstOrDefault(w => w.ServerId == recent.WorldServerId!.Value);
                if (world != null)
                {
                    var card = new ServerCardViewModel(world, _navigationService, _favoritesService, _logger);
                    RecentServers.Add(card);
                }
            }
            _logger.Debug("Built Recent section with {Count} servers", RecentServers.Count);

            // 3. Build Favorites section
            FavoriteServers.Clear();
            var favoriteWorlds = Worlds.Where(w => _favoritesService.IsFavorite(w.ServerId, null)).ToList();
            foreach (var world in favoriteWorlds)
            {
                var card = new ServerCardViewModel(world, _navigationService, _favoritesService, _logger);
                FavoriteServers.Add(card);
            }
            _logger.Debug("Built Favorites section with {Count} servers", FavoriteServers.Count);

            // 4. Build All Other Worlds section (excluding featured world and recent only)
            // NOTE: Favorites ARE included in All Worlds - the star icon will be filled to indicate favorite status
            AllOtherWorlds.Clear();
            var featuredWorldId = FeaturedWorld?.WorldId;
            var recentServerIds = recentServersList.Select(r => r.WorldServerId!.Value).ToHashSet();

            var otherWorlds = Worlds.Where(w =>
                w.WorldId != featuredWorldId &&
                !recentServerIds.Contains(w.ServerId)
            ).ToList();

            foreach (var world in otherWorlds)
            {
                var card = new ServerCardViewModel(world, _navigationService, _favoritesService, _logger);
                AllOtherWorlds.Add(card);
            }
            _logger.Debug("Built All Other Worlds section with {Count} servers (includes favorites with filled stars)", AllOtherWorlds.Count);

            _logger.Information("Sectioned layout complete: Featured={Featured}, Recent={Recent}, Favorites={Favorites}, All={All}",
                FeaturedWorld != null ? 1 : 0, RecentServers.Count, FavoriteServers.Count, AllOtherWorlds.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error building sectioned layout");
        }
    }

    /// <summary>
    /// Refreshes the worlds list from the API.
    /// Updates the last load timestamp for Singleton lifetime tracking.
    /// </summary>
    [RelayCommand]
    private async Task RefreshWorldsAsync()
    {
        _logger.Information("User requested manual worlds refresh");
        await LoadFeaturedWorldsAsync(); // Load Featured first
        await LoadWorldsAsync();
        BuildSectionedLayout(); // Rebuild UI sections with refreshed data

        // Update timestamp for Singleton lifetime tracking
        _lastLoadTime = DateTime.UtcNow;
        _logger.Information("Manual refresh complete at {Time}", _lastLoadTime);

        // Notify UI that timestamp changed
        OnPropertyChanged(nameof(LastUpdateText));
        OnPropertyChanged(nameof(ShowLastUpdateText));
    }

    /// <summary>
    /// Performs a search with the current search text.
    /// Search filters both Featured and All Worlds sections.
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        _logger.Information("User initiated search: {SearchText}", SearchText);
        await LoadFeaturedWorldsAsync(); // Reload featured with search filter
        await LoadWorldsAsync(); // Reload all worlds with search filter
        BuildSectionedLayout(); // Rebuild UI sections with search results
    }

    /// <summary>
    /// Filters worlds by the selected ruleset.
    /// </summary>
    [RelayCommand]
    private async Task FilterByRuleSetAsync()
    {
        _logger.Information("User changed ruleset filter: {RuleSet}", SelectedRuleSet?.ToString() ?? "All");
        await LoadFeaturedWorldsAsync(); // Reload featured with filter
        await LoadWorldsAsync();
        BuildSectionedLayout(); // Rebuild UI sections with filtered data
    }

    /// <summary>
    /// Shows details for the specified world.
    /// </summary>
    /// <param name="world">The world to show details for.</param>
    [RelayCommand]
    private void ShowWorldDetails(WorldDto? world)
    {
        if (world == null)
        {
            _logger.Warning("ShowWorldDetails called with null world");
            ErrorMessage = "Invalid world selection";
            return;
        }

        // Validate world has a valid server ID
        if (world.ServerId == Guid.Empty)
        {
            _logger.Warning("ShowWorldDetails called with empty ServerId for world: {WorldName}", world.Name);
            ErrorMessage = "Invalid world ID";
            return;
        }

        _logger.Information("User viewing details for world: {WorldName} (ServerId: {ServerId})", world.Name, world.ServerId);

        // Navigate to WorldDetailView using NavigationService
        _navigationService.NavigateTo<WorldDetailViewModel>(world);
    }

    /// <summary>
    /// Clears the search text and reloads all worlds.
    /// </summary>
    [RelayCommand]
    private async Task ClearSearchAsync()
    {
        // Set flag to suppress debounce timer when programmatically clearing search
        _suppressSearchDebounce = true;
        try
        {
            SearchText = string.Empty;
            await LoadFeaturedWorldsAsync(); // Reload Featured first
            await LoadWorldsAsync();
            BuildSectionedLayout(); // Rebuild UI sections with all worlds
        }
        finally
        {
            _suppressSearchDebounce = false;
        }
    }

    /// <summary>
    /// Clears the ruleset filter and reloads all worlds.
    /// </summary>
    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        SelectedRuleSet = null;
        await LoadFeaturedWorldsAsync(); // Reload Featured first
        await LoadWorldsAsync();
        BuildSectionedLayout(); // Rebuild UI sections with all worlds
    }

    /// <summary>
    /// Dismisses the offline mode banner for the current session.
    /// </summary>
    [RelayCommand]
    private void DismissOfflineBanner()
    {
        _logger.Debug("User dismissed offline mode banner");
        IsOfflineBannerDismissed = true;

        // Save dismissal state to session so it persists during navigation
        // State will reset when the launcher is restarted
        _mainWindow.SetOfflineBannerDismissed(true);
    }

    /// <summary>
    /// Opens the OldPortal website in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenWebsite()
    {
        try
        {
            _logger.Information("User opening OldPortal website from worlds browse");
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

    /// <summary>
    /// Dismisses the website promo banner permanently.
    /// </summary>
    [RelayCommand]
    private void DismissWebsiteBanner()
    {
        _logger.Debug("User permanently dismissed website promo banner");
        IsWebsiteBannerDismissed = true;

        // Save dismissal state to config permanently
        _configService.UpdateConfiguration(config =>
        {
            config.IsWebsiteBannerDismissed = true;
        });
    }

    /// <summary>
    /// Command to toggle favorite status of the featured world.
    /// </summary>
    [RelayCommand]
    private void ToggleFeaturedWorldFavorite()
    {
        if (FeaturedWorld == null)
        {
            _logger.Warning("ToggleFeaturedWorldFavorite command invoked but no featured world available");
            return;
        }

        // Toggle favorite status
        bool newStatus = _favoritesService.ToggleFavorite(
            FeaturedWorld.ServerId,
            null,
            FeaturedWorld.Name,
            false); // Not a manual server

        // Update local property
        IsFeaturedWorldFavorite = newStatus;

        _logger.Information("Featured world favorite toggled: {WorldName}, IsFavorite: {IsFavorite}",
            FeaturedWorld.Name, IsFeaturedWorldFavorite);

        // Rebuild layout to update favorite sections
        BuildSectionedLayout();
    }

    /// <summary>
    /// Loads featured worlds from the service.
    /// Featured worlds ARE filtered by both search text and ruleset.
    /// </summary>
    private async Task LoadFeaturedWorldsAsync()
    {
        try
        {
            IsLoadingFeatured = true;
            ErrorMessage = null;

            _logger.Debug("Loading featured worlds");
            var featuredWorlds = await _worldsService.GetFeaturedWorldsAsync();

            // Apply search filter if specified
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLower();
                featuredWorlds = featuredWorlds.Where(w =>
                    w.Name.ToLower().Contains(searchLower) ||
                    (w.Description != null && w.Description.ToLower().Contains(searchLower))
                ).ToList();
                _logger.Debug("Filtered featured worlds by search '{SearchText}': {Count} worlds",
                    SearchText, featuredWorlds.Count);
            }

            // Apply ruleset filter if specified
            if (SelectedRuleSet.HasValue)
            {
                featuredWorlds = featuredWorlds.Where(w => w.EffectiveRuleSet == SelectedRuleSet.Value).ToList();
                _logger.Debug("Filtered featured worlds by ruleset {RuleSet}: {Count} worlds",
                    SelectedRuleSet.Value, featuredWorlds.Count);
            }

            // Apply online status filter if specified
            if (ShowOnlineOnly)
            {
                featuredWorlds = featuredWorlds.Where(w => w.IsOnline).ToList();
                _logger.Debug("Filtered featured worlds by online status: {Count} worlds", featuredWorlds.Count);
            }

            // Apply sorting based on selected option and direction
            featuredWorlds = ApplySorting(featuredWorlds).ToList();

            // Replace entire collection to force UI refresh (prevents visual duplicates)
            FeaturedWorlds = new ObservableCollection<WorldDto>(featuredWorlds);

            _logger.Information("Loaded {Count} featured worlds", featuredWorlds.Count);

            // Check if we're in offline mode
            CheckOfflineMode(featuredWorlds.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading featured worlds");
            ErrorMessage = "Failed to load featured worlds. Using cached data.";
            IsOfflineMode = true;
        }
        finally
        {
            IsLoadingFeatured = false;
        }
    }

    /// <summary>
    /// Loads all worlds with current search/filter criteria from the service.
    /// </summary>
    private async Task LoadWorldsAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            StatusMessage = null;

            _logger.Debug("Loading worlds (search: {Search}, ruleset: {RuleSet})",
                string.IsNullOrWhiteSpace(SearchText) ? "none" : SearchText,
                SelectedRuleSet?.ToString() ?? "all");

            // Sanitize search text before sending to API
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : InputSanitizer.SanitizeSearchQuery(SearchText);

            if (!string.IsNullOrWhiteSpace(SearchText) && string.IsNullOrWhiteSpace(search))
            {
                _logger.Warning("Search text contains invalid characters and was rejected");
                StatusMessage = "Search contains invalid characters";
                return;
            }

            var worlds = await _worldsService.GetAllWorldsAsync(search, SelectedRuleSet);

            // NOTE: Don't exclude featured worlds here - BuildSectionedLayout() will handle
            // excluding the single featured world from AllOtherWorlds section
            // (FeaturedWorlds collection is used only for selecting the daily featured world)

            // Apply online status filter if specified
            if (ShowOnlineOnly)
            {
                worlds = worlds.Where(w => w.IsOnline).ToList();
                _logger.Debug("Filtered worlds by online status: {Count} worlds", worlds.Count);
            }

            // Apply view filter (favorites, recent, or all)
            if (ActiveViewFilter == "favorites")
            {
                worlds = worlds.Where(w => _favoritesService.IsFavorite(w.ServerId, null)).ToList();
                _logger.Debug("Filtered worlds by favorites: {Count} worlds", worlds.Count);
            }
            else if (ActiveViewFilter == "recent")
            {
                var recentServers = _recentServersService.GetRecentServers();
                var recentServerIds = recentServers
                    .Where(r => r.WorldServerId.HasValue)
                    .Select(r => r.WorldServerId!.Value)
                    .ToHashSet();

                worlds = worlds.Where(w => recentServerIds.Contains(w.ServerId)).ToList();
                _logger.Debug("Filtered worlds by recent: {Count} worlds", worlds.Count);
            }

            // Apply sorting based on selected option and direction
            worlds = ApplySorting(worlds).ToList();

            // Replace entire collection to force UI refresh (prevents visual duplicates)
            Worlds = new ObservableCollection<WorldDto>(worlds);

            // Rebuild server cards for card grid display
            RebuildServerCards();

            _logger.Information("Loaded {Count} worlds total", worlds.Count);

            // Check if we're in offline mode
            CheckOfflineMode(worlds.Count);

            // Update status message
            if (Worlds.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(SearchText) || SelectedRuleSet.HasValue || ShowOnlineOnly)
                {
                    StatusMessage = "No worlds match your search criteria.";
                }
                else
                {
                    StatusMessage = "No worlds available.";
                }
            }
            else
            {
                var filterInfo = string.Empty;
                if (!string.IsNullOrWhiteSpace(SearchText))
                    filterInfo += $" matching \"{SearchText}\"";
                if (SelectedRuleSet.HasValue)
                    filterInfo += $" with ruleset {SelectedRuleSet.Value}";
                if (ShowOnlineOnly)
                    filterInfo += " (online only)";

                StatusMessage = $"Showing {Worlds.Count} world(s){filterInfo}";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading worlds");
            ErrorMessage = "Failed to load worlds. Please check your connection.";
            IsOfflineMode = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Checks if we're in offline mode based on WorldsService API tracking.
    /// Uses the IsOnline property from WorldsService which tracks actual API reachability.
    /// </summary>
    private void CheckOfflineMode(int worldCount)
    {
        // Use WorldsService.IsOnline to properly track API reachability
        IsOfflineMode = !_worldsService.IsOnline;

        if (IsOfflineMode)
        {
            // Get cache age for display
            CacheAgeText = _worldsService.GetFormattedCacheAge();

            if (worldCount > 0)
            {
                StatusMessage = "Offline mode - showing cached data";
            }
            else
            {
                StatusMessage = "Offline mode - no cached data available";
                CacheAgeText = null; // No cache age if no data
            }
        }
        else
        {
            // Online - clear cache age text
            CacheAgeText = null;

            if (worldCount == 0)
            {
                // Online but no results - different from being offline
                StatusMessage = "No worlds available";
            }
        }

        // Notify MainWindow of offline status change
        _mainWindow.UpdateOfflineStatus(IsOfflineMode);
    }

    /// <summary>
    /// Called when SearchText property changes.
    /// Implements debouncing to avoid excessive API calls while typing.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        // Don't restart timer if we're programmatically clearing the search
        if (_suppressSearchDebounce)
            return;

        // Reset and restart the debounce timer
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer?.Start();
    }

    /// <summary>
    /// Called when the search debounce timer elapses.
    /// Triggers the actual search operation.
    /// </summary>
    private async void OnSearchDebounceElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _logger.Debug("Search debounce elapsed, executing search");
        await LoadFeaturedWorldsAsync(); // Reload featured with search filter
        await LoadWorldsAsync(); // Reload all worlds with search filter
        BuildSectionedLayout(); // Rebuild UI sections with filtered data
    }

    /// <summary>
    /// Called when SelectedRuleSet property changes.
    /// Reloads worlds with the new filter.
    /// </summary>
    partial void OnSelectedRuleSetChanged(RuleSet? value)
    {
        _ = FilterByRuleSetAsync();
    }

    /// <summary>
    /// Called when SelectedRuleSetFilter property changes.
    /// Updates the actual RuleSet filter and reloads worlds.
    /// </summary>
    partial void OnSelectedRuleSetFilterChanged(RuleSetFilterOption? value)
    {
        SelectedRuleSet = value?.Value;
    }

    /// <summary>
    /// Called when ShowOnlineOnly property changes.
    /// Reloads worlds with the new filter.
    /// </summary>
    partial void OnShowOnlineOnlyChanged(bool value)
    {
        _logger.Information("User toggled online filter: {ShowOnlineOnly}", value);
        _ = ReloadDataAsync();
    }

    /// <summary>
    /// Called when SelectedSortOption property changes.
    /// Reloads worlds with the new sort order.
    /// </summary>
    partial void OnSelectedSortOptionChanged(WorldSortOption value)
    {
        _logger.Information("User changed sort option: {SortOption}", value);

        // Save preference to config
        _configService.Current.WorldSortOption = value;
        _configService.SaveConfiguration();

        _ = ReloadDataAsync();
    }

    /// <summary>
    /// Called when SelectedSortDirection property changes.
    /// Reloads worlds with the new sort direction.
    /// </summary>
    partial void OnSelectedSortDirectionChanged(SortDirection value)
    {
        _logger.Information("User changed sort direction: {SortDirection}", value);

        // Save preference to config
        _configService.Current.WorldSortDirection = value;
        _configService.SaveConfiguration();

        _ = ReloadDataAsync();
    }

    /// <summary>
    /// Safely reloads data with cancellation of previous operations and UI thread synchronization.
    /// </summary>
    private async Task ReloadDataAsync()
    {
        // Cancel any existing load operation
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = new System.Threading.CancellationTokenSource();

        // Prevent concurrent loads
        if (IsLoading)
        {
            _logger.Debug("Load already in progress, skipping duplicate request");
            return;
        }

        try
        {
            IsLoading = true;
            var token = _loadCancellationTokenSource.Token;

            // Load data on background thread
            await Task.Run(async () =>
            {
                if (token.IsCancellationRequested) return;
                await LoadFeaturedWorldsAsync();

                if (token.IsCancellationRequested) return;
                await LoadWorldsAsync();
            }, token);

            // Build UI on UI thread
            if (!token.IsCancellationRequested)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        BuildSectionedLayout();
                    }
                });
            }
        }
        catch (System.OperationCanceledException)
        {
            _logger.Debug("Reload operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during data reload");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Called when SelectedWorld property changes.
    /// Can be used to automatically show details when a world is selected.
    /// </summary>
    partial void OnSelectedWorldChanged(WorldDto? value)
    {
        if (value != null)
        {
            _logger.Debug("Selected world changed: {WorldName}", value.Name);
            // Optionally auto-show details
            // ShowWorldDetails(value);
        }
    }

    /// <summary>
    /// Checks the online status of all worlds via local UDP ping.
    /// Uses ThwargLauncher's efficient approach:
    /// - Parallel checking (Task.WhenAll)
    /// - Adaptive intervals (5min for online, 15sec for offline)
    /// - Per-world check time tracking
    /// Combines both featured worlds and regular worlds.
    /// </summary>
    private async Task CheckAllWorldStatusAsync()
    {
        // Combine both collections for status checking
        var allWorlds = FeaturedWorlds.Concat(Worlds).ToList();

        if (allWorlds.Count == 0)
            return;

        _logger.Debug("Checking status for {Count} worlds via local UDP", allWorlds.Count);

        // Check all worlds in parallel (ThwargLauncher approach)
        var checkTasks = allWorlds.Select(async world =>
        {
            try
            {
                // Skip if host/port invalid
                if (string.IsNullOrWhiteSpace(world.Host) || world.Port <= 0)
                    return;

                // Adaptive polling: skip if checked too recently
                var elapsedSinceCheck = DateTime.UtcNow - (world.LastStatusCheck ?? DateTime.MinValue);
                var requiredInterval = world.IsOnline
                    ? TimeSpan.FromSeconds(world.StatusOnlineIntervalSeconds)  // 5 min for online
                    : TimeSpan.FromSeconds(world.StatusOfflineIntervalSeconds); // 15 sec for offline

                if (elapsedSinceCheck < requiredInterval)
                {
                    _logger.Debug("Skipping check for {World} - last checked {Elapsed}s ago",
                        world.Name, (int)elapsedSinceCheck.TotalSeconds);
                    return;
                }

                // Perform UDP ping using ServerMonitorService
                await _serverMonitorService.CheckServerAsync(world);

                // Note: ServerMonitorService will fire ServerStatusChanged event
                // which updates the UI automatically via OnServerStatusChanged handler
                // No need to manually update world properties here
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error checking world status for {Host}:{Port}", world.Host, world.Port);
            }
        }).ToArray();

        await Task.WhenAll(checkTasks);
    }

    /// <summary>
    /// Event handler for server status changes from ServerMonitorService.
    /// Updates WorldDto objects in the UI when status changes are detected.
    /// </summary>
    private void OnServerStatusChanged(object? sender, ServerStatusChangedEventArgs e)
    {
        try
        {
            // Update on UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Find the world in all collections and update it
                var worldsToUpdate = new[]
                {
                    FeaturedWorlds.FirstOrDefault(w => w.ServerId == e.ServerId),
                    Worlds.FirstOrDefault(w => w.ServerId == e.ServerId)
                }.Where(w => w != null).ToList();

                foreach (var world in worldsToUpdate)
                {
                    if (world != null)
                    {
                        var wasOnline = world.IsOnline;
                        world.IsOnline = e.IsOnline;
                        world.LastStatusCheck = e.CheckedAtUtc;

                        if (wasOnline != e.IsOnline)
                        {
                            _logger.Information("UI updated: {ServerName} status changed to {NewStatus}",
                                e.ServerName, e.IsOnline ? "ONLINE" : "OFFLINE");
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling server status change for {ServerName}", e.ServerName);
        }
    }

    /// <summary>
    /// Applies sorting to a collection of worlds based on the selected sort option and direction.
    /// </summary>
    /// <param name="worlds">The collection of worlds to sort.</param>
    /// <returns>The sorted collection.</returns>
    private IEnumerable<WorldDto> ApplySorting(IEnumerable<WorldDto> worlds)
    {
        IOrderedEnumerable<WorldDto> sortedWorlds;

        // Apply primary sort based on selected option
        switch (SelectedSortOption)
        {
            case WorldSortOption.Name:
                sortedWorlds = SelectedSortDirection == SortDirection.Ascending
                    ? worlds.OrderBy(w => w.Name)
                    : worlds.OrderByDescending(w => w.Name);
                break;

            case WorldSortOption.PlayerCount:
                sortedWorlds = SelectedSortDirection == SortDirection.Ascending
                    ? worlds.OrderBy(w => w.PlayerCount)
                    : worlds.OrderByDescending(w => w.PlayerCount);
                break;

            case WorldSortOption.Uptime:
                // Sort by uptime percentage (0-100)
                sortedWorlds = SelectedSortDirection == SortDirection.Ascending
                    ? worlds.OrderBy(w => w.UptimePercentage)
                    : worlds.OrderByDescending(w => w.UptimePercentage);
                break;

            case WorldSortOption.ServerType:
                sortedWorlds = SelectedSortDirection == SortDirection.Ascending
                    ? worlds.OrderBy(w => w.ServerType)
                    : worlds.OrderByDescending(w => w.ServerType);
                break;

            default:
                // Default to name sorting
                sortedWorlds = worlds.OrderBy(w => w.Name);
                break;
        }

        _logger.Debug("Applied sorting: {SortOption} {Direction}",
            SelectedSortOption, SelectedSortDirection);

        return sortedWorlds;
    }

    /// <summary>
    /// Rebuilds the ServerCards collection from the current Worlds collection.
    /// Wraps each WorldDto in a ServerCardViewModel for display in the card grid.
    /// </summary>
    private void RebuildServerCards()
    {
        ServerCards.Clear();
        foreach (var world in Worlds)
        {
            var card = new ServerCardViewModel(world, _navigationService, _favoritesService, _logger);
            ServerCards.Add(card);
        }
        _logger.Debug("Rebuilt {Count} server cards", ServerCards.Count);
    }

    /// <summary>
    /// Gets a human-readable "time ago" text from a DateTime.
    /// </summary>
    /// <param name="time">The time to format.</param>
    /// <returns>A string like "just now", "5m ago", "2h ago", etc.</returns>
    private string GetTimeAgoText(DateTime time)
    {
        var age = DateTime.UtcNow - time;
        if (age.TotalSeconds < 60) return "just now";
        if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes}m ago";
        if (age.TotalHours < 24) return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }

    /// <summary>
    /// Disposes resources used by the WorldsBrowseViewModel.
    /// IMPORTANT: For Singleton lifetime, this is only called when app closes.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.Information("WorldsBrowseViewModel disposing (app shutdown)");

        // Unsubscribe from server status events
        _serverMonitorService.ServerStatusChanged -= OnServerStatusChanged;

        // Cancel and dispose any ongoing load operations
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = null;

        _searchDebounceTimer?.Stop();
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = null;

        _statusCheckTimer?.Dispose();
        _statusCheckTimer = null;

        _disposed = true;
        _logger.Debug("WorldsBrowseViewModel disposed");
    }
}

/// <summary>
/// Helper class for ruleset filter dropdown options.
/// Allows binding a nullable RuleSet with a display name.
/// </summary>
public record RuleSetFilterOption(RuleSet? Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Sort options for the world list.
/// </summary>
public enum WorldSortOption
{
    Name,
    PlayerCount,
    Uptime,
    ServerType
}

/// <summary>
/// Sort direction for list sorting.
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Helper class for sort option dropdown items.
/// </summary>
public record SortOptionItem(WorldSortOption Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Helper class for sort direction dropdown items.
/// </summary>
public record SortDirectionItem(SortDirection Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}
