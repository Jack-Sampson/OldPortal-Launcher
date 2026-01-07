// TODO: [LAUNCH-108] Phase 1 Week 3 - FavoriteStarConverter
// Component: Launcher
// Module: UI Redesign - Card Grid Layout
// Description: Converter for favorite star icon (filled vs outline)

using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OPLauncher.Converters;

/// <summary>
/// Converter that returns filled star (⭐) if favorited, outline star (☆) if not.
/// </summary>
public class FavoriteStarConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isFavorite)
        {
            return isFavorite ? "⭐" : "☆";
        }
        return "☆";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
