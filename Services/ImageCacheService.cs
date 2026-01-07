// TODO: [PERF-001] Image Disk Caching Service
// Component: Launcher
// Module: Performance Optimization
// Description: Manages disk-based caching of remote images to improve load times and reduce bandwidth

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace OPLauncher.Services;

/// <summary>
/// Service for managing disk-based caching of remote images.
/// Caches downloaded images for 7 days to improve performance and reduce bandwidth usage.
/// </summary>
public class ImageCacheService
{
    private readonly string _cacheDirectory;
    private readonly LoggingService _logger;
    private const int CacheExpiryDays = 7; // Keep images for 7 days

    /// <summary>
    /// Initializes a new instance of the ImageCacheService.
    /// </summary>
    /// <param name="configService">The configuration service for cache directory.</param>
    /// <param name="logger">The logging service.</param>
    public ImageCacheService(ConfigService configService, LoggingService logger)
    {
        _logger = logger;
        _cacheDirectory = Path.Combine(configService.GetConfigDirectory(), "image_cache");

        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheDirectory);

        _logger.Debug("ImageCacheService initialized with cache directory: {CacheDirectory}", _cacheDirectory);

        // Clean up expired cache entries on startup
        _ = Task.Run(CleanExpiredCacheAsync);
    }

    /// <summary>
    /// Gets the cached image path if it exists and is not expired.
    /// </summary>
    /// <param name="url">The original image URL.</param>
    /// <returns>The cached file path if available, null otherwise.</returns>
    public string? GetCachedImagePath(string url)
    {
        try
        {
            var cacheFileName = GetCacheFileName(url);
            var cachePath = Path.Combine(_cacheDirectory, cacheFileName);

            if (!File.Exists(cachePath))
            {
                _logger.Debug("[ImageCache] Cache miss for: {Url}", url);
                return null;
            }

            var fileAge = DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath);
            if (fileAge.TotalDays > CacheExpiryDays)
            {
                _logger.Debug("[ImageCache] Cache expired for: {Url} (age: {Age} days)", url, fileAge.TotalDays);

                // Delete expired file
                try
                {
                    File.Delete(cachePath);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "[ImageCache] Failed to delete expired cache file: {Path}", cachePath);
                }

                return null;
            }

            _logger.Debug("[ImageCache] Cache hit for: {Url} (age: {Age} hours)", url, fileAge.TotalHours);
            return cachePath;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ImageCache] Error checking cache for: {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Caches an image from a stream.
    /// </summary>
    /// <param name="url">The original image URL.</param>
    /// <param name="imageStream">The image data stream.</param>
    public void CacheImage(string url, Stream imageStream)
    {
        try
        {
            var cacheFileName = GetCacheFileName(url);
            var cachePath = Path.Combine(_cacheDirectory, cacheFileName);

            // Reset stream position if possible
            if (imageStream.CanSeek)
            {
                imageStream.Position = 0;
            }

            // Write to cache file
            using var fileStream = File.Create(cachePath);
            imageStream.CopyTo(fileStream);

            var fileSize = new FileInfo(cachePath).Length;
            _logger.Debug("[ImageCache] Cached image: {Url} ({Size} bytes) -> {Path}", url, fileSize, cachePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ImageCache] Failed to cache image: {Url}", url);
        }
    }

    /// <summary>
    /// Clears all cached images.
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                _logger.Information("[ImageCache] Clearing all cached images");

                var files = Directory.GetFiles(_cacheDirectory);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "[ImageCache] Failed to delete cache file: {File}", file);
                    }
                }

                _logger.Information("[ImageCache] Cleared {Count} cached images", files.Length);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[ImageCache] Error clearing cache");
            }
        });
    }

    /// <summary>
    /// Cleans up expired cache entries.
    /// </summary>
    private async Task CleanExpiredCacheAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                _logger.Debug("[ImageCache] Cleaning expired cache entries");

                var files = Directory.GetFiles(_cacheDirectory);
                var deletedCount = 0;

                foreach (var file in files)
                {
                    try
                    {
                        var fileAge = DateTime.UtcNow - File.GetLastWriteTimeUtc(file);
                        if (fileAge.TotalDays > CacheExpiryDays)
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "[ImageCache] Failed to delete expired file: {File}", file);
                    }
                }

                if (deletedCount > 0)
                {
                    _logger.Information("[ImageCache] Cleaned up {Count} expired cache entries", deletedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[ImageCache] Error cleaning expired cache");
            }
        });
    }

    /// <summary>
    /// Gets the total size of the image cache in bytes.
    /// </summary>
    /// <returns>The total cache size in bytes.</returns>
    public long GetCacheSize()
    {
        try
        {
            var files = Directory.GetFiles(_cacheDirectory);
            return files.Sum(f => new FileInfo(f).Length);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ImageCache] Error calculating cache size");
            return 0;
        }
    }

    /// <summary>
    /// Gets a cache-safe filename from a URL using SHA256 hash.
    /// </summary>
    /// <param name="url">The URL to hash.</param>
    /// <returns>A safe filename for the cached image.</returns>
    private string GetCacheFileName(string url)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
        var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        // Determine file extension from URL
        var extension = ".jpg"; // Default
        if (url.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            extension = ".png";
        else if (url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            extension = ".gif";
        else if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            extension = ".webp";

        return $"{hashString}{extension}";
    }
}
