using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace UsageNotch.App.Controls;

/// <summary>Anneau qui part de midi et tourne dans le sens horaire. Un changement de <see cref="TargetFraction"/> s'anime en 300 ms.</summary>
public sealed class ProgressRing : FrameworkElement
{
    public static readonly DependencyProperty TargetFractionProperty = DependencyProperty.Register(
        nameof(TargetFraction), typeof(double?), typeof(ProgressRing), new PropertyMetadata(null, OnTargetFractionChanged));

    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
        nameof(Fraction), typeof(double), typeof(ProgressRing), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingBrushProperty = DependencyProperty.Register(
        nameof(RingBrush), typeof(Brush), typeof(ProgressRing), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(ProgressRing), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingThicknessProperty = DependencyProperty.Register(
        nameof(RingThickness), typeof(double), typeof(ProgressRing), new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double? TargetFraction { get => (double?)GetValue(TargetFractionProperty); set => SetValue(TargetFractionProperty, value); }
    public double Fraction { get => (double)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public Brush RingBrush { get => (Brush)GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public double RingThickness { get => (double)GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }

    private static void OnTargetFractionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ring = (ProgressRing)d;
        var to = Math.Clamp((e.NewValue as double?) ?? 0.0, 0.0, 1.0);
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        ring.BeginAnimation(FractionProperty, animation);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= RingThickness) return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size - RingThickness) / 2;
        dc.DrawEllipse(null, new Pen(TrackBrush, RingThickness), center, radius, radius);

        var fraction = Math.Clamp(Fraction, 0.0, 1.0);
        if (fraction <= 0.0005) return;

        var pen = new Pen(RingBrush, RingThickness) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        if (fraction >= 0.9995)
        {
            dc.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        dc.DrawGeometry(null, pen, ArcGeometry(center, radius, fraction));
    }

    /// <summary>Arc de midi vers le sens horaire, pour 0 &lt; fraction &lt; 1. Partagé avec l'icône de notification.</summary>
    public static Geometry ArcGeometry(Point center, double radius, double fraction)
    {
        var angle = fraction * 2 * Math.PI;
        var start = new Point(center.X, center.Y - radius);
        var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.ArcTo(end, new Size(radius, radius), 0, isLargeArc: fraction > 0.5, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();
        return geometry;
    }
}
