using System;
using System.Collections.Generic;

namespace OPLauncher.Models;

/// <summary>
/// Represents a saved multi-client launch configuration for a specific world.
/// Stores the order and delay settings for each credential.
/// </summary>
public class MultiLaunchConfiguration
{
    /// <summary>
    /// Gets or sets the unique identifier for this configuration.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the world ID this configuration belongs to.
    /// </summary>
    public int WorldId { get; set; }

    /// <summary>
    /// Gets or sets when this configuration was last modified.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Gets or sets the list of launch entry configurations.
    /// </summary>
    public List<LaunchEntryConfig> Entries { get; set; } = new();
}

/// <summary>
/// Represents the configuration for a single credential in the multi-launch order.
/// </summary>
public class LaunchEntryConfig
{
    /// <summary>
    /// Gets or sets the credential ID this entry refers to.
    /// </summary>
    public int CredentialId { get; set; }

    /// <summary>
    /// Gets or sets the launch order (1-based).
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the delay in seconds after launching this credential.
    /// </summary>
    public int DelaySeconds { get; set; }
}
