// TODO: [LAUNCH-114] Phase 2 Week 5 - NewsViewModel
// Component: Launcher
// Module: UI Redesign - Home View & News Feed
// Description: Full news feed view model with filtering and pagination

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
/// View model for the full News feed view.
/// Displays all news items with category filtering and pagination.
/// </summary>
public partial class NewsViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly LoggingService _logger;
    private readonly HttpClient _httpClient;

    private List<NewsDto> _allNews = new();
    private int _currentPage = 0;
    private const int PageSize = 6; // Load 6 news items per page

    /// <summary>
    /// Displayed news items (filtered and paginated).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<NewsDto> _newsItems = new();

    /// <summary>
    /// Selected category filter (null = All categories).
    /// </summary>
    [ObservableProperty]
    private NewsCategory? _selectedCategory;

    /// <summary>
    /// Whether news is currently loading.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Whether more news items are available to load.
    /// </summary>
    [ObservableProperty]
    private bool _hasMoreItems;

    /// <summary>
    /// Total count of news items (for display).
    /// </summary>
    [ObservableProperty]
    private int _totalNewsCount;

    /// <summary>
    /// Currently displayed count (for display).
    /// </summary>
    [ObservableProperty]
    private int _displayedNewsCount;

    /// <summary>
    /// Available category filter options for dropdown.
    /// </summary>
    public ObservableCollection<CategoryFilterOption> CategoryFilters { get; }

    /// <summary>
    /// Selected category filter option.
    /// </summary>
    [ObservableProperty]
    private CategoryFilterOption? _selectedCategoryFilter;

    /// <summary>
    /// Called when selected category filter changes.
    /// </summary>
    partial void OnSelectedCategoryFilterChanged(CategoryFilterOption? value)
    {
        SelectedCategory = value?.Category;

        // Reset pagination and reload with new filter
        _currentPage = 0;
        NewsItems.Clear();
        LoadNextPage();
    }

    /// <summary>
    /// Initializes a new instance of the NewsViewModel.
    /// </summary>
    public NewsViewModel(
        INavigationService navigationService,
        LoggingService logger,
        HttpClient httpClient)
    {
        _navigationService = navigationService;
        _logger = logger;
        _httpClient = httpClient;

        // Initialize category filters
        CategoryFilters = new ObservableCollection<CategoryFilterOption>
        {
            new CategoryFilterOption { DisplayName = "📰 All News", Category = null },
            new CategoryFilterOption { DisplayName = "📰 General", Category = NewsCategory.General },
            new CategoryFilterOption { DisplayName = "📜 Lore", Category = NewsCategory.Lore },
            new CategoryFilterOption { DisplayName = "📖 Guide", Category = NewsCategory.Guide },
            new CategoryFilterOption { DisplayName = "🏛️ History", Category = NewsCategory.History },
            new CategoryFilterOption { DisplayName = "✨ Nostalgia", Category = NewsCategory.Nostalgia },
            new CategoryFilterOption { DisplayName = "👥 Community", Category = NewsCategory.Community },
            new CategoryFilterOption { DisplayName = "🚀 Launcher", Category = NewsCategory.Launcher },
            new CategoryFilterOption { DisplayName = "🌐 Server", Category = NewsCategory.Server },
            new CategoryFilterOption { DisplayName = "📢 Update", Category = NewsCategory.Update }
        };

        // Select "All News" by default
        SelectedCategoryFilter = CategoryFilters.First();

        _logger.Debug("NewsViewModel initialized");

        // Initial load will happen in OnNavigatedTo
    }

    /// <summary>
    /// Called when this view is navigated to.
    /// Refreshes news from API to ensure latest articles are displayed.
    /// </summary>
    /// <param name="parameter">Optional navigation parameter (unused)</param>
    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);
        _logger.Information("NewsView navigated to - refreshing news from API");

        // Refresh news every time the view is shown
        _ = LoadNewsAsync();
    }

    /// <summary>
    /// Loads news items from the API.
    /// Shows empty state if no news is available or API is unreachable.
    /// </summary>
    private async Task LoadNewsAsync()
    {
        try
        {
            IsLoading = true;

            // Try to load news from API
            var newsFromApi = await LoadNewsFromApiAsync();

            if (newsFromApi != null && newsFromApi.Any())
            {
                _allNews = newsFromApi;
                _logger.Information("Loaded {Count} news items from API", _allNews.Count);
            }
            else
            {
                // No news from API - show empty state instead of fallback
                _allNews = new List<NewsDto>();
                _logger.Warning("API returned no news - showing empty state");
            }

            // Reset pagination
            _currentPage = 0;
            NewsItems.Clear();

            // Load first page
            LoadNextPage();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading news from API");

            // Show error state - don't use fallback data
            _allNews = new List<NewsDto>();
            SetError("Unable to load news. Please check your internet connection and try again.");

            // Reset pagination
            _currentPage = 0;
            NewsItems.Clear();
            LoadNextPage();
        }
        finally
        {
            IsLoading = false;
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
            _logger.Debug("Fetching news from API: {Endpoint}", ApiEndpoints.News.GetWithLimit(50));

            var response = await _httpClient.GetAsync(ApiEndpoints.News.GetWithLimit(50));

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
    /// API response wrapper for news endpoint.
    /// </summary>
    private class NewsApiResponse
    {
        public List<NewsDto> News { get; set; } = new();
    }

    /// <summary>
    /// Loads the next page of news items based on current filter.
    /// </summary>
    private void LoadNextPage()
    {
        // Filter news by selected category
        var filteredNews = SelectedCategory.HasValue
            ? _allNews.Where(n => n.Category == SelectedCategory.Value).ToList()
            : _allNews;

        TotalNewsCount = filteredNews.Count;

        // Get items for current page
        var itemsToLoad = filteredNews
            .Skip(_currentPage * PageSize)
            .Take(PageSize)
            .ToList();

        // Add to displayed collection
        foreach (var newsItem in itemsToLoad)
        {
            NewsItems.Add(newsItem);
        }

        DisplayedNewsCount = NewsItems.Count;

        // Check if more items are available
        HasMoreItems = DisplayedNewsCount < TotalNewsCount;

        _logger.Debug("Loaded page {Page}, displaying {Displayed} of {Total} news items",
            _currentPage, DisplayedNewsCount, TotalNewsCount);
    }

    /// <summary>
    /// Command to load more news items.
    /// </summary>
    [RelayCommand]
    private async Task LoadMore()
    {
        if (!HasMoreItems || IsLoading)
            return;

        try
        {
            IsLoading = true;

            // Simulate API delay
            await Task.Delay(300);

            _currentPage++;
            LoadNextPage();

            _logger.Information("Loaded more news, page {Page}", _currentPage);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading more news");
            SetError("Failed to load more news.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Command to filter news by category.
    /// </summary>
    [RelayCommand]
    private void FilterByCategory(NewsCategory? category)
    {
        _logger.Information("Filter news by category: {Category}", category?.ToString() ?? "All");

        SelectedCategory = category;

        // Reset pagination and reload
        _currentPage = 0;
        NewsItems.Clear();
        LoadNextPage();
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

}

/// <summary>
/// Filter option for news category dropdown.
/// </summary>
public class CategoryFilterOption
{
    /// <summary>
    /// Display name for the category (with emoji).
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Associated news category (null = All categories).
    /// </summary>
    public NewsCategory? Category { get; set; }
}
