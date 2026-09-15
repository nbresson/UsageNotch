namespace UsageNotch.Presentation.Services;

/// <summary>Sélecteur de couleur du système. Rend <c>#RRGGBB</c>, ou null si l'utilisateur annule.</summary>
public interface IColorPicker
{
    string? Pick(string initialHex);
}
