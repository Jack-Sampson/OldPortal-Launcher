using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OPLauncher.Converters;

/// <summary>
/// Converter that returns true if the enum value equals the parameter.
/// Used for showing/hiding UI elements based on current enum state.
/// </summary>
public class EnumEqualityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        // Get the enum value as string
        var valueStr = value.ToString();
        var paramStr = parameter.ToString();

        return string.Equals(valueStr, paramStr, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter that returns true if the enum value does NOT equal the parameter.
/// Inverse of EnumEqualityConverter.
/// </summary>
public class EnumNotEqualConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return true;

        // Get the enum value as string
        var valueStr = value.ToString();
        var paramStr = parameter.ToString();

        return !string.Equals(valueStr, paramStr, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
