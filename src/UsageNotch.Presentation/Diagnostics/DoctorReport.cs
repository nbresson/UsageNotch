using System.Globalization;
using System.Text;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Diagnostics;

public sealed record DoctorInputs(
    string CredentialsDirectory,
    ClaudeCredential? Credential,
    string ClaudeSettingsPath,
    bool HooksInstalled,
    string HookExePath,
    bool HookExeExists,
    int Port,
    bool PortFree,
    IReadOnlyList<MonitorInfo> Monitors,
    string UsagePath,
    UsageSnapshot Usage,
    string SettingsPath,
    Settings Settings,
    DateTimeOffset Now,
    TimeZoneInfo Zone);

/// <summary>Texte de <c>UsageNotch.App.exe doctor</c>. Ne contient jamais la valeur du jeton.</summary>
public static class DoctorReport
{
    public static string Build(DoctorInputs i)
    {
        var sb = new StringBuilder();
        void Line(string text) => sb.Append(text).Append('\n');

        var local = TimeZoneInfo.ConvertTime(i.Now, i.Zone);
        Line($"UsageNotch — diagnostic du {local.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}");

        Line(i.Credential is { } c
            ? $"Identifiants Claude Code : présents (jeton de {c.AccessToken.Length} caractères, "
              + (c.IsExpired ? "expiré — Claude Code le renouvelle à sa prochaine utilisation)" : "valide)")
              + $" — {i.CredentialsDirectory}"
            : $"Identifiants Claude Code : absents — {i.CredentialsDirectory}");

        Line($"Hooks Claude Code : {(i.HooksInstalled ? "installés" : "non installés")} — {i.ClaudeSettingsPath}");
        Line($"Exécutable hook : {(i.HookExeExists ? "présent" : "absent")} — {i.HookExePath}");
        Line($"Port {i.Port} : {(i.PortFree ? "libre" : "occupé (une instance tourne peut-être déjà)")}");

        Line($"Écrans ({i.Monitors.Count}) :");
        foreach (var m in i.Monitors)
        {
            var primary = m.IsPrimary ? " (principal)" : "";
            Line($"  {m.DeviceId}{primary} — {m.Bounds.Width}×{m.Bounds.Height} à ({m.Bounds.X}, {m.Bounds.Y}), échelle {ScalePercent(m.Scale)}");
        }

        var u = i.Usage;
        if (u.FetchedAt == DateTimeOffset.MinValue && u.Windows.Count == 0)
        {
            Line($"Dernière lecture d'usage : aucune — {i.UsagePath}");
        }
        else
        {
            Line($"Dernière lecture d'usage : {StatusText(u.Status)}, {FrenchText.UpdatedAgo(u.FetchedAt, i.Now).ToLowerInvariant()} — {i.UsagePath}");
            foreach (var w in u.Windows)
            {
                Line($"  {w.Label} : {FrenchText.Percent(w.UsedFraction)} ({FrenchText.ResetCopy(w.ResetsAt, i.Now, i.Zone)})");
            }
        }
        if (u.Note.Length > 0) Line($"  Note : {u.Note}");

        var s = i.Settings;
        var screen = s.MonitorDeviceId is null ? "écran principal" : $"écran {s.MonitorDeviceId}";
        Line($"Réglages : bord {EdgeText(s.Edge)}, {screen}, mode {VisibilityText(s.Visibility)}, échelle {ScalePercent(s.Scale)} — {i.SettingsPath}");

        return sb.ToString();
    }

    private static string ScalePercent(double scale) =>
        $"{(int)Math.Round(scale * 100, MidpointRounding.AwayFromZero)}{FrenchText.Nbsp}%";

    private static string StatusText(SnapshotStatus status) => status switch
    {
        SnapshotStatus.Ok => "à jour",
        SnapshotStatus.Stale => "ancienne",
        SnapshotStatus.NeedsAuth => "authentification requise",
        SnapshotStatus.Backoff => "en attente (limite d'appels)",
        SnapshotStatus.Error => "erreur",
        _ => "absente",
    };

    private static string EdgeText(ScreenEdge edge) => edge switch
    {
        ScreenEdge.Left => "gauche",
        ScreenEdge.Top => "haut",
        ScreenEdge.Bottom => "bas",
        _ => "droite",
    };

    private static string VisibilityText(VisibilityMode mode) => mode switch
    {
        VisibilityMode.Folded => "replié",
        VisibilityMode.Hidden => "masqué",
        _ => "déplié",
    };
}
