using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UsageNotch.App.Converters;

[ValueConversion(typeof(string), typeof(Brush))]
public sealed class HexBrushConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, SolidColorBrush> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static SolidColorBrush ToBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Brushes.Transparent;
        return Cache.GetOrAdd(hex, static h =>
        {
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(h));
                brush.Freeze();
                return brush;
            }
            catch (Exception e) when (e is FormatException or NotSupportedException)
            {
                return Brushes.Transparent;
            }
        });
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ToBrush(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
