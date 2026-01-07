using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using OPLauncher.Models;

namespace OPLauncher.Services;

/// <summary>
/// Service for managing multi-client batch groups with LiteDB storage.
/// Batch groups are server-scoped launch profiles containing credential sequences.
/// </summary>
public class BatchGroupService
{
    private readonly ConfigService _configService;
    private readonly CredentialVaultService _credentialVaultService;
    private readonly LoggingService _logger;
    private readonly string _databasePath;
    private readonly object _lock = new();

    private const string BatchGroupsCollection = "batch_groups";
    private const string LaunchHistoryCollection = "launch_history";
    private const int MaxHistoryEntries = 50;

    /// <summary>
    /// Initializes a new instance of the BatchGroupService.
    /// </summary>
    /// <param name="configService">The configuration service for database path.</param>
    /// <param name="credentialVaultService">The credential vault service for validation.</param>
    /// <param name="logger">The logging service for diagnostics.</param>
    public BatchGroupService(
        ConfigService configService,
        CredentialVaultService credentialVaultService,
        LoggingService logger)
    {
        _configService = configService;
        _credentialVaultService = credentialVaultService;
        _logger = logger;

        // Set up database path
        var configDir = _configService.GetConfigDirectory();
        _databasePath = Path.Combine(configDir, "batch_groups.db");

        _logger.Debug("BatchGroupService initialized with database path: {DatabasePath}", _databasePath);

        // Initialize database
        InitializeDatabase();
    }

    /// <summary>
    /// Initializes the database and creates necessary indexes.
    /// </summary>
    private void InitializeDatabase()
    {
        try
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<BatchGroup>(BatchGroupsCollection);

                // Create index on WorldId for fast server-scoped queries
                collection.EnsureIndex(b => b.WorldId);

                _logger.Debug("BatchGroup database initialized successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error initializing BatchGroup database");
        }
    }

    /// <summary>
    /// Gets all batch groups for a specific server.
    /// This is the primary query method since batches are server-scoped.
    /// </summary>
    /// <param name="worldId">The world/server ID.</param>
    /// <returns>List of batch groups for the server, ordered by last used descending.</returns>
    public async Task<List<BatchGroup>> GetBatchGroupsForServerAsync(int worldId)
    {
        return await Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<BatchGroup>(BatchGroupsCollection);

                    var batches = collection.Find(b => b.WorldId == worldId).ToList();

                    // Sort by last used descending, then by name
                    batches = batches
                        .OrderByDescending(b => b.LastUsedDate ?? DateTime.MinValue)
                        .ThenBy(b => b.Name)
                        .ToList();

                    _logger.Debug("Retrieved {Count} batch groups for world {WorldId}", batches.Count, worldId);

                    return batches;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving batch groups for world {WorldId}", worldId);
                return new List<BatchGroup>();
            }
        });
    }

    /// <summary>
    /// Gets a specific batch group by ID.
    /// </summary>
    /// <param name="id">The batch group ID.</param>
    /// <returns>The batch group, or null if not found.</returns>
    public async Task<BatchGroup?> GetBatchGroupAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<BatchGroup>(BatchGroupsCollection);

                    var batch = collection.FindById(id);

                    _logger.Debug("Retrieved batch group {Id}: {Found}", id, batch != null);

                    return batch;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving batch group {Id}", id);
                return null;
            }
        });
    }

    /// <summary>
    /// Saves (inserts or updates) a batch group.
    /// </summary>
    /// <param name="batch">The batch group to save.</param>
    /// <returns>True if save was successful, false otherwise.</returns>
    public async Task<bool> SaveBatchGroupAsync(BatchGroup batch)
    {
        return await Task.Run(async () =>
        {
            try
            {
                // Validate before saving
                var validationErrors = await GetValidationErrorsAsync(batch);
                if (validationErrors.Count > 0)
                {
                    _logger.Warning("Batch group validation failed: {Errors}", string.Join(", ", validationErrors));
                    return false;
                }

                // Renumber entries to ensure sequential order
                batch.RenumberEntries();

                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<BatchGroup>(BatchGroupsCollection);

                    // Check if this is a new batch or update
                    var existing = collection.FindById(batch.Id);

                    if (existing != null)
                    {
                        // Update existing batch
                        collection.Update(batch);
                        _logger.Information("Updated batch group {Id}: {Name}", batch.Id, batch.Name);
                    }
                    else
                    {
                        // Insert new batch
                        collection.Insert(batch);
                        _logger.Information("Created new batch group {Id}: {Name}", batch.Id, batch.Name);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error saving batch group {Id}", batch.Id);
                return false;
            }
        });
    }

    /// <summary>
    /// Deletes a batch group.
    /// </summary>
    /// <param name="id">The batch group ID to delete.</param>
    /// <returns>True if delete was successful, false otherwise.</returns>
    public async Task<bool> DeleteBatchGroupAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<BatchGroup>(BatchGroupsCollection);

                    var result = collection.Delete(id);

                    if (result)
                    {
                        _logger.Information("Deleted batch group {Id}", id);
                    }
                    else
                    {
                        _logger.Warning("Batch group {Id} not found for deletion", id);
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting batch group {Id}", id);
                return false;
            }
        });
    }

    /// <summary>
    /// Gets the last used batch group for a specific server.
    /// </summary>
    /// <param name="worldId">The world/server ID.</param>
    /// <returns>The most recently used batch, or null if none exist.</returns>
    public async Task<BatchGroup?> GetLastUsedBatchForServerAsync(int worldId)
    {
        var batches = await GetBatchGroupsForServerAsync(worldId);
        return batches.FirstOrDefault(b => b.LastUsedDate.HasValue);
    }

    /// <summary>
    /// Gets the favorite batch group for a specific server.
    /// </summary>
    /// <param name="worldId">The world/server ID.</param>
    /// <returns>The favorite batch, or null if none is marked as favorite.</returns>
    public async Task<BatchGroup?> GetFavoriteBatchForServerAsync(int worldId)
    {
        var batches = await GetBatchGroupsForServerAsync(worldId);
        return batches.FirstOrDefault(b => b.IsFavorite);
    }

    /// <summary>
    /// Sets or clears the favorite flag for a batch group.
    /// Only one batch per server can be favorite.
    /// </summary>
    /// <param name="id">The batch group ID to set as favorite.</param>
    /// <param name="isFavorite">True to set as favorite, false to clear.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public async Task<bool> SetFavoriteAsync(Guid id, bool isFavorite)
    {
        return await Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<BatchGroup>(BatchGroupsCollection);

                    var batch = collection.FindById(id);
                    if (batch == null)
                    {
                        _logger.Warning("Batch group {Id} not found for favorite update", id);
                        return false;
                    }

                    if (isFavorite)
                    {
                        // Clear favorite from other batches for this server
                        var otherBatches = collection.Find(b => b.WorldId == batch.WorldId && b.Id != id);
                        foreach (var other in otherBatches)
                        {
                            if (other.IsFavorite)
                            {
                                other.IsFavorite = false;
                                collection.Update(other);
                                _logger.Debug("Cleared favorite from batch {Id}", other.Id);
                            }
                        }
                    }

                    batch.IsFavorite = isFavorite;
                    collection.Update(batch);

                    _logger.Information("Set favorite={IsFavorite} for batch group {Id}", isFavorite, id);

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error setting favorite for batch group {Id}", id);
                return false;
            }
        });
    }

    /// <summary>
    /// Validates a batch group.
    /// </summary>
    /// <param name="batch">The batch group to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public async Task<bool> ValidateBatchAsync(BatchGroup batch)
    {
        var errors = await GetValidationErrorsAsync(batch);
        return errors.Count == 0;
    }

    /// <summary>
    /// Gets validation errors for a batch group.
    /// Checks model validation and credential existence.
    /// </summary>
    /// <param name="batch">The batch group to validate.</param>
    /// <returns>List of validation error messages.</returns>
    public async Task<List<string>> GetValidationErrorsAsync(BatchGroup batch)
    {
        var errors = new List<string>();

        // Basic model validation
        errors.AddRange(batch.GetValidationErrors());

        // Check that all credentials exist
        try
        {
            var credentials = await _credentialVaultService.GetCredentialsForWorldAsync(batch.WorldId);
            var credentialUsernames = credentials.Select(c => c.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in batch.Entries)
            {
                if (!credentialUsernames.Contains(entry.CredentialUsername))
                {
                    errors.Add($"Credential '{entry.CredentialUsername}' not found for this server");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error validating batch group credentials");
            errors.Add("Failed to validate credentials");
        }

        return errors;
    }

    /// <summary>
    /// Updates the LastUsedDate for a batch group.
    /// Should be called after successfully launching a batch.
    /// </summary>
    /// <param name="id">The batch group ID.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public async Task<bool> MarkAsUsedAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<BatchGroup>(BatchGroupsCollection);

                    var batch = collection.FindById(id);
                    if (batch == null)
                    {
                        _logger.Warning("Batch group {Id} not found for marking as used", id);
                        return false;
                    }

                    batch.MarkAsUsed();
                    collection.Update(batch);

                    _logger.Debug("Marked batch group {Id} as used", id);

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking batch group {Id} as used", id);
                return false;
            }
        });
    }

    /// <summary>
    /// Records a launch history entry for a batch group.
    /// </summary>
    /// <param name="batch">The batch group that was launched.</param>
    /// <param name="result">The launch sequence result.</param>
    /// <returns>True if history was recorded successfully, false otherwise.</returns>
    public async Task<bool> RecordLaunchHistoryAsync(BatchGroup batch, LaunchSequenceResult result)
    {
        return await Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var historyCollection = db.GetCollection<MultiClientLaunchHistory>(LaunchHistoryCollection);

                    // Create history entry
                    var historyEntry = new MultiClientLaunchHistory
                    {
                        BatchGroupId = batch.Id,
                        BatchName = batch.Name,
                        WorldId = batch.WorldId,
                        LaunchDateTime = DateTime.UtcNow,
                        TotalClients = result.TotalTasks,
                        SuccessCount = result.SuccessCount,
                        FailureCount = result.FailureCount,
                        Duration = result.TotalDuration
                    };

                    // Insert history entry
                    historyCollection.Insert(historyEntry);

                    // Ensure index on WorldId and LaunchDateTime for efficient queries
                    historyCollection.EnsureIndex(h => h.WorldId);
                    historyCollection.EnsureIndex(h => h.LaunchDateTime);

                    // Trim old history entries (keep last MaxHistoryEntries)
                    var allHistory = historyCollection.FindAll()
                        .OrderByDescending(h => h.LaunchDateTime)
                        .ToList();

                    if (allHistory.Count > MaxHistoryEntries)
                    {
                        var toDelete = allHistory.Skip(MaxHistoryEntries).ToList();
                        foreach (var old in toDelete)
                        {
                            historyCollection.Delete(old.Id);
                        }
                        _logger.Debug("Trimmed {Count} old history entries", toDelete.Count);
                    }

                    _logger.Information("Recorded launch history for batch {BatchName}: {Success}/{Total}",
                        batch.Name, result.SuccessCount, result.TotalTasks);

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error recording launch history for batch {BatchId}", batch.Id);
                return false;
            }
        });
    }

    /// <summary>
    /// Gets the launch history for a specific server.
    /// </summary>
    /// <param name="worldId">The world/server ID.</param>
    /// <param name="limit">Maximum number of entries to return (default 5).</param>
    /// <returns>List of launch history entries, ordered by most recent first.</returns>
    public async Task<List<MultiClientLaunchHistory>> GetLaunchHistoryForServerAsync(int worldId, int limit = 5)
    {
        return await Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var historyCollection = db.GetCollection<MultiClientLaunchHistory>(LaunchHistoryCollection);

                    var history = historyCollection.Find(h => h.WorldId == worldId)
                        .OrderByDescending(h => h.LaunchDateTime)
                        .Take(limit)
                        .ToList();

                    _logger.Debug("Retrieved {Count} history entries for world {WorldId}", history.Count, worldId);

                    return history;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving launch history for world {WorldId}", worldId);
                return new List<MultiClientLaunchHistory>();
            }
        });
    }

    /// <summary>
    /// Gets the most recent launch history entry for any server.
    /// </summary>
    /// <returns>The most recent launch history entry, or null if none exists.</returns>
    public async Task<MultiClientLaunchHistory?> GetMostRecentLaunchAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var historyCollection = db.GetCollection<MultiClientLaunchHistory>(LaunchHistoryCollection);

                    var mostRecent = historyCollection.FindAll()
                        .OrderByDescending(h => h.LaunchDateTime)
                        .FirstOrDefault();

                    _logger.Debug("Retrieved most recent launch: {BatchName}", mostRecent?.BatchName ?? "None");

                    return mostRecent;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving most recent launch");
                return null;
            }
        });
    }
}

