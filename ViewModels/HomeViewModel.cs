// TODO: [LAUNCH-110] Phase 2 Week 4 - HomeViewModel
// Component: Launcher
// Module: UI Redesign - Home View & News Feed
// Description: Home view model with featured server, news feed, and recent servers

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.DTOs;
using OPLauncher.Services;
using OPLauncher.Utilities;

namespace OPLauncher.ViewModels;

/// <summary>
/// View model for the Home view.
/// Displays featured server, news feed, and recent servers.
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly WorldsService _worldsService;
    private readonly INavigationService _navigationService;
    private readonly FavoritesService _favoritesService;
    private readonly DatabaseService _databaseService;
    private readonly HttpClient _httpClient;
    private readonly LoggingService _logger;

    private const string NewsCacheCollection = "news_cache";
    private const int NewsCacheExpiryMinutes = 1440; // Cache news for 24 hours (news updated once daily)
    private const int HomeNewsDisplayCount = 3; // Show 3 news items on home screen

    /// <summary>
    /// The featured server (top by player count).
    /// </summary>
    [ObservableProperty]
    private WorldDto? _featuredServer;

    /// <summary>
    /// Whether the featured server is favorited.
    /// </summary>
    [ObservableProperty]
    private bool _isFeaturedServerFavorite;

    /// <summary>
    /// Resolved banner URL for the featured server.
    /// Uses ImageUrlResolver to handle API-relative paths and provide ruleset-specific fallbacks.
    /// </summary>
    public string FeaturedServerBannerUrl
    {
        get
        {
            if (FeaturedServer == null)
                return ImageUrlResolver.ResolveImageUrl(null, ImageFallbackType.GenericServer);

            var fallbackType = FeaturedServer.EffectiveRuleSet switch
            {
                DTOs.RuleSet.PvP => ImageFallbackType.ServerPvP,
                DTOs.RuleSet.PvE => ImageFallbackType.ServerPvE,
                DTOs.RuleSet.RP => ImageFallbackType.ServerRP,
                DTOs.RuleSet.Retail => ImageFallbackType.ServerRetail,
                DTOs.RuleSet.Hardcore => ImageFallbackType.ServerHardcore,
                DTOs.RuleSet.Custom => ImageFallbackType.ServerCustom,
                _ => ImageFallbackType.GenericServer
            };

            return ImageUrlResolver.ResolveImageUrl(FeaturedServer.CardBannerImageUrl, fallbackType);
        }
    }

    /// <summary>
    /// Called when FeaturedServer property changes.
    /// Notifies dependent computed properties.
    /// </summary>
    partial void OnFeaturedServerChanged(WorldDto? value)
    {
        OnPropertyChanged(nameof(FeaturedServerBannerUrl));
    }

    /// <summary>
    /// News items collection.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<NewsDto> _newsItems = new();

    /// <summary>
    /// Recent servers collection.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<WorldDto> _recentServers = new();

    /// <summary>
    /// Whether the home view is loading.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Initializes a new instance of the HomeViewModel.
    /// </summary>
    public HomeViewModel(
        WorldsService worldsService,
        INavigationService navigationService,
        FavoritesService favoritesService,
        DatabaseService databaseService,
        HttpClient httpClient,
        LoggingService logger)
    {
        _worldsService = worldsService;
        _navigationService = navigationService;
        _favoritesService = favoritesService;
        _databaseService = databaseService;
        _httpClient = httpClient;
        _logger = logger;

        _logger.Debug("HomeViewModel initialized");

        // Load initial data
        _ = LoadHomeDataAsync();
    }

    /// <summary>
    /// Loads home view data (featured server, news, recent servers).
    /// </summary>
    private async Task LoadHomeDataAsync()
    {
        try
        {
            IsLoading = true;

            // Load featured server (daily rotation ensures all servers get a turn)
            await LoadFeaturedServerAsync();

            // Load news items with API and caching
            await LoadNewsItemsAsync();

            _logger.Information("Home data loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading home data");
            SetError("Failed to load home data. Please try again.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads the featured server using fair daily rotation.
    /// Ensures all registered servers get a turn as the featured server.
    /// Rotates every 24 hours based on day of year.
    /// </summary>
    private async Task LoadFeaturedServerAsync()
    {
        try
        {
            var worlds = await _worldsService.GetFeaturedWorldsAsync();

            // Select featured server using daily rotation (ensures all servers get a turn)
            if (worlds.Any())
            {
                var dayOfYear = DateTime.UtcNow.DayOfYear;
                var featuredIndex = dayOfYear % worlds.Count;
                FeaturedServer = worlds[featuredIndex];

                _logger.Information("Featured server for day {Day}: {ServerName} (index {Index} of {Total})",
                    dayOfYear, FeaturedServer.Name, featuredIndex, worlds.Count);

                // Update favorite status
                IsFeaturedServerFavorite = _favoritesService.IsFavorite(FeaturedServer.ServerId, null);
            }
            else
            {
                FeaturedServer = null;
                IsFeaturedServerFavorite = false;
                _logger.Warning("No servers available for featured section");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading featured server");
            FeaturedServer = null;
            IsFeaturedServerFavorite = false;
        }
    }

    /// <summary>
    /// Loads news items with cache-first strategy:
    /// 1. Check cache (15 min expiry)
    /// 2. If cache expired/empty, fetch from API
    /// 3. If API fails, use fallback news
    /// </summary>
    private async Task LoadNewsItemsAsync()
    {
        try
        {
            NewsItems.Clear();
            List<NewsDto> newsToDisplay;

            // Step 1: Try to load from cache
            var cachedNews = GetCachedNews();
            if (cachedNews != null && cachedNews.Any())
            {
                _logger.Debug("Loaded {Count} news items from cache", cachedNews.Count);
                newsToDisplay = cachedNews;
            }
            else
            {
                // Step 2: Cache expired or empty - try API
                var apiNews = await LoadNewsFromApiAsync();

                if (apiNews != null && apiNews.Any())
                {
                    _logger.Information("Loaded {Count} news items from API", apiNews.Count);

                    // Cache the API results
                    CacheNews(apiNews);
                    newsToDisplay = apiNews;
                }
                else
                {
                    // Step 3: API failed - use fallback news
                    _logger.Warning("API returned no news, using fallback news");
                    newsToDisplay = GetFallbackNews().ToList();
                }
            }

            // Display only the first 3 news items on home screen
            var displayNews = newsToDisplay.Take(HomeNewsDisplayCount);
            foreach (var newsItem in displayNews)
            {
                NewsItems.Add(newsItem);
            }

            _logger.Debug("Displaying {Count} news items on home screen", NewsItems.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading news items");

            // On error, show fallback news
            var fallbackNews = GetFallbackNews().Take(HomeNewsDisplayCount);
            foreach (var newsItem in fallbackNews)
            {
                NewsItems.Add(newsItem);
            }
        }
    }

    /// <summary>
    /// Loads news from the API endpoint.
    /// Returns null if the API call fails.
    /// </summary>
    private async Task<List<NewsDto>?> LoadNewsFromApiAsync()
    {
        try
        {
            _logger.Debug("Fetching news from API: {Endpoint}", ApiEndpoints.News.GetWithLimit(20));

            var response = await _httpClient.GetAsync(ApiEndpoints.News.GetWithLimit(20));

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("API returned status code {StatusCode} for news endpoint", response.StatusCode);
                return null;
            }

            var newsResponse = await response.Content.ReadFromJsonAsync<NewsApiResponse>();

            if (newsResponse?.News == null || !newsResponse.News.Any())
            {
                _logger.Warning("API returned empty news list");
                return null;
            }

            _logger.Information("Successfully fetched {Count} news items from API", newsResponse.News.Count);
            return newsResponse.News;
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "HTTP error fetching news from API");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error fetching news from API");
            return null;
        }
    }

    /// <summary>
    /// Gets cached news from LiteDB if not expired.
    /// </summary>
    private List<NewsDto>? GetCachedNews()
    {
        try
        {
            var cached = _databaseService.FindOne<CachedNewsEntry>(NewsCacheCollection, x => x.CacheKey == "home_news");

            if (cached == null)
            {
                _logger.Debug("No cached news found");
                return null;
            }

            // Check if cache is expired
            var cacheAge = DateTime.UtcNow - cached.CachedAt;
            if (cacheAge.TotalMinutes > NewsCacheExpiryMinutes)
            {
                _logger.Debug("News cache expired (age: {Age} minutes)", cacheAge.TotalMinutes);
                return null;
            }

            _logger.Debug("News cache valid (age: {Age} minutes)", cacheAge.TotalMinutes);
            return cached.News;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading news from cache");
            return null;
        }
    }

    /// <summary>
    /// Caches news items to LiteDB.
    /// </summary>
    private void CacheNews(List<NewsDto> news)
    {
        try
        {
            var cacheEntry = new CachedNewsEntry
            {
                CacheKey = "home_news",
                News = news,
                CachedAt = DateTime.UtcNow
            };

            _databaseService.Upsert(NewsCacheCollection, cacheEntry);
            _logger.Debug("Cached {Count} news items", news.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error caching news items");
        }
    }

    /// <summary>
    /// Gets fallback news items with AC Lore theme.
    /// Used when API is unavailable or returns no news.
    /// </summary>
    private NewsDto[] GetFallbackNews()
    {
        return new[]
        {
            new NewsDto
            {
                Id = Guid.NewGuid(),
                Title = "Welcome to OldPortal!",
                Excerpt = "Your gateway to the worlds of Dereth. Visit OldPortal.com for the latest news, server updates, and community discussions.",
                ImageUrl = null, // Will use news-general.png fallback
                Author = "Asheron",
                PublishedAt = DateTime.UtcNow.AddHours(-2),
                Category = NewsCategory.Launcher,
                Url = "https://oldportal.com"
            },
            new NewsDto
            {
                Id = Guid.NewGuid(),
                Title = "The Portal Stones Resonate",
                Excerpt = "Travelers report increased activity across the portal network. Multiple worlds now accessible through the OldPortal Launcher.",
                ImageUrl = null, // Will use news-general.png fallback
                Author = "Elysa Strathelar",
                PublishedAt = DateTime.UtcNow.AddDays(-1),
                Category = NewsCategory.Server,
                Url = "https://oldportal.com"
            },
            new NewsDto
            {
                Id = Guid.NewGuid(),
                Title = "Isparian Gathering",
                Excerpt = "Join fellow adventurers in the OldPortal community. Share your tales, strategies, and discoveries from across the shards of Dereth.",
                ImageUrl = null, // Will use news-general.png fallback
                Author = "Nuhmudira",
                PublishedAt = DateTime.UtcNow.AddDays(-3),
                Category = NewsCategory.Community,
                Url = "https://oldportal.com"
            }
        };
    }

    /// <summary>
    /// Command to play the featured server.
    /// </summary>
    [RelayCommand]
    private void PlayFeaturedServer()
    {
        if (FeaturedServer == null)
        {
            _logger.Warning("PlayFeaturedServer command invoked but no featured server available");
            return;
        }

        _logger.Information("Navigate to featured server details: {ServerName}", FeaturedServer.Name);
        _navigationService.NavigateTo<WorldDetailViewModel>(FeaturedServer);
    }

    /// <summary>
    /// Command to open a news item URL in the browser.
    /// </summary>
    [RelayCommand]
    private void OpenNewsUrl(NewsDto newsItem)
    {
        if (newsItem == null || string.IsNullOrWhiteSpace(newsItem.Url))
        {
            _logger.Warning("OpenNewsUrl command invoked but news item or URL is null");
            return;
        }

        _logger.Information("Opening news URL: {Url}", newsItem.Url);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = newsItem.Url,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening news URL: {Url}", newsItem.Url);
            SetError("Failed to open news link.");
        }
    }

    /// <summary>
    /// Command to navigate to the Browse Worlds view.
    /// </summary>
    [RelayCommand]
    private void BrowseAllServers()
    {
        _logger.Information("Navigate to Browse Worlds from Home");
        _navigationService.NavigateTo<WorldsBrowseViewModel>();
    }

    /// <summary>
    /// Command to navigate to a server details view.
    /// </summary>
    [RelayCommand]
    private void ViewServerDetails(WorldDto server)
    {
        if (server == null)
        {
            _logger.Warning("ViewServerDetails command invoked but server is null");
            return;
        }

        _logger.Information("Navigate to server details: {ServerName}", server.Name);
        _navigationService.NavigateTo<WorldDetailViewModel>(server);
    }

    /// <summary>
    /// Command to toggle favorite status of the featured server.
    /// </summary>
    [RelayCommand]
    private void ToggleFeaturedServerFavorite()
    {
        if (FeaturedServer == null)
        {
            _logger.Warning("ToggleFeaturedServerFavorite command invoked but no featured server available");
            return;
        }

        // Toggle favorite status
        bool newStatus = _favoritesService.ToggleFavorite(
            FeaturedServer.ServerId,
            null,
            FeaturedServer.Name,
            false); // Not a manual server

        // Update local property
        IsFeaturedServerFavorite = newStatus;

        _logger.Information("Featured server favorite toggled: {ServerName}, IsFavorite: {IsFavorite}",
            FeaturedServer.Name, IsFeaturedServerFavorite);
    }

    /// <summary>
    /// API response wrapper for news endpoint.
    /// </summary>
    private class NewsApiResponse
    {
        public List<NewsDto> News { get; set; } = new();
    }
}

/// <summary>
/// Model for cached news entries in the database.
/// </summary>
public class CachedNewsEntry
{
    public int Id { get; set; }
    public string CacheKey { get; set; } = string.Empty;
    public List<NewsDto> News { get; set; } = new();
    public DateTime CachedAt { get; set; }
}
