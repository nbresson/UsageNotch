using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Une couleur éditable du thème : libellé, valeur <c>#RRGGBB</c>, erreur de saisie, bouton « Choisir… ».</summary>
public sealed class ColorSlot : ObservableObject
{
    private readonly Func<Theme, string> _get;
    private readonly Func<Theme, string, Theme> _set;
    private readonly Func<Theme> _theme;
    private readonly Action<Func<Theme, Theme>> _editTheme;
    private readonly IColorPicker _picker;
    private string _error = "";

    internal ColorSlot(
        string key,
        string label,
        Func<Theme, string> get,
        Func<Theme, string, Theme> set,
        Func<Theme> theme,
        Action<Func<Theme, Theme>> editTheme,
        IColorPicker picker)
    {
        Key = key;
        Label = label;
        _get = get;
        _set = set;
        _theme = theme;
        _editTheme = editTheme;
        _picker = picker;
        PickCommand = new RelayCommand(Pick);
    }

    /// <summary>Nom de la propriété de <see cref="Theme"/>, stable : sert d'identifiant d'automatisation.</summary>
    public string Key { get; }

    public string Label { get; }

    public string Hex
    {
        get => _get(_theme());
        set => Apply(value);
    }

    public string Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public IRelayCommand PickCommand { get; }

    internal void Refresh() => OnPropertyChanged(nameof(Hex));

    private void Apply(string? input)
    {
        if (!HexColor.TryNormalize(input, out var hex))
        {
            Error = $"Couleur invalide : « {input} ». Format attendu : #RRGGBB.";
            Refresh();
            return;
        }

        Error = "";
        if (string.Equals(hex, Hex, StringComparison.OrdinalIgnoreCase))
        {
            Refresh();
            return;
        }
        _editTheme(t => _set(t, hex));
    }

    private void Pick()
    {
        var picked = _picker.Pick(Hex);
        if (picked is not null) Apply(picked);
    }
}
