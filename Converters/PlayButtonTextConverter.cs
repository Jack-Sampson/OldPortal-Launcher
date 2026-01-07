// TODO: [LAUNCH-108] Phase 1 Week 3 - PlayButtonTextConverter
// Component: Launcher
// Module: UI Redesign - Card Grid Layout
// Description: Converter for play button text based on server status

using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OPLauncher.Converters;

/// <summary>
/// Converter that returns "PLAY" for online servers, "INFO" for offline servers.
/// </summary>
public class PlayButtonTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isOnline)
        {
            return isOnline ? "PLAY" : "INFO";
        }
        return "INFO";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
