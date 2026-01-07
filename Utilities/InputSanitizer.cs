using System.Text;
using System.Text.RegularExpressions;

namespace OPLauncher.Utilities;

/// <summary>
/// Provides input sanitization and validation methods to prevent injection attacks.
/// </summary>
public static class InputSanitizer
{
    /// <summary>
    /// Sanitizes a string by removing or escaping potentially dangerous characters.
    /// Useful for preventing XSS and injection attacks.
    /// </summary>
    /// <param name="input">The input string to sanitize.</param>
    /// <param name="allowedCharacters">Optional regex pattern of allowed characters. If null, uses default safe character set.</param>
    /// <returns>Sanitized string safe for use in the application.</returns>
    public static string? Sanitize(string? input, string? allowedCharacters = null)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Trim whitespace
        var sanitized = input.Trim();

        // If custom allowed characters pattern provided, use it
        if (!string.IsNullOrWhiteSpace(allowedCharacters))
        {
            sanitized = Regex.Replace(sanitized, allowedCharacters, string.Empty);
            return sanitized;
        }

        // Default: Remove control characters and potentially dangerous characters
        sanitized = RemoveControlCharacters(sanitized);
        sanitized = RemoveDangerousCharacters(sanitized);

        return sanitized;
    }

    /// <summary>
    /// Sanitizes an email address, ensuring it follows proper email format.
    /// </summary>
    /// <param name="email">The email address to sanitize.</param>
    /// <returns>Sanitized email address or null if invalid.</returns>
    public static string? SanitizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        // Trim and convert to lowercase
        var sanitized = email.Trim().ToLowerInvariant();

        // Basic email validation regex
        var emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

        if (!Regex.IsMatch(sanitized, emailPattern))
            return null;

        return sanitized;
    }

    /// <summary>
    /// Sanitizes a username, allowing only alphanumeric characters, underscores, and hyphens.
    /// </summary>
    /// <param name="username">The username to sanitize.</param>
    /// <param name="maxLength">Maximum allowed length (default: 50).</param>
    /// <returns>Sanitized username.</returns>
    public static string? SanitizeUsername(string? username, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        // Remove whitespace and convert to lowercase
        var sanitized = username.Trim();

        // Allow only alphanumeric, underscore, hyphen, and period
        sanitized = Regex.Replace(sanitized, @"[^a-zA-Z0-9_\-\.]", string.Empty);

        // Limit length
        if (sanitized.Length > maxLength)
            sanitized = sanitized.Substring(0, maxLength);

        return sanitized;
    }

    /// <summary>
    /// Sanitizes a display name, allowing letters, numbers, spaces, and common punctuation.
    /// </summary>
    /// <param name="displayName">The display name to sanitize.</param>
    /// <param name="maxLength">Maximum allowed length (default: 100).</param>
    /// <returns>Sanitized display name.</returns>
    public static string? SanitizeDisplayName(string? displayName, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        // Trim whitespace
        var sanitized = displayName.Trim();

        // Remove control characters
        sanitized = RemoveControlCharacters(sanitized);

        // Allow only letters, numbers, spaces, and common punctuation
        sanitized = Regex.Replace(sanitized, @"[^a-zA-Z0-9\s\-_.,!?'()]", string.Empty);

        // Remove multiple consecutive spaces
        sanitized = Regex.Replace(sanitized, @"\s+", " ");

        // Limit length
        if (sanitized.Length > maxLength)
            sanitized = sanitized.Substring(0, maxLength);

        return sanitized;
    }

    /// <summary>
    /// Validates and sanitizes a world ID, ensuring it's a positive integer.
    /// </summary>
    /// <param name="worldId">The world ID to validate.</param>
    /// <returns>True if the world ID is valid; otherwise, false.</returns>
    public static bool IsValidWorldId(int worldId)
    {
        return worldId > 0;
    }

    /// <summary>
    /// Validates and sanitizes a URL, ensuring it uses HTTPS.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <param name="requireHttps">If true, only allows HTTPS URLs (default: true).</param>
    /// <returns>Sanitized URL or null if invalid.</returns>
    public static string? SanitizeUrl(string? url, bool requireHttps = true)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // Trim whitespace
        var sanitized = url.Trim();

        // Try to parse as URI
        if (!Uri.TryCreate(sanitized, UriKind.Absolute, out var uri))
            return null;

        // Check scheme
        if (requireHttps && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        if (!requireHttps && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return null;

        return uri.ToString();
    }

    /// <summary>
    /// Removes control characters (non-printable characters) from a string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>String without control characters.</returns>
    private static string RemoveControlCharacters(string input)
    {
        var sb = new StringBuilder(input.Length);

        foreach (var c in input)
        {
            // Keep only printable characters and common whitespace
            if (!char.IsControl(c) || c == '\n' || c == '\r' || c == '\t')
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Removes potentially dangerous characters that could be used in injection attacks.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>String without dangerous characters.</returns>
    private static string RemoveDangerousCharacters(string input)
    {
        // Remove characters commonly used in injection attacks
        // This is a basic filter - API-side validation is still required
        var dangerous = new[] { '<', '>', '"', '\'', '&', ';', '|', '`', '$', '\\', '\0' };

        var sb = new StringBuilder(input.Length);

        foreach (var c in input)
        {
            if (!dangerous.Contains(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Validates a search query string, ensuring it doesn't contain injection patterns.
    /// </summary>
    /// <param name="searchQuery">The search query to validate.</param>
    /// <param name="maxLength">Maximum allowed length (default: 200).</param>
    /// <returns>Sanitized search query or null if invalid.</returns>
    public static string? SanitizeSearchQuery(string? searchQuery, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
            return null;

        // Trim whitespace
        var sanitized = searchQuery.Trim();

        // Remove control characters
        sanitized = RemoveControlCharacters(sanitized);

        // Allow letters, numbers, spaces, and basic punctuation
        sanitized = Regex.Replace(sanitized, @"[^a-zA-Z0-9\s\-_.,!?()'""&]", string.Empty);

        // Remove multiple consecutive spaces
        sanitized = Regex.Replace(sanitized, @"\s+", " ");

        // Limit length
        if (sanitized.Length > maxLength)
            sanitized = sanitized.Substring(0, maxLength);

        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    /// <summary>
    /// Validates an integer input to ensure it's within acceptable range.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="min">Minimum allowed value.</param>
    /// <param name="max">Maximum allowed value.</param>
    /// <returns>True if the value is within range; otherwise, false.</returns>
    public static bool IsIntInRange(int value, int min, int max)
    {
        return value >= min && value <= max;
    }

    /// <summary>
    /// Sanitizes a file path, removing directory traversal patterns.
    /// </summary>
    /// <param name="filePath">The file path to sanitize.</param>
    /// <returns>Sanitized file path or null if potentially malicious.</returns>
    public static string? SanitizeFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        var sanitized = filePath.Trim();

        // Check for directory traversal patterns
        if (sanitized.Contains("..") || sanitized.Contains("~"))
            return null;

        // Remove potentially dangerous characters
        sanitized = Regex.Replace(sanitized, @"[<>:""|?*]", string.Empty);

        return sanitized;
    }
}
