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

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        WindowStyles.ReturnActivation(msg, wParam, lParam, Dispatcher);
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
        CardHost.Margin = MarginForEdge(s.Edge);
    }

    private static Thickness MarginForEdge(ScreenEdge edge) => edge switch
    {
        ScreenEdge.Left => new Thickness(10, 0, 0, 0),
        ScreenEdge.Top => new Thickness(0, 10, 0, 0),
        ScreenEdge.Bottom => new Thickness(0, 0, 0, 10),
        _ => new Thickness(0, 0, 10, 0),
    };

    private void OpenCard()
    {
        var alreadyShown = IsVisible;
        if (!alreadyShown)
        {
            Opacity = 0;
            Show();
        }
        Reposition();

        // Rouverte pendant son fondu de fermeture, la carte est déjà en place : on inverse seulement le fondu, sinon le
        // glissement repartirait de 8 DIP et la carte sauterait.
        if (!alreadyShown)
        {
            var from = SlideFrom(_vm.Settings.Edge);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(from.X, 0, OpenDuration) { EasingFunction = ease });
            Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(from.Y, 0, OpenDuration) { EasingFunction = ease });
        }
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

        var cardRect = _placer.CardRect(placement, s, width, height);
        WindowStyles.MoveResize(_hwnd, cardRect);
        UpdateArrowPosition(placement, cardRect, scale, s);
    }

    private void UpdateArrowPosition(PlacementResult placement, Core.Placement.PixelRect cardRect, double monitorScale, Settings s)
    {
        var effScale = monitorScale * s.Scale;
        if (effScale <= 0) return;

        var edge = s.Edge;
        if (edge is ScreenEdge.Top or ScreenEdge.Bottom)
        {
            var relPhysicalX = placement.PillRect.CenterX - cardRect.X;
            var coordX = relPhysicalX / effScale;
            var cardWidthDip = cardRect.Width / effScale;
            var maxLeft = Math.Max(14, cardWidthDip - 34);
            var arrowLeft = Math.Clamp(coordX - 10, 14, maxLeft);

            Arrow.HorizontalAlignment = HorizontalAlignment.Left;
            if (edge == ScreenEdge.Top)
            {
                Arrow.VerticalAlignment = VerticalAlignment.Top;
                Arrow.Margin = new Thickness(arrowLeft, -10, 0, 0);
            }
            else
            {
                Arrow.VerticalAlignment = VerticalAlignment.Bottom;
                Arrow.Margin = new Thickness(arrowLeft, 0, 0, -10);
            }
        }
        else
        {
            var relPhysicalY = placement.PillRect.CenterY - cardRect.Y;
            var coordY = relPhysicalY / effScale;
            var cardHeightDip = cardRect.Height / effScale;
            var maxTop = Math.Max(14, cardHeightDip - 34);
            var arrowTop = Math.Clamp(coordY - 10, 14, maxTop);

            Arrow.VerticalAlignment = VerticalAlignment.Top;
            if (edge == ScreenEdge.Left)
            {
                Arrow.HorizontalAlignment = HorizontalAlignment.Left;
                Arrow.Margin = new Thickness(-10, arrowTop, 0, 0);
            }
            else
            {
                Arrow.HorizontalAlignment = HorizontalAlignment.Right;
                Arrow.Margin = new Thickness(0, arrowTop, -10, 0);
            }
        }
    }

    private void OnSafetyTick(object? sender, EventArgs e)
    {
        if (!_vm.CardVisible || _vm.Locked)
        {
            if (!_vm.CardVisible) _safety.Stop();
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
        if (_hwnd != 0)
        {
            HwndSource.FromHwnd(_hwnd)?.RemoveHook(WndProc);
        }
        base.OnClosed(e);
    }
}
