using LiteDB;
using OPLauncher.Models;
using OPLauncher.DTOs;
using OPLauncher.Utilities;

namespace OPLauncher.Services;

/// <summary>
/// Service for securely storing and retrieving ACE account credentials for different worlds.
/// All passwords are encrypted using DPAPI (Data Protection API) before being stored in LiteDB.
/// </summary>
public class CredentialVaultService
{
    private readonly ConfigService _configService;
    private readonly LoggingService _logger;
    private readonly string _databasePath;
    private readonly object _lock = new();

    private const string CredentialsCollection = "saved_credentials";

    /// <summary>
    /// Initializes a new instance of the CredentialVaultService.
    /// </summary>
    /// <param name="configService">The configuration service for database path.</param>
    /// <param name="logger">The logging service for diagnostics.</param>
    public CredentialVaultService(ConfigService configService, LoggingService logger)
    {
        _configService = configService;
        _logger = logger;

        // Set up database path
        var configDir = _configService.GetConfigDirectory();
        _databasePath = Path.Combine(configDir, "credentials_vault.db");

        _logger.Debug("CredentialVaultService initialized with database path: {DatabasePath}", _databasePath);

        // Initialize database
        InitializeDatabase();
    }

    /// <summary>
    /// Saves or updates a credential for a specific world.
    /// The password is encrypted using DPAPI before storage.
    /// </summary>
    /// <param name="worldId">The world ID this credential is for.</param>
    /// <param name="username">The ACE account username.</param>
    /// <param name="password">The plaintext password (will be encrypted before storage).</param>
    /// <param name="displayName">Optional display name for this credential.</param>
    /// <returns>True if save was successful, false otherwise.</returns>
    public async Task<bool> SaveCredentialAsync(int worldId, string username, string password, string? displayName = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username))
                {
                    _logger.Warning("Cannot save credential: username is empty");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    _logger.Warning("Cannot save credential: password is empty");
                    return false;
                }

                _logger.Information("Saving credential for world {WorldId}, username: {Username}", worldId, username);

                // Encrypt the password using DPAPI
                string encryptedPassword;
                try
                {
                    encryptedPassword = SecurityHelper.EncryptString(password);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to encrypt password using DPAPI");
                    return false;
                }

                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<SavedCredential>(CredentialsCollection);

                    // Check if credential already exists for this world/username combination
                    var existing = collection.FindOne(c => c.WorldId == worldId && c.Username == username);

                    if (existing != null)
                    {
                        // Update existing credential
                        existing.Update(encryptedPassword, displayName);
                        collection.Update(existing);
                        _logger.Information("Updated existing credential for world {WorldId}, username: {Username}", worldId, username);
                    }
                    else
                    {
                        // Create new credential
                        var credential = SavedCredential.Create(worldId, username, encryptedPassword, displayName);
                        collection.Insert(credential);
                        _logger.Information("Saved new credential for world {WorldId}, username: {Username}", worldId, username);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error saving credential for world {WorldId}, username: {Username}", worldId, username);
                return false;
            }
        });
    }

    /// <summary>
    /// Retrieves all saved credentials for a specific world.
    /// Passwords remain encrypted in the returned objects.
    /// Use DecryptPassword() to decrypt when needed.
    /// </summary>
    /// <param name="worldId">The world ID to retrieve credentials for.</param>
    /// <returns>List of saved credentials for the world, or empty list if none found.</returns>
    public async Task<List<SavedCredential>> GetCredentialsForWorldAsync(int worldId)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.Debug("Retrieving credentials for world {WorldId}", worldId);

                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<SavedCredential>(CredentialsCollection);

                    var credentials = collection
                        .Find(c => c.WorldId == worldId)
                        .OrderByDescending(c => c.LastUsed)
                        .ToList();

                    _logger.Information("Found {Count} credential(s) for world {WorldId}", credentials.Count, worldId);
                    return credentials;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving credentials for world {WorldId}", worldId);
                return new List<SavedCredential>();
            }
        });
    }

    /// <summary>
    /// Retrieves all saved credentials across all worlds.
    /// Useful for management/migration scenarios.
    /// </summary>
    /// <returns>List of all saved credentials.</returns>
    public async Task<List<SavedCredential>> GetAllCredentialsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.Debug("Retrieving all credentials");

                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<SavedCredential>(CredentialsCollection);

                    var credentials = collection
                        .FindAll()
                        .OrderBy(c => c.WorldId)
                        .ThenByDescending(c => c.LastUsed)
                        .ToList();

                    _logger.Information("Found {Count} total credential(s)", credentials.Count);
                    return credentials;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving all credentials");
                return new List<SavedCredential>();
            }
        });
    }

    /// <summary>
    /// Deletes a specific credential by world ID and username.
    /// </summary>
    /// <param name="worldId">The world ID.</param>
    /// <param name="username">The username to delete.</param>
    /// <returns>True if deletion was successful, false otherwise.</returns>
    public async Task<bool> DeleteCredentialAsync(int worldId, string username)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username))
                {
                    _logger.Warning("Cannot delete credential: username is empty");
                    return false;
                }

                _logger.Information("Deleting credential for world {WorldId}, username: {Username}", worldId, username);

                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<SavedCredential>(CredentialsCollection);

                    var credential = collection.FindOne(c => c.WorldId == worldId && c.Username == username);
                    if (credential == null)
                    {
                        _logger.Warning("Credential not found for world {WorldId}, username: {Username}", worldId, username);
                        return false;
                    }

                    var deleted = collection.Delete(credential.Id);
                    if (deleted)
                    {
                        _logger.Information("Deleted credential for world {WorldId}, username: {Username}", worldId, username);
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting credential for world {WorldId}, username: {Username}", worldId, username);
                return false;
            }
        });
    }

    /// <summary>
    /// Deletes all credentials for a specific world.
    /// Useful when removing a world or clearing data.
    /// </summary>
    /// <param name="worldId">The world ID to delete all credentials for.</param>
    /// <returns>The number of credentials deleted.</returns>
    public async Task<int> DeleteAllCredentialsForWorldAsync(int worldId)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.Information("Deleting all credentials for world {WorldId}", worldId);

                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<SavedCredential>(CredentialsCollection);

                    var deletedCount = collection.DeleteMany(c => c.WorldId == worldId);

                    _logger.Information("Deleted {Count} credential(s) for world {WorldId}", deletedCount, worldId);
                    return deletedCount;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting credentials for world {WorldId}", worldId);
                return 0;
            }
        });
    }

    /// <summary>
    /// Decrypts a password from a SavedCredential.
    /// The password is decrypted using DPAPI.
    /// </summary>
    /// <param name="credential">The credential containing the encrypted password.</param>
    /// <returns>The decrypted password, or null if decryption fails.</returns>
    public string? DecryptPassword(SavedCredential credential)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(credential.EncryptedPassword))
            {
                _logger.Warning("Cannot decrypt password: encrypted password is empty");
                return null;
            }

            var decryptedPassword = SecurityHelper.DecryptString(credential.EncryptedPassword);
            _logger.Debug("Successfully decrypted password for world {WorldId}, username: {Username}",
                credential.WorldId, credential.Username);
            return decryptedPassword;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to decrypt password for world {WorldId}, username: {Username}",
                credential.WorldId, credential.Username);
            return null;
        }
    }

    /// <summary>
    /// Updates the LastUsed timestamp for a credential.
    /// Should be called when a credential is used to launch the game.
    /// </summary>
    /// <param name="worldId">The world ID.</param>
    /// <param name="username">The username.</param>
    /// <returns>True if update was successful, false otherwise.</returns>
    public async Task<bool> UpdateLastUsedAsync(int worldId, string username)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.Debug("Updating last used timestamp for world {WorldId}, username: {Username}", worldId, username);

                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<SavedCredential>(CredentialsCollection);

                    var credential = collection.FindOne(c => c.WorldId == worldId && c.Username == username);
                    if (credential == null)
                    {
                        _logger.Warning("Credential not found for world {WorldId}, username: {Username}", worldId, username);
                        return false;
                    }

                    credential.MarkAsUsed();
                    collection.Update(credential);

                    _logger.Debug("Updated last used timestamp for world {WorldId}, username: {Username}", worldId, username);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating last used timestamp for world {WorldId}, username: {Username}", worldId, username);
                return false;
            }
        });
    }

    /// <summary>
    /// Gets the most recently used credential for a specific world.
    /// Useful for auto-selecting the default credential.
    /// </summary>
    /// <param name="worldId">The world ID.</param>
    /// <returns>The most recently used credential, or null if none found.</returns>
    public async Task<SavedCredential?> GetMostRecentCredentialAsync(int worldId)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.Debug("Getting most recent credential for world {WorldId}", worldId);

                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<SavedCredential>(CredentialsCollection);

                    var credential = collection
                        .Find(c => c.WorldId == worldId)
                        .OrderByDescending(c => c.LastUsed)
                        .FirstOrDefault();

                    if (credential != null)
                    {
                        _logger.Debug("Found most recent credential for world {WorldId}: {Username}",
                            worldId, credential.Username);
                    }
                    else
                    {
                        _logger.Debug("No credentials found for world {WorldId}", worldId);
                    }

                    return credential;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting most recent credential for world {WorldId}", worldId);
                return null;
            }
        });
    }

    /// <summary>
    /// Checks if any credentials exist for a specific world.
    /// </summary>
    /// <param name="worldId">The world ID to check.</param>
    /// <returns>True if at least one credential exists, false otherwise.</returns>
    public async Task<bool> HasCredentialsForWorldAsync(int worldId)
    {
        return await Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<SavedCredential>(CredentialsCollection);

                    return collection.Exists(c => c.WorldId == worldId);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking credentials for world {WorldId}", worldId);
                return false;
            }
        });
    }

    /// <summary>
    /// Clears all stored credentials.
    /// Use with caution - this is permanent and cannot be undone.
    /// </summary>
    /// <returns>The number of credentials deleted.</returns>
    public async Task<int> ClearAllCredentialsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.Warning("Clearing ALL credentials from vault");

                lock (_lock)
                {
                    using var db = new LiteDatabase(_databasePath);
                    var collection = db.GetCollection<SavedCredential>(CredentialsCollection);

                    var deletedCount = collection.DeleteAll();

                    _logger.Information("Deleted {Count} credential(s) from vault", deletedCount);
                    return deletedCount;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error clearing all credentials");
                return 0;
            }
        });
    }

    /// <summary>
    /// Initializes the LiteDB database and creates necessary indexes.
    /// </summary>
    private void InitializeDatabase()
    {
        try
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_databasePath);
                var collection = db.GetCollection<SavedCredential>(CredentialsCollection);

                // Create indexes for efficient querying
                collection.EnsureIndex(c => c.WorldId);
                collection.EnsureIndex(c => c.Username);
                collection.EnsureIndex(c => c.LastUsed);

                _logger.Debug("LiteDB credential vault initialized");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize credential vault database");
        }
    }

    /// <summary>
    /// Exports all credentials to a backup file.
    /// The exported data remains encrypted.
    /// </summary>
    /// <param name="exportPath">The file path to export to.</param>
    /// <returns>True if export was successful, false otherwise.</returns>
    public async Task<bool> ExportCredentialsAsync(string exportPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.Information("Exporting credentials to: {ExportPath}", exportPath);

                lock (_lock)
                {
                    // Simply copy the database file
                    File.Copy(_databasePath, exportPath, overwrite: true);

                    _logger.Information("Credentials exported successfully to: {ExportPath}", exportPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to export credentials to: {ExportPath}", exportPath);
                return false;
            }
        });
    }

    /// <summary>
    /// Imports credentials from a backup file.
    /// This will merge with existing credentials (newer LastUsed wins for conflicts).
    /// </summary>
    /// <param name="importPath">The file path to import from.</param>
    /// <returns>The number of credentials imported.</returns>
    public async Task<int> ImportCredentialsAsync(string importPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(importPath))
                {
                    _logger.Warning("Import file not found: {ImportPath}", importPath);
                    return 0;
                }

                _logger.Information("Importing credentials from: {ImportPath}", importPath);

                var importedCount = 0;

                // Read credentials from import file
                using (var importDb = new LiteDatabase(importPath))
                {
                    var importCollection = importDb.GetCollection<SavedCredential>(CredentialsCollection);
                    var importedCredentials = importCollection.FindAll().ToList();

                    lock (_lock)
                    {
                        using var db = new LiteDatabase(_databasePath);
                        var collection = db.GetCollection<SavedCredential>(CredentialsCollection);

                        foreach (var importedCredential in importedCredentials)
                        {
                            var existing = collection.FindOne(c =>
                                c.WorldId == importedCredential.WorldId &&
                                c.Username == importedCredential.Username);

                            if (existing != null)
                            {
                                // Merge: keep the one with most recent LastUsed
                                if (importedCredential.LastUsed > existing.LastUsed)
                                {
                                    existing.EncryptedPassword = importedCredential.EncryptedPassword;
                                    existing.DisplayName = importedCredential.DisplayName;
                                    existing.LastUsed = importedCredential.LastUsed;
                                    existing.UpdatedAt = DateTime.UtcNow;
                                    collection.Update(existing);
                                    importedCount++;
                                }
                            }
                            else
                            {
                                // New credential - insert
                                collection.Insert(importedCredential);
                                importedCount++;
                            }
                        }
                    }
                }

                _logger.Information("Imported {Count} credential(s) from: {ImportPath}", importedCount, importPath);
                return importedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to import credentials from: {ImportPath}", importPath);
                return 0;
            }
        });
    }
}
