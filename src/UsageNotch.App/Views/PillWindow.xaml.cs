using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using UsageNotch.App.Controls;
using UsageNotch.App.Interop;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Pill;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Views;

public partial class PillWindow : Window
{
    private static readonly TimeSpan FoldAnimation = TimeSpan.FromMilliseconds(200);

    private readonly NotchViewModel _vm;
    private readonly NotchPlacer _placer;
    private readonly SettingsStore _settings;
    private readonly Func<Task> _quit;
    private readonly Action _openSettings;

    private bool _initialized;
    private bool _dragging;
    private double _dragFraction;
    private double _thicknessDip = PillMetrics.Thickness;

    public PillWindow(NotchViewModel vm, NotchPlacer placer, SettingsStore settings, Func<Task> quit, Action openSettings)
    {
        _vm = vm;
        _placer = placer;
        _settings = settings;
        _quit = quit;
        _openSettings = openSettings;

        InitializeComponent();
        DataContext = vm;

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            ((Storyboard)Resources["Spin"]).Begin(this, isControllable: true);
            ((Storyboard)Resources["Pulse"]).Begin(this, isControllable: true);
        };
        MouseEnter += (_, _) => { if (!_dragging) _vm.PointerEnteredPill(); };
        MouseLeave += (_, _) => { if (!_dragging) _vm.PointerLeftPill(); };
        PreviewMouseLeftButtonDown += OnLeftButtonDown;
        PreviewMouseLeftButtonUp += OnLeftButtonUp;
        MouseMove += OnMouseMove;
        _vm.PropertyChanged += OnViewModelChanged;
    }

    public nint Handle { get; private set; }

    public PlacementResult? Placement { get; private set; }

    public event Action? PlacementChanged;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        Handle = new WindowInteropHelper(this).Handle;
        WindowStyles.MakeToolWindowNoActivate(Handle);
        HwndSource.FromHwnd(Handle)!.AddHook(WndProc);
        _initialized = true;
        ApplySettings(fromSourceInitialized: true);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_MOUSEACTIVATE:
                handled = true;
                return NativeMethods.MA_NOACTIVATE;
            case NativeMethods.WM_DISPLAYCHANGE:
            case NativeMethods.WM_SETTINGCHANGE:
            case NativeMethods.WM_DPICHANGED:
                // WPF traite d'abord le message (mise à l'échelle), puis on replace la pilule.
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => ApplySettings()));
                break;
        }
        return 0;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_initialized)
        {
            // Démarré en mode Masqué : la fenêtre n'a jamais été montrée ; la montrer déclenche SourceInitialized.
            if (e.PropertyName == nameof(NotchViewModel.Settings) && _vm.Settings.Visibility != VisibilityMode.Hidden) Show();
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(NotchViewModel.Settings):
                ApplySettings();
                break;
            case nameof(NotchViewModel.Unfolded):
                UpdateFold(animated: true);
                break;
            case nameof(NotchViewModel.Cell):
                BodyStack.Opacity = _vm.Cell.Dimmed ? 0.5 : 1.0;
                break;
        }
    }

    /// <summary>Recalcule forme, contenu et position à partir des réglages courants.</summary>
    private void ApplySettings(bool fromSourceInitialized = false)
    {
        if (!_initialized) return;
        var s = _vm.Settings;

        if (s.Visibility == VisibilityMode.Hidden)
        {
            Hide();
            return;
        }

        var placement = _placer.Compute(s);
        Placement = placement;

        var scale = s.Scale;
        _thicknessDip = PillMetrics.Thickness * scale;
        var length = PillMetrics.WindowLength * scale;
        var fillet = PillMetrics.Fillet * scale;
        var vertical = s.Edge is ScreenEdge.Right or ScreenEdge.Left;

        PillPath.Data = PillShapeBuilder.Pill(s.Edge, _thicknessDip, length, PillMetrics.CornerRadius * scale, fillet);
        var bandDip = s.FoldedThicknessPx / placement.Monitor.Scale;
        BandPath.Data = new RectangleGeometry(PillShapeBuilder.Band(s.Edge, _thicknessDip, length, bandDip, fillet));

        var body = PillShapeBuilder.Body(s.Edge, _thicknessDip, length, fillet);
        Canvas.SetLeft(Body, body.X);
        Canvas.SetTop(Body, body.Y);
        Body.Width = body.Width;
        Body.Height = body.Height;
        BodyScale.ScaleX = scale;
        BodyScale.ScaleY = scale;
        BodyStack.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        RingHost.Margin = vertical ? new Thickness(0, 0, 0, 4) : new Thickness(0, 0, 6, 0);
        BodyStack.Opacity = _vm.Cell.Dimmed ? 0.5 : 1.0;

        // Pendant SourceInitialized, Show() est déjà en cours : ne pas le rappeler.
        if (!fromSourceInitialized && !IsVisible) Show();
        // SetWindowPos déclenche une mise en page synchrone de la racine si la taille change (bords haut et bas) : elle
        // agencerait les enfants des Canvas avec leur ancienne taille (pilule rognée). On termine d'abord la mise en page.
        UpdateLayout();
        WindowStyles.MoveResize(Handle, placement.PillRect);
        UpdateFold(animated: false);
        PlacementChanged?.Invoke();
    }

    private void UpdateFold(bool animated)
    {
        var s = _vm.Settings;
        var folded = s.Visibility == VisibilityMode.Folded && !_vm.Unfolded;
        var target = folded ? PillShapeBuilder.SlideOffset(s.Edge, _thicknessDip) : new Vector(0, 0);

        BandPath.Visibility = folded ? Visibility.Visible : Visibility.Collapsed;

        if (!animated)
        {
            Slide.BeginAnimation(TranslateTransform.XProperty, null);
            Slide.BeginAnimation(TranslateTransform.YProperty, null);
            Slide.X = target.X;
            Slide.Y = target.Y;
            PillLayer.Visibility = folded ? Visibility.Hidden : Visibility.Visible;
            return;
        }

        if (!folded) PillLayer.Visibility = Visibility.Visible;
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var x = new DoubleAnimation(target.X, FoldAnimation) { EasingFunction = ease };
        var y = new DoubleAnimation(target.Y, FoldAnimation) { EasingFunction = ease };
        if (folded)
        {
            x.Completed += (_, _) =>
            {
                if (_vm.Settings.Visibility == VisibilityMode.Folded && !_vm.Unfolded) PillLayer.Visibility = Visibility.Hidden;
            };
        }
        Slide.BeginAnimation(TranslateTransform.XProperty, x);
        Slide.BeginAnimation(TranslateTransform.YProperty, y);
    }

    private void OnLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Placement is null || !WindowStyles.IsAltDown()) return;
        _dragging = true;
        _dragFraction = _vm.Settings.PositionFor(_vm.Settings.Edge);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || Placement is null) return;
        var (cx, cy) = WindowStyles.CursorPosition();
        var edge = _vm.Settings.Edge;
        _dragFraction = _placer.FractionForCursor(Placement, edge, cx, cy);
        var preview = _placer.Compute(_vm.Settings.WithPosition(edge, _dragFraction));
        WindowStyles.MoveResize(Handle, preview.PillRect);
    }

    private void OnLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
            _settings.Save(_settings.Current.WithPosition(_vm.Settings.Edge, _dragFraction));
            if (!IsMouseOver) _vm.PointerLeftPill();
            e.Handled = true;
            return;
        }
        _vm.ToggleLockCommand.Execute(null);
        e.Handled = true;
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => _openSettings();

    private void OnQuitClick(object sender, RoutedEventArgs e) => _ = _quit();

    protected override void OnClosed(EventArgs e)
    {
        _vm.PropertyChanged -= OnViewModelChanged;
        base.OnClosed(e);
    }
}
