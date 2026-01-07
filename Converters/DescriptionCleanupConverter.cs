using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using Avalonia.Data.Converters;

namespace OPLauncher.Converters;

/// <summary>
/// Converts HTML and Markdown formatted text to clean plain text.
/// Removes markup while preserving readability and structure.
/// </summary>
public class DescriptionCleanupConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return value;
        }

        // Step 1: Convert common HTML block elements to line breaks
        text = Regex.Replace(text, @"<(br|BR)\s*/?>", "\n", RegexOptions.Compiled);
        text = Regex.Replace(text, @"</(p|P|div|DIV|h[1-6]|H[1-6]|li|LI)>", "\n", RegexOptions.Compiled);
        text = Regex.Replace(text, @"<(ul|UL|ol|OL)>", "\n", RegexOptions.Compiled);
        text = Regex.Replace(text, @"</(ul|UL|ol|OL)>", "\n", RegexOptions.Compiled);

        // Step 2: Remove all HTML tags
        text = Regex.Replace(text, @"<[^>]+>", "", RegexOptions.Compiled);

        // Step 3: Decode HTML entities (&amp; &lt; &gt; &quot; &#39; etc.)
        text = HttpUtility.HtmlDecode(text);

        // Step 4: Clean up Markdown syntax

        // Headers (## Header -> Header)
        text = Regex.Replace(text, @"^#{1,6}\s+(.+)$", "$1", RegexOptions.Multiline | RegexOptions.Compiled);

        // Bold/Italic (**text** or __text__ -> text)
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1", RegexOptions.Compiled);
        text = Regex.Replace(text, @"__(.+?)__", "$1", RegexOptions.Compiled);
        text = Regex.Replace(text, @"\*(.+?)\*", "$1", RegexOptions.Compiled);
        text = Regex.Replace(text, @"_(.+?)_", "$1", RegexOptions.Compiled);

        // Code blocks (```code``` or `code` -> code)
        text = Regex.Replace(text, @"```[\s\S]*?```", "", RegexOptions.Compiled);
        text = Regex.Replace(text, @"`(.+?)`", "$1", RegexOptions.Compiled);

        // Links ([text](url) -> text (url))
        text = Regex.Replace(text, @"\[([^\]]+)\]\(([^\)]+)\)", "$1 ($2)", RegexOptions.Compiled);

        // Images (![alt](url) -> [Image: alt])
        text = Regex.Replace(text, @"!\[([^\]]*)\]\([^\)]+\)", "[Image: $1]", RegexOptions.Compiled);

        // Bullet points (- item or * item -> • item)
        text = Regex.Replace(text, @"^[\s]*[-\*]\s+", "• ", RegexOptions.Multiline | RegexOptions.Compiled);

        // Numbered lists (1. item -> item)
        text = Regex.Replace(text, @"^[\s]*\d+\.\s+", "• ", RegexOptions.Multiline | RegexOptions.Compiled);

        // Blockquotes (> text -> text)
        text = Regex.Replace(text, @"^>\s+", "", RegexOptions.Multiline | RegexOptions.Compiled);

        // Horizontal rules (--- or *** -> empty)
        text = Regex.Replace(text, @"^[\s]*[-\*_]{3,}[\s]*$", "", RegexOptions.Multiline | RegexOptions.Compiled);

        // Step 5: Clean up whitespace

        // Normalize line breaks (Windows/Unix/Mac)
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // Remove excessive blank lines (more than 2 consecutive newlines)
        text = Regex.Replace(text, @"\n{3,}", "\n\n", RegexOptions.Compiled);

        // Remove leading/trailing whitespace from each line
        text = Regex.Replace(text, @"[ \t]+$", "", RegexOptions.Multiline | RegexOptions.Compiled);
        text = Regex.Replace(text, @"^[ \t]+", "", RegexOptions.Multiline | RegexOptions.Compiled);

        // Trim overall whitespace
        text = text.Trim();

        return text;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("DescriptionCleanupConverter is one-way only.");
    }
}
