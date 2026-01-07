// TODO: [LAUNCH-118] Unified Image URL Resolution
// Component: Launcher
// Module: Utilities - Image Resolution
// Description: Centralized image URL resolver with fallback support for news and server banners

using System;
using System.Diagnostics;
using Serilog;

namespace OPLauncher.Utilities;

/// <summary>
/// Centralized image URL resolver with intelligent fallback support.
/// Handles resolution for news banners, server banners, and other remote images.
/// </summary>
public static class ImageUrlResolver
{
    private const string ApiBaseUrl = "https://oldportal.com";

    /// <summary>
    /// Resolves image URL with fallback support.
    /// Handles external URLs, API-relative paths, local assets, and null/empty values.
    /// </summary>
    /// <param name="imageUrl">URL from API (can be null, relative, or absolute)</param>
    /// <param name="fallbackType">Type of fallback to use when imageUrl is null/empty</param>
    /// <returns>Resolved image URL or Avalonia resource URI</returns>
    public static string ResolveImageUrl(string? imageUrl, ImageFallbackType fallbackType)
    {
        // Empty/null: use typed fallback
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            var fallback = GetFallbackImage(fallbackType);
            Log.Information("[ImageUrlResolver] Using fallback for {FallbackType}: {FallbackUrl}", fallbackType, fallback);
            Debug.WriteLine($"[ImageUrlResolver] Using fallback for {fallbackType}: {fallback}");
            return fallback;
        }

        // Already a full URL (external hosting): return as-is
        if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("[ImageUrlResolver] External URL: {ImageUrl}", imageUrl);
            Debug.WriteLine($"[ImageUrlResolver] External URL: {imageUrl}");
            return imageUrl;
        }

        // Local asset path: convert to Avalonia resource URI (assets are embedded as AvaloniaResource)
        if (imageUrl.StartsWith("/Assets/", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = $"avares://OPLauncher{imageUrl}";
            Log.Information("[ImageUrlResolver] Local asset: {OriginalPath} -> {ResolvedUri}", imageUrl, resolved);
            Debug.WriteLine($"[ImageUrlResolver] Local asset: {imageUrl} -> {resolved}");
            return resolved;
        }

        // Relative API path (e.g., "/images/servers/card-banners/abc.png"): prepend base URL
        var apiUrl = $"{ApiBaseUrl}{imageUrl}";
        Log.Information("[ImageUrlResolver] API path: {RelativePath} -> {FullUrl}", imageUrl, apiUrl);
        Debug.WriteLine($"[ImageUrlResolver] API path: {imageUrl} -> {apiUrl}");
        return apiUrl;
    }

    /// <summary>
    /// Gets the appropriate fallback image based on the fallback type.
    /// All fallback images are bundled launcher assets embedded as AvaloniaResource.
    /// Returns Avalonia resource URIs (avares://) which resolve embedded resources from the assembly.
    /// </summary>
    /// <param name="type">Type of fallback image to retrieve</param>
    /// <returns>Avalonia resource URI for the fallback image</returns>
    private static string GetFallbackImage(ImageFallbackType type)
    {
        return type switch
        {
            // News category fallbacks
            ImageFallbackType.NewsGeneral => "avares://OPLauncher/Assets/Images/Banners/news-general.png",
            ImageFallbackType.NewsLauncher => "avares://OPLauncher/Assets/Images/Banners/news-launcher.png",
            ImageFallbackType.NewsServer => "avares://OPLauncher/Assets/Images/Banners/news-server.png",
            ImageFallbackType.NewsCommunity => "avares://OPLauncher/Assets/Images/Banners/news-community.png",
            ImageFallbackType.NewsUpdate => "avares://OPLauncher/Assets/Images/Banners/news-update.png",

            // Server ruleset fallbacks
            ImageFallbackType.ServerPvP => "avares://OPLauncher/Assets/Images/Banners/pvp-banner.png",
            ImageFallbackType.ServerPvE => "avares://OPLauncher/Assets/Images/Banners/pve-banner.png",
            ImageFallbackType.ServerRP => "avares://OPLauncher/Assets/Images/Banners/rp-banner.png",
            ImageFallbackType.ServerRetail => "avares://OPLauncher/Assets/Images/Banners/retail-banner.png",
            ImageFallbackType.ServerCustom => "avares://OPLauncher/Assets/Images/Banners/custom-banner.png",
            ImageFallbackType.ServerHardcore => "avares://OPLauncher/Assets/Images/Banners/hardcore-banner.png",

            // Generic fallbacks
            ImageFallbackType.GenericNews => "avares://OPLauncher/Assets/Images/Banners/news-general.png",
            ImageFallbackType.GenericServer => "avares://OPLauncher/Assets/Images/Banners/custom-banner.png",

            // Ultimate fallback
            _ => "avares://OPLauncher/Assets/Images/Banners/custom-banner.png"
        };
    }
}

/// <summary>
/// Enumeration of fallback image types for different contexts.
/// Used by ImageUrlResolver to select appropriate default images.
/// </summary>
public enum ImageFallbackType
{
    // News category fallbacks (category-specific)
    NewsGeneral,
    NewsLauncher,
    NewsServer,
    NewsCommunity,
    NewsUpdate,

    // Server ruleset fallbacks (ruleset-specific)
    ServerPvP,
    ServerPvE,
    ServerRP,
    ServerRetail,
    ServerCustom,
    ServerHardcore,

    // Generic fallbacks (non-specific)
    GenericNews,
    GenericServer
}
