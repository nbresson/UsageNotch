using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using UsageNotch.App.Converters;
using UsageNotch.App.Hosting;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Pill;
using UsageNotch.Presentation.Services;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Tray;

/// <summary>Icône de zone de notification (spec §6) : menu identique à celui de la pilule, clic gauche → réglages.</summary>
public sealed class TrayIconService(
    NotchViewModel vm,
    IHookSetup hooks,
    IShellActions shell,
    IAutoStart autoStart,
    AppPaths paths,
    ILogger<TrayIconService> logger) : IDisposable
{
    private TaskbarIcon? _icon;
    private MenuItem? _lockItem;
    private MenuItem? _hooksItem;
    private MenuItem? _autoStartItem;
    private System.Drawing.Icon? _trayIcon;

    public void Start(Func<Task> quit, Action openSettings)
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = vm.TrayText,
            ContextMenu = BuildMenu(quit, openSettings),
        };
        SetIcon(RenderIcon(vm.Cell));
        _icon.TrayLeftMouseUp += (_, _) => openSettings();
        _icon.ForceCreate(enablesEfficiencyMode: false);
        ApplyVisibility();
        vm.PropertyChanged += OnViewModelChanged;
    }

    /// <summary>Remplace l'icône et libère l'ancienne (chaque <see cref="System.Drawing.Icon"/> détient un HICON natif).</summary>
    private void SetIcon(System.Drawing.Icon icon)
    {
        var previous = _trayIcon;
        _trayIcon = icon;
        if (_icon is not null) _icon.Icon = icon;
        previous?.Dispose();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_icon is null) return;
        switch (e.PropertyName)
        {
            case nameof(NotchViewModel.TrayText):
                _icon.ToolTipText = vm.TrayText;
                break;
            case nameof(NotchViewModel.Cell):
                SetIcon(RenderIcon(vm.Cell));
                break;
            case nameof(NotchViewModel.Settings):
                ApplyVisibility();
                break;
            case nameof(NotchViewModel.Locked):
                if (_lockItem is not null) _lockItem.IsChecked = vm.Locked;
                break;
        }
    }

    private void ApplyVisibility()
    {
        if (_icon is null) return;
        var s = vm.Settings;
        _icon.Visibility = s.TrayIconVisible || s.Visibility == VisibilityMode.Hidden ? Visibility.Visible : Visibility.Collapsed;
    }

    private ContextMenu BuildMenu(Func<Task> quit, Action openSettings)
    {
        var menu = new ContextMenu();

        menu.Items.Add(Item("Rafraîchir maintenant", () => vm.RefreshCommand.Execute(null)));
        _lockItem = Item("Garder la carte ouverte", () => vm.ToggleLockCommand.Execute(null));
        menu.Items.Add(_lockItem);
        menu.Items.Add(new Separator());

        _hooksItem = Item("Hooks Claude Code installés", ToggleHooks);
        menu.Items.Add(_hooksItem);
        // En démo, ne jamais toucher à la vraie valeur Run de HKCU : l'élément est désactivé et son clic ne fait rien.
        _autoStartItem = autoStart.IsAvailable
            ? Item("Démarrer avec Windows", ToggleAutoStart)
            : new MenuItem { Header = "Démarrer avec Windows (indisponible en démo)", IsEnabled = false };
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new Separator());

        menu.Items.Add(Item("Réglages…", openSettings));
        menu.Items.Add(Item("Ouvrir le dossier de données", () => shell.OpenFolder(paths.DataDirectory)));
        menu.Items.Add(Item("Diagnostic", RunDoctor));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Quitter", () => _ = quit()));

        menu.Opened += async (_, _) =>
        {
            _lockItem.IsChecked = vm.Locked;
            if (autoStart.IsAvailable) _autoStartItem.IsChecked = autoStart.IsEnabled();
            // Lecture de settings.json de Claude Code hors du thread UI : le menu s'ouvre sans attendre, la coche suit.
            var installed = await Task.Run(hooks.IsInstalled);
            _hooksItem.IsChecked = installed;
        };
        return menu;
    }

    private static MenuItem Item(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        return item;
    }

    private void ToggleHooks()
    {
        var result = hooks.IsInstalled() ? hooks.Uninstall() : hooks.Install();
        if (!result.Succeeded) Warn(result.Message);
    }

    private void ToggleAutoStart()
    {
        var error = autoStart.TrySet(!autoStart.IsEnabled());
        if (error is not null) Warn(error);
    }

    private void RunDoctor()
    {
        var error = shell.RunDoctor();
        if (error is not null) Warn(error);
    }

    private void Warn(string message)
    {
        logger.LogWarning("{Message}", message);
        MessageBox.Show(message, "UsageNotch", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>
    /// Anneau 32×32 à la couleur du niveau, sur fond de pilule ; point ambre au centre si une session attend.
    /// <see cref="TaskbarIcon.IconSource"/> (ImageSource) ne sait décoder qu'un BitmapImage/BitmapFrame adossé à
    /// une URI (H.NotifyIcon.ImageExtensions.ToStreamAsync) : lui passer un RenderTargetBitmap en mémoire lève
    /// NotImplementedException à l'exécution. On encode donc nous-mêmes en ICO via l'extension publique
    /// BitmapSource.ToStream() de la même bibliothèque, et on l'affecte à TaskbarIcon.Icon (System.Drawing.Icon),
    /// prévu pour les icônes générées dynamiquement.
    /// </summary>
    private static System.Drawing.Icon RenderIcon(CellModel cell)
    {
        const int size = 32;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var center = new Point(size / 2.0, size / 2.0);
            dc.DrawEllipse(Brushes.Black, null, center, 15.5, 15.5);
            dc.DrawEllipse(null, new Pen(HexBrushConverter.ToBrush(cell.TrackColor), 5), center, 11, 11);
            if (cell.RingFraction is { } f && f > 0.0005)
            {
                var pen = new Pen(HexBrushConverter.ToBrush(cell.RingColor), 5);
                if (f >= 0.9995) dc.DrawEllipse(null, pen, center, 11, 11);
                // Nom complet : System.Windows.Controls est aussi importé.
                else dc.DrawGeometry(null, pen, UsageNotch.App.Controls.ProgressRing.ArcGeometry(center, 11, f));
            }
            if (cell.Activity == ActivityKind.Attention)
            {
                dc.DrawEllipse(HexBrushConverter.ToBrush(cell.ActivityColor), null, center, 4, 4);
            }
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        using var stream = bitmap.ToStream();
        return new System.Drawing.Icon(stream);
    }

    public void Dispose()
    {
        vm.PropertyChanged -= OnViewModelChanged;
        _icon?.Dispose();
        _trayIcon?.Dispose();
    }
}
