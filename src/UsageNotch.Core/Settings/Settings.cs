namespace UsageNotch.Core.Settings;

/// <summary>Réglages immuables. Une clé absente du JSON garde sa valeur par défaut ; <see cref="Clamp"/> ramène les valeurs dans les bornes.</summary>
public sealed record Settings
{
    public const int CurrentVersion = 2;
    public const double ScaleMin = 0.4;
    public const double ScaleMax = 1.5;
    public const int FoldedThicknessMin = 2;
    public const int FoldedThicknessMax = 12;
    public const int DefaultPort = 48666;
    public const double NotifyThresholdMin = 0.6;
    public const double NotifyThresholdMax = 0.95;

    public int Version { get; init; } = CurrentVersion;
    public int Port { get; init; } = DefaultPort;
    public ScreenEdge Edge { get; init; } = ScreenEdge.Right;
    public double PositionRight { get; init; } = 0.5;
    public double PositionLeft { get; init; } = 0.5;
    public double PositionTop { get; init; } = 0.5;
    public double PositionBottom { get; init; } = 0.5;
    /// <summary>Identifiant de périphérique de l'écran d'ancrage ; null = écran principal.</summary>
    public string? MonitorDeviceId { get; init; }
    public double Scale { get; init; } = 1.0;
    public CellContent CellContent { get; init; } = CellContent.RingAndPercent;
    /// <summary>Couleur propre à chaque anneau, ou couleur du niveau de chacun.</summary>
    public RingColoring Coloring { get; init; } = RingColoring.PerRing;
    public VisibilityMode Visibility { get; init; } = VisibilityMode.Expanded;
    public int FoldedThicknessPx { get; init; } = 4;
    public ThemePreset ThemePreset { get; init; } = ThemePreset.Codenotch;
    public Theme CustomTheme { get; init; } = Theme.Codenotch;
    public bool AutoOpenCard { get; init; } = true;
    public bool SoundEnabled { get; init; } = true;
    /// <summary>Noms de sons système Windows : Asterisk, Beep, Exclamation, Hand, Question.</summary>
    public string DoneSound { get; init; } = "Asterisk";
    public string AttentionSound { get; init; } = "Exclamation";
    public bool TrayIconVisible { get; init; } = true;
    /// <summary>Masque la pilule et suspend carte et son quand une application est en plein écran sur l'écran de la pilule.</summary>
    public bool HideInFullscreen { get; init; } = true;
    /// <summary>Notification Windows quand une fenêtre de limite franchit <see cref="NotifyThreshold"/>, puis 100 %.</summary>
    public bool ThresholdNotifications { get; init; } = true;
    /// <summary>Premier seuil d'alerte, de <see cref="NotifyThresholdMin"/> à <see cref="NotifyThresholdMax"/>.</summary>
    public double NotifyThreshold { get; init; } = 0.8;
    public bool DebugLogging { get; init; }
    /// <summary>false = le hook ne relance pas l'application (mis à false par Quitter, remis à true au démarrage manuel).</summary>
    public bool AutoLaunch { get; init; } = true;

    public double PositionFor(ScreenEdge edge) => edge switch
    {
        ScreenEdge.Left => PositionLeft,
        ScreenEdge.Top => PositionTop,
        ScreenEdge.Bottom => PositionBottom,
        _ => PositionRight,
    };

    public Settings WithPosition(ScreenEdge edge, double fraction)
    {
        var f = Math.Clamp(fraction, 0.0, 1.0);
        return edge switch
        {
            ScreenEdge.Left => this with { PositionLeft = f },
            ScreenEdge.Top => this with { PositionTop = f },
            ScreenEdge.Bottom => this with { PositionBottom = f },
            _ => this with { PositionRight = f },
        };
    }

    public Settings Clamp() => this with
    {
        Version = CurrentVersion,
        // « Pourcentage seul » a été retiré des choix : le membre survit dans l'enum pour que la relecture
        // d'un fichier de version 1 ne lève pas et ne condamne pas tout le fichier.
        CellContent = CellContent == CellContent.PercentOnly ? CellContent.RingAndPercent : CellContent,
        Port = Port is >= 1024 and <= 65535 ? Port : DefaultPort,
        PositionRight = Math.Clamp(PositionRight, 0.0, 1.0),
        PositionLeft = Math.Clamp(PositionLeft, 0.0, 1.0),
        PositionTop = Math.Clamp(PositionTop, 0.0, 1.0),
        PositionBottom = Math.Clamp(PositionBottom, 0.0, 1.0),
        Scale = Math.Clamp(Scale, ScaleMin, ScaleMax),
        FoldedThicknessPx = Math.Clamp(FoldedThicknessPx, FoldedThicknessMin, FoldedThicknessMax),
        NotifyThreshold = Math.Clamp(NotifyThreshold, NotifyThresholdMin, NotifyThresholdMax),
        // Un JSON édité à la main peut porter une valeur "null" explicite malgré le type non-nullable.
        CustomTheme = (CustomTheme ?? Theme.Codenotch).Clamp(),
        DoneSound = string.IsNullOrWhiteSpace(DoneSound) ? "Asterisk" : DoneSound,
        AttentionSound = string.IsNullOrWhiteSpace(AttentionSound) ? "Exclamation" : AttentionSound,
        // Sans pilule ni icône, l'app serait injoignable.
        TrayIconVisible = Visibility == VisibilityMode.Hidden || TrayIconVisible,
    };
}
