// Component: OPLauncher
// TODO: [LAUNCH-Migration] Create DTOs matching SharedAPI structure
// Description: World/server data DTO mapping to SharedAPI ServerListResponse/ServerDetailsResponse

using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OPLauncher.DTOs;

/// <summary>
/// World/server data transfer object
/// Maps to SharedAPI ServerListResponse and ServerDetailsResponse
/// Made observable to support local UDP status checking alongside API data
/// </summary>
public partial class WorldDto : ObservableObject
{
    /// <summary>
    /// Server ID as Guid (from API) - also exposed as WorldId for compatibility
    /// </summary>
    [JsonPropertyName("serverId")]
    public Guid ServerId { get; set; }

    /// <summary>
    /// Server ID (Guid from API, mapped to int for local usage)
    /// Generated from ServerId hash code for backward compatibility
    /// </summary>
    [JsonIgnore]
    public int Id => ServerId.GetHashCode();

    /// <summary>
    /// Alias for ServerId for compatibility with reference code
    /// </summary>
    [JsonIgnore]
    public Guid WorldId => ServerId;

    /// <summary>
    /// Server display name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL-friendly slug for the server (e.g., "asheron4funcom")
    /// Used for constructing web URLs to the server's page on oldportal.com
    /// </summary>
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    /// <summary>
    /// Server hostname or IP address
    /// </summary>
    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Server port number
    /// </summary>
    [JsonPropertyName("port")]
    public int Port { get; set; }

    /// <summary>
    /// Emulator type (ACE or GDLE)
    /// </summary>
    [JsonPropertyName("serverType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ServerType ServerType { get; set; }

    /// <summary>
    /// Server description (nullable)
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Server website URL (nullable)
    /// </summary>
    [JsonPropertyName("websiteUrl")]
    public string? WebsiteUrl { get; set; }

    /// <summary>
    /// Discord invite URL (nullable)
    /// </summary>
    [JsonPropertyName("discordUrl")]
    public string? DiscordUrl { get; set; }

    /// <summary>
    /// Optional card banner image URL (displayed on Browse Worlds card)
    /// Recommended: 400x150px, Formats: PNG, JPG, JPEG, GIF
    /// Falls back to OldPortal branding if null
    /// </summary>
    [JsonPropertyName("cardBannerImageUrl")]
    public string? CardBannerImageUrl { get; set; }

    /// <summary>
    /// Whether the server is currently online.
    /// Value from API, but can be overridden by local UDP check.
    /// </summary>
    [JsonPropertyName("isOnline")]
    [ObservableProperty]
    private bool _isOnline;

    /// <summary>
    /// Current player count.
    /// Value from API, but can be overridden by local UDP check.
    /// </summary>
    [JsonPropertyName("playerCount")]
    [ObservableProperty]
    private int _playerCount;

    /// <summary>
    /// Server uptime percentage (0-100)
    /// </summary>
    [JsonPropertyName("uptimePercentage")]
    public double UptimePercentage { get; set; }

    /// <summary>
    /// Whether the server has been verified (activity in last 14 days)
    /// </summary>
    [JsonPropertyName("verifiedLast14Days")]
    public bool VerifiedLast14Days { get; set; }

    /// <summary>
    /// Whether the server owner has sponsor badge status
    /// </summary>
    [JsonPropertyName("isSponsor")]
    public bool IsSponsor { get; set; }

    /// <summary>
    /// Server registration timestamp (UTC)
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last update timestamp (UTC)
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Last heartbeat timestamp (UTC, nullable)
    /// </summary>
    [JsonPropertyName("lastHeartbeat")]
    public DateTime? LastHeartbeat { get; set; }

    /// <summary>
    /// Community Hub thread ID for this server
    /// </summary>
    [JsonPropertyName("communityThreadId")]
    public Guid? CommunityThreadId { get; set; }

    /// <summary>
    /// Server tags as comma-separated string from API
    /// </summary>
    [JsonPropertyName("tags")]
    public string? TagsString { get; set; }

    /// <summary>
    /// Server tags parsed as list (e.g., "PvP", "Roleplay", "New Player Friendly")
    /// </summary>
    [JsonIgnore]
    public List<string> Tags
    {
        get
        {
            if (string.IsNullOrWhiteSpace(TagsString))
                return new List<string>();

            return TagsString
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
        }
    }

    /// <summary>
    /// Server ruleset (PvE, PvP, RP, Retail, Custom)
    /// Nullable to handle API responses that don't include this field yet
    /// </summary>
    [JsonPropertyName("ruleSet")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RuleSet? RuleSet { get; set; }

    /// <summary>
    /// Gets the effective ruleset, defaulting to Custom if not specified
    /// </summary>
    [JsonIgnore]
    public RuleSet EffectiveRuleSet => RuleSet ?? DTOs.RuleSet.Custom;

    /// <summary>
    /// When the online status was last checked locally via UDP.
    /// Used for adaptive polling (not serialized to/from API).
    /// </summary>
    [JsonIgnore]
    [ObservableProperty]
    private DateTime? _lastStatusCheck;

    /// <summary>
    /// Interval in seconds to check server status when server is ONLINE.
    /// Default: 300 seconds (5 minutes) - reduces unnecessary checks for stable servers.
    /// </summary>
    [JsonIgnore]
    public int StatusOnlineIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Interval in seconds to check server status when server is OFFLINE.
    /// Default: 15 seconds - faster response to server recovery.
    /// </summary>
    [JsonIgnore]
    public int StatusOfflineIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Maximum player capacity for the server
    /// </summary>
    [JsonPropertyName("maxPlayers")]
    public int MaxPlayers { get; set; } = 100;

    /// <summary>
    /// Whether the server is verified (alias for VerifiedLast14Days)
    /// </summary>
    [JsonIgnore]
    public bool IsVerified => VerifiedLast14Days;

    /// <summary>
    /// Calculated status based on IsOnline
    /// </summary>
    [JsonIgnore]
    public WorldStatus Status => IsOnline ? WorldStatus.Online : WorldStatus.Offline;

    /// <summary>
    /// Gets connection information for game launch
    /// </summary>
    public WorldConnectionDto ToConnectionInfo()
    {
        return new WorldConnectionDto
        {
            Host = Host,
            Port = Port,
            ServerType = ServerType,
            Name = Name,
            Description = Description
        };
    }
}
