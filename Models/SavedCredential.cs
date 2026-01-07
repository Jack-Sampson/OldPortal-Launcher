namespace OPLauncher.Models;

/// <summary>
/// Represents a saved ACE account credential for a specific world.
/// Passwords are stored encrypted using DPAPI for security.
/// </summary>
public class SavedCredential
{
    /// <summary>
    /// Gets or sets the unique identifier for this credential entry.
    /// Used as the LiteDB primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the world ID this credential is associated with.
    /// </summary>
    public int WorldId { get; set; }

    /// <summary>
    /// Gets or sets the ACE account username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the encrypted password.
    /// This value is encrypted using DPAPI (Data Protection API) with CurrentUser scope.
    /// NEVER store or log this value in plaintext.
    /// </summary>
    public string EncryptedPassword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional display name for this credential.
    /// Useful for distinguishing multiple characters on the same server.
    /// Example: "My Main Character", "Alt Account", etc.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets when this credential was last used to launch the game.
    /// Used for sorting credentials (most recently used first).
    /// </summary>
    public DateTime LastUsed { get; set; }

    /// <summary>
    /// Gets or sets when this credential was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when this credential was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets the display text for this credential.
    /// Uses DisplayName if available, otherwise falls back to Username.
    /// </summary>
    /// <returns>The friendly name to display in UI.</returns>
    public string GetDisplayText()
    {
        if (!string.IsNullOrWhiteSpace(DisplayName))
            return DisplayName;

        return Username;
    }

    /// <summary>
    /// Gets a descriptive label for this credential including username.
    /// Format: "DisplayName (username)" or just "username" if no display name.
    /// </summary>
    /// <returns>A descriptive label for UI display.</returns>
    public string GetFullDisplayText()
    {
        if (!string.IsNullOrWhiteSpace(DisplayName))
            return $"{DisplayName} ({Username})";

        return Username;
    }

    /// <summary>
    /// Checks if this credential was used recently (within the last 30 days).
    /// Useful for highlighting frequently used credentials.
    /// </summary>
    /// <returns>True if used within the last 30 days, false otherwise.</returns>
    public bool IsRecentlyUsed()
    {
        return (DateTime.UtcNow - LastUsed).TotalDays <= 30;
    }

    /// <summary>
    /// Creates a new SavedCredential with the current timestamp.
    /// Password should already be encrypted before calling this method.
    /// </summary>
    /// <param name="worldId">The world ID.</param>
    /// <param name="username">The username.</param>
    /// <param name="encryptedPassword">The DPAPI-encrypted password.</param>
    /// <param name="displayName">Optional display name.</param>
    /// <returns>A new SavedCredential instance.</returns>
    public static SavedCredential Create(int worldId, string username, string encryptedPassword, string? displayName = null)
    {
        var now = DateTime.UtcNow;
        return new SavedCredential
        {
            WorldId = worldId,
            Username = username,
            EncryptedPassword = encryptedPassword,
            DisplayName = displayName,
            CreatedAt = now,
            LastUsed = now
        };
    }

    /// <summary>
    /// Updates the LastUsed timestamp to the current time.
    /// Should be called when a credential is used to launch the game.
    /// </summary>
    public void MarkAsUsed()
    {
        LastUsed = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the credential with new information.
    /// Sets the UpdatedAt timestamp.
    /// </summary>
    /// <param name="encryptedPassword">New encrypted password (if changed).</param>
    /// <param name="displayName">New display name (if changed).</param>
    public void Update(string? encryptedPassword = null, string? displayName = null)
    {
        if (encryptedPassword != null)
            EncryptedPassword = encryptedPassword;

        if (displayName != null)
            DisplayName = displayName;

        UpdatedAt = DateTime.UtcNow;
    }
}
