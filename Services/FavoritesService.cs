// TODO: [LAUNCH-126] Phase 3 Week 6 - FavoritesService
// Component: Launcher
// Module: UI Redesign - Server Details & Favorites
// Description: Service for managing favorite servers using centralized LiteDB

using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using OPLauncher.Models;

namespace OPLauncher.Services;

/// <summary>
/// Service for managing favorite servers stored in centralized LiteDB.
/// Supports both API-synced servers and manual servers.
/// </summary>
public class FavoritesService
{
    private readonly LoggingService _logger;
    private readonly DatabaseService _databaseService;
    private const string CollectionName = "favorites";

    public FavoritesService(LoggingService logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
        _logger.Debug("FavoritesService initialized using centralized database");
    }

    /// <summary>
    /// Adds a server to favorites.
    /// </summary>
    public void AddFavorite(Guid? worldServerId, int? manualServerId, string serverName, bool isManualServer)
    {
        _databaseService.WithCollection<FavoriteServer>(CollectionName, collection =>
        {
            // Check if already favorited
            var existing = collection.FindOne(f =>
                (f.WorldServerId == worldServerId && worldServerId.HasValue) ||
                (f.ManualServerId == manualServerId && manualServerId.HasValue));

            if (existing != null)
            {
                _logger.Debug("Server {ServerName} is already favorited", serverName);
                return;
            }

            var favorite = new FavoriteServer
            {
                WorldServerId = worldServerId,
                ManualServerId = manualServerId,
                ServerName = serverName,
                IsManualServer = isManualServer,
                AddedAt = DateTime.UtcNow,
                SortOrder = 0
            };

            collection.Insert(favorite);
            _logger.Information("Added server {ServerName} to favorites", serverName);
        });
    }

    /// <summary>
    /// Removes a server from favorites.
    /// </summary>
    public void RemoveFavorite(Guid? worldServerId, int? manualServerId)
    {
        var deleted = _databaseService.WithCollection<FavoriteServer, int>(CollectionName, collection =>
        {
            return collection.DeleteMany(f =>
                (f.WorldServerId == worldServerId && worldServerId.HasValue) ||
                (f.ManualServerId == manualServerId && manualServerId.HasValue));
        });

        _logger.Information("Removed {Count} favorite(s)", deleted);
    }

    /// <summary>
    /// Checks if a server is favorited.
    /// </summary>
    public bool IsFavorite(Guid? worldServerId, int? manualServerId)
    {
        return _databaseService.WithCollection<FavoriteServer, bool>(CollectionName, collection =>
        {
            return collection.Exists(f =>
                (f.WorldServerId == worldServerId && worldServerId.HasValue) ||
                (f.ManualServerId == manualServerId && manualServerId.HasValue));
        });
    }

    /// <summary>
    /// Gets all favorite servers ordered by AddedAt descending.
    /// </summary>
    public List<FavoriteServer> GetAllFavorites()
    {
        return _databaseService.WithCollection<FavoriteServer, List<FavoriteServer>>(CollectionName, collection =>
        {
            return collection.Query()
                .OrderByDescending(f => f.AddedAt)
                .ToList();
        });
    }

    /// <summary>
    /// Gets favorite server IDs for API servers.
    /// Used to quickly check if servers in browse view are favorited.
    /// </summary>
    public HashSet<Guid> GetFavoriteWorldServerIds()
    {
        return _databaseService.WithCollection<FavoriteServer, HashSet<Guid>>(CollectionName, collection =>
        {
            return collection.Query()
                .Where(f => f.WorldServerId.HasValue)
                .Select(f => f.WorldServerId!.Value)
                .ToEnumerable()
                .ToHashSet();
        });
    }

    /// <summary>
    /// Gets favorite server IDs for manual servers.
    /// </summary>
    public HashSet<int> GetFavoriteManualServerIds()
    {
        return _databaseService.WithCollection<FavoriteServer, HashSet<int>>(CollectionName, collection =>
        {
            return collection.Query()
                .Where(f => f.ManualServerId.HasValue)
                .Select(f => f.ManualServerId!.Value)
                .ToEnumerable()
                .ToHashSet();
        });
    }

    /// <summary>
    /// Toggles favorite status for a server.
    /// </summary>
    public bool ToggleFavorite(Guid? worldServerId, int? manualServerId, string serverName, bool isManualServer)
    {
        if (IsFavorite(worldServerId, manualServerId))
        {
            RemoveFavorite(worldServerId, manualServerId);
            return false; // Not favorited anymore
        }
        else
        {
            AddFavorite(worldServerId, manualServerId, serverName, isManualServer);
            return true; // Now favorited
        }
    }

    /// <summary>
    /// Gets count of favorite servers.
    /// </summary>
    public int GetFavoriteCount()
    {
        return _databaseService.WithCollection<FavoriteServer, int>(CollectionName, collection =>
        {
            return collection.Count();
        });
    }
}
