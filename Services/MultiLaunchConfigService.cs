using System;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using OPLauncher.Models;

namespace OPLauncher.Services;

/// <summary>
/// Service for managing multi-client launch configurations.
/// Stores and retrieves launch order and delay preferences per world.
/// </summary>
public class MultiLaunchConfigService
{
    private readonly LoggingService _logger;
    private readonly string _dbPath;

    /// <summary>
    /// Initializes a new instance of the MultiLaunchConfigService.
    /// </summary>
    public MultiLaunchConfigService(LoggingService logger)
    {
        _logger = logger;

        // Use the same database location as other services
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = System.IO.Path.Combine(appDataPath, "OldPortal", "launcher");

        if (!System.IO.Directory.Exists(appFolder))
        {
            System.IO.Directory.CreateDirectory(appFolder);
        }

        _dbPath = System.IO.Path.Combine(appFolder, "data.db");
        _logger.Debug("MultiLaunchConfigService initialized with database: {DbPath}", _dbPath);
    }

    /// <summary>
    /// Gets the saved multi-launch configuration for a specific world.
    /// </summary>
    /// <param name="worldId">The world ID to get configuration for.</param>
    /// <returns>The saved configuration, or null if none exists.</returns>
    public async Task<MultiLaunchConfiguration?> GetConfigurationAsync(int worldId)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var db = new LiteDatabase(_dbPath);
                var collection = db.GetCollection<MultiLaunchConfiguration>("multi_launch_configs");

                var config = collection.FindOne(c => c.WorldId == worldId);

                if (config != null)
                {
                    _logger.Debug("Loaded multi-launch config for world {WorldId} with {Count} entries",
                        worldId, config.Entries.Count);
                }
                else
                {
                    _logger.Debug("No saved multi-launch config found for world {WorldId}", worldId);
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load multi-launch configuration for world {WorldId}", worldId);
                return null;
            }
        });
    }

    /// <summary>
    /// Saves a multi-launch configuration for a specific world.
    /// </summary>
    /// <param name="config">The configuration to save.</param>
    public async Task SaveConfigurationAsync(MultiLaunchConfiguration config)
    {
        await Task.Run(() =>
        {
            try
            {
                config.LastModified = DateTime.UtcNow;

                using var db = new LiteDatabase(_dbPath);
                var collection = db.GetCollection<MultiLaunchConfiguration>("multi_launch_configs");

                // Ensure index on WorldId for fast lookups
                collection.EnsureIndex(c => c.WorldId);

                // Delete existing config for this world
                collection.DeleteMany(c => c.WorldId == config.WorldId);

                // Insert new config
                collection.Insert(config);

                _logger.Information("Saved multi-launch config for world {WorldId} with {Count} entries",
                    config.WorldId, config.Entries.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save multi-launch configuration for world {WorldId}", config.WorldId);
                throw;
            }
        });
    }

    /// <summary>
    /// Deletes the saved multi-launch configuration for a specific world.
    /// </summary>
    /// <param name="worldId">The world ID to delete configuration for.</param>
    public async Task DeleteConfigurationAsync(int worldId)
    {
        await Task.Run(() =>
        {
            try
            {
                using var db = new LiteDatabase(_dbPath);
                var collection = db.GetCollection<MultiLaunchConfiguration>("multi_launch_configs");

                var deletedCount = collection.DeleteMany(c => c.WorldId == worldId);

                _logger.Information("Deleted multi-launch config for world {WorldId} ({Count} records)",
                    worldId, deletedCount);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete multi-launch configuration for world {WorldId}", worldId);
                throw;
            }
        });
    }
}
