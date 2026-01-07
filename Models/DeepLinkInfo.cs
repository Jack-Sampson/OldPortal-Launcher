namespace OPLauncher.Models;

/// <summary>
/// Represents deep link information passed via command line arguments.
/// </summary>
public class DeepLinkInfo
{
    /// <summary>
    /// The server ID to navigate to.
    /// </summary>
    public Guid ServerId { get; set; }

    /// <summary>
    /// The original URI that was parsed.
    /// </summary>
    public string OriginalUri { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new instance of DeepLinkInfo.
    /// </summary>
    /// <param name="serverId">The server ID (Guid).</param>
    /// <param name="originalUri">The original URI string.</param>
    public DeepLinkInfo(Guid serverId, string originalUri)
    {
        ServerId = serverId;
        OriginalUri = originalUri;
    }
}
