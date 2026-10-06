using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using UsageNotch.Core.Settings;

namespace UsageNotch.App.Converters;

/// <summary>
/// Fournit des géométries de flèche pré-figées (frozen) selon le bord d'écran,
/// éliminant les réallocations et le parsing répétitif à l'exécution.
/// </summary>
public sealed class ArrowGeometryConverter : IValueConverter
{
    public static readonly Geometry LeftArrow = CreateFrozen("M10,0 L0,10 L10,20 Z");
    public static readonly Geometry TopArrow = CreateFrozen("M0,10 L10,0 L20,10 Z");
    public static readonly Geometry BottomArrow = CreateFrozen("M0,0 L10,10 L20,0 Z");
    public static readonly Geometry RightArrow = CreateFrozen("M0,0 L10,10 L0,20 Z");

    private static Geometry CreateFrozen(string path)
    {
        var geom = Geometry.Parse(path);
        geom.Freeze();
        return geom;
    }

    public static Geometry ForEdge(ScreenEdge edge) => edge switch
    {
        ScreenEdge.Left => LeftArrow,
        ScreenEdge.Top => TopArrow,
        ScreenEdge.Bottom => BottomArrow,
        _ => RightArrow,
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ScreenEdge edge) return ForEdge(edge);
        return RightArrow;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
