using System;
using System.Collections.Generic;
using System.Linq;

namespace OPLauncher.Models;

/// <summary>
/// Represents a saved multi-client launch profile (batch group) for a specific server.
/// Batch groups are server-scoped and contain a list of credentials to launch in sequence.
/// </summary>
public class BatchGroup
{
    /// <summary>
    /// Gets or sets the unique identifier for this batch group.
    /// Used as the LiteDB primary key.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the name of this batch group.
    /// Examples: "Frostfell Triple Box", "Main + 2 Buffers", "PvP Squad"
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional description for this batch group.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the world/server ID this batch group belongs to.
    /// Batch groups are server-scoped - they only work with credentials from this server.
    /// </summary>
    public int WorldId { get; set; }

    /// <summary>
    /// Gets or sets the list of entries (accounts) in this batch group.
    /// Each entry specifies a credential and launch order.
    /// </summary>
    public List<BatchEntry> Entries { get; set; } = new List<BatchEntry>();

    /// <summary>
    /// Gets or sets when this batch group was created (UTC).
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when this batch group was last used to launch (UTC).
    /// Used for sorting and "recent batches" features.
    /// </summary>
    public DateTime? LastUsedDate { get; set; }

    /// <summary>
    /// Gets or sets whether this batch group is marked as favorite.
    /// Only one batch per server should be favorite (enforced by service).
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Gets the number of entries in this batch group.
    /// </summary>
    public int EntryCount => Entries.Count;

    /// <summary>
    /// Validates this batch group.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return false;

        if (Name.Length > 100)
            return false;

        if (Entries.Count == 0)
            return false;

        // Check all entries are valid
        if (Entries.Any(e => !e.IsValid()))
            return false;

        // Check for duplicate usernames
        var usernames = Entries.Select(e => e.CredentialUsername).ToList();
        if (usernames.Count != usernames.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            return false;

        return true;
    }

    /// <summary>
    /// Gets validation errors for this batch group.
    /// </summary>
    /// <returns>List of validation error messages.</returns>
    public List<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Batch name is required");

        if (Name.Length > 100)
            errors.Add("Batch name cannot exceed 100 characters");

        if (Entries.Count == 0)
            errors.Add("Batch must have at least one entry");

        // Check for duplicate usernames
        var usernames = Entries.Select(e => e.CredentialUsername).ToList();
        var duplicates = usernames.GroupBy(u => u, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicate in duplicates)
        {
            errors.Add($"Duplicate credential: {duplicate}");
        }

        // Validate each entry
        for (int i = 0; i < Entries.Count; i++)
        {
            var entryErrors = Entries[i].GetValidationErrors();
            if (entryErrors.Count > 0)
            {
                errors.Add($"Entry {i + 1}: {string.Join(", ", entryErrors)}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Reorders the entries by their LaunchOrder property.
    /// </summary>
    public void ReorderEntries()
    {
        Entries = Entries.OrderBy(e => e.LaunchOrder).ToList();
    }

    /// <summary>
    /// Recalculates the LaunchOrder for all entries sequentially (1, 2, 3...).
    /// Useful after adding/removing entries to ensure no gaps.
    /// </summary>
    public void RenumberEntries()
    {
        ReorderEntries();
        for (int i = 0; i < Entries.Count; i++)
        {
            Entries[i].LaunchOrder = i + 1;
        }
    }

    /// <summary>
    /// Updates the LastUsedDate to now.
    /// Should be called when the batch is successfully launched.
    /// </summary>
    public void MarkAsUsed()
    {
        LastUsedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a copy of this batch group with a new ID and name.
    /// Useful for duplicating batch configurations.
    /// </summary>
    /// <param name="newName">The name for the duplicated batch.</param>
    /// <returns>A new BatchGroup with copied entries.</returns>
    public BatchGroup Duplicate(string newName)
    {
        return new BatchGroup
        {
            Id = Guid.NewGuid(),
            Name = newName,
            Description = Description,
            WorldId = WorldId,
            Entries = Entries.Select(e => e.Clone()).ToList(),
            CreatedDate = DateTime.UtcNow,
            LastUsedDate = null,
            IsFavorite = false
        };
    }
}
