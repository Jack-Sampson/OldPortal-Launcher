using System.Net.Http.Json;
using LiteDB;
using OPLauncher.Models;
using OPLauncher.DTOs;
using OPLauncher.Utilities;

namespace OPLauncher.Services;

/// <summary>
/// Service for managing world/server data with LiteDB caching and offline mode support.
/// Implements a 24-hour cache TTL for world data to reduce API calls and improve performance.
/// </summary>
public class WorldsService
{
    private readonly HttpClient _httpClient;
    private readonly ConfigService _configService;
    private readonly LoggingService _logger;
    private readonly string _databasePath;
    private readonly object _cacheLock = new();

    private const string WorldsCacheCollection = "worlds_cache";
    private const string ConnectionInfoCacheCollection = "connection_info_cache";
    private const string FeaturedWorldsCacheKey = "featured_worlds";
    private const string AllWorldsCacheKey = "all_worlds";

    /// <summary>
    /// Gets whether the API is currently reachable based on the last request.
    /// </summary>
    public bool IsOnline { get; private set; } = true;

    /// <summary>
    /// Gets the timestamp of the last successful API call (UTC).
    /// </summary>
    public DateTime? LastApiSuccess { get; private set; }

    /// <summary>
    /// Gets the timestamp of the last API failure (UTC).
    /// </summary>
    public DateTime? LastApiFailure { get; private set; }

    /// <summary>
    /// Initializes a new instance of the WorldsService.
    /// </summary>
    /// <param name="httpClient">The HTTP client for API communication.</param>
    /// <param name="configService">The configuration service for API URL and settings.</param>
    /// <param name="logger">The logging service for diagnostics.</param>
    public WorldsService(HttpClient httpClient, ConfigService configService, LoggingService logger)
    {
        _httpClient = httpClient;
        _configService = configService;
        _logger = logger;

        // Set up database path
        var configDir = _configService.GetConfigDirectory();
        _databasePath = Path.Combine(configDir, "worlds_cache.db");

        _logger.Debug("WorldsService initialized with database path: {DatabasePath}", _databasePath);

        // Initialize database
        InitializeDatabase();
    }

    /// <summary>
    /// Gets the list of featured worlds.
    /// Uses cached data if available and not expired, otherwise fetches from API.
    /// Falls back to cached data (even if expired) if API is unavailable (offline mode).
    /// </summary>
    /// <returns>List of featured worlds, or empty list if unavailable.</returns>
    public async Task<List<WorldDto>> GetFeaturedWorldsAsync()
    {
        try
        {
            _logger.Information("Fetching featured worlds");

            // Try to get from cache first
            var cachedWorlds = GetCachedWorldList(FeaturedWorldsCacheKey);
            if (cachedWorlds != null && !IsCacheExpired(cachedWorlds))
            {
                _logger.Debug("Returning {Count} featured worlds from cache (age: {Age})",
                    cachedWorlds.Count, GetCacheAge(cachedWorlds));
                return cachedWorlds.Select(cw => cw.World).ToList();
            }

            // Cache expired or not found - fetch from API
            var endpoint = ApiEndpoints.Worlds.Featured;
            _logger.Debug("Cache expired or not found, fetching featured worlds from API: {Endpoint}", endpoint);
            var response = await _httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                RecordApiFailure();
                _logger.Warning("Failed to fetch featured worlds from API: {StatusCode} (URL: {Url})",
                    response.StatusCode, endpoint);
                return HandleOfflineMode(cachedWorlds, "featured worlds");
            }

            var worldListResponse = await response.Content.ReadFromJsonAsync<WorldListResponseDto>();
            if (worldListResponse == null)
            {
                RecordApiFailure();
                _logger.Warning("API returned null for featured worlds");
                return HandleOfflineMode(cachedWorlds, "featured worlds");
            }

            // Cache the fresh data and record success (even if empty - that's a valid response)
            RecordApiSuccess();
            CacheWorldList(FeaturedWorldsCacheKey, worldListResponse.Worlds);

            if (worldListResponse.Worlds.Count == 0)
            {
                _logger.Information("Fetched 0 featured worlds from API (no matches)");
            }
            else
            {
                _logger.Information("Fetched {Count} featured worlds from API", worldListResponse.Worlds.Count);
            }

            return worldListResponse.Worlds;
        }
        catch (HttpRequestException ex)
        {
            RecordApiFailure();
            _logger.Error(ex, "HTTP error while fetching featured worlds");
            var cachedWorlds = GetCachedWorldList(FeaturedWorldsCacheKey);
            return HandleOfflineMode(cachedWorlds, "featured worlds");
        }
        catch (Exception ex)
        {
            RecordApiFailure();
            _logger.Error(ex, "Unexpected error while fetching featured worlds");
            return new List<WorldDto>();
        }
    }

    /// <summary>
    /// Gets all worlds, optionally filtered by search query and ruleset.
    /// Uses cached data if available and not expired, otherwise fetches from API.
    /// Falls back to cached data (even if expired) if API is unavailable (offline mode).
    /// </summary>
    /// <param name="search">Optional search query to filter world names/descriptions.</param>
    /// <param name="ruleSet">Optional ruleset filter.</param>
    /// <returns>List of worlds matching the criteria, or empty list if unavailable.</returns>
    public async Task<List<WorldDto>> GetAllWorldsAsync(string? search = null, RuleSet? ruleSet = null)
    {
        try
        {
            _logger.Information("Fetching all worlds (search: {Search}, ruleset: {RuleSet})",
                search ?? "none", ruleSet?.ToString() ?? "none");

            // Build cache key based on filters
            var cacheKey = BuildCacheKey(AllWorldsCacheKey, search, ruleSet);

            // Try to get from cache first
            var cachedWorlds = GetCachedWorldList(cacheKey);
            if (cachedWorlds != null && !IsCacheExpired(cachedWorlds))
            {
                _logger.Debug("Returning {Count} worlds from cache (age: {Age})",
                    cachedWorlds.Count, GetCacheAge(cachedWorlds));
                return cachedWorlds.Select(cw => cw.World).ToList();
            }

            // Cache expired or not found - fetch from API
            // Build query string
            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(search))
                queryParams.Add($"search={Uri.EscapeDataString(search)}");
            if (ruleSet.HasValue)
                queryParams.Add($"ruleSet={ruleSet.Value}");

            var endpoint = ApiEndpoints.Worlds.List;
            if (queryParams.Count > 0)
                endpoint += "?" + string.Join("&", queryParams);

            _logger.Debug("Cache expired or not found, fetching worlds from API: {Endpoint}", endpoint);
            var response = await _httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                RecordApiFailure();
                _logger.Warning("Failed to fetch worlds from API: {StatusCode} (URL: {Url})",
                    response.StatusCode, endpoint);
                return HandleOfflineMode(cachedWorlds, "worlds");
            }

            var worldListResponse = await response.Content.ReadFromJsonAsync<WorldListResponseDto>();
            if (worldListResponse == null)
            {
                RecordApiFailure();
                _logger.Warning("API returned null for worlds list");
                return HandleOfflineMode(cachedWorlds, "worlds");
            }

            // Cache the fresh data and record success (even if empty - that's a valid response)
            RecordApiSuccess();
            CacheWorldList(cacheKey, worldListResponse.Worlds);

            if (worldListResponse.Worlds.Count == 0)
            {
                _logger.Information("Fetched 0 worlds from API (no matches for current filters)");
            }
            else
            {
                _logger.Information("Fetched {Count} worlds from API", worldListResponse.Worlds.Count);
            }

            return worldListResponse.Worlds;
        }
        catch (HttpRequestException ex)
        {
            RecordApiFailure();
            _logger.Error(ex, "HTTP error while fetching worlds");
            var cacheKey = BuildCacheKey(AllWorldsCacheKey, search, ruleSet);
            var cachedWorlds = GetCachedWorldList(cacheKey);
            return HandleOfflineMode(cachedWorlds, "worlds");
        }
        catch (Exception ex)
        {
            RecordApiFailure();
            _logger.Error(ex, "Unexpected error while fetching worlds");
            return new List<WorldDto>();
        }
    }

    /// <summary>
    /// Gets a specific world by its server ID.
    /// Uses cached data if available and not expired, otherwise fetches from API.
    /// Falls back to cached data (even if expired) if API is unavailable (offline mode).
    /// </summary>
    /// <param name="serverId">The unique server identifier (Guid).</param>
    /// <returns>The world data, or null if not found.</returns>
    public async Task<WorldDto?> GetWorldByIdAsync(Guid serverId)
    {
        try
        {
            _logger.Information("Fetching world by server ID: {ServerId}", serverId);

            var cacheKey = $"world_{serverId}";

            // Try to get from cache first
            var cachedWorld = GetCachedWorld(cacheKey);
            if (cachedWorld != null && !cachedWorld.IsExpired())
            {
                _logger.Debug("Returning world {ServerId} from cache (age: {Age})",
                    serverId, cachedWorld.GetAge());
                return cachedWorld.World;
            }

            // Cache expired or not found - fetch from API
            _logger.Debug("Cache expired or not found, fetching world {ServerId} from API", serverId);

            // Use Guid ServerId to fetch from API
            var endpoint = $"{ApiEndpoints.BaseUrl}/servers/{serverId}";
            var response = await _httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                RecordApiFailure();
                _logger.Warning("Failed to fetch world {ServerId} from API: {StatusCode}", serverId, response.StatusCode);

                // Use expired cache if available (offline mode)
                if (cachedWorld != null)
                {
                    _logger.Information("Using expired cached data for world {ServerId} (offline mode)", serverId);
                    return cachedWorld.World;
                }

                return null;
            }

            var world = await response.Content.ReadFromJsonAsync<WorldDto>();
            if (world == null)
            {
                RecordApiFailure();
                _logger.Warning("API returned null for world {ServerId}", serverId);
                return cachedWorld?.World;
            }

            // Cache the fresh data and record success
            RecordApiSuccess();
            CacheWorld(cacheKey, world);

            _logger.Information("Fetched world {ServerId} from API", serverId);
            return world;
        }
        catch (HttpRequestException ex)
        {
            RecordApiFailure();
            _logger.Error(ex, "HTTP error while fetching world {ServerId}", serverId);
            var cacheKey = $"world_{serverId}";
            var cachedWorld = GetCachedWorld(cacheKey);
            if (cachedWorld != null)
            {
                _logger.Information("Using cached data for world {ServerId} (offline mode)", serverId);
                return cachedWorld.World;
            }
            return null;
        }
        catch (Exception ex)
        {
            RecordApiFailure();
            _logger.Error(ex, "Unexpected error while fetching world {ServerId}", serverId);
            return null;
        }
    }

    /// <summary>
    /// Gets connection information for a specific world.
    /// Uses the world's existing data (host, port, serverType) instead of a separate API call.
    /// </summary>
    /// <param name="serverId">The unique server identifier (Guid).</param>
    /// <returns>Connection information, or null if world not found.</returns>
    public async Task<WorldConnectionDto?> GetConnectionInfoAsync(Guid serverId)
    {
        try
        {
            _logger.Information("Getting connection info for server {ServerId}", serverId);

            // Try to get from cache first (if not expired)
            var cachedInfo = GetCachedConnectionInfo(serverId);
            if (cachedInfo != null && !cachedInfo.IsExpired())
            {
                _logger.Debug("Returning connection info for server {ServerId} from cache (age: {Age})",
                    serverId, cachedInfo.GetFormattedAge());
                return cachedInfo.ConnectionInfo;
            }

            // Cache expired or not found - get world data and extract connection info
            // The world data already contains all connection information (host, port, serverType)
            var world = await GetWorldByIdAsync(serverId);

            if (world == null)
            {
                _logger.Warning("Server {ServerId} not found, cannot get connection info", serverId);

                // Try to use expired cache as fallback (offline mode)
                if (cachedInfo != null)
                {
                    _logger.Information("Using cached connection info for server {ServerId} (offline mode, cached {Age})",
                        serverId, cachedInfo.GetFormattedAge());
                    return cachedInfo.ConnectionInfo;
                }

                return null;
            }

            // Extract connection info from world data
            var connectionInfo = world.ToConnectionInfo();

            // Validate that connection info has required fields (Host and valid Port)
            // This prevents returning incomplete data when server is registered but not configured
            if (string.IsNullOrWhiteSpace(connectionInfo.Host) ||
                connectionInfo.Port <= 0 ||
                connectionInfo.Port > 65535)
            {
                RecordApiFailure();
                _logger.Warning("Server {ServerId} has incomplete connection info: Host='{Host}', Port={Port}",
                    serverId, connectionInfo.Host ?? "null", connectionInfo.Port);

                // Try to use cached info as fallback (may have been configured previously)
                if (cachedInfo != null &&
                    !string.IsNullOrWhiteSpace(cachedInfo.ConnectionInfo.Host) &&
                    cachedInfo.ConnectionInfo.Port > 0)
                {
                    _logger.Information("Using cached connection info for server {ServerId} (incomplete API data)", serverId);
                    return cachedInfo.ConnectionInfo;
                }

                // No valid connection info available
                return null;
            }

            // Record success and cache the fresh data
            RecordApiSuccess();
            CacheConnectionInfo(serverId, connectionInfo);

            _logger.Information("Fetched connection info for server {ServerId}: {Host}:{Port}",
                serverId, connectionInfo.Host, connectionInfo.Port);
            return connectionInfo;
        }
        catch (HttpRequestException ex)
        {
            RecordApiFailure();
            _logger.Error(ex, "HTTP error while fetching connection info for server {ServerId}", serverId);

            // Try to use cached info as fallback (offline mode)
            var cachedInfo = GetCachedConnectionInfo(serverId);
            if (cachedInfo != null)
            {
                _logger.Information("Using cached connection info for server {ServerId} (offline mode, cached {Age})",
                    serverId, cachedInfo.GetFormattedAge());
                return cachedInfo.ConnectionInfo;
            }

            return null;
        }
        catch (Exception ex)
        {
            RecordApiFailure();
            _logger.Error(ex, "Unexpected error while fetching connection info for server {ServerId}", serverId);
            return null;
        }
    }

    /// <summary>
    /// Clears all cached world data and connection info.
    /// Useful for forcing a refresh or clearing stale data.
    /// </summary>
    public void ClearCache()
    {
        try
        {
            lock (_cacheLock)
            {
                using var db = new LiteDatabase(_databasePath);

                // Clear world cache
                var worldsCollection = db.GetCollection<CachedWorldListEntry>(WorldsCacheCollection);
                var worldsDeleted = worldsCollection.DeleteAll();

                // Clear connection info cache
                var connectionCollection = db.GetCollection<CachedConnectionInfo>(ConnectionInfoCacheCollection);
                var connectionsDeleted = connectionCollection.DeleteAll();

                _logger.Information("Cleared cache ({WorldsCount} worlds, {ConnectionCount} connection entries deleted)",
                    worldsDeleted, connectionsDeleted);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error while clearing cache");
        }
    }

    /// <summary>
    /// Clears expired cache entries to keep database clean.
    /// Called periodically or manually.
    /// </summary>
    public void ClearExpiredCache()
    {
        try
        {
            lock (_cacheLock)
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<CachedWorldListEntry>(WorldsCacheCollection);

                var allEntries = collection.FindAll().ToList();
                var expiredCount = 0;

                foreach (var entry in allEntries)
                {
                    if (entry.CachedAt.AddMinutes(5) < DateTime.UtcNow)
                    {
                        collection.Delete(entry.Id);
                        expiredCount++;
                    }
                }

                _logger.Information("Cleared {Count} expired cache entries", expiredCount);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error while clearing expired cache");
        }
    }

    #region Private Cache Methods

    private void InitializeDatabase()
    {
        try
        {
            lock (_cacheLock)
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<CachedWorldListEntry>(WorldsCacheCollection);
                collection.EnsureIndex(x => x.CacheKey);

                _logger.Debug("LiteDB world cache initialized");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize world cache database");
        }
    }

    private void CacheWorld(string cacheKey, WorldDto world)
    {
        try
        {
            lock (_cacheLock)
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<CachedWorldListEntry>(WorldsCacheCollection);

                var entry = new CachedWorldListEntry
                {
                    CacheKey = cacheKey,
                    Worlds = new List<CachedWorld> { CachedWorld.FromWorld(world) },
                    CachedAt = DateTime.UtcNow
                };

                collection.Upsert(entry);
                _logger.Debug("Cached world with key: {CacheKey}", cacheKey);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to cache world with key: {CacheKey}", cacheKey);
        }
    }

    private void CacheWorldList(string cacheKey, List<WorldDto> worlds)
    {
        try
        {
            lock (_cacheLock)
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<CachedWorldListEntry>(WorldsCacheCollection);

                var entry = new CachedWorldListEntry
                {
                    CacheKey = cacheKey,
                    Worlds = worlds.Select(w => CachedWorld.FromWorld(w)).ToList(),
                    CachedAt = DateTime.UtcNow
                };

                collection.Upsert(entry);
                _logger.Debug("Cached {Count} worlds with key: {CacheKey}", worlds.Count, cacheKey);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to cache worlds with key: {CacheKey}", cacheKey);
        }
    }

    private CachedWorld? GetCachedWorld(string cacheKey)
    {
        try
        {
            lock (_cacheLock)
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<CachedWorldListEntry>(WorldsCacheCollection);

                var entry = collection.FindOne(x => x.CacheKey == cacheKey);
                return entry?.Worlds.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to get cached world with key: {CacheKey}", cacheKey);
            return null;
        }
    }

    private List<CachedWorld>? GetCachedWorldList(string cacheKey)
    {
        try
        {
            lock (_cacheLock)
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<CachedWorldListEntry>(WorldsCacheCollection);

                var entry = collection.FindOne(x => x.CacheKey == cacheKey);
                return entry?.Worlds;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to get cached world list with key: {CacheKey}", cacheKey);
            return null;
        }
    }

    private bool IsCacheExpired(List<CachedWorld>? cachedWorlds)
    {
        if (cachedWorlds == null || cachedWorlds.Count == 0)
            return true;

        return cachedWorlds.First().IsExpired();
    }

    private string GetCacheAge(List<CachedWorld> cachedWorlds)
    {
        if (cachedWorlds == null || cachedWorlds.Count == 0)
            return "N/A";

        var age = cachedWorlds.First().GetAge();
        return $"{age.TotalMinutes:F1} minutes";
    }

    private string BuildCacheKey(string baseKey, string? search, RuleSet? ruleSet)
    {
        var parts = new List<string> { baseKey };
        if (!string.IsNullOrWhiteSpace(search))
            parts.Add($"search_{search}");
        if (ruleSet.HasValue)
            parts.Add($"ruleset_{ruleSet.Value}");

        return string.Join("_", parts);
    }

    private List<WorldDto> HandleOfflineMode(List<CachedWorld>? cachedWorlds, string dataType)
    {
        if (cachedWorlds != null && cachedWorlds.Count > 0)
        {
            _logger.Information("Using expired cached data for {DataType} (offline mode)", dataType);
            return cachedWorlds.Select(cw => cw.World).ToList();
        }

        _logger.Warning("No cached {DataType} available for offline mode", dataType);
        return new List<WorldDto>();
    }

    private void RecordApiSuccess()
    {
        IsOnline = true;
        LastApiSuccess = DateTime.UtcNow;
        _logger.Debug("API request succeeded - service is online");
    }

    private void RecordApiFailure()
    {
        IsOnline = false;
        LastApiFailure = DateTime.UtcNow;
        _logger.Debug("API request failed - service is offline");
    }

    private void CacheConnectionInfo(Guid serverId, WorldConnectionDto connectionInfo)
    {
        try
        {
            lock (_cacheLock)
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<CachedConnectionInfo>(ConnectionInfoCacheCollection);
                collection.EnsureIndex(x => x.ServerId);

                var cached = CachedConnectionInfo.FromConnectionInfo(serverId, connectionInfo);
                collection.Upsert(cached);

                _logger.Debug("Cached connection info for server {ServerId}", serverId);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to cache connection info for server {ServerId}", serverId);
        }
    }

    private CachedConnectionInfo? GetCachedConnectionInfo(Guid serverId)
    {
        try
        {
            lock (_cacheLock)
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<CachedConnectionInfo>(ConnectionInfoCacheCollection);

                return collection.FindOne(x => x.ServerId == serverId);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to get cached connection info for server {ServerId}", serverId);
            return null;
        }
    }

    /// <summary>
    /// Gets a formatted cache age string for display in the UI.
    /// Returns null if no cache exists or if currently online with fresh data.
    /// </summary>
    /// <param name="cacheKey">The cache key to check age for.</param>
    /// <returns>Formatted string like "Last updated 2 hours ago" or null.</returns>
    public string? GetFormattedCacheAge(string cacheKey = AllWorldsCacheKey)
    {
        try
        {
            var cachedWorlds = GetCachedWorldList(cacheKey);
            if (cachedWorlds == null || cachedWorlds.Count == 0)
                return null;

            var age = cachedWorlds.First().GetAge();

            if (age.TotalMinutes < 1)
                return "Last updated just now";
            if (age.TotalMinutes < 60)
                return $"Last updated {(int)age.TotalMinutes} minute{((int)age.TotalMinutes == 1 ? "" : "s")} ago";
            if (age.TotalHours < 24)
                return $"Last updated {(int)age.TotalHours} hour{((int)age.TotalHours == 1 ? "" : "s")} ago";
            if (age.TotalDays < 7)
                return $"Last updated {(int)age.TotalDays} day{((int)age.TotalDays == 1 ? "" : "s")} ago";

            return $"Last updated {(int)age.TotalDays} days ago";
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to get formatted cache age for key: {CacheKey}", cacheKey);
            return null;
        }
    }

    #endregion

    #region Cache Entry Models

    /// <summary>
    /// Internal model for storing cached world lists in LiteDB.
    /// </summary>
    private class CachedWorldListEntry
    {
        public int Id { get; set; }
        public string CacheKey { get; set; } = string.Empty;
        public List<CachedWorld> Worlds { get; set; } = new();
        public DateTime CachedAt { get; set; }
    }

    #endregion
}
