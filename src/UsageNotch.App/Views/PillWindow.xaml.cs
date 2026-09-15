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
    private bool _spinRunning;
    private bool _pulseRunning;
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
        switch (msg)
        {
            case NativeMethods.WM_MOUSEACTIVATE:
                handled = true;
                return NativeMethods.MA_NOACTIVATE;
            case NativeMethods.WM_DISPLAYCHANGE:
            case NativeMethods.WM_SETTINGCHANGE:
            case NativeMethods.WM_DPICHANGED:
                // WPF traite d'abord le message (mise à l'échelle), puis on replace la pilule. Une seule demande en attente ;
                // pendant un glisser, on ne déplace rien : l'enregistrement au relâchement replace la pilule.
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
            // Démarré en mode Masqué : la fenêtre n'a jamais été montrée ; la montrer déclenche SourceInitialized.
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
            case nameof(NotchViewModel.Cell):
                BodyStack.Opacity = _vm.Cell.Dimmed ? 0.5 : 1.0;
                UpdateAnimations();
                break;
        }
    }

    /// <summary>Recalcule forme, contenu et position à partir des réglages courants.</summary>
    private void ApplySettings(bool fromSourceInitialized = false)
    {
        if (!_initialized || _closed) return;
        var s = _vm.Settings;

        // Mode Masqué, ou application en plein écran sur l'écran de la pilule : aucune surface à l'écran.
        if (s.Visibility == VisibilityMode.Hidden || _vm.FullscreenActive)
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

    /// <summary>
    /// Démarre ou arrête les animations en boucle selon ce qui est réellement affiché : une animation Forever sur une
    /// cible masquée consomme du processeur en permanence. Un storyboard déjà lancé n'est jamais relancé.
    /// </summary>
    private void UpdateAnimations()
    {
        var activity = _vm.Cell.Activity;
        // L'arc et l'anneau d'attention vivent dans l'anneau : en contenu « pourcentage seul », ils ne sont pas dessinés.
        var ringShown = IsVisible && PillLayer.Visibility == Visibility.Visible && _vm.Cell.ShowRing;
        SetStoryboard("Spin", ref _spinRunning, ringShown && activity == ActivityKind.Running);
        SetStoryboard("Pulse", ref _pulseRunning, ringShown && activity == ActivityKind.Attention);
        SetStoryboard("BandPulse", ref _bandPulseRunning,
            IsVisible && BandPath.Visibility == Visibility.Visible && activity == ActivityKind.Attention);
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
            // Relâcher la capture d'abord (IsMouseOver redevient exact) ; cela déclenche LostMouseCapture → EndDrag.
            // L'appel explicite ne fait rien si c'est déjà fait : la position n'est enregistrée qu'une fois.
            ReleaseMouseCapture();
            EndDrag();
            e.Handled = true;
            return;
        }
        _vm.ToggleLockCommand.Execute(null);
        e.Handled = true;
    }

    /// <summary>Termine un glisser (relâchement ou capture perdue : Alt+Tab, UAC, menu) et enregistre la position une seule fois.</summary>
    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        _settings.Save(_settings.Current.WithPosition(_vm.Settings.Edge, _dragFraction));
        if (!IsMouseOver) _vm.PointerLeftPill();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => _openSettings();

    private void OnQuitClick(object sender, RoutedEventArgs e) => _ = _quit();

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _vm.PropertyChanged -= OnViewModelChanged;
        base.OnClosed(e);
    }
}
