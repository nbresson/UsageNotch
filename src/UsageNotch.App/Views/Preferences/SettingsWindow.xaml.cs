using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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

        SourceInitialized += (_, _) => _picker.Owner = new WindowInteropHelper(this).Handle;
        Activated += (_, _) => _vm.Position.RefreshMonitorsCommand.Execute(null);
        Deactivated += (_, _) => _vm.Flush();
        Closed += (_, _) => _picker.Owner = 0;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Entrée valide la saisie d'un champ de texte sans attendre la perte de focus.</summary>
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.OriginalSource is not TextBox box) return;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }
}
