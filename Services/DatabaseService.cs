using LiteDB;
using OPLauncher.Models;

namespace OPLauncher.Services;

/// <summary>
/// Centralized service for managing LiteDB database operations.
/// Provides thread-safe access to all launcher data collections with optional encryption support.
/// </summary>
public class DatabaseService : IDisposable
{
    private readonly ConfigService _configService;
    private readonly LoggingService _logger;
    private readonly string _databasePath;
    private readonly object _lock = new();
    private readonly Lazy<LiteDatabase> _database;
    private bool _disposed;

    // Collection names
    private const string WorldsCacheCollection = "worlds_cache";
    private const string CredentialsCollection = "saved_credentials";
    private const string NewsCacheCollection = "news_cache";
    private const string DatabaseVersionCollection = "db_version";
    private const int CurrentDatabaseVersion = 1;

    /// <summary>
    /// Initializes a new instance of the DatabaseService.
    /// </summary>
    /// <param name="configService">The configuration service for database path.</param>
    /// <param name="logger">The logging service for diagnostics.</param>
    public DatabaseService(ConfigService configService, LoggingService logger)
    {
        _configService = configService;
        _logger = logger;

        // Set up centralized database path
        var configDir = _configService.GetConfigDirectory();
        _databasePath = Path.Combine(configDir, "oldportal.db");

        // Initialize lazy database connection with optional encryption
        _database = new Lazy<LiteDatabase>(() =>
        {
            var connectionString = _databasePath;

            // Optional: Add encryption if configured
            // Format: "Filename=mydb.db;Password=mypass"
            if (!string.IsNullOrWhiteSpace(_configService.Current.DatabasePassword))
            {
                connectionString = $"Filename={_databasePath};Password={_configService.Current.DatabasePassword}";
                _logger.Debug("LiteDB encryption enabled");
            }

            var db = new LiteDatabase(connectionString);
            InitializeDatabase(db);
            return db;
        });

        _logger.Debug("DatabaseService initialized with path: {DatabasePath}", _databasePath);
    }

    #region Collection Access Methods

    /// <summary>
    /// Gets the worlds cache collection.
    /// </summary>
    /// <returns>LiteDB collection for cached worlds.</returns>
    public ILiteCollection<CachedWorldListEntry> GetWorldsCacheCollection()
    {
        lock (_lock)
        {
            return _database.Value.GetCollection<CachedWorldListEntry>(WorldsCacheCollection);
        }
    }

    /// <summary>
    /// Gets the credentials collection.
    /// </summary>
    /// <returns>LiteDB collection for saved credentials.</returns>
    public ILiteCollection<SavedCredential> GetCredentialsCollection()
    {
        lock (_lock)
        {
            return _database.Value.GetCollection<SavedCredential>(CredentialsCollection);
        }
    }


    #endregion

    #region Generic CRUD Operations

    /// <summary>
    /// Generic method to insert a document into a collection.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="collectionName">The collection name.</param>
    /// <param name="document">The document to insert.</param>
    /// <returns>The inserted document's ID.</returns>
    public BsonValue Insert<T>(string collectionName, T document)
    {
        try
        {
            lock (_lock)
            {
                var collection = _database.Value.GetCollection<T>(collectionName);
                var id = collection.Insert(document);
                _logger.Debug("Inserted document into collection: {CollectionName}", collectionName);
                return id;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error inserting document into collection: {CollectionName}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Generic method to update a document in a collection.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="collectionName">The collection name.</param>
    /// <param name="document">The document to update.</param>
    /// <returns>True if update was successful.</returns>
    public bool Update<T>(string collectionName, T document)
    {
        try
        {
            lock (_lock)
            {
                var collection = _database.Value.GetCollection<T>(collectionName);
                var result = collection.Update(document);
                _logger.Debug("Updated document in collection: {CollectionName}", collectionName);
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating document in collection: {CollectionName}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Generic method to upsert a document in a collection.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="collectionName">The collection name.</param>
    /// <param name="document">The document to upsert.</param>
    /// <returns>True if document was updated, false if inserted.</returns>
    public bool Upsert<T>(string collectionName, T document)
    {
        try
        {
            lock (_lock)
            {
                var collection = _database.Value.GetCollection<T>(collectionName);
                var result = collection.Upsert(document);
                _logger.Debug("Upserted document in collection: {CollectionName}", collectionName);
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error upserting document in collection: {CollectionName}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Generic method to delete a document by ID.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="collectionName">The collection name.</param>
    /// <param name="id">The document ID.</param>
    /// <returns>True if deletion was successful.</returns>
    public bool Delete<T>(string collectionName, BsonValue id)
    {
        try
        {
            lock (_lock)
            {
                var collection = _database.Value.GetCollection<T>(collectionName);
                var result = collection.Delete(id);
                _logger.Debug("Deleted document from collection: {CollectionName}, ID: {Id}", collectionName, id);
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting document from collection: {CollectionName}, ID: {Id}", collectionName, id);
            throw;
        }
    }

    /// <summary>
    /// Generic method to find a single document by predicate.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="collectionName">The collection name.</param>
    /// <param name="predicate">The search predicate.</param>
    /// <returns>The found document, or default if not found.</returns>
    public T? FindOne<T>(string collectionName, System.Linq.Expressions.Expression<Func<T, bool>> predicate)
    {
        try
        {
            lock (_lock)
            {
                var collection = _database.Value.GetCollection<T>(collectionName);
                return collection.FindOne(predicate);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error finding document in collection: {CollectionName}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Generic method to find all documents matching a predicate.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="collectionName">The collection name.</param>
    /// <param name="predicate">The search predicate.</param>
    /// <returns>List of matching documents.</returns>
    public List<T> Find<T>(string collectionName, System.Linq.Expressions.Expression<Func<T, bool>> predicate)
    {
        try
        {
            lock (_lock)
            {
                var collection = _database.Value.GetCollection<T>(collectionName);
                return collection.Find(predicate).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error finding documents in collection: {CollectionName}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Generic method to find all documents in a collection.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="collectionName">The collection name.</param>
    /// <returns>List of all documents.</returns>
    public List<T> FindAll<T>(string collectionName)
    {
        try
        {
            lock (_lock)
            {
                var collection = _database.Value.GetCollection<T>(collectionName);
                return collection.FindAll().ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error finding all documents in collection: {CollectionName}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Generic method to delete all documents in a collection.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="collectionName">The collection name.</param>
    /// <returns>The number of documents deleted.</returns>
    public int DeleteAll<T>(string collectionName)
    {
        try
        {
            lock (_lock)
            {
                var collection = _database.Value.GetCollection<T>(collectionName);
                var count = collection.DeleteAll();
                _logger.Information("Deleted {Count} documents from collection: {CollectionName}", count, collectionName);
                return count;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting all documents in collection: {CollectionName}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Executes a custom database operation with proper locking.
    /// Use this for complex queries that require direct database access.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="operation">The database operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    public TResult Execute<TResult>(Func<ILiteDatabase, TResult> operation)
    {
        try
        {
            lock (_lock)
            {
                return operation(_database.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error executing custom database operation");
            throw;
        }
    }

    /// <summary>
    /// Executes a custom database operation with proper locking (no return value).
    /// Use this for complex queries that require direct database access.
    /// </summary>
    /// <param name="operation">The database operation to execute.</param>
    public void Execute(Action<ILiteDatabase> operation)
    {
        try
        {
            lock (_lock)
            {
                operation(_database.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error executing custom database operation");
            throw;
        }
    }

    /// <summary>
    /// Gets a typed collection from the database.
    /// Use this when you need direct access to a collection for complex queries.
    /// Note: This method acquires the lock internally, so don't hold onto the collection reference.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="collectionName">The collection name.</param>
    /// <param name="operation">The operation to perform on the collection.</param>
    /// <returns>The result of the operation.</returns>
    public TResult WithCollection<T, TResult>(string collectionName, Func<ILiteCollection<T>, TResult> operation)
    {
        try
        {
            lock (_lock)
            {
                var collection = _database.Value.GetCollection<T>(collectionName);
                return operation(collection);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error executing operation on collection: {CollectionName}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Executes an operation on a typed collection (no return value).
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="collectionName">The collection name.</param>
    /// <param name="operation">The operation to perform on the collection.</param>
    public void WithCollection<T>(string collectionName, Action<ILiteCollection<T>> operation)
    {
        try
        {
            lock (_lock)
            {
                var collection = _database.Value.GetCollection<T>(collectionName);
                operation(collection);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error executing operation on collection: {CollectionName}", collectionName);
            throw;
        }
    }

    #endregion

    #region Database Management

    /// <summary>
    /// Initializes the database with required collections and indexes.
    /// </summary>
    /// <param name="db">The LiteDatabase instance.</param>
    private void InitializeDatabase(LiteDatabase db)
    {
        try
        {
            _logger.Information("Initializing centralized LiteDB database...");

            // Check and apply migrations if needed
            var currentVersion = GetDatabaseVersion(db);
            if (currentVersion < CurrentDatabaseVersion)
            {
                _logger.Information("Database version {CurrentVersion} detected, migrating to version {TargetVersion}",
                    currentVersion, CurrentDatabaseVersion);
                MigrateDatabase(db, currentVersion, CurrentDatabaseVersion);
            }

            // Initialize worlds cache collection
            var worldsCache = db.GetCollection<CachedWorldListEntry>(WorldsCacheCollection);
            worldsCache.EnsureIndex(x => x.CacheKey);
            _logger.Debug("Worlds cache collection initialized with index on CacheKey");

            // Initialize credentials collection
            var credentials = db.GetCollection<SavedCredential>(CredentialsCollection);
            credentials.EnsureIndex(x => x.WorldId);
            credentials.EnsureIndex(x => x.Username);
            credentials.EnsureIndex(x => x.LastUsed);
            _logger.Debug("Credentials collection initialized with indexes on WorldId, Username, and LastUsed");

            // Database version is stored in a simple collection
            // No initialization needed

            // Initialize news cache collection
            var newsCache = db.GetCollection<ViewModels.CachedNewsEntry>(NewsCacheCollection);
            newsCache.EnsureIndex(x => x.CacheKey);
            _logger.Debug("News cache collection initialized with index on CacheKey");

            _logger.Information("LiteDB database initialized successfully (version {Version})", CurrentDatabaseVersion);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize LiteDB database");
            throw;
        }
    }

    /// <summary>
    /// Gets the current database version.
    /// </summary>
    /// <param name="db">The LiteDatabase instance.</param>
    /// <returns>The database version number.</returns>
    private int GetDatabaseVersion(LiteDatabase db)
    {
        try
        {
            var versionCollection = db.GetCollection<DatabaseVersion>(DatabaseVersionCollection);
            var versionDoc = versionCollection.FindOne(Query.All());

            return versionDoc?.Version ?? 0; // New database if not found
        }
        catch
        {
            return 0; // Error reading version, treat as new database
        }
    }

    /// <summary>
    /// Sets the database version.
    /// </summary>
    /// <param name="db">The LiteDatabase instance.</param>
    /// <param name="version">The version number to set.</param>
    private void SetDatabaseVersion(LiteDatabase db, int version)
    {
        try
        {
            var versionCollection = db.GetCollection<DatabaseVersion>(DatabaseVersionCollection);
            versionCollection.DeleteAll(); // Only ever one version document

            versionCollection.Insert(new DatabaseVersion
            {
                Version = version,
                UpdatedAt = DateTime.UtcNow
            });

            _logger.Information("Database version set to: {Version}", version);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set database version");
            throw;
        }
    }

    /// <summary>
    /// Migrates the database from one version to another.
    /// </summary>
    /// <param name="db">The LiteDatabase instance.</param>
    /// <param name="fromVersion">The current version.</param>
    /// <param name="toVersion">The target version.</param>
    private void MigrateDatabase(LiteDatabase db, int fromVersion, int toVersion)
    {
        _logger.Information("Migrating database from version {FromVersion} to {ToVersion}", fromVersion, toVersion);

        // Apply migrations sequentially
        for (int version = fromVersion + 1; version <= toVersion; version++)
        {
            _logger.Information("Applying migration to version {Version}", version);

            switch (version)
            {
                case 1:
                    // Initial schema - no migration needed
                    _logger.Debug("Migration to version 1: Initial schema");
                    break;

                // Future migrations can be added here
                // case 2:
                //     MigrateToVersion2(db);
                //     break;

                default:
                    _logger.Warning("Unknown migration version: {Version}", version);
                    break;
            }
        }

        // Update database version
        SetDatabaseVersion(db, toVersion);
        _logger.Information("Database migration completed successfully");
    }

    /// <summary>
    /// Compacts the database to reduce file size.
    /// Removes deleted records and optimizes storage.
    /// </summary>
    public void CompactDatabase()
    {
        try
        {
            lock (_lock)
            {
                _logger.Information("Compacting database...");
                var sizeBefore = new FileInfo(_databasePath).Length;

                _database.Value.Rebuild();

                var sizeAfter = new FileInfo(_databasePath).Length;
                var savedBytes = sizeBefore - sizeAfter;
                var savedPercentage = (double)savedBytes / sizeBefore * 100;

                _logger.Information("Database compacted: {SizeBefore} bytes → {SizeAfter} bytes (saved {SavedBytes} bytes, {SavedPercentage:F1}%)",
                    sizeBefore, sizeAfter, savedBytes, savedPercentage);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error compacting database");
        }
    }

    /// <summary>
    /// Creates a backup of the database.
    /// </summary>
    /// <param name="backupPath">The path to save the backup file.</param>
    /// <returns>True if backup was successful.</returns>
    public bool BackupDatabase(string backupPath)
    {
        try
        {
            lock (_lock)
            {
                _logger.Information("Creating database backup: {BackupPath}", backupPath);

                // Ensure database is flushed
                _database.Value.Checkpoint();

                // Copy database file
                File.Copy(_databasePath, backupPath, overwrite: true);

                _logger.Information("Database backup created successfully: {BackupPath}", backupPath);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create database backup: {BackupPath}", backupPath);
            return false;
        }
    }

    /// <summary>
    /// Gets database statistics.
    /// </summary>
    /// <returns>Dictionary of statistic names and values.</returns>
    public Dictionary<string, object> GetDatabaseStatistics()
    {
        try
        {
            lock (_lock)
            {
                var stats = new Dictionary<string, object>();

                if (File.Exists(_databasePath))
                {
                    var fileInfo = new FileInfo(_databasePath);
                    stats["FileSize"] = fileInfo.Length;
                    stats["FileSizeFormatted"] = FormatFileSize(fileInfo.Length);
                    stats["LastModified"] = fileInfo.LastWriteTime;
                }

                var worldsCache = _database.Value.GetCollection<CachedWorldListEntry>(WorldsCacheCollection);
                stats["WorldsCacheCount"] = worldsCache.Count();

                var credentials = _database.Value.GetCollection<SavedCredential>(CredentialsCollection);
                stats["CredentialsCount"] = credentials.Count();

                stats["DatabaseVersion"] = GetDatabaseVersion(_database.Value);

                return stats;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting database statistics");
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    /// <param name="bytes">The file size in bytes.</param>
    /// <returns>Formatted file size string.</returns>
    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes of the DatabaseService and closes the database connection.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            if (_database.IsValueCreated)
            {
                _database.Value.Dispose();
                _logger.Debug("LiteDB database disposed");
            }

            _disposed = true;
        }
    }

    #endregion
}

/// <summary>
/// Model for cached world list entries in the database.
/// </summary>
public class CachedWorldListEntry
{
    public int Id { get; set; }
    public string CacheKey { get; set; } = string.Empty;
    public List<CachedWorld> Worlds { get; set; } = new();
    public DateTime CachedAt { get; set; }
}
