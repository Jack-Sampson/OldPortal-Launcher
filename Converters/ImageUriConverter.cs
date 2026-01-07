// TODO: [LAUNCH-118] Image URI to Bitmap Converter
// Component: Launcher
// Module: Converters - Image Loading
// Description: Converts URI strings (avares:// or https://) to Avalonia Bitmap objects with disk caching

using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OPLauncher.Services;
using Serilog;

namespace OPLauncher.Converters;

/// <summary>
/// Converts image URI strings to Avalonia Bitmap objects.
/// Supports both embedded resources (avares://) and HTTP/HTTPS URLs.
/// Implements disk caching for remote images to improve performance and reduce bandwidth.
/// </summary>
public class ImageUriConverter : IValueConverter
{
    private static readonly HttpClient _httpClient = new();
    private static ImageCacheService? _imageCacheService;

    /// <summary>
    /// Initializes the image cache service (called from App initialization).
    /// </summary>
    public static void Initialize(ImageCacheService imageCacheService)
    {
        _imageCacheService = imageCacheService;
        Log.Information("[ImageUriConverter] Initialized with image caching enabled");
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string uriString || string.IsNullOrWhiteSpace(uriString))
        {
            Log.Warning("[ImageUriConverter] Received null or empty URI string, using fallback");
            return LoadFallbackImage();
        }

        try
        {
            var uri = new Uri(uriString, UriKind.RelativeOrAbsolute);

            // Handle avares:// URIs (embedded resources)
            if (uri.Scheme == "avares")
            {
                Log.Information("[ImageUriConverter] Loading embedded resource: {Uri}", uriString);
                try
                {
                    var assets = AssetLoader.Open(uri);
                    return new Bitmap(assets);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[ImageUriConverter] Failed to load embedded resource: {Uri}, using fallback", uriString);
                    return LoadFallbackImage();
                }
            }

            // Handle HTTP/HTTPS URIs (remote images)
            if (uri.Scheme == "http" || uri.Scheme == "https")
            {
                Log.Information("[ImageUriConverter] Loading remote image: {Uri}", uriString);

                // Return a placeholder that triggers async loading
                // We'll load on the thread pool and return the bitmap synchronously by blocking
                // This is acceptable for image loading as Avalonia handles it on background threads
                var bitmap = LoadRemoteImageSync(uriString);
                return bitmap ?? LoadFallbackImage();
            }

            Log.Warning("[ImageUriConverter] Unsupported URI scheme: {Scheme} in {Uri}, using fallback", uri.Scheme, uriString);
            return LoadFallbackImage();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ImageUriConverter] Failed to load image from URI: {Uri}, using fallback", uriString);
            return LoadFallbackImage();
        }
    }

    private static Bitmap? LoadFallbackImage()
    {
        try
        {
            var fallbackUri = new Uri("avares://OPLauncher/Assets/Images/Banners/custom-banner.png");
            var assets = AssetLoader.Open(fallbackUri);
            return new Bitmap(assets);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ImageUriConverter] Failed to load fallback image");
            return null;
        }
    }

    private static Bitmap? LoadRemoteImageSync(string url)
    {
        try
        {
            // Step 1: Check disk cache first (if cache service is initialized)
            if (_imageCacheService != null)
            {
                var cachedPath = _imageCacheService.GetCachedImagePath(url);
                if (!string.IsNullOrEmpty(cachedPath))
                {
                    try
                    {
                        Log.Information("[ImageUriConverter] Loading image from cache: {Url}", url);
                        return new Bitmap(cachedPath);
                    }
                    catch (Exception cacheEx)
                    {
                        Log.Warning(cacheEx, "[ImageUriConverter] Failed to load cached image, downloading fresh: {Url}", url);
                    }
                }
            }

            // Step 2: Download from network
            Log.Information("[ImageUriConverter] Downloading image from network: {Url}", url);

            // Use GetAwaiter().GetResult() to synchronously wait for the async operation
            // This is acceptable in a converter as Avalonia calls converters on background threads for images
            var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
            Log.Information("[ImageUriConverter] HTTP response: {StatusCode} for {Url}", response.StatusCode, url);

            response.EnsureSuccessStatusCode();

            using var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            Log.Information("[ImageUriConverter] Downloaded {Bytes} bytes from {Url}", stream.Length, url);

            // Load bitmap from stream - must copy to MemoryStream as the HTTP stream will be disposed
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            var bitmap = new Bitmap(memoryStream);
            Log.Information("[ImageUriConverter] Successfully created bitmap from {Url}", url);

            // Step 3: Cache the image for next time
            if (_imageCacheService != null)
            {
                try
                {
                    memoryStream.Position = 0;
                    _imageCacheService.CacheImage(url, memoryStream);
                }
                catch (Exception cacheEx)
                {
                    Log.Warning(cacheEx, "[ImageUriConverter] Failed to cache image: {Url}", url);
                }
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ImageUriConverter] Failed to download remote image: {Url}", url);
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("ImageUriConverter only supports one-way binding");
    }
}
