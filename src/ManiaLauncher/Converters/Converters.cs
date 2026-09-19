using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ManiaLauncher.Converters;

/// <summary>true -> Visible, false -> Collapsed.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

/// <summary>Inverse of BoolToVisibility (true -> Collapsed).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}

/// <summary>0..1 ratio -> percentage string, e.g. "42 %".</summary>
public sealed class RatioToPercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? $"{d * 100:0}%" : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Ratio -> ProgressBar Value in 0..100.</summary>
public sealed class RatioToHundredConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? d * 100.0 : 0.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>string.IsNullOrEmpty -> Visibility per parameter ("vis"/"col").</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var empty = string.IsNullOrEmpty(value as string);
        var invert = parameter as string == "invert";
        var visible = invert ? empty : !empty;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Formats bytes into a human-readable string.</summary>
public sealed class BytesToHumanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double and not long and not int) return "";
        double bytes = value switch
        {
            double d => d,
            long l => l,
            int i => i,
            _ => 0
        };
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int u = 0;
        while (bytes >= 1024 && u < units.Length - 1) { bytes /= 1024; u++; }
        return $"{bytes:0.#} {units[u]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Non-null object -> true (used to enable buttons).</summary>
public sealed class NullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value != null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Non-null object -> Visible (used for optional UI blocks).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value != null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// "#RRGGBB"/"#AARRGGBB" string -> SolidColorBrush.
/// ConverterParameter is the fallback hex used when the value is null or invalid.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = (value as string)?.Trim();
        if (string.IsNullOrEmpty(hex)) hex = parameter as string;
        if (string.IsNullOrEmpty(hex)) return Brushes.Transparent;

        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch
        {
            try
            {
                return parameter is string fb && !string.IsNullOrEmpty(fb)
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(fb))
                    : Brushes.Transparent;
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
