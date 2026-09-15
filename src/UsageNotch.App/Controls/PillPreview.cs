using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using UsageNotch.App.Converters;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Pill;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.App.Controls;

/// <summary>
/// Aperçu statique des pilules d'exemple : même forme, mêmes couleurs, même échelle que la vraie pilule, sans animation en
/// boucle (aucun coût processeur au repos). En mode Replié, la bande de chaque exemple est dessinée à côté de sa pilule.
/// </summary>
public sealed class PillPreview : ContentControl
{
    public const string HiddenNote = "Mode Masqué : la pilule n'est pas affichée ; l'icône de notification reste disponible.";

    private const double SampleSpacing = 18;
    private const double BandGap = 10;
    private const double EdgeLineThickness = 2;

    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(PreviewModel), typeof(PillPreview), new PropertyMetadata(null, (d, _) => ((PillPreview)d).Rebuild()));

    private static readonly SolidColorBrush CaptionBrush = Frozen(Color.FromRgb(0x9A, 0x9A, 0x9A));
    private static readonly SolidColorBrush EdgeBrush = Frozen(Color.FromRgb(0x80, 0x80, 0x80));
    private static readonly SolidColorBrush NoteBackground = Frozen(Color.FromArgb(0xB0, 0x00, 0x00, 0x00));
    private static readonly Geometry RunningArc = FrozenGeometry("M 22,8 A 14,14 0 0 1 36,22");

    public PillPreview()
    {
        Focusable = false;
        IsTabStop = false;
    }

    public PreviewModel? Model
    {
        get => (PreviewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private void Rebuild()
    {
        if (Model is not { } model)
        {
            Content = null;
            return;
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var sample in model.Samples) row.Children.Add(BuildSample(model, sample));

        var root = new StackPanel();
        root.Children.Add(row);
        if (model.Visibility == VisibilityMode.Hidden)
        {
            root.Children.Add(new Border
            {
                Background = NoteBackground,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock { Text = HiddenNote, Foreground = Brushes.White, FontSize = 12 },
            });
        }
        Content = root;
    }

    private static StackPanel BuildSample(PreviewModel model, PreviewSample sample)
    {
        var vertical = model.Edge is ScreenEdge.Right or ScreenEdge.Left;
        var thickness = PillMetrics.Thickness * model.Scale;
        var length = PillMetrics.WindowLength * model.Scale;
        var fillet = PillMetrics.Fillet * model.Scale;

        var shapes = new StackPanel
        {
            Orientation = vertical ? Orientation.Horizontal : Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        shapes.Children.Add(BuildPill(model, sample.Cell, thickness, length, fillet, vertical));
        if (model.Visibility == VisibilityMode.Folded)
        {
            var band = BuildBand(model, sample.Cell, thickness, length, fillet, vertical);
            band.Margin = vertical ? new Thickness(BandGap, 0, 0, 0) : new Thickness(0, BandGap, 0, 0);
            shapes.Children.Add(band);
        }

        var column = new StackPanel { Margin = new Thickness(SampleSpacing, 0, SampleSpacing, 0), VerticalAlignment = VerticalAlignment.Center };
        column.Children.Add(shapes);
        column.Children.Add(new TextBlock
        {
            Text = sample.Caption,
            FontSize = 11,
            Foreground = CaptionBrush,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        return column;
    }

    private static Canvas BuildPill(PreviewModel model, CellModel cell, double thickness, double length, double fillet, bool vertical)
    {
        var theme = model.Theme;
        var canvas = new Canvas { Width = vertical ? thickness : length, Height = vertical ? length : thickness };
        canvas.Children.Add(new Path
        {
            Data = PillShapeBuilder.Pill(model.Edge, thickness, length, PillMetrics.CornerRadius * model.Scale, fillet),
            Fill = HexBrushConverter.ToBrush(theme.PillBackground),
            Stroke = HexBrushConverter.ToBrush(theme.PillBorder),
            StrokeThickness = 1,
            Opacity = theme.PillOpacity,
        });

        var bodyRect = PillShapeBuilder.Body(model.Edge, thickness, length, fillet);
        var body = new Grid { Width = bodyRect.Width, Height = bodyRect.Height, Opacity = cell.Dimmed ? 0.5 : 1.0 };
        Canvas.SetLeft(body, bodyRect.X);
        Canvas.SetTop(body, bodyRect.Y);

        var stack = new StackPanel
        {
            Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LayoutTransform = new ScaleTransform(model.Scale, model.Scale),
        };
        if (cell.ShowRing) stack.Children.Add(BuildRing(cell, vertical));
        if (cell.ShowPercent)
        {
            stack.Children.Add(new TextBlock
            {
                Text = cell.PercentText,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = HexBrushConverter.ToBrush(cell.TextColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        body.Children.Add(stack);
        canvas.Children.Add(body);
        canvas.Children.Add(EdgeLine(model.Edge, canvas.Width, canvas.Height));
        return canvas;
    }

    private static Grid BuildRing(CellModel cell, bool vertical)
    {
        var host = new Grid
        {
            Width = PillMetrics.RingSize,
            Height = PillMetrics.RingSize,
            Margin = vertical ? new Thickness(0, 0, 0, 4) : new Thickness(0, 0, 6, 0),
        };
        host.Children.Add(new ProgressRing
        {
            RingThickness = 5,
            RingBrush = HexBrushConverter.ToBrush(cell.RingColor),
            TrackBrush = HexBrushConverter.ToBrush(cell.TrackColor),
            // Fraction d'abord : l'animation lancée par TargetFraction part alors de la valeur finale, sans balayage à chaque retouche.
            Fraction = cell.RingFraction ?? 0,
            TargetFraction = cell.RingFraction,
        });

        var activity = HexBrushConverter.ToBrush(cell.ActivityColor);
        switch (cell.Activity)
        {
            case ActivityKind.Running:
                host.Children.Add(new Path
                {
                    Width = PillMetrics.RingSize,
                    Height = PillMetrics.RingSize,
                    Stretch = Stretch.None,
                    Data = RunningArc,
                    StrokeThickness = 2.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Stroke = activity,
                });
                break;
            case ActivityKind.Attention:
                host.Children.Add(new Ellipse { Width = 28, Height = 28, StrokeThickness = 2.5, Stroke = activity });
                break;
            case ActivityKind.Done:
                host.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = activity });
                break;
        }
        return host;
    }

    private static Canvas BuildBand(PreviewModel model, CellModel cell, double thickness, double length, double fillet, bool vertical)
    {
        var canvas = new Canvas { Width = vertical ? thickness : length, Height = vertical ? length : thickness };
        var band = PillShapeBuilder.Band(model.Edge, thickness, length, model.FoldedThicknessPx, fillet);
        var rect = new Rectangle { Width = band.Width, Height = band.Height, Fill = HexBrushConverter.ToBrush(cell.BandColor) };
        Canvas.SetLeft(rect, band.X);
        Canvas.SetTop(rect, band.Y);
        canvas.Children.Add(rect);
        canvas.Children.Add(EdgeLine(model.Edge, canvas.Width, canvas.Height));
        return canvas;
    }

    /// <summary>Un trait gris figure le bord de l'écran, pour lire l'orientation de la pilule.</summary>
    private static Rectangle EdgeLine(ScreenEdge edge, double width, double height)
    {
        var alongHeight = edge is ScreenEdge.Right or ScreenEdge.Left;
        var line = new Rectangle
        {
            Fill = EdgeBrush,
            Width = alongHeight ? EdgeLineThickness : width,
            Height = alongHeight ? height : EdgeLineThickness,
        };
        Canvas.SetLeft(line, edge switch { ScreenEdge.Right => width, ScreenEdge.Left => -EdgeLineThickness, _ => 0 });
        Canvas.SetTop(line, edge switch { ScreenEdge.Bottom => height, ScreenEdge.Top => -EdgeLineThickness, _ => 0 });
        return line;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Geometry FrozenGeometry(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }
}
