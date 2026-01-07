// TODO: [LAUNCH-109] Phase 2 Week 4 - NewsDto
// Component: Launcher
// Module: UI Redesign - Home View & News Feed
// Description: News item data transfer object for API and local fallback news

using System;
using System.Text.Json.Serialization;
using OPLauncher.Utilities;

namespace OPLauncher.DTOs;

/// <summary>
/// News item data transfer object.
/// Maps to SharedAPI /api/v1/news endpoint response.
/// </summary>
public class NewsDto
{
    /// <summary>
    /// Unique identifier for the news item.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// News item title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Short excerpt or summary of the news item.
    /// </summary>
    [JsonPropertyName("excerpt")]
    public string Excerpt { get; set; } = string.Empty;

    /// <summary>
    /// URL to the news item's featured image.
    /// </summary>
    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Author of the news item.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = "OldPortal Team";

    /// <summary>
    /// Publication date and time (UTC).
    /// </summary>
    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// News category (launcher, server, community, update).
    /// </summary>
    [JsonPropertyName("category")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NewsCategory Category { get; set; }

    /// <summary>
    /// Full URL to the article (opens in browser).
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets a relative time description (e.g., "2 hours ago", "3 days ago").
    /// </summary>
    [JsonIgnore]
    public string RelativeTime
    {
        get
        {
            var timeSpan = DateTime.UtcNow - PublishedAt;

            if (timeSpan.TotalMinutes < 1)
                return "Just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes == 1 ? "" : "s")} ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours == 1 ? "" : "s")} ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays == 1 ? "" : "s")} ago";
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} month{((int)(timeSpan.TotalDays / 30) == 1 ? "" : "s")} ago";

            return $"{(int)(timeSpan.TotalDays / 365)} year{((int)(timeSpan.TotalDays / 365) == 1 ? "" : "s")} ago";
        }
    }

    /// <summary>
    /// Gets a formatted publication date (e.g., "Nov 15, 2025").
    /// </summary>
    [JsonIgnore]
    public string FormattedDate => PublishedAt.ToString("MMM dd, yyyy");

    /// <summary>
    /// Gets the category display text with emoji.
    /// </summary>
    [JsonIgnore]
    public string CategoryDisplay => Category switch
    {
        NewsCategory.General => "📰 General",
        NewsCategory.Launcher => "🚀 Launcher",
        NewsCategory.Server => "🌐 Server",
        NewsCategory.Community => "👥 Community",
        NewsCategory.Update => "📢 Update",
        NewsCategory.Lore => "📜 Lore",
        NewsCategory.Guide => "📖 Guide",
        NewsCategory.History => "🏛️ History",
        NewsCategory.Nostalgia => "✨ Nostalgia",
        NewsCategory.Servers => "🖥️ Servers",
        NewsCategory.Armory => "⚔️ Armory",
        _ => "📰 News"
    };

    /// <summary>
    /// Gets the full image URL for display.
    /// Uses ImageUrlResolver for unified image resolution with category-specific fallbacks.
    /// Handles relative paths by prepending the base API URL,
    /// and returns category-specific fallback images for null/empty values.
    /// </summary>
    [JsonIgnore]
    public string FullImageUrl
    {
        get
        {
            // Determine fallback type based on news category
            var fallbackType = Category switch
            {
                NewsCategory.Launcher => ImageFallbackType.NewsLauncher,
                NewsCategory.Server => ImageFallbackType.NewsServer,
                NewsCategory.Servers => ImageFallbackType.NewsServer,
                NewsCategory.Community => ImageFallbackType.NewsCommunity,
                NewsCategory.Update => ImageFallbackType.NewsUpdate,
                NewsCategory.General => ImageFallbackType.NewsGeneral,
                NewsCategory.Lore => ImageFallbackType.NewsGeneral,
                NewsCategory.Guide => ImageFallbackType.NewsGeneral,
                NewsCategory.History => ImageFallbackType.NewsGeneral,
                NewsCategory.Nostalgia => ImageFallbackType.NewsCommunity,
                NewsCategory.Armory => ImageFallbackType.NewsGeneral,
                _ => ImageFallbackType.GenericNews
            };

            // Use unified image resolver
            return ImageUrlResolver.ResolveImageUrl(ImageUrl, fallbackType);
        }
    }
}

/// <summary>
/// News category enumeration.
/// Must match SharedAPI NewsCategory enum values.
/// </summary>
public enum NewsCategory
{
    /// <summary>
    /// General announcements and news.
    /// </summary>
    General,

    /// <summary>
    /// Launcher-related news (features, updates, fixes).
    /// </summary>
    Launcher,

    /// <summary>
    /// Server-related news (new servers, events, shutdowns).
    /// </summary>
    Server,

    /// <summary>
    /// Community-related news (achievements, spotlights, discussions).
    /// </summary>
    Community,

    /// <summary>
    /// General platform updates and announcements.
    /// </summary>
    Update,

    /// <summary>
    /// Lore and story-related articles.
    /// </summary>
    Lore,

    /// <summary>
    /// Quest guides and tips.
    /// </summary>
    Guide,

    /// <summary>
    /// Historical content.
    /// </summary>
    History,

    /// <summary>
    /// Nostalgic/throwback content.
    /// </summary>
    Nostalgia,

    /// <summary>
    /// Server-related content (spotlights, profiles, reports).
    /// </summary>
    Servers,

    /// <summary>
    /// Character/armory related content.
    /// </summary>
    Armory
}
