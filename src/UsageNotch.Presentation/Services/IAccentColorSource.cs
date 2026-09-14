namespace UsageNotch.Presentation.Services;

/// <summary>Couleur d'accentuation de Windows en <c>#RRGGBB</c>, ou null si elle est illisible.</summary>
public interface IAccentColorSource
{
    string? AccentHex { get; }
}
