using FluentAssertions;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Diagnostics;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Tests.Diagnostics;

public class DoctorReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private const string Token = "sk-ant-oat01-SECRET-TOKEN-VALUE";

    private static DoctorInputs Inputs(ClaudeCredential? cred = null, bool hooks = true, bool exe = true, bool portFree = true,
        UsageSnapshot? usage = null, Settings? settings = null) => new(
        CredentialsDirectory: @"C:\Users\u\.claude",
        Credential: cred,
        ClaudeSettingsPath: @"C:\Users\u\.claude\settings.json",
        HooksInstalled: hooks,
        HookExePath: @"C:\apps\UsageNotch\UsageNotch.Hook.exe",
        HookExeExists: exe,
        Port: 48666,
        PortFree: portFree,
        Monitors:
        [
            new MonitorInfo(@"\\.\DISPLAY1", true, new PixelRect(0, 0, 2560, 1440), new PixelRect(0, 0, 2560, 1392), 1.5),
            new MonitorInfo(@"\\.\DISPLAY2", false, new PixelRect(2560, 0, 1920, 1080), new PixelRect(2560, 0, 1920, 1040), 1.0),
        ],
        UsagePath: @"C:\Users\u\AppData\Roaming\UsageNotch\usage.json",
        Usage: usage ?? new UsageSnapshot(SnapshotStatus.Ok,
            [new LimitWindow("session", "Session en cours", 0.73, Now.AddMinutes(51))], Now.AddMinutes(-3), "", null),
        SettingsPath: @"C:\Users\u\AppData\Roaming\UsageNotch\settings.json",
        Settings: settings ?? new Settings(),
        Now: Now,
        Zone: TimeZoneInfo.Utc);

    [Fact]
    public void A_healthy_setup_reports_every_section()
    {
        var text = DoctorReport.Build(Inputs(new ClaudeCredential(Token, IsExpired: false)));

        text.Should().Contain("Identifiants Claude Code : présents (jeton de 31 caractères, valide)");
        text.Should().Contain(@"Hooks Claude Code : installés — C:\Users\u\.claude\settings.json");
        text.Should().Contain(@"Exécutable hook : présent — C:\apps\UsageNotch\UsageNotch.Hook.exe");
        text.Should().Contain("Port 48666 : libre");
        text.Should().Contain("Écrans (2) :");
        text.Should().Contain(@"\\.\DISPLAY1 (principal) — 2560×1440 à (0, 0), échelle 150" + FrenchText.Nbsp + "%");
        text.Should().Contain(@"\\.\DISPLAY2 — 1920×1080 à (2560, 0), échelle 100" + FrenchText.Nbsp + "%");
        text.Should().Contain("Dernière lecture d'usage : à jour, mis à jour il y a 3 min");
        text.Should().Contain("Session en cours : 73" + FrenchText.Nbsp + "% (Réinitialisation dans 51 min)");
        text.Should().Contain("Réglages : bord droite, écran principal, mode déplié, échelle 100" + FrenchText.Nbsp + "%");
    }

    [Fact]
    public void The_token_value_never_appears()
    {
        DoctorReport.Build(Inputs(new ClaudeCredential(Token, IsExpired: true))).Should().NotContain("SECRET");
    }

    [Fact]
    public void Problems_are_spelled_out()
    {
        var text = DoctorReport.Build(Inputs(cred: null, hooks: false, exe: false, portFree: false,
            usage: new UsageSnapshot(SnapshotStatus.NeedsAuth, [], Now, "Identifiant refusé (changement de compte ?).", null),
            settings: new Settings { Edge = ScreenEdge.Top, MonitorDeviceId = @"\\.\DISPLAY2", Visibility = VisibilityMode.Folded, Scale = 0.75 }));

        text.Should().Contain(@"Identifiants Claude Code : absents — C:\Users\u\.claude");
        text.Should().Contain("Hooks Claude Code : non installés");
        text.Should().Contain("Exécutable hook : absent");
        text.Should().Contain("Port 48666 : occupé (une instance tourne peut-être déjà)");
        text.Should().Contain("Dernière lecture d'usage : authentification requise");
        text.Should().Contain("Note : Identifiant refusé (changement de compte ?).");
        text.Should().Contain(@"Réglages : bord haut, écran \\.\DISPLAY2, mode replié, échelle 75" + FrenchText.Nbsp + "%");
    }

    [Fact]
    public void An_expired_token_is_flagged()
    {
        DoctorReport.Build(Inputs(new ClaudeCredential(Token, IsExpired: true)))
            .Should().Contain("(jeton de 31 caractères, expiré — Claude Code le renouvelle à sa prochaine utilisation)");
    }

    [Fact]
    public void A_never_read_usage_says_so()
    {
        DoctorReport.Build(Inputs(usage: UsageSnapshot.Empty)).Should().Contain("Dernière lecture d'usage : aucune");
    }
}
