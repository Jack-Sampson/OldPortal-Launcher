using LiteDB;
using OPLauncher.Models;

namespace OPLauncher.Services;

/// <summary>
/// Service for managing manually-added servers (localhost, private servers, etc.).
/// Provides CRUD operations for servers stored in centralized LiteDB.
/// </summary>
public class ManualServersService
{
    private readonly DatabaseService _databaseService;
    private readonly LoggingService _logger;

    private const string CollectionName = "manual_servers";

    /// <summary>
    /// Initializes a new instance of the ManualServersService.
    /// </summary>
    /// <param name="databaseService">The centralized database service.</param>
    /// <param name="logger">The logging service for diagnostics.</param>
    public ManualServersService(DatabaseService databaseService, LoggingService logger)
    {
        _databaseService = databaseService;
        _logger = logger;

        _logger.Debug("ManualServersService initialized using centralized database");

        // Initialize collection indexes
        InitializeCollection();
    }

    /// <summary>
    /// Gets all manually-added servers.
    /// </summary>
    /// <returns>List of all manual servers, ordered by most recently added.</returns>
    public async Task<List<ManualServer>> GetAllServersAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var servers = _databaseService.WithCollection<ManualServer, List<ManualServer>>(
                    CollectionName,
                    collection => collection.FindAll().OrderByDescending(s => s.AddedAt).ToList());

                _logger.Debug("Retrieved {Count} manual servers", servers.Count);
                return servers;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving manual servers");
                return new List<ManualServer>();
            }
        });
    }

    /// <summary>
    /// Gets a specific manual server by ID.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <returns>The server if found, null otherwise.</returns>
    public async Task<ManualServer?> GetServerByIdAsync(int id)
    {
        return await Task.Run(() =>
        {
            try
            {
                return _databaseService.WithCollection<ManualServer, ManualServer?>(
                    CollectionName,
                    collection => collection.FindById(id));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving manual server {Id}", id);
                return null;
            }
        });
    }

    /// <summary>
    /// Adds a new manual server.
    /// </summary>
    /// <param name="server">The server to add.</param>
    /// <returns>The added server with ID populated, or null if validation fails.</returns>
    public async Task<ManualServer?> AddServerAsync(ManualServer server)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Validate
                var validationErrors = server.Validate();
                if (validationErrors.Count > 0)
                {
                    _logger.Warning("Manual server validation failed: {Errors}",
                        string.Join(", ", validationErrors));
                    return null;
                }

                // Check for duplicate name
                if (ServerNameExists(server.Name, server.Id))
                {
                    _logger.Warning("Manual server with name '{Name}' already exists", server.Name);
                    return null;
                }

                // Check for duplicate host:port
                if (ServerConnectionExists(server.Host, server.Port, server.Id))
                {
                    _logger.Warning("Manual server with connection {Host}:{Port} already exists",
                        server.Host, server.Port);
                    return null;
                }

                _databaseService.WithCollection<ManualServer>(CollectionName, collection =>
                {
                    server.AddedAt = DateTime.UtcNow;
                    server.LastConnected = null;

                    var id = collection.Insert(server);
                    server.Id = id.AsInt32;

                    _logger.Information("Added manual server: {Name} ({Host}:{Port})",
                        server.Name, server.Host, server.Port);
                });

                return server;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error adding manual server");
                return null;
            }
        });
    }

    /// <summary>
    /// Updates an existing manual server.
    /// </summary>
    /// <param name="server">The server with updated data.</param>
    /// <returns>True if updated successfully, false otherwise.</returns>
    public async Task<bool> UpdateServerAsync(ManualServer server)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Validate
                var validationErrors = server.Validate();
                if (validationErrors.Count > 0)
                {
                    _logger.Warning("Manual server validation failed: {Errors}",
                        string.Join(", ", validationErrors));
                    return false;
                }

                // Check for duplicate name (excluding this server)
                if (ServerNameExists(server.Name, server.Id))
                {
                    _logger.Warning("Manual server with name '{Name}' already exists", server.Name);
                    return false;
                }

                // Check for duplicate host:port (excluding this server)
                if (ServerConnectionExists(server.Host, server.Port, server.Id))
                {
                    _logger.Warning("Manual server with connection {Host}:{Port} already exists",
                        server.Host, server.Port);
                    return false;
                }

                return _databaseService.WithCollection<ManualServer, bool>(CollectionName, collection =>
                {
                    var success = collection.Update(server);

                    if (success)
                    {
                        _logger.Information("Updated manual server: {Name} ({Host}:{Port})",
                            server.Name, server.Host, server.Port);
                    }
                    else
                    {
                        _logger.Warning("Failed to update manual server {Id} - not found", server.Id);
                    }

                    return success;
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating manual server");
                return false;
            }
        });
    }

    /// <summary>
    /// Deletes a manual server.
    /// </summary>
    /// <param name="id">The ID of the server to delete.</param>
    /// <returns>True if deleted successfully, false otherwise.</returns>
    public async Task<bool> DeleteServerAsync(int id)
    {
        return await Task.Run(() =>
        {
            try
            {
                return _databaseService.WithCollection<ManualServer, bool>(CollectionName, collection =>
                {
                    var server = collection.FindById(id);
                    if (server == null)
                    {
                        _logger.Warning("Cannot delete manual server {Id} - not found", id);
                        return false;
                    }

                    var success = collection.Delete(id);

                    if (success)
                    {
                        _logger.Information("Deleted manual server: {Name} ({Host}:{Port})",
                            server.Name, server.Host, server.Port);
                    }

                    return success;
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting manual server");
                return false;
            }
        });
    }

    /// <summary>
    /// Updates the last connected timestamp for a server.
    /// </summary>
    /// <param name="id">The server ID.</param>
    public async Task UpdateLastConnectedAsync(int id)
    {
        await Task.Run(() =>
        {
            try
            {
                _databaseService.WithCollection<ManualServer>(CollectionName, collection =>
                {
                    var server = collection.FindById(id);
                    if (server != null)
                    {
                        server.LastConnected = DateTime.UtcNow;
                        collection.Update(server);

                        _logger.Debug("Updated last connected time for manual server {Id}", id);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating last connected time for server {Id}", id);
            }
        });
    }

    /// <summary>
    /// Clears all manual servers.
    /// </summary>
    /// <returns>The number of servers deleted.</returns>
    public async Task<int> ClearAllServersAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var count = _databaseService.WithCollection<ManualServer, int>(CollectionName, collection =>
                {
                    return collection.DeleteAll();
                });

                _logger.Information("Cleared all manual servers ({Count} deleted)", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error clearing manual servers");
                return 0;
            }
        });
    }

    #region Private Helper Methods

    /// <summary>
    /// Initializes the collection with indexes for faster lookups.
    /// </summary>
    private void InitializeCollection()
    {
        try
        {
            _databaseService.WithCollection<ManualServer>(CollectionName, collection =>
            {
                // Create indexes for faster lookups
                collection.EnsureIndex(x => x.Name);
                collection.EnsureIndex(x => x.Host);
                collection.EnsureIndex(x => x.AddedAt);

                _logger.Debug("Manual servers collection initialized with indexes");
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize manual servers collection");
        }
    }

    private bool ServerNameExists(string name, int excludeId = 0)
    {
        try
        {
            return _databaseService.WithCollection<ManualServer, bool>(CollectionName, collection =>
            {
                return collection.Exists(s =>
                    s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    s.Id != excludeId);
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking for duplicate server name");
            return false;
        }
    }

    private bool ServerConnectionExists(string host, int port, int excludeId = 0)
    {
        try
        {
            return _databaseService.WithCollection<ManualServer, bool>(CollectionName, collection =>
            {
                return collection.Exists(s =>
                    s.Host.Equals(host, StringComparison.OrdinalIgnoreCase) &&
                    s.Port == port &&
                    s.Id != excludeId);
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking for duplicate server connection");
            return false;
        }
    }

    #endregion
}
