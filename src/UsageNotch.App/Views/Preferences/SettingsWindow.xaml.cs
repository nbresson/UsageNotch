using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using UsageNotch.App.Interop;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.App.Views.Preferences;

/// <summary>Fenêtre classique, activable (spec §6). La perte de focus enregistre ; l'activation relit la liste des écrans.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly NativeColorPicker _picker;

    public SettingsWindow(SettingsViewModel vm, NativeColorPicker picker)
    {
        _vm = vm;
        _picker = picker;
        InitializeComponent();
        DataContext = vm;
        _vm.PropertyChanged += OnViewModelChanged;

        SourceInitialized += (_, _) =>
        {
            _picker.Owner = new WindowInteropHelper(this).Handle;
            FitToWorkArea();
        };
        Activated += (_, _) =>
        {
            _vm.Position.RefreshMonitorsCommand.Execute(null);
            _vm.ClaudeCode.RefreshCommand.Execute(null);
            _vm.Behavior.RefreshAutoStart();
        };
        Deactivated += (_, _) => _vm.Flush();
        Closed += (_, _) =>
        {
            _vm.PropertyChanged -= OnViewModelChanged;
            _picker.Owner = 0;
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Chaque page s'ouvre en haut : le défilement partagé ne garde pas la position de la page précédente.</summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SelectedPage)) PageScroller.ScrollToTop();
    }

    private const double WorkAreaMargin = 24;

    /// <summary>Réduit la fenêtre à la zone de travail de son écran, marge comprise, puis la centre dans cette zone.</summary>
    private void FitToWorkArea()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>(), szDevice = "" };
        if (monitor == 0 || !NativeMethods.GetMonitorInfo(monitor, ref info)) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var workLeft = info.rcWork.Left / dpi.DpiScaleX;
        var workTop = info.rcWork.Top / dpi.DpiScaleY;
        var workWidth = (info.rcWork.Right - info.rcWork.Left) / dpi.DpiScaleX;
        var workHeight = (info.rcWork.Bottom - info.rcWork.Top) / dpi.DpiScaleY;

        var availableWidth = workWidth - 2 * WorkAreaMargin;
        var availableHeight = workHeight - 2 * WorkAreaMargin;
        MinWidth = Math.Min(MinWidth, availableWidth);
        MinHeight = Math.Min(MinHeight, availableHeight);
        Width = Math.Min(Width, availableWidth);
        Height = Math.Min(Height, availableHeight);
        Left = workLeft + (workWidth - Width) / 2;
        Top = workTop + (workHeight - Height) / 2;
    }

    /// <summary>Entrée valide la saisie d'un champ de texte sans attendre la perte de focus.</summary>
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.OriginalSource is not TextBox box) return;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }
}
