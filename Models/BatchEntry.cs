using System;

namespace OPLauncher.Models;

/// <summary>
/// Represents a single account entry in a multi-client batch group.
/// Each entry specifies which credential to use and in what order.
/// </summary>
public class BatchEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for this batch entry.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the username of the credential to launch.
    /// This references a SavedCredential.Username for the same server.
    /// </summary>
    public string CredentialUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the launch order within the batch.
    /// Clients are launched in ascending order (1, 2, 3, etc.).
    /// </summary>
    public int LaunchOrder { get; set; } = 1;

    /// <summary>
    /// Gets or sets the delay in seconds to wait after launching this client.
    /// Range: 0-30 seconds. 0 = no delay (simultaneous with next).
    /// </summary>
    public int DelaySeconds { get; set; } = 3;

    /// <summary>
    /// Gets or sets optional notes for this entry.
    /// Examples: "Main DPS", "Buffer", "Healer", etc.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Validates this batch entry.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(CredentialUsername))
            return false;

        if (LaunchOrder < 1)
            return false;

        if (DelaySeconds < 0 || DelaySeconds > 30)
            return false;

        if (Notes != null && Notes.Length > 200)
            return false;

        return true;
    }

    /// <summary>
    /// Gets validation errors for this entry.
    /// </summary>
    /// <returns>List of validation error messages.</returns>
    public List<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(CredentialUsername))
            errors.Add("Credential username is required");

        if (LaunchOrder < 1)
            errors.Add("Launch order must be at least 1");

        if (DelaySeconds < 0 || DelaySeconds > 30)
            errors.Add("Delay must be between 0 and 30 seconds");

        if (Notes != null && Notes.Length > 200)
            errors.Add("Notes cannot exceed 200 characters");

        return errors;
    }

    /// <summary>
    /// Creates a copy of this batch entry.
    /// </summary>
    /// <returns>A new BatchEntry with the same values but a new ID.</returns>
    public BatchEntry Clone()
    {
        return new BatchEntry
        {
            Id = Guid.NewGuid(),
            CredentialUsername = CredentialUsername,
            LaunchOrder = LaunchOrder,
            DelaySeconds = DelaySeconds,
            Notes = Notes
        };
    }
}
