// TODO: [LAUNCH-151] Phase 4 - PercentToWidthConverter for progress bar
// Component: Launcher
// Module: UI Redesign - Onboarding
// Description: Converts percentage (0-100) to width based on max width parameter

using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OPLauncher.Converters;

/// <summary>
/// Converts a percentage value (0-100) to a width value based on a maximum width parameter.
/// </summary>
public class PercentToWidthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double percentage)
            return 0.0;

        if (parameter is not string paramStr || !double.TryParse(paramStr, out double maxWidth))
            return 0.0;

        // Calculate width: (percentage / 100) * maxWidth
        var width = (percentage / 100.0) * maxWidth;

        // Clamp to valid range
        return Math.Max(0, Math.Min(width, maxWidth));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
