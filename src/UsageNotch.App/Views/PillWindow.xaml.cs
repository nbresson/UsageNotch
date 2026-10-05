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
    private bool _closed;
    private bool _dragging;
    private bool _reapplyPending;
    private bool _spin1Running;
    private bool _pulse1Running;
    private bool _spin2Running;
    private bool _pulse2Running;
    private bool _bandPulseRunning;
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
        Loaded += (_, _) => UpdateAnimations();
        IsVisibleChanged += (_, _) => UpdateAnimations();
        MouseEnter += (_, _) => { if (!_dragging) _vm.PointerEnteredPill(); };
        MouseLeave += (_, _) => { if (!_dragging) _vm.PointerLeftPill(); };
        PreviewMouseLeftButtonDown += OnLeftButtonDown;
        PreviewMouseLeftButtonUp += OnLeftButtonUp;
        MouseMove += OnMouseMove;
        LostMouseCapture += (_, _) => EndDrag();
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
        WindowStyles.ReturnActivation(msg, wParam, lParam, Dispatcher);
        switch (msg)
        {
            case NativeMethods.WM_MOUSEACTIVATE:
                handled = true;
                return NativeMethods.MA_NOACTIVATE;
            case NativeMethods.WM_DISPLAYCHANGE:
            case NativeMethods.WM_SETTINGCHANGE:
            case NativeMethods.WM_DPICHANGED:
                if (_reapplyPending) break;
                _reapplyPending = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    _reapplyPending = false;
                    if (!_dragging) ApplySettings();
                }));
                break;
        }
        return 0;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_initialized)
        {
            if (e.PropertyName is nameof(NotchViewModel.Settings) or nameof(NotchViewModel.FullscreenActive)
                && _vm.Settings.Visibility != VisibilityMode.Hidden && !_vm.FullscreenActive)
            {
                Show();
            }
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(NotchViewModel.Settings):
            case nameof(NotchViewModel.FullscreenActive):
                ApplySettings();
                break;
            case nameof(NotchViewModel.Unfolded):
                UpdateFold(animated: true);
                break;
            case nameof(NotchViewModel.Pill):
            case nameof(NotchViewModel.Cell):
                UpdateCellOpacities();
                UpdateBrandGeometry();
                UpdateAnimations();
                break;
        }
    }

    private void UpdateCellOpacities()
    {
        if (_vm.Pill is null) return;
        Cell1Stack.Opacity = (_vm.Pill.Cells.Count > 0 && _vm.Pill.Cells[0].Dimmed) ? 0.5 : 1.0;
        Cell2Stack.Opacity = (_vm.Pill.Cells.Count > 1 && _vm.Pill.Cells[1].Dimmed) ? 0.5 : 1.0;
    }

    /// <summary>Recalcule forme, contenu et position à partir des réglages courants.</summary>
    private void ApplySettings(bool fromSourceInitialized = false)
    {
        if (!_initialized || _closed) return;
        var s = _vm.Settings;

        if (s.Visibility == VisibilityMode.Hidden || _vm.FullscreenActive)
        {
            Hide();
            return;
        }

        var placement = _placer.Compute(s);
        Placement = placement;

        var scale = s.Scale;
        _thicknessDip = PillMetrics.Thickness * scale;
        var length = PillMetrics.WindowLengthFor(s.Provider, s.Edge, s.CellContent) * scale;
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

        var isDual = _vm.Pill?.IsDual == true;
        CellDivider.Visibility = isDual ? Visibility.Visible : Visibility.Collapsed;
        Cell2Stack.Visibility = isDual ? Visibility.Visible : Visibility.Collapsed;

        BodyStack.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        Cell1Stack.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        Cell2Stack.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;

        if (vertical)
        {
            RingHost1.Margin = new Thickness(0, 0, 0, 4);
            RingHost2.Margin = new Thickness(0, 0, 0, 4);
            CellDivider.Width = 24;
            CellDivider.Height = 1;
            CellDivider.Margin = new Thickness(0, 5.5, 0, 5.5);
        }
        else
        {
            RingHost1.Margin = new Thickness(0, 0, 6, 0);
            RingHost2.Margin = new Thickness(0, 0, 6, 0);
            CellDivider.Width = 1;
            CellDivider.Height = 24;
            CellDivider.Margin = new Thickness(5.5, 0, 5.5, 0);
        }

        UpdateCellOpacities();

        if (!fromSourceInitialized && !IsVisible) Show();
        UpdateLayout();
        WindowStyles.MoveResize(Handle, placement.PillRect);
        UpdateFold(animated: false);
        UpdateBrandGeometry();
        PlacementChanged?.Invoke();
        UpdateAnimations();
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
            UpdateAnimations();
            return;
        }

        if (!folded)
        {
            PillLayer.Visibility = Visibility.Visible;
        }
        UpdateAnimations();
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var x = new DoubleAnimation(target.X, FoldAnimation) { EasingFunction = ease };
        var y = new DoubleAnimation(target.Y, FoldAnimation) { EasingFunction = ease };
        if (folded)
        {
            x.Completed += (_, _) =>
            {
                if (_vm.Settings.Visibility == VisibilityMode.Folded && !_vm.Unfolded) PillLayer.Visibility = Visibility.Hidden;
                UpdateAnimations();
            };
        }
        Slide.BeginAnimation(TranslateTransform.XProperty, x);
        Slide.BeginAnimation(TranslateTransform.YProperty, y);
    }

    private void UpdateAnimations()
    {
        var ringShown = IsVisible && PillLayer.Visibility == Visibility.Visible;
        var act1 = _vm.Pill?.Cell1?.Activity ?? ActivityKind.None;
        SetStoryboard("Spin1", ref _spin1Running, ringShown && act1 == ActivityKind.Running);
        SetStoryboard("Pulse1", ref _pulse1Running, ringShown && act1 == ActivityKind.Attention);

        if (_vm.Pill?.IsDual == true && _vm.Pill.Cell2 is not null)
        {
            var act2 = _vm.Pill.Cell2.Activity;
            SetStoryboard("Spin2", ref _spin2Running, ringShown && act2 == ActivityKind.Running);
            SetStoryboard("Pulse2", ref _pulse2Running, ringShown && act2 == ActivityKind.Attention);
        }
        else
        {
            SetStoryboard("Spin2", ref _spin2Running, false);
            SetStoryboard("Pulse2", ref _pulse2Running, false);
        }

        var hasAttention = _vm.Pill?.Cells.Any(c => c.Activity == ActivityKind.Attention) ?? false;
        SetStoryboard("BandPulse", ref _bandPulseRunning,
            IsVisible && BandPath.Visibility == Visibility.Visible && hasAttention);
    }

    private void SetStoryboard(string key, ref bool running, bool desired)
    {
        if (running == desired) return;
        var storyboard = (Storyboard)Resources[key];
        if (desired) storyboard.Begin(this, isControllable: true);
        else storyboard.Stop(this);
        running = desired;
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
        if (!_dragging || Placement is null || e.LeftButton != MouseButtonState.Pressed) return;
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
            ReleaseMouseCapture();
            EndDrag();
            e.Handled = true;
            return;
        }
        _vm.ToggleLockCommand.Execute(null);
        e.Handled = true;
    }

    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        _settings.Save(_settings.Current.WithPosition(_vm.Settings.Edge, _dragFraction));
        if (!IsMouseOver) _vm.PointerLeftPill();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => _openSettings();

    private void OnQuitClick(object sender, RoutedEventArgs e) => _ = _quit();

    private void UpdateBrandGeometry()
    {
        var geom1 = BrandGeometry.ForProvider(_vm.Pill?.Cell1?.ProviderId ?? "claude");
        LogoMuted1.Data = geom1;
        LogoTint1.Data = geom1;

        if (_vm.Pill?.IsDual == true && _vm.Pill.Cell2 is not null)
        {
            var geom2 = BrandGeometry.ForProvider(_vm.Pill.Cell2.ProviderId);
            LogoMuted2.Data = geom2;
            LogoTint2.Data = geom2;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _vm.PropertyChanged -= OnViewModelChanged;
        base.OnClosed(e);
    }
}
