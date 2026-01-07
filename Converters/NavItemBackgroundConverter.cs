// TODO: [LAUNCH-104] Phase 1 Week 2 - NavItemBackgroundConverter
// Component: Launcher
// Module: UI Redesign - Navigation Architecture
// Description: Converter for highlighting active navigation items

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OPLauncher.Converters;

/// <summary>
/// Converter that returns a highlighted background brush for the active navigation item.
/// Color value matches BackgroundTertiary from theme files.
/// </summary>
public class NavItemBackgroundConverter : IValueConverter
{
    // Theme color (DarkTheme.axaml, LightTheme.axaml)
    private static readonly Color ActiveBackgroundColor = Color.Parse("#2A2A2E"); // BackgroundTertiary

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string activeItem && parameter is string itemName)
        {
            // Return highlighted background if this is the active item
            if (string.Equals(activeItem, itemName, StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(ActiveBackgroundColor);
            }
        }

        // Return transparent for inactive items
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
