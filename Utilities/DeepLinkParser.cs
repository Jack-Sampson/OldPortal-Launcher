using System;
using System.Text.RegularExpressions;

namespace OPLauncher.Utilities;

/// <summary>
/// Parses deep link URIs in the format: oldportal://launch/{serverId}
/// </summary>
public static class DeepLinkParser
{
    private const string ProtocolScheme = "oldportal";
    private const string LaunchAction = "launch";

    /// <summary>
    /// Attempts to parse a deep link URI.
    /// </summary>
    /// <param name="uri">The URI string to parse (e.g., "oldportal://launch/550e8400-e29b-41d4-a716-446655440000").</param>
    /// <param name="serverId">The parsed server ID (Guid) if successful.</param>
    /// <returns>True if the URI was successfully parsed; otherwise, false.</returns>
    public static bool TryParseLaunchUri(string? uri, out Guid serverId)
    {
        serverId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(uri))
            return false;

        try
        {
            // Remove quotes if present (Windows may wrap protocol URIs in quotes)
            uri = uri.Trim().Trim('"', '\'');

            // Parse as URI
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
                return false;

            // Verify scheme
            if (!parsedUri.Scheme.Equals(ProtocolScheme, StringComparison.OrdinalIgnoreCase))
                return false;

            // Verify host is "launch"
            if (!parsedUri.Host.Equals(LaunchAction, StringComparison.OrdinalIgnoreCase))
                return false;

            // Extract server ID from path (format: /550e8400-e29b-41d4-a716-446655440000)
            var path = parsedUri.AbsolutePath.TrimStart('/');
            if (string.IsNullOrWhiteSpace(path))
                return false;

            // Parse server ID (Guid)
            if (!Guid.TryParse(path, out serverId))
                return false;

            // Validate server ID is not empty
            return serverId != Guid.Empty;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to find a deep link in command line arguments.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <param name="serverId">The parsed server ID (Guid) if found.</param>
    /// <returns>True if a valid deep link was found; otherwise, false.</returns>
    public static bool TryFindDeepLinkInArgs(string[] args, out Guid serverId)
    {
        serverId = Guid.Empty;

        if (args == null || args.Length == 0)
            return false;

        // Check each argument for a valid deep link
        foreach (var arg in args)
        {
            if (TryParseLaunchUri(arg, out serverId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a deep link URI for a given server ID.
    /// </summary>
    /// <param name="serverId">The server ID (Guid).</param>
    /// <returns>A deep link URI string (e.g., "oldportal://launch/550e8400-e29b-41d4-a716-446655440000").</returns>
    public static string CreateLaunchUri(Guid serverId)
    {
        if (serverId == Guid.Empty)
            throw new ArgumentException("Invalid server ID", nameof(serverId));

        return $"{ProtocolScheme}://{LaunchAction}/{serverId}";
    }

    /// <summary>
    /// Checks if a string appears to be a deep link URI.
    /// </summary>
    /// <param name="uri">The string to check.</param>
    /// <returns>True if the string looks like a deep link; otherwise, false.</returns>
    public static bool IsDeepLink(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        uri = uri.Trim().Trim('"', '\'');

        return uri.StartsWith($"{ProtocolScheme}://", StringComparison.OrdinalIgnoreCase);
    }
}
