// TODO: [LAUNCH-108] Phase 1 Week 3 - OnlineStatusColorConverter
// Component: Launcher
// Module: UI Redesign - Card Grid Layout
// Description: Converter for online status indicator color

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OPLauncher.Converters;

/// <summary>
/// Converter that returns green brush for online servers, gray brush for offline servers.
/// Color values match StatusOnline and StatusOffline from theme files.
/// </summary>
public class OnlineStatusColorConverter : IValueConverter
{
    // Theme colors (DarkTheme.axaml, LightTheme.axaml)
    private static readonly Color StatusOnlineColor = Color.Parse("#10B981");   // Green
    private static readonly Color StatusOfflineColor = Color.Parse("#6B7280");  // Gray

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isOnline)
        {
            return isOnline
                ? new SolidColorBrush(StatusOnlineColor)
                : new SolidColorBrush(StatusOfflineColor);
        }
        return new SolidColorBrush(StatusOfflineColor); // Default to offline
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
