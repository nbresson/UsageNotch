using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using UsageNotch.App.Interop;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Views;

public partial class CardWindow : Window
{
    private const double SlideDistance = 8;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(150);

    private readonly NotchViewModel _vm;
    private readonly NotchPlacer _placer;
    private readonly PillWindow _pill;
    private readonly DispatcherTimer _safety;
    private nint _hwnd;

    public CardWindow(NotchViewModel vm, NotchPlacer placer, PillWindow pill)
    {
        _vm = vm;
        _placer = placer;
        _pill = pill;

        InitializeComponent();
        DataContext = vm;

        _safety = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, OnSafetyTick, Dispatcher);
        _safety.Stop();

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            WindowStyles.MakeToolWindowNoActivate(_hwnd);
            HwndSource.FromHwnd(_hwnd)!.AddHook(WndProc);
        };
        MouseEnter += (_, _) => _vm.PointerEnteredCard();
        MouseLeave += (_, _) => _vm.PointerLeftCard();
        SizeChanged += (_, _) => Reposition();
        _pill.PlacementChanged += Reposition;
        _vm.PropertyChanged += OnViewModelChanged;
        ApplySettings();
    }

    private static nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_MOUSEACTIVATE) return 0;
        handled = true;
        return NativeMethods.MA_NOACTIVATE;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NotchViewModel.CardVisible):
                if (_vm.CardVisible) OpenCard();
                else CloseCard();
                break;
            case nameof(NotchViewModel.Settings):
                ApplySettings();
                Reposition();
                break;
        }
    }

    private void ApplySettings()
    {
        var s = _vm.Settings;
        RootScale.ScaleX = s.Scale;
        RootScale.ScaleY = s.Scale;

        switch (s.Edge)
        {
            case ScreenEdge.Left:
                CardHost.Margin = new Thickness(10, 0, 0, 0);
                Arrow.Data = Geometry.Parse("M10,0 L0,10 L10,20 Z");
                Arrow.HorizontalAlignment = HorizontalAlignment.Left;
                Arrow.VerticalAlignment = VerticalAlignment.Center;
                Arrow.Margin = new Thickness(-10, 0, 0, 0);
                break;
            case ScreenEdge.Top:
                CardHost.Margin = new Thickness(0, 10, 0, 0);
                Arrow.Data = Geometry.Parse("M0,10 L10,0 L20,10 Z");
                Arrow.HorizontalAlignment = HorizontalAlignment.Center;
                Arrow.VerticalAlignment = VerticalAlignment.Top;
                Arrow.Margin = new Thickness(0, -10, 0, 0);
                break;
            case ScreenEdge.Bottom:
                CardHost.Margin = new Thickness(0, 0, 0, 10);
                Arrow.Data = Geometry.Parse("M0,0 L10,10 L20,0 Z");
                Arrow.HorizontalAlignment = HorizontalAlignment.Center;
                Arrow.VerticalAlignment = VerticalAlignment.Bottom;
                Arrow.Margin = new Thickness(0, 0, 0, -10);
                break;
            default:
                CardHost.Margin = new Thickness(0, 0, 10, 0);
                Arrow.Data = Geometry.Parse("M0,0 L10,10 L0,20 Z");
                Arrow.HorizontalAlignment = HorizontalAlignment.Right;
                Arrow.VerticalAlignment = VerticalAlignment.Center;
                Arrow.Margin = new Thickness(0, 0, -10, 0);
                break;
        }
    }

    private void OpenCard()
    {
        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }
        Reposition();

        var from = SlideFrom(_vm.Settings.Edge);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(from.X, 0, OpenDuration) { EasingFunction = ease });
        Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(from.Y, 0, OpenDuration) { EasingFunction = ease });
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, OpenDuration));
        _safety.Start();
    }

    private void CloseCard()
    {
        _safety.Stop();
        if (!IsVisible) return;
        var fade = new DoubleAnimation(0, CloseDuration);
        fade.Completed += (_, _) =>
        {
            if (!_vm.CardVisible) Hide();
        };
        BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>La carte arrive depuis la pilule : décalage initial vers le bord de l'écran.</summary>
    private static Vector SlideFrom(ScreenEdge edge) => edge switch
    {
        ScreenEdge.Left => new Vector(-SlideDistance, 0),
        ScreenEdge.Top => new Vector(0, -SlideDistance),
        ScreenEdge.Bottom => new Vector(0, SlideDistance),
        _ => new Vector(SlideDistance, 0),
    };

    private void Reposition()
    {
        if (!IsVisible || _hwnd == 0) return;
        var s = _vm.Settings;
        var placement = s.Visibility == VisibilityMode.Hidden || _pill.Placement is null
            ? _placer.Compute(s)
            : _pill.Placement;

        var scale = placement.Monitor.Scale;
        var width = (int)Math.Ceiling(ActualWidth * scale);
        var height = (int)Math.Ceiling(ActualHeight * scale);
        if (width <= 0 || height <= 0) return;

        WindowStyles.MoveResize(_hwnd, _placer.CardRect(placement, s, width, height));
    }

    private void OnSafetyTick(object? sender, EventArgs e)
    {
        if (!_vm.CardVisible)
        {
            _safety.Stop();
            return;
        }

        var (x, y) = WindowStyles.CursorPosition();
        var inPill = _pill.IsVisible && WindowStyles.GetRect(_pill.Handle) is { } pr && pr.Contains(x, y);
        var inCard = _hwnd != 0 && WindowStyles.GetRect(_hwnd) is { } cr && cr.Contains(x, y);
        if (inPill || inCard) return;

        _vm.PointerLeftPill();
        _vm.PointerLeftCard();
    }

    protected override void OnClosed(EventArgs e)
    {
        _safety.Stop();
        _pill.PlacementChanged -= Reposition;
        _vm.PropertyChanged -= OnViewModelChanged;
        base.OnClosed(e);
    }
}
