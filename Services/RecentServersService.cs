// TODO: [LAUNCH-128] Phase 3 Week 6 - RecentServersService
// Component: Launcher
// Module: UI Redesign - Server Details & Favorites
// Description: Service for tracking recently played servers using centralized LiteDB

using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using OPLauncher.Models;

namespace OPLauncher.Services;

/// <summary>
/// Service for tracking recently played servers in centralized LiteDB.
/// Automatically records when user connects to servers.
/// </summary>
public class RecentServersService
{
    private readonly LoggingService _logger;
    private readonly DatabaseService _databaseService;
    private const string CollectionName = "recent_servers";
    private const int MaxRecentServers = 20; // Keep last 20 servers

    public RecentServersService(LoggingService logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
        _logger.Debug("RecentServersService initialized using centralized database");
    }

    /// <summary>
    /// Records that user played on a server.
    /// Updates existing entry or creates new one.
    /// </summary>
    public void RecordServerPlayed(Guid? worldServerId, int? manualServerId, string serverName, bool isManualServer)
    {
        _databaseService.WithCollection<RecentServer>(CollectionName, collection =>
        {
            // Find existing entry
            var existing = collection.FindOne(r =>
                (r.WorldServerId == worldServerId && worldServerId.HasValue) ||
                (r.ManualServerId == manualServerId && manualServerId.HasValue));

            if (existing != null)
            {
                // Update existing entry
                existing.LastPlayedAt = DateTime.UtcNow;
                existing.PlayCount++;
                existing.ServerName = serverName; // Update name in case it changed
                collection.Update(existing);

                _logger.Debug("Updated recent server {ServerName}, play count: {PlayCount}",
                    serverName, existing.PlayCount);
            }
            else
            {
                // Create new entry
                var recent = new RecentServer
                {
                    WorldServerId = worldServerId,
                    ManualServerId = manualServerId,
                    ServerName = serverName,
                    IsManualServer = isManualServer,
                    LastPlayedAt = DateTime.UtcNow,
                    PlayCount = 1
                };

                collection.Insert(recent);
                _logger.Information("Added new recent server {ServerName}", serverName);

                // Clean up old entries if we exceed max
                CleanupOldEntries(collection);
            }
        });
    }

    /// <summary>
    /// Gets all recent servers ordered by LastPlayedAt descending.
    /// </summary>
    public List<RecentServer> GetRecentServers(int limit = MaxRecentServers)
    {
        return _databaseService.WithCollection<RecentServer, List<RecentServer>>(CollectionName, collection =>
        {
            return collection.Query()
                .OrderByDescending(r => r.LastPlayedAt)
                .Limit(limit)
                .ToList();
        });
    }

    /// <summary>
    /// Gets the most recently played server.
    /// </summary>
    public RecentServer? GetMostRecentServer()
    {
        return _databaseService.WithCollection<RecentServer, RecentServer?>(CollectionName, collection =>
        {
            return collection.Query()
                .OrderByDescending(r => r.LastPlayedAt)
                .FirstOrDefault();
        });
    }

    /// <summary>
    /// Checks if a server has been played recently (within last 7 days).
    /// </summary>
    public bool WasPlayedRecently(Guid? worldServerId, int? manualServerId, int withinDays = 7)
    {
        return _databaseService.WithCollection<RecentServer, bool>(CollectionName, collection =>
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-withinDays);

            var recent = collection.FindOne(r =>
                ((r.WorldServerId == worldServerId && worldServerId.HasValue) ||
                 (r.ManualServerId == manualServerId && manualServerId.HasValue)) &&
                r.LastPlayedAt >= cutoffDate);

            return recent != null;
        });
    }

    /// <summary>
    /// Gets total play count for a server.
    /// </summary>
    public int GetServerPlayCount(Guid? worldServerId, int? manualServerId)
    {
        return _databaseService.WithCollection<RecentServer, int>(CollectionName, collection =>
        {
            var recent = collection.FindOne(r =>
                (r.WorldServerId == worldServerId && worldServerId.HasValue) ||
                (r.ManualServerId == manualServerId && manualServerId.HasValue));

            return recent?.PlayCount ?? 0;
        });
    }

    /// <summary>
    /// Clears all recent server history.
    /// </summary>
    public void ClearRecentServers()
    {
        var deletedCount = _databaseService.WithCollection<RecentServer, int>(CollectionName, collection =>
        {
            return collection.DeleteAll();
        });

        _logger.Information("Cleared {Count} recent servers", deletedCount);
    }

    /// <summary>
    /// Removes old entries to maintain max limit.
    /// </summary>
    private void CleanupOldEntries(ILiteCollection<RecentServer> collection)
    {
        var totalCount = collection.Count();

        if (totalCount > MaxRecentServers)
        {
            // Get IDs of servers to delete (oldest ones beyond limit)
            var toDelete = collection.Query()
                .OrderByDescending(r => r.LastPlayedAt)
                .Skip(MaxRecentServers)
                .ToEnumerable()
                .Select(r => r.Id)
                .ToList();

            foreach (var id in toDelete)
            {
                collection.Delete(id);
            }

            _logger.Debug("Cleaned up {Count} old recent server entries", toDelete.Count);
        }
    }

    /// <summary>
    /// Gets count of recent servers.
    /// </summary>
    public int GetRecentCount()
    {
        return _databaseService.WithCollection<RecentServer, int>(CollectionName, collection =>
        {
            return collection.Count();
        });
    }
}
