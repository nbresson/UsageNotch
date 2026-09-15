using System.Globalization;
using System.Windows;
using System.Windows.Data;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.App.Converters;

/// <summary>Visible si l'activité vaut le nom passé en paramètre (ex. « Running »), sinon Collapsed.</summary>
public sealed class ActivityVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ActivityKind kind
        && parameter is string name
        && Enum.TryParse<ActivityKind>(name, out var wanted)
        && kind == wanted
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
