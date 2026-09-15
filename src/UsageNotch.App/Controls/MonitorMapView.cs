using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.App.Controls;

/// <summary>Miniature cliquable des écrans : un bouton par écran, l'écran choisi en surbrillance, la pilule en orange.</summary>
public sealed class MonitorMapView : Canvas
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(MonitorMapModel), typeof(MonitorMapView), new PropertyMetadata(null, OnInputChanged));

    public static readonly DependencyProperty SelectCommandProperty = DependencyProperty.Register(
        nameof(SelectCommand), typeof(ICommand), typeof(MonitorMapView), new PropertyMetadata(null, OnInputChanged));

    private static readonly ControlTemplate TileTemplate = (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>"
        + "<Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' "
        + "BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='3'>"
        + "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' /></Border></ControlTemplate>");

    private static readonly SolidColorBrush NormalFill = Frozen(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush NormalBorder = Frozen(Color.FromRgb(0x8A, 0x8A, 0x8A));
    private static readonly SolidColorBrush SelectedFill = Frozen(Color.FromRgb(0xDC, 0xEB, 0xFA));
    private static readonly SolidColorBrush SelectedBorder = Frozen(Color.FromRgb(0x00, 0x67, 0xC0));
    private static readonly SolidColorBrush MarkerFill = Frozen(Color.FromRgb(0xFF, 0x45, 0x00));

    public MonitorMapView()
    {
        Width = PositionPageViewModel.MapWidth;
        Height = PositionPageViewModel.MapHeight;
        ClipToBounds = true;
    }

    public MonitorMapModel? Model
    {
        get => (MonitorMapModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public ICommand? SelectCommand
    {
        get => (ICommand?)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    // Un Canvas n'a pas de pair d'automatisation : sans celui-ci, l'identifiant MonitorMap serait introuvable.
    protected override AutomationPeer OnCreateAutomationPeer() => new MonitorMapAutomationPeer(this);

    private static void OnInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MonitorMapView)d).Rebuild();

    private void Rebuild()
    {
        Children.Clear();
        if (Model is not { } model) return;

        foreach (var tile in model.Tiles)
        {
            var name = tile.IsPrimary ? $"Écran {tile.Number} (principal)" : $"Écran {tile.Number}";
            var button = new Button
            {
                Template = TileTemplate,
                // Valeurs locales : elles l'emportent sur le style implicite des boutons de la fenêtre (MinWidth 90).
                MinWidth = 0,
                Padding = new Thickness(0),
                Width = tile.Rect.Width,
                Height = tile.Rect.Height,
                Background = tile.IsSelected ? SelectedFill : NormalFill,
                BorderBrush = tile.IsSelected ? SelectedBorder : NormalBorder,
                BorderThickness = new Thickness(tile.IsSelected ? 2 : 1),
                Command = SelectCommand,
                CommandParameter = tile.DeviceId,
                Cursor = Cursors.Hand,
                ToolTip = $"{name} — {tile.DeviceId}",
                Content = new TextBlock
                {
                    Text = tile.IsPrimary ? $"{tile.Number.ToString(CultureInfo.InvariantCulture)} (principal)" : tile.Number.ToString(CultureInfo.InvariantCulture),
                    FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            };
            AutomationProperties.SetAutomationId(button, $"MonitorTile{tile.Number.ToString(CultureInfo.InvariantCulture)}");
            AutomationProperties.SetName(button, name);
            SetLeft(button, tile.Rect.X);
            SetTop(button, tile.Rect.Y);
            Children.Add(button);
        }

        if (model.PillMarker is { } marker)
        {
            var rect = new Rectangle { Width = marker.Width, Height = marker.Height, Fill = MarkerFill, IsHitTestVisible = false };
            SetLeft(rect, marker.X);
            SetTop(rect, marker.Y);
            Children.Add(rect);
        }
    }

    private sealed class MonitorMapAutomationPeer(MonitorMapView owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(MonitorMapView);
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
