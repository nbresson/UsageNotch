# UsageNotch — Plan 2 : l'application (notch fonctionnel)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Livrer l'application Windows qui affiche la pilule et sa carte de détail sur le bord et l'écran choisis, en mode déplié, replié ou masqué, avec l'état des sessions Claude Code, la zone de notification, le retour au terminal, le diagnostic et le mode démo — les réglages se modifiant pour l'instant dans `settings.json`, relu à chaud.

**Architecture:** Toute la logique de présentation vit dans une bibliothèque `UsageNotch.Presentation` (net10.0, sans WPF) testée par xUnit : textes français, modèles de cellule et de carte, détection des transitions, contrôleur de survol, ViewModel, rapport de diagnostic. Le projet WPF `UsageNotch.App` (net10.0-windows) ne contient que des fenêtres, des contrôles, l'interop Win32 et l'hébergement ; il se vérifie à l'exécution avec `--demo` et des captures d'écran.

**Tech Stack:** .NET 10 (SDK 10.0.401), C# 14, WPF, CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 10.0.12, H.NotifyIcon.Wpf 2.4.1, xUnit, FluentAssertions 7.2.0, Microsoft.Extensions.TimeProvider.Testing.

**Spec:** `docs/superpowers/specs/2026-09-14-usagenotch-design.md` (§3, §5 retour au terminal et affichage, §6, §7 hors page de réglages, §8, §9).
**Contrats hérités du Plan 1 :** `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md` (section « Contrats dont le Plan 2 dépend »).

## Découpage

La spec couvre deux sous-systèmes d'interface indépendants. Ce plan livre le notch complet et utilisable ; la **fenêtre de réglages** (spec §7 « Fenêtre de réglages », cinq pages avec aperçu) fait l'objet du **Plan 3**. En attendant, « Réglages… » ouvre `settings.json` dans le Bloc-notes et l'application le relit à chaud.

## Écarts assumés par rapport à la spec

- **Passage des clics** : assuré par la transparence par pixel des fenêtres `AllowsTransparency`, que Windows ignore lors du test de clic. `WM_NCHITTEST` n'est pas utilisé : `HTTRANSPARENT` ne transmet le clic qu'aux fenêtres du même thread, jamais à une autre application.
- **Interop** : déclarations `[DllImport]` écrites à la main dans un seul fichier au lieu de Microsoft.Windows.CsWin32. Le code du plan reste exact et vérifiable sans générateur.
- **Clic gauche sur la pilule** : il n'y a qu'une cellule (Claude), donc « corps » et « cellule » se confondent. Le clic gauche verrouille ou libère la carte ; « Rafraîchir maintenant » est dans le menu contextuel.
- **Clic gauche sur l'icône de notification** : affiche la carte 5 s (utile en mode Masqué). Il ouvrira la fenêtre de réglages au Plan 3.
- **Flèche de la carte** : centrée sur le côté tourné vers la pilule. La carte étant centrée sur la pilule, elle pointe sur l'anneau sauf quand la carte est repoussée par le bord de l'écran.
- **Mode Replié** : la fenêtre garde la taille de la pilule ; seule la bande est dessinée et reçoit la souris, le reste est transparent et laisse passer les clics. Le dépli est une animation de glissement du contenu, pas un redimensionnement de fenêtre.

## Global Constraints

- Cibles : `net10.0` pour `UsageNotch.Presentation` et ses tests ; `net10.0-windows` avec `UseWPF` pour `UsageNotch.App`. SDK épinglé par `global.json` (10.0.401). `Directory.Build.props` impose `Nullable`, `ImplicitUsings`, `LangVersion 14`, `TreatWarningsAsErrors`.
- `UsageNotch.Presentation` ne référence ni WPF ni `System.Windows` : couleurs en chaînes `#RRGGBB`, temps via `TimeProvider`.
- Tous les textes affichés sont en français. Pourcentages : nombre entier, espace insécable U+00A0, `%` (ex. `73 %` avec U+00A0).
- Aucun jeton, aucun contenu de prompt dans les journaux ni dans le diagnostic.
- Données : `%APPDATA%\UsageNotch\` (`settings.json`, `usage.json`, `logs\`). En mode `--demo`, `usage.json` va dans `%TEMP%\UsageNotch-demo\` pour ne jamais écraser la vraie lecture.
- Contrats du Plan 1 à respecter : l'application s'appelle `UsageNotch.App.exe` et `UsageNotch.Hook.exe` est dans le même dossier ; `--from-hook` fait quitter une seconde instance sans rien afficher ; « Quitter » met `AutoLaunch` à `false` et un démarrage manuel le remet à `true` ; les abonnements aux événements des magasins se font **avant** `host.StartAsync` ; les événements arrivent sur des threads d'arrière-plan et sont relus sur le thread UI ; `UsagePoller` appelle lui-même `UsageStore.Load()`.
- `HttpClient.Timeout = ClaudeUsageProvider.Timeout`.
- Mode démo isolé de la vraie application : mutex `Local\UsageNotch-demo` et port 48667 (`AppPaths.DemoPort`), écrit dans les réglages de démo tant qu'ils gardent le port par défaut. Le hook réel vise toujours 48666 ; une démo ne reçoit donc jamais d'événement Claude Code réel. Toutes les vérifications en démo utilisent 48667.
- Constantes de mise en page (DIP, avant échelle) : épaisseur de pilule 64, longueur de corps 104, rayon des coins 16, congé 16, anneau 44, écart pilule-carte 10, marge écran 8.
- Délais : fermeture de la carte 250 ms, repli 400 ms, dépli 200 ms, ouverture carte 180 ms, fermeture carte 150 ms, filet de sécurité du survol 200 ms, ouverture automatique 5 s, rafraîchissement des textes de réinitialisation 30 s.
- Sécurité des vérifications manuelles : ne jamais modifier `%USERPROFILE%\.claude\settings.json` ni lire `.credentials.json` hors du code de l'application ; ne jamais installer les hooks sur la vraie configuration pendant l'exécution du plan (la tâche 15 le laisse à l'utilisateur).
- Commits fréquents, un par tâche au minimum, messages en anglais `type(scope): description`, fichier de message en UTF-8 sans BOM, terminés par :
  ```
  Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01DhmM4QvqWKwELXSCEZcweW
  ```

## Procédures de vérification visuelle

Les tâches 11 à 15 se vérifient à l'exécution. Ces procédures, en PowerShell, sont communes à toutes. Elles déplacent le vrai pointeur de la souris. Enregistrer les captures dans le dossier scratchpad de la session, jamais dans le dépôt, et les lire avec l'outil de lecture d'image.

**Préparation** (une fois par session PowerShell, avant les autres procédures) :
```powershell
Add-Type -Namespace Verify -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern System.IntPtr SetThreadDpiAwarenessContext(System.IntPtr ctx);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern System.IntPtr FindWindow(string cls, string title);
[DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern System.IntPtr WindowFromPoint(POINT p);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, System.UIntPtr extra);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
[StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
'@
[Verify.Win]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null   # coordonnées physiques
$scratch = '<dossier scratchpad de la session>'
function Get-NotchRect([string]$title) {
    $h = [Verify.Win]::FindWindow($null, $title)
    $r = New-Object Verify.Win+RECT
    [Verify.Win]::GetWindowRect($h, [ref]$r) | Out-Null
    [pscustomobject]@{ Handle = $h; Left = $r.Left; Top = $r.Top; Right = $r.Right; Bottom = $r.Bottom;
                       CenterX = [int](($r.Left + $r.Right) / 2); CenterY = [int](($r.Top + $r.Bottom) / 2) }
}
```

**Capture d'écran** (`$name` = nom court de l'étape) :
```powershell
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$b = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
$g.Dispose()
$bmp.Save((Join-Path $scratch "shot-$name.png"))
$bmp.Dispose()
```
Pour une image plus lisible, recadrer autour de la pilule avec `$bmp.Clone((New-Object System.Drawing.Rectangle $x, $y, $w, $h), $bmp.PixelFormat)` avant l'enregistrement.

**Test de clic** (pilule au bord droit) :
```powershell
$p = Get-NotchRect "UsageNotch — pilule"
$center = New-Object Verify.Win+POINT; $center.X = $p.CenterX; $center.Y = $p.CenterY
$corner = New-Object Verify.Win+POINT; $corner.X = $p.Left + 1; $corner.Y = $p.Top + 1
"centre sur la pilule : $([Verify.Win]::WindowFromPoint($center) -eq $p.Handle)"
"coin transparent sur la pilule : $([Verify.Win]::WindowFromPoint($corner) -eq $p.Handle)"
```
Attendu : `True` puis `False`. Le coin supérieur gauche du rectangle est hors de la forme (côté intérieur, au-dessus du congé) : Windows doit le laisser à la fenêtre du dessous.

**Survol** : `[Verify.Win]::SetCursorPos($x, $y) | Out-Null`, puis attendre le délai indiqué par l'étape.

**Clic** : `[Verify.Win]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds 150; [Verify.Win]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); [Verify.Win]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)`

**Démarrer et arrêter la démo** :
```powershell
$exe = "src\UsageNotch.App\bin\Debug\net10.0-windows\UsageNotch.App.exe"
if (Get-NetTCPConnection -LocalPort 48667 -State Listen -ErrorAction SilentlyContinue) { throw "port 48667 occupé" }
$app = Start-Process $exe -ArgumentList "--demo" -PassThru
Start-Sleep -Seconds 4
# … étapes …
Stop-Process -Id $app.Id
```
Ne lancer l'application qu'en `--demo` (ou `doctor --demo`) pendant l'exécution de ce plan : le mode normal appelle l'API d'Anthropic avec le jeton de l'utilisateur et écrit dans `%APPDATA%\UsageNotch`.

## Structure des fichiers

```
src/UsageNotch.Presentation/
  UsageNotch.Presentation.csproj
  AppArguments.cs                  analyse de la ligne de commande
  Formatting/FrenchText.cs         pourcentages, réinitialisation, « mis à jour il y a », durées
  Pill/ActivityKind.cs             enum
  Pill/CellModel.cs                record de la cellule
  Pill/PillMetrics.cs              dimensions logiques de la pilule
  Pill/PillPresenter.cs            snapshot + sessions + thème → CellModel
  Card/CardModel.cs                records WindowRow, SessionRow, CardModel
  Card/CardPresenter.cs            snapshot + sessions + thème → CardModel
  Behavior/TransitionWatcher.cs    détection Done / Attention
  Behavior/HoverController.cs      carte visible, dépli, verrou, aperçu 5 s
  Behavior/TerminalWindowChooser.cs choix de la fenêtre hôte d'une session
  Services/IUiDispatcher.cs        interfaces des services fournis par l'App
  Services/ISessionFocus.cs
  Services/ISoundPlayer.cs
  Services/IAccentColorSource.cs
  ViewModels/NotchViewModel.cs     état observable de la pilule et de la carte
  Diagnostics/DoctorReport.cs      texte du diagnostic
src/UsageNotch.Core/
  Logging/FileLoggerProvider.cs    journal fichier quotidien, 7 jours
src/UsageNotch.App/
  UsageNotch.App.csproj
  app.manifest                     PerMonitorV2
  App.xaml / App.xaml.cs           démarrage, instance unique, arrêt
  Hosting/AppPaths.cs              chemins de données (réels ou démo)
  Hosting/AppHost.cs               conteneur DI et services
  Hosting/NotchShell.cs            création et liaison de tout ce qui s'affiche
  Hosting/SingleInstance.cs        mutex + signal /open-settings
  Hosting/WpfDispatcher.cs         IUiDispatcher
  Hosting/DemoMode.cs              fournisseur et sessions de démonstration
  Hosting/SettingsFileWatcher.cs   relecture à chaud de settings.json
  Hosting/DoctorCommand.cs         sortie du diagnostic
  Interop/NativeMethods.cs         toutes les déclarations Win32
  Interop/WindowStyles.cs          non-activation, placement physique, curseur
  Interop/MonitorService.cs        énumération des écrans
  Interop/TerminalFocus.cs         ISessionFocus
  Interop/SystemAccentColor.cs     IAccentColorSource
  Interop/SystemSoundPlayer.cs     ISoundPlayer
  Interop/AutoStart.cs             clé Run
  Converters/HexBrushConverter.cs  couleur hex → brosse gelée
  Converters/ActivityVisibilityConverter.cs
  Converters/NotNullVisibilityConverter.cs
  Controls/ProgressRing.cs         anneau animé et géométrie d'arc
  Controls/PillShapeBuilder.cs     géométrie de la pilule et de la bande
  Views/PillWindow.xaml(.cs)       pilule, bande, menu contextuel, glisser Alt
  Views/CardWindow.xaml(.cs)       carte de détail
  Views/NotchPlacer.cs             position physique de la pilule et de la carte
  Tray/TrayIconService.cs          icône et menu de la zone de notification
tests/UsageNotch.Presentation.Tests/
  UsageNotch.Presentation.Tests.csproj
  TempDir.cs, ImmediateDispatcher.cs
  AppArgumentsTests.cs
  Formatting/FrenchTextTests.cs
  Pill/PillPresenterTests.cs
  Card/CardPresenterTests.cs
  Behavior/TransitionWatcherTests.cs
  Behavior/HoverControllerTests.cs
  Behavior/TerminalWindowChooserTests.cs
  ViewModels/NotchViewModelTests.cs
  Diagnostics/DoctorReportTests.cs
tests/UsageNotch.Core.Tests/Logging/FileLoggerProviderTests.cs
scripts/publish.ps1                publication App + Hook AOT dans publish\
```

Le dossier des fenêtres s'appelle `Views` et non `Windows` : un espace de noms `UsageNotch.App.Windows` masquerait `System.Windows` dans le code généré par WPF.

---

### Task 1 : Projet Presentation et textes français

**Files:**
- Create: `src/UsageNotch.Presentation/UsageNotch.Presentation.csproj`, `src/UsageNotch.Presentation/Formatting/FrenchText.cs`
- Create: `tests/UsageNotch.Presentation.Tests/UsageNotch.Presentation.Tests.csproj`, `tests/UsageNotch.Presentation.Tests/TempDir.cs`, `tests/UsageNotch.Presentation.Tests/Formatting/FrenchTextTests.cs`
- Modify: `UsageNotch.sln` (ajout des deux projets)

**Interfaces:**
- Produces: `static class FrenchText` avec `const char Nbsp` (U+00A0), `string Percent(double fraction)`, `string ResetCopy(DateTimeOffset resetsAt, DateTimeOffset now, TimeZoneInfo zone)`, `string UpdatedAgo(DateTimeOffset fetchedAt, DateTimeOffset now)`, `string Duration(TimeSpan span)`.

- [ ] **Step 1 : Créer les projets**

```powershell
dotnet new classlib -n UsageNotch.Presentation -o src/UsageNotch.Presentation -f net10.0
dotnet new xunit -n UsageNotch.Presentation.Tests -o tests/UsageNotch.Presentation.Tests -f net10.0
dotnet sln add src/UsageNotch.Presentation tests/UsageNotch.Presentation.Tests
dotnet add src/UsageNotch.Presentation reference src/UsageNotch.Core
dotnet add tests/UsageNotch.Presentation.Tests reference src/UsageNotch.Presentation src/UsageNotch.Core
Remove-Item src/UsageNotch.Presentation/Class1.cs, tests/UsageNotch.Presentation.Tests/UnitTest1.cs
```

`src/UsageNotch.Presentation/UsageNotch.Presentation.csproj` :
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>UsageNotch.Presentation</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\UsageNotch.Core\UsageNotch.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="UsageNotch.Presentation.Tests" />
  </ItemGroup>
</Project>
```

Dans `tests/UsageNotch.Presentation.Tests/UsageNotch.Presentation.Tests.csproj`, garder les paquets du modèle xunit et `<Using Include="Xunit" />`, puis ajouter :
```xml
<ItemGroup>
  <PackageReference Include="FluentAssertions" Version="7.2.0" />
  <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.10.0" />
</ItemGroup>
```

`tests/UsageNotch.Presentation.Tests/TempDir.cs` :
```csharp
namespace UsageNotch.Presentation.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "usagenotch-presentation-tests", Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
```

- [ ] **Step 2 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/Formatting/FrenchTextTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Tests.Formatting;

public class FrenchTextTests
{
    // Lundi 14 septembre 2026, 12:00 UTC
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(0.734, "73")]
    [InlineData(0.736, "74")]
    [InlineData(1.0, "100")]
    [InlineData(1.7, "100")]
    [InlineData(-0.2, "0")]
    public void Percent_is_a_rounded_integer_with_a_non_breaking_space(double fraction, string expected) =>
        FrenchText.Percent(fraction).Should().Be(expected + FrenchText.Nbsp + "%");

    [Fact]
    public void Reset_in_the_past_is_imminent() =>
        FrenchText.ResetCopy(Now.AddSeconds(-5), Now, Utc).Should().Be("Réinitialisation imminente");

    [Theory]
    [InlineData(20, "Réinitialisation dans 1 min")]
    [InlineData(51 * 60, "Réinitialisation dans 51 min")]
    [InlineData(59 * 60 + 40, "Réinitialisation dans 60 min")]
    public void Reset_under_an_hour_is_relative(int seconds, string expected) =>
        FrenchText.ResetCopy(Now.AddSeconds(seconds), Now, Utc).Should().Be(expected);

    [Fact]
    public void Reset_later_today_or_tomorrow_within_24h_shows_the_time() =>
        FrenchText.ResetCopy(Now.AddHours(5).AddMinutes(7), Now, Utc).Should().Be("Réinitialisation à 17:07");

    [Fact]
    public void Reset_beyond_24h_shows_the_abbreviated_day_and_time() =>
        FrenchText.ResetCopy(new DateTimeOffset(2026, 9, 17, 0, 0, 0, TimeSpan.Zero), Now, Utc)
            .Should().Be("Réinitialisation jeu. 00:00");

    [Fact]
    public void Reset_time_uses_the_given_time_zone()
    {
        var paris = TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");
        FrenchText.ResetCopy(Now.AddHours(3), Now, paris).Should().Be("Réinitialisation à 17:00");
    }

    [Theory]
    [InlineData(10, "Mis à jour à l'instant")]
    [InlineData(60, "Mis à jour il y a 1 min")]
    [InlineData(12 * 60, "Mis à jour il y a 12 min")]
    [InlineData(3 * 3600 + 100, "Mis à jour il y a 3 h")]
    [InlineData(50 * 3600, "Mis à jour il y a 2 j")]
    public void Updated_ago_scales_its_unit(int secondsAgo, string expected) =>
        FrenchText.UpdatedAgo(Now.AddSeconds(-secondsAgo), Now).Should().Be(expected);

    [Theory]
    [InlineData(42, "42 s")]
    [InlineData(3 * 60 + 10, "3 min")]
    [InlineData(65 * 60, "1 h 05")]
    [InlineData(0, "0 s")]
    public void Duration_is_compact(int seconds, string expected) =>
        FrenchText.Duration(TimeSpan.FromSeconds(seconds)).Should().Be(expected);
}
```

- [ ] **Step 3 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter FrenchTextTests`
Expected: erreur de compilation, `UsageNotch.Presentation.Formatting` inconnu.

- [ ] **Step 4 : Implémenter**

`src/UsageNotch.Presentation/Formatting/FrenchText.cs` :
```csharp
using System.Globalization;

namespace UsageNotch.Presentation.Formatting;

/// <summary>Tous les textes chiffrés affichés par le notch, en français.</summary>
public static class FrenchText
{
    public const char Nbsp = '\u00A0';

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static string Percent(double fraction)
    {
        var value = (int)Math.Round(Math.Clamp(fraction, 0.0, 1.0) * 100, MidpointRounding.AwayFromZero);
        return $"{value}{Nbsp}%";
    }

    /// <summary>Sous une heure : relatif. Sous 24 h : heure. Au-delà : jour abrégé et heure.</summary>
    public static string ResetCopy(DateTimeOffset resetsAt, DateTimeOffset now, TimeZoneInfo zone)
    {
        var diff = resetsAt - now;
        if (diff <= TimeSpan.Zero) return "Réinitialisation imminente";
        if (diff < TimeSpan.FromHours(1))
        {
            var minutes = Math.Max(1, (int)Math.Round(diff.TotalMinutes, MidpointRounding.AwayFromZero));
            return $"Réinitialisation dans {minutes} min";
        }

        var local = TimeZoneInfo.ConvertTime(resetsAt, zone);
        var time = local.ToString("HH:mm", French);
        if (diff < TimeSpan.FromHours(24)) return $"Réinitialisation à {time}";
        return $"Réinitialisation {local.ToString("ddd", French)} {time}";
    }

    public static string UpdatedAgo(DateTimeOffset fetchedAt, DateTimeOffset now)
    {
        var age = now - fetchedAt;
        if (age < TimeSpan.FromMinutes(1)) return "Mis à jour à l'instant";
        if (age < TimeSpan.FromHours(1)) return $"Mis à jour il y a {(int)age.TotalMinutes} min";
        if (age < TimeSpan.FromHours(48)) return $"Mis à jour il y a {(int)age.TotalHours} h";
        return $"Mis à jour il y a {(int)age.TotalDays} j";
    }

    public static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span < TimeSpan.FromMinutes(1)) return $"{(int)span.TotalSeconds} s";
        if (span < TimeSpan.FromHours(1)) return $"{(int)span.TotalMinutes} min";
        return $"{(int)span.TotalHours} h {span.Minutes:00}";
    }
}
```

- [ ] **Step 5 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter FrenchTextTests`
Expected: 22 tests passés. Puis `dotnet test` à la racine : 238 tests passés (216 + 22), aucun avertissement.

- [ ] **Step 6 : Commit**

```powershell
git add UsageNotch.sln src/UsageNotch.Presentation tests/UsageNotch.Presentation.Tests
git commit -F <fichier message>   # feat(presentation): project and French text formatting
```

---

### Task 2 : Modèle de cellule et dimensions de la pilule

**Files:**
- Create: `src/UsageNotch.Presentation/Pill/ActivityKind.cs`, `Pill/CellModel.cs`, `Pill/PillMetrics.cs`, `Pill/PillPresenter.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs`

**Interfaces:**
- Consumes: `UsageSnapshot`, `SnapshotStatus`, `SessionState`, `Theme`, `CellContent` (Core) ; `FrenchText.Percent` (Task 1).
- Produces:
  - `enum ActivityKind { None, Done, Running, Attention }`
  - `record CellModel(double? RingFraction, string RingColor, string TrackColor, string PercentText, string TextColor, bool ShowRing, bool ShowPercent, bool Dimmed, bool Exhausted, ActivityKind Activity, string ActivityColor, string BandColor)`
  - `static class PillMetrics` : constantes `Thickness = 64`, `BodyLength = 104`, `CornerRadius = 16`, `Fillet = 16`, `RingSize = 44`, `CardGap = 10`, `ScreenMargin = 8` (double, DIP) ; `double WindowLength => BodyLength + 2 * Fillet`.
  - `static class PillPresenter` : `static readonly TimeSpan StaleAfter` (10 min), `CellModel Cell(UsageSnapshot snapshot, string headlineWindowId, SessionState aggregate, Theme theme, CellContent content, DateTimeOffset now)`, `ActivityKind ActivityOf(SessionState state)`, `string ActivityColor(ActivityKind kind, Theme theme)`, `bool IsWaitingForFirstReading(UsageSnapshot snapshot)`.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.Presentation.Tests.Pill;

public class PillPresenterTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Theme Theme = UsageNotch.Core.Settings.Theme.Codenotch;

    private static UsageSnapshot Snap(SnapshotStatus status, double? session, DateTimeOffset? fetched = null, string note = "")
    {
        var windows = session is { } f
            ? new[] { new LimitWindow("session", "Session en cours", f, Now.AddHours(2)) }
            : Array.Empty<LimitWindow>();
        return new UsageSnapshot(status, windows, fetched ?? Now, note, null);
    }

    private static CellModel Cell(UsageSnapshot s, SessionState agg = SessionState.Idle, CellContent content = CellContent.RingAndPercent) =>
        PillPresenter.Cell(s, "session", agg, Theme, content, Now);

    [Fact]
    public void An_ok_reading_shows_the_headline_fraction_percent_and_level_colour()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.73));
        c.RingFraction.Should().BeApproximately(0.73, 1e-9);
        c.PercentText.Should().Be("73" + FrenchText.Nbsp + "%");
        c.RingColor.Should().Be(Theme.LevelWatch);
        c.TrackColor.Should().Be(Theme.RingTrack);
        c.TextColor.Should().Be(Theme.Text);
        c.Dimmed.Should().BeFalse();
        c.Exhausted.Should().BeFalse();
        c.BandColor.Should().Be(Theme.LevelWatch);
    }

    [Theory]
    [InlineData(0.10, "#28E07B")]
    [InlineData(0.85, "#FF4500")]
    public void The_ring_colour_follows_the_theme_thresholds(double used, string colour) =>
        Cell(Snap(SnapshotStatus.Ok, used)).RingColor.Should().Be(colour);

    [Fact]
    public void A_full_window_is_exhausted()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 1.0));
        c.Exhausted.Should().BeTrue();
        c.RingFraction.Should().Be(1.0);
    }

    [Fact]
    public void A_missing_headline_window_is_a_dash_not_another_window()
    {
        var s = new UsageSnapshot(SnapshotStatus.Ok,
            [new LimitWindow("weekly_all", "Hebdomadaire (tous modèles)", 0.6, Now.AddDays(3))], Now, "", null);
        var c = Cell(s);
        c.RingFraction.Should().BeNull();
        c.PercentText.Should().Be("—");
        c.RingColor.Should().Be(Theme.RingTrack);
        c.BandColor.Should().Be(Theme.RingTrack);
    }

    [Fact]
    public void The_empty_startup_snapshot_waits_with_an_ellipsis()
    {
        var c = Cell(UsageSnapshot.Empty);
        PillPresenter.IsWaitingForFirstReading(UsageSnapshot.Empty).Should().BeTrue();
        c.RingFraction.Should().BeNull();
        c.PercentText.Should().Be("…");
    }

    [Fact]
    public void Needs_auth_shows_a_dash_even_with_old_windows()
    {
        var c = Cell(Snap(SnapshotStatus.NeedsAuth, 0.4, note: "Identifiant refusé"));
        c.RingFraction.Should().BeNull();
        c.PercentText.Should().Be("—");
    }

    [Fact]
    public void A_stale_status_dims_the_cell()
    {
        Cell(Snap(SnapshotStatus.Stale, 0.4)).Dimmed.Should().BeTrue();
    }

    [Fact]
    public void An_ok_reading_older_than_ten_minutes_is_dimmed()
    {
        Cell(Snap(SnapshotStatus.Ok, 0.4, fetched: Now.AddMinutes(-9))).Dimmed.Should().BeFalse();
        Cell(Snap(SnapshotStatus.Ok, 0.4, fetched: Now.AddMinutes(-11))).Dimmed.Should().BeTrue();
    }

    [Theory]
    [InlineData(SessionState.Idle, ActivityKind.None)]
    [InlineData(SessionState.Done, ActivityKind.Done)]
    [InlineData(SessionState.Running, ActivityKind.Running)]
    [InlineData(SessionState.Attention, ActivityKind.Attention)]
    public void The_aggregate_session_state_maps_to_an_activity(SessionState state, ActivityKind kind) =>
        Cell(Snap(SnapshotStatus.Ok, 0.2), state).Activity.Should().Be(kind);

    [Fact]
    public void Activity_colours_come_from_the_theme()
    {
        PillPresenter.ActivityColor(ActivityKind.Running, Theme).Should().Be(Theme.Running);
        PillPresenter.ActivityColor(ActivityKind.Attention, Theme).Should().Be(Theme.Attention);
        PillPresenter.ActivityColor(ActivityKind.Done, Theme).Should().Be(Theme.Done);
        PillPresenter.ActivityColor(ActivityKind.None, Theme).Should().Be(Theme.RingTrack);
    }

    [Fact]
    public void Attention_turns_the_folded_band_amber()
    {
        Cell(Snap(SnapshotStatus.Ok, 0.2), SessionState.Attention).BandColor.Should().Be(Theme.Attention);
    }

    [Theory]
    [InlineData(CellContent.RingAndPercent, true, true)]
    [InlineData(CellContent.RingOnly, true, false)]
    [InlineData(CellContent.PercentOnly, false, true)]
    public void Cell_content_setting_controls_what_is_shown(CellContent content, bool ring, bool percent)
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.2), content: content);
        c.ShowRing.Should().Be(ring);
        c.ShowPercent.Should().Be(percent);
    }

    [Fact]
    public void Window_length_includes_both_fillets() =>
        PillMetrics.WindowLength.Should().Be(PillMetrics.BodyLength + 2 * PillMetrics.Fillet);
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter PillPresenterTests`
Expected: erreur de compilation, `UsageNotch.Presentation.Pill` inconnu.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Presentation/Pill/ActivityKind.cs` :
```csharp
namespace UsageNotch.Presentation.Pill;

/// <summary>Ce que l'anneau intérieur montre : rien, un point « terminé », un arc qui tourne, un anneau qui pulse.</summary>
public enum ActivityKind
{
    None,
    Done,
    Running,
    Attention,
}
```

`src/UsageNotch.Presentation/Pill/CellModel.cs` :
```csharp
namespace UsageNotch.Presentation.Pill;

/// <summary>
/// Tout ce que la cellule dessine. <see cref="RingFraction"/> null = pas de lecture exploitable (tiret ou attente).
/// Couleurs en <c>#RRGGBB</c>.
/// </summary>
public sealed record CellModel(
    double? RingFraction,
    string RingColor,
    string TrackColor,
    string PercentText,
    string TextColor,
    bool ShowRing,
    bool ShowPercent,
    bool Dimmed,
    bool Exhausted,
    ActivityKind Activity,
    string ActivityColor,
    string BandColor);
```

`src/UsageNotch.Presentation/Pill/PillMetrics.cs` :
```csharp
namespace UsageNotch.Presentation.Pill;

/// <summary>Dimensions logiques (DIP) à l'échelle 100 %. L'App les multiplie par Settings.Scale.</summary>
public static class PillMetrics
{
    public const double Thickness = 64;
    public const double BodyLength = 104;
    public const double CornerRadius = 16;
    public const double Fillet = 16;
    public const double RingSize = 44;
    public const double CardGap = 10;
    public const double ScreenMargin = 8;

    /// <summary>Longueur de la fenêtre le long du bord : le corps plus les deux congés qui le soudent au bord.</summary>
    public static double WindowLength => BodyLength + 2 * Fillet;
}
```

`src/UsageNotch.Presentation/Pill/PillPresenter.cs` :
```csharp
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Pill;

public static class PillPresenter
{
    /// <summary>Une lecture Ok plus vieille que ça est assombrie : le planificateur relit toutes les 5 min au repos.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

    public static CellModel Cell(
        UsageSnapshot snapshot,
        string headlineWindowId,
        SessionState aggregate,
        Theme theme,
        CellContent content,
        DateTimeOffset now)
    {
        var activity = ActivityOf(aggregate);
        var headline = snapshot.Status == SnapshotStatus.NeedsAuth ? null : snapshot.Window(headlineWindowId);

        double? fraction = headline is null ? null : Math.Clamp(headline.UsedFraction, 0.0, 1.0);
        var ringColor = fraction is { } f ? theme.LevelColor(f) : theme.RingTrack;
        var percent = fraction is { } p
            ? FrenchText.Percent(p)
            : IsWaitingForFirstReading(snapshot) ? "…" : "—";

        var dimmed = snapshot.Status == SnapshotStatus.Stale
            || (snapshot.FetchedAt != DateTimeOffset.MinValue && now - snapshot.FetchedAt > StaleAfter);

        return new CellModel(
            RingFraction: fraction,
            RingColor: ringColor,
            TrackColor: theme.RingTrack,
            PercentText: percent,
            TextColor: theme.Text,
            ShowRing: content != CellContent.PercentOnly,
            ShowPercent: content != CellContent.RingOnly,
            Dimmed: dimmed,
            Exhausted: fraction >= 1.0,
            Activity: activity,
            ActivityColor: ActivityColor(activity, theme),
            BandColor: activity == ActivityKind.Attention ? theme.Attention : ringColor);
    }

    public static ActivityKind ActivityOf(SessionState state) => state switch
    {
        SessionState.Attention => ActivityKind.Attention,
        SessionState.Running => ActivityKind.Running,
        SessionState.Done => ActivityKind.Done,
        _ => ActivityKind.None,
    };

    public static string ActivityColor(ActivityKind kind, Theme theme) => kind switch
    {
        ActivityKind.Attention => theme.Attention,
        ActivityKind.Running => theme.Running,
        ActivityKind.Done => theme.Done,
        _ => theme.RingTrack,
    };

    /// <summary>Le snapshot vide du démarrage : aucune lecture, aucune note, jamais lu.</summary>
    public static bool IsWaitingForFirstReading(UsageSnapshot snapshot) =>
        snapshot.Windows.Count == 0 && snapshot.Note.Length == 0 && snapshot.FetchedAt == DateTimeOffset.MinValue;
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter PillPresenterTests`
Expected: 19 tests passés (10 Facts + 9 cas de Theory).

- [ ] **Step 5 : Commit**

`feat(presentation): cell model and pill presenter`

---

### Task 3 : Modèle de la carte de détail

**Files:**
- Create: `src/UsageNotch.Presentation/Card/CardModel.cs`, `Card/CardPresenter.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Card/CardPresenterTests.cs`

**Interfaces:**
- Consumes: `UsageSnapshot`, `Session`, `SessionState`, `Theme` (Core) ; `FrenchText` (Task 1) ; `ActivityKind`, `PillPresenter.ActivityOf`, `PillPresenter.ActivityColor`, `PillPresenter.StaleAfter`, `PillPresenter.IsWaitingForFirstReading` (Task 2).
- Produces:
  - `record WindowRow(string Label, string ResetText, double Fraction, string BarColor, string UsedText)`
  - `record SessionRow(string SessionId, string Title, string Detail, ActivityKind Activity, string DotColor)`
  - `record CardModel(string Title, string? Subtitle, IReadOnlyList<WindowRow> Windows, string? Note, IReadOnlyList<SessionRow> Sessions)`
  - `static class CardPresenter` : `const int MaxSessions = 5`, `CardModel Build(UsageSnapshot snapshot, string displayName, IReadOnlyList<Session> sessions, Theme theme, DateTimeOffset now, TimeZoneInfo zone)`.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/Card/CardPresenterTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Card;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.Presentation.Tests.Card;

public class CardPresenterTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Theme Theme = UsageNotch.Core.Settings.Theme.Codenotch;

    private static Session S(string id, SessionState state, string title = "proj · abcd", string action = "",
        string attention = "", string prompt = "", TimeSpan total = default, int startedMinutesAgo = 1) =>
        new(id, title, state, Now.AddMinutes(-startedMinutesAgo), total, action, attention, prompt, "", 0, "", Now);

    private static UsageSnapshot Ok(params LimitWindow[] windows) => new(SnapshotStatus.Ok, windows, Now, "", null);

    private static CardModel Build(UsageSnapshot s, params Session[] sessions) =>
        CardPresenter.Build(s, "Claude", sessions, Theme, Now, TimeZoneInfo.Utc);

    [Fact]
    public void Window_rows_show_label_reset_bar_and_used_text()
    {
        var card = Build(Ok(
            new LimitWindow("session", "Session en cours", 0.73, Now.AddMinutes(51)),
            new LimitWindow("weekly_all", "Hebdomadaire (tous modèles)", 0.07, new DateTimeOffset(2026, 9, 17, 0, 0, 0, TimeSpan.Zero))));

        card.Title.Should().Be("Claude");
        card.Subtitle.Should().BeNull();
        card.Note.Should().BeNull();
        card.Windows.Should().HaveCount(2);
        card.Windows[0].Should().Be(new WindowRow("Session en cours", "Réinitialisation dans 51 min", 0.73, Theme.LevelWatch,
            "73" + FrenchText.Nbsp + "% utilisé"));
        card.Windows[1].ResetText.Should().Be("Réinitialisation jeu. 00:00");
        card.Windows[1].BarColor.Should().Be(Theme.LevelAmple);
    }

    [Fact]
    public void A_stale_snapshot_gets_an_updated_ago_subtitle_and_keeps_its_note()
    {
        var s = new UsageSnapshot(SnapshotStatus.Stale,
            [new LimitWindow("session", "Session en cours", 0.4, Now.AddHours(2))], Now.AddMinutes(-12), "HTTP 500", null);
        var card = Build(s);
        card.Subtitle.Should().Be("Mis à jour il y a 12 min");
        card.Note.Should().Be("HTTP 500");
    }

    [Fact]
    public void Needs_auth_shows_only_the_note()
    {
        var s = new UsageSnapshot(SnapshotStatus.NeedsAuth,
            [new LimitWindow("session", "Session en cours", 0.4, Now.AddHours(2))], Now, "Identifiant refusé (changement de compte ?).", null);
        var card = Build(s);
        card.Windows.Should().BeEmpty();
        card.Note.Should().Be("Identifiant refusé (changement de compte ?).");
    }

    [Fact]
    public void The_startup_snapshot_says_it_is_waiting()
    {
        var card = Build(UsageSnapshot.Empty);
        card.Windows.Should().BeEmpty();
        card.Note.Should().Be("En attente de la première lecture…");
        card.Subtitle.Should().BeNull();
    }

    [Fact]
    public void Idle_sessions_are_not_listed_and_at_most_five_are()
    {
        var sessions = Enumerable.Range(0, 7).Select(i => S($"r{i}", SessionState.Running)).Append(S("idle", SessionState.Idle)).ToArray();
        var card = Build(Ok(), sessions);
        card.Sessions.Should().HaveCount(CardPresenter.MaxSessions);
        card.Sessions.Should().NotContain(r => r.SessionId == "idle");
    }

    [Fact]
    public void Session_details_depend_on_the_state()
    {
        var card = Build(Ok(),
            S("a", SessionState.Attention, attention: "Autoriser Bash ?"),
            S("a2", SessionState.Attention),
            S("r", SessionState.Running, action: "🔧 Bash : dotnet test", prompt: "fais X"),
            S("r2", SessionState.Running, prompt: "corrige le bug"),
            S("r3", SessionState.Running),
            S("d", SessionState.Done, total: TimeSpan.FromMinutes(3)));

        card.Sessions.Select(r => r.Detail).Should().Equal(
            "Autoriser Bash ?", "Attend votre réponse", "🔧 Bash : dotnet test", "corrige le bug", "En cours");
        card.Sessions.Select(r => r.Activity).Should().Equal(
            ActivityKind.Attention, ActivityKind.Attention, ActivityKind.Running, ActivityKind.Running, ActivityKind.Running);
        card.Sessions[0].DotColor.Should().Be(Theme.Attention);
        card.Sessions[2].DotColor.Should().Be(Theme.Running);
    }

    [Fact]
    public void A_done_session_shows_its_duration()
    {
        var card = Build(Ok(), S("d", SessionState.Done, title: "api · 1234", total: TimeSpan.FromSeconds(42)));
        card.Sessions.Should().ContainSingle().Which.Should().Be(
            new SessionRow("d", "api · 1234", "Terminé en 42 s", ActivityKind.Done, Theme.Done));
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter CardPresenterTests`
Expected: erreur de compilation, `UsageNotch.Presentation.Card` inconnu.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Presentation/Card/CardModel.cs` :
```csharp
using UsageNotch.Presentation.Pill;

namespace UsageNotch.Presentation.Card;

public sealed record WindowRow(string Label, string ResetText, double Fraction, string BarColor, string UsedText);

public sealed record SessionRow(string SessionId, string Title, string Detail, ActivityKind Activity, string DotColor);

public sealed record CardModel(
    string Title,
    string? Subtitle,
    IReadOnlyList<WindowRow> Windows,
    string? Note,
    IReadOnlyList<SessionRow> Sessions);
```

`src/UsageNotch.Presentation/Card/CardPresenter.cs` :
```csharp
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.Presentation.Card;

public static class CardPresenter
{
    public const int MaxSessions = 5;

    /// <summary>Les sessions arrivent déjà triées par SessionStore.Snapshot() (état puis début décroissant).</summary>
    public static CardModel Build(
        UsageSnapshot snapshot,
        string displayName,
        IReadOnlyList<Session> sessions,
        Theme theme,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        var waiting = PillPresenter.IsWaitingForFirstReading(snapshot);

        var neverRead = snapshot.FetchedAt == DateTimeOffset.MinValue;
        var dimmed = !neverRead && (snapshot.Status == SnapshotStatus.Stale || now - snapshot.FetchedAt > PillPresenter.StaleAfter);
        var subtitle = dimmed ? FrenchText.UpdatedAgo(snapshot.FetchedAt, now) : null;

        IReadOnlyList<WindowRow> windows = snapshot.Status == SnapshotStatus.NeedsAuth
            ? []
            : snapshot.Windows.Select(w => Row(w, theme, now, zone)).ToList();

        var note = snapshot.Note.Length > 0
            ? snapshot.Note
            : waiting ? "En attente de la première lecture…" : null;

        var rows = sessions
            .Where(s => s.State != SessionState.Idle)
            .Take(MaxSessions)
            .Select(s => SessionRowOf(s, theme))
            .ToList();

        return new CardModel(displayName, subtitle, windows, note, rows);
    }

    private static WindowRow Row(LimitWindow w, Theme theme, DateTimeOffset now, TimeZoneInfo zone)
    {
        var fraction = Math.Clamp(w.UsedFraction, 0.0, 1.0);
        return new WindowRow(
            w.Label,
            FrenchText.ResetCopy(w.ResetsAt, now, zone),
            fraction,
            theme.LevelColor(fraction),
            FrenchText.Percent(fraction) + " utilisé");
    }

    private static SessionRow SessionRowOf(Session s, Theme theme)
    {
        var activity = PillPresenter.ActivityOf(s.State);
        var detail = s.State switch
        {
            SessionState.Attention => s.AttentionMessage.Length > 0 ? s.AttentionMessage : "Attend votre réponse",
            SessionState.Running => s.LastAction.Length > 0 ? s.LastAction : s.Prompt.Length > 0 ? s.Prompt : "En cours",
            SessionState.Done => "Terminé en " + FrenchText.Duration(s.Total),
            _ => "",
        };
        return new SessionRow(s.Id, s.Title, detail, activity, PillPresenter.ActivityColor(activity, theme));
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter CardPresenterTests`
Expected: 7 tests passés.

- [ ] **Step 5 : Commit**

`feat(presentation): detail card model`

---

### Task 4 : Détection des transitions de session

**Files:**
- Create: `src/UsageNotch.Presentation/Behavior/TransitionWatcher.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Behavior/TransitionWatcherTests.cs`

**Interfaces:**
- Consumes: `Session`, `SessionState` (Core).
- Produces: `enum TransitionKind { Done, Attention }`, `record SessionTransition(string SessionId, string Title, TransitionKind Kind)`, `sealed class TransitionWatcher` avec `IReadOnlyList<SessionTransition> Observe(IReadOnlyList<Session> sessions)`.

Règle (spec §5) : une transition vers Done ou Attention est annoncée ; rien n'est annoncé pour une session vue pour la première fois (elle arrive sans historique) ; une session qui disparaît est oubliée.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/Behavior/TransitionWatcherTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Sessions;
using UsageNotch.Presentation.Behavior;

namespace UsageNotch.Presentation.Tests.Behavior;

public class TransitionWatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static Session S(string id, SessionState state) =>
        new(id, $"proj · {id}", state, Now, TimeSpan.Zero, "", "", "", "", 0, "", Now);

    [Fact]
    public void A_session_seen_for_the_first_time_is_never_announced()
    {
        var w = new TransitionWatcher();
        w.Observe([S("a", SessionState.Done), S("b", SessionState.Attention)]).Should().BeEmpty();
    }

    [Fact]
    public void Running_to_done_is_announced_once()
    {
        var w = new TransitionWatcher();
        w.Observe([S("a", SessionState.Running)]);

        w.Observe([S("a", SessionState.Done)]).Should().Equal(new SessionTransition("a", "proj · a", TransitionKind.Done));
        w.Observe([S("a", SessionState.Done)]).Should().BeEmpty();
    }

    [Fact]
    public void Running_to_attention_is_announced_and_back_to_running_is_not()
    {
        var w = new TransitionWatcher();
        w.Observe([S("a", SessionState.Running)]);

        w.Observe([S("a", SessionState.Attention)]).Should().ContainSingle().Which.Kind.Should().Be(TransitionKind.Attention);
        w.Observe([S("a", SessionState.Running)]).Should().BeEmpty();
    }

    [Fact]
    public void Idle_to_running_is_not_announced()
    {
        var w = new TransitionWatcher();
        w.Observe([S("a", SessionState.Idle)]);
        w.Observe([S("a", SessionState.Running)]).Should().BeEmpty();
    }

    [Fact]
    public void Several_sessions_are_reported_in_input_order()
    {
        var w = new TransitionWatcher();
        w.Observe([S("a", SessionState.Running), S("b", SessionState.Running)]);

        w.Observe([S("b", SessionState.Attention), S("a", SessionState.Done)])
            .Select(t => t.SessionId).Should().Equal("b", "a");
    }

    [Fact]
    public void A_removed_session_is_forgotten_and_counts_as_new_if_it_returns()
    {
        var w = new TransitionWatcher();
        w.Observe([S("a", SessionState.Running)]);
        w.Observe([]);
        w.Observe([S("a", SessionState.Done)]).Should().BeEmpty();
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter TransitionWatcherTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Presentation/Behavior/TransitionWatcher.cs` :
```csharp
using UsageNotch.Core.Sessions;

namespace UsageNotch.Presentation.Behavior;

public enum TransitionKind
{
    Done,
    Attention,
}

public sealed record SessionTransition(string SessionId, string Title, TransitionKind Kind);

/// <summary>
/// Compare chaque instantané des sessions au précédent et rend les passages vers Done ou Attention.
/// Une session inconnue est enregistrée sans être annoncée. Non thread-safe : appeler depuis le thread UI.
/// </summary>
public sealed class TransitionWatcher
{
    private Dictionary<string, SessionState> _last = new(StringComparer.Ordinal);

    public IReadOnlyList<SessionTransition> Observe(IReadOnlyList<Session> sessions)
    {
        var next = new Dictionary<string, SessionState>(StringComparer.Ordinal);
        var transitions = new List<SessionTransition>();

        foreach (var s in sessions)
        {
            next[s.Id] = s.State;
            if (!_last.TryGetValue(s.Id, out var previous)) continue;

            if (s.State == SessionState.Done && previous != SessionState.Done)
            {
                transitions.Add(new SessionTransition(s.Id, s.Title, TransitionKind.Done));
            }
            else if (s.State == SessionState.Attention && previous != SessionState.Attention)
            {
                transitions.Add(new SessionTransition(s.Id, s.Title, TransitionKind.Attention));
            }
        }

        _last = next;
        return transitions;
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter TransitionWatcherTests`
Expected: 6 tests passés.

- [ ] **Step 5 : Commit**

`feat(presentation): session transition watcher`

---

### Task 5 : Contrôleur de survol, verrou et aperçu

**Files:**
- Create: `src/UsageNotch.Presentation/Behavior/HoverController.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Behavior/HoverControllerTests.cs`

**Interfaces:**
- Produces: `sealed class HoverController(TimeProvider time) : IDisposable` avec `static readonly TimeSpan CloseDelay` (250 ms), `FoldDelay` (400 ms), `PeekDuration` (5 s) ; propriétés `bool CardVisible`, `bool Unfolded`, `bool Locked` ; `event Action? Changed` ; méthodes `PointerEnteredPill()`, `PointerLeftPill()`, `PointerEnteredCard()`, `PointerLeftCard()`, `ToggleLock()`, `Peek()`.

Règles (spec §6 « Survol », §7 « Modes de visibilité ») :
- Entrer sur la pilule ou la carte ouvre la carte et déplie la pilule, et annule toute fermeture en cours.
- Quand le pointeur n'est plus sur aucune des deux, que la carte n'est pas verrouillée et qu'aucun aperçu n'est en cours : la carte se ferme 250 ms plus tard et la pilule se replie 400 ms plus tard (les deux délais partent du premier départ du pointeur ; une notification de sortie répétée ne les repousse pas).
- `ToggleLock` verrouille (ouvre et déplie) ou libère (puis applique la règle précédente).
- `Peek` ouvre et déplie pendant 5 s, puis applique la règle précédente.
- `Changed` n'est levé que si `CardVisible`, `Unfolded` ou `Locked` a changé, toujours hors du verrou interne. Les minuteries peuvent le lever sur un thread d'arrière-plan.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/Behavior/HoverControllerTests.cs` :
```csharp
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Presentation.Behavior;

namespace UsageNotch.Presentation.Tests.Behavior;

public class HoverControllerTests
{
    private static (HoverController Hover, FakeTimeProvider Time, Func<int> Changes) Build()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero));
        var hover = new HoverController(time);
        var count = 0;
        hover.Changed += () => count++;
        return (hover, time, () => count);
    }

    [Fact]
    public void Starts_closed_folded_and_unlocked()
    {
        var (h, _, changes) = Build();
        h.CardVisible.Should().BeFalse();
        h.Unfolded.Should().BeFalse();
        h.Locked.Should().BeFalse();
        changes().Should().Be(0);
    }

    [Fact]
    public void Entering_the_pill_opens_and_unfolds_once()
    {
        var (h, _, changes) = Build();
        h.PointerEnteredPill();
        h.PointerEnteredPill();
        h.CardVisible.Should().BeTrue();
        h.Unfolded.Should().BeTrue();
        changes().Should().Be(1);
    }

    [Fact]
    public void Leaving_closes_after_250ms_and_folds_after_400ms()
    {
        var (h, time, _) = Build();
        h.PointerEnteredPill();
        h.PointerLeftPill();

        time.Advance(TimeSpan.FromMilliseconds(249));
        h.CardVisible.Should().BeTrue();

        time.Advance(TimeSpan.FromMilliseconds(1));
        h.CardVisible.Should().BeFalse();
        h.Unfolded.Should().BeTrue();

        time.Advance(TimeSpan.FromMilliseconds(150));
        h.Unfolded.Should().BeFalse();
    }

    [Fact]
    public void Crossing_from_the_pill_to_the_card_keeps_it_open()
    {
        var (h, time, _) = Build();
        h.PointerEnteredPill();
        h.PointerLeftPill();
        time.Advance(TimeSpan.FromMilliseconds(100));
        h.PointerEnteredCard();
        time.Advance(TimeSpan.FromSeconds(2));
        h.CardVisible.Should().BeTrue();
        h.Unfolded.Should().BeTrue();

        h.PointerLeftCard();
        time.Advance(HoverController.CloseDelay);
        h.CardVisible.Should().BeFalse();
    }

    [Fact]
    public void A_locked_card_survives_the_pointer_leaving_and_closes_after_unlock()
    {
        var (h, time, _) = Build();
        h.ToggleLock();
        h.Locked.Should().BeTrue();
        h.CardVisible.Should().BeTrue();

        h.PointerEnteredPill();
        h.PointerLeftPill();
        time.Advance(TimeSpan.FromSeconds(3));
        h.CardVisible.Should().BeTrue();

        h.ToggleLock();
        h.Locked.Should().BeFalse();
        time.Advance(HoverController.CloseDelay);
        h.CardVisible.Should().BeFalse();
    }

    [Fact]
    public void Unlocking_while_hovered_keeps_the_card_open()
    {
        var (h, time, _) = Build();
        h.PointerEnteredPill();
        h.ToggleLock();
        h.ToggleLock();
        time.Advance(TimeSpan.FromSeconds(1));
        h.CardVisible.Should().BeTrue();
    }

    [Fact]
    public void Peek_opens_for_five_seconds_then_closes()
    {
        var (h, time, _) = Build();
        h.Peek();
        h.CardVisible.Should().BeTrue();
        h.Unfolded.Should().BeTrue();

        time.Advance(HoverController.PeekDuration);
        h.CardVisible.Should().BeTrue();

        time.Advance(HoverController.CloseDelay);
        h.CardVisible.Should().BeFalse();
        time.Advance(HoverController.FoldDelay);
        h.Unfolded.Should().BeFalse();
    }

    [Fact]
    public void Peek_while_hovered_does_not_close_when_it_ends()
    {
        var (h, time, _) = Build();
        h.PointerEnteredPill();
        h.Peek();
        time.Advance(TimeSpan.FromSeconds(10));
        h.CardVisible.Should().BeTrue();
    }

    [Fact]
    public void A_second_peek_restarts_the_five_seconds()
    {
        var (h, time, _) = Build();
        h.Peek();
        time.Advance(TimeSpan.FromSeconds(4));
        h.Peek();
        time.Advance(TimeSpan.FromSeconds(4) + HoverController.CloseDelay);
        h.CardVisible.Should().BeTrue();
    }

    [Fact]
    public void Repeated_pointer_left_notifications_do_not_postpone_the_close()
    {
        var (h, time, _) = Build();
        h.PointerEnteredPill();
        h.PointerLeftPill();
        time.Advance(TimeSpan.FromMilliseconds(200));
        h.PointerLeftPill();
        h.PointerLeftCard();
        time.Advance(TimeSpan.FromMilliseconds(50));
        h.CardVisible.Should().BeFalse();
    }

    [Fact]
    public void Dispose_stops_pending_timers()
    {
        var (h, time, changes) = Build();
        h.PointerEnteredPill();
        h.PointerLeftPill();
        var before = changes();
        h.Dispose();
        time.Advance(TimeSpan.FromSeconds(1));
        changes().Should().Be(before);
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter HoverControllerTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Presentation/Behavior/HoverController.cs` :
```csharp
namespace UsageNotch.Presentation.Behavior;

/// <summary>
/// Décide si la carte est visible, si la pilule est dépliée et si la carte est verrouillée, à partir des entrées et
/// sorties du pointeur, du verrou et des aperçus. Thread-safe. <see cref="Changed"/> peut être levé sur un thread de minuterie.
/// </summary>
public sealed class HoverController(TimeProvider time) : IDisposable
{
    public static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan FoldDelay = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan PeekDuration = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private bool _onPill;
    private bool _onCard;
    private bool _peeking;
    private bool _disposed;
    private ITimer? _closeTimer;
    private ITimer? _foldTimer;
    private ITimer? _peekTimer;

    private bool _cardVisible;
    private bool _unfolded;
    private bool _locked;

    public event Action? Changed;

    public bool CardVisible { get { lock (_gate) return _cardVisible; } }
    public bool Unfolded { get { lock (_gate) return _unfolded; } }
    public bool Locked { get { lock (_gate) return _locked; } }

    public void PointerEnteredPill() => Mutate(() => { _onPill = true; OpenLocked(); });
    public void PointerLeftPill() => Mutate(() => { _onPill = false; ScheduleCloseLocked(); });
    public void PointerEnteredCard() => Mutate(() => { _onCard = true; OpenLocked(); });
    public void PointerLeftCard() => Mutate(() => { _onCard = false; ScheduleCloseLocked(); });

    public void ToggleLock() => Mutate(() =>
    {
        _locked = !_locked;
        if (_locked) OpenLocked();
        else ScheduleCloseLocked();
    });

    public void Peek() => Mutate(() =>
    {
        _peeking = true;
        OpenLocked();
        _peekTimer?.Dispose();
        _peekTimer = time.CreateTimer(_ => Mutate(() =>
        {
            _peeking = false;
            ScheduleCloseLocked();
        }), null, PeekDuration, Timeout.InfiniteTimeSpan);
    });

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            DisposeTimersLocked();
            _peekTimer?.Dispose();
            _peekTimer = null;
        }
    }

    private void OpenLocked()
    {
        DisposeTimersLocked();
        _cardVisible = true;
        _unfolded = true;
    }

    private void ScheduleCloseLocked()
    {
        if (_onPill || _onCard || _locked || _peeking) return;
        // Déjà programmé : une sortie répétée (filet de sécurité de l'App) ne doit pas repousser la fermeture.
        if (_closeTimer is not null || _foldTimer is not null) return;
        _closeTimer = time.CreateTimer(_ => Mutate(() =>
        {
            if (!IsIdleLocked()) return;
            _cardVisible = false;
        }), null, CloseDelay, Timeout.InfiniteTimeSpan);
        _foldTimer = time.CreateTimer(_ => Mutate(() =>
        {
            if (!IsIdleLocked()) return;
            _cardVisible = false;
            _unfolded = false;
        }), null, FoldDelay, Timeout.InfiniteTimeSpan);
    }

    private bool IsIdleLocked() => !(_onPill || _onCard || _locked || _peeking);

    private void DisposeTimersLocked()
    {
        _closeTimer?.Dispose();
        _foldTimer?.Dispose();
        _closeTimer = null;
        _foldTimer = null;
    }

    private void Mutate(Action change)
    {
        bool changed;
        lock (_gate)
        {
            if (_disposed) return;
            var before = (_cardVisible, _unfolded, _locked);
            change();
            changed = before != (_cardVisible, _unfolded, _locked);
        }
        if (changed) Changed?.Invoke();
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter HoverControllerTests`
Expected: 11 tests passés. Relancer le filtre 3 fois : toujours 11 (FakeTimeProvider exécute les minuteries de façon synchrone dans `Advance`).

- [ ] **Step 5 : Commit**

`feat(presentation): hover, lock and peek controller`

---

### Task 6 : Services et ViewModel du notch

**Files:**
- Create: `src/UsageNotch.Presentation/Services/IUiDispatcher.cs`, `Services/ISessionFocus.cs`, `Services/ISoundPlayer.cs`, `Services/IAccentColorSource.cs`, `ViewModels/NotchViewModel.cs`
- Test: `tests/UsageNotch.Presentation.Tests/ImmediateDispatcher.cs`, `tests/UsageNotch.Presentation.Tests/ViewModels/NotchViewModelTests.cs`

**Interfaces:**
- Consumes: `UsageStore`, `SessionStore`, `SettingsStore`, `IUsageProvider`, `Theme`, `HookEvent` (Core) ; `PillPresenter`, `CellModel` (Task 2) ; `CardPresenter`, `CardModel` (Task 3) ; `TransitionWatcher`, `TransitionKind` (Task 4) ; `HoverController` (Task 5).
- Produces:
  - `interface IUiDispatcher { void Post(Action action); }`
  - `interface ISessionFocus { bool Focus(int? parentPid); }`
  - `interface ISoundPlayer { void Play(string soundName); }`
  - `interface IAccentColorSource { string? AccentHex { get; } }`
  - `sealed class NotchViewModel : ObservableObject, IDisposable` — constructeur `(UsageStore usage, SessionStore sessions, SettingsStore settings, IUsageProvider provider, Action requestRefresh, HoverController hover, IUiDispatcher ui, ISessionFocus focus, ISoundPlayer sound, IAccentColorSource accent, TimeProvider time, TimeZoneInfo zone)` ; propriétés observables `CellModel Cell`, `CardModel Card`, `Theme Theme`, `Settings Settings`, `bool CardVisible`, `bool Unfolded`, `bool Locked`, `string TrayText` ; commandes `IRelayCommand RefreshCommand`, `IRelayCommand ToggleLockCommand`, `IRelayCommand PeekCommand`, `IRelayCommand<string> DismissSessionCommand`, `IRelayCommand<string> FocusSessionCommand` ; méthodes `PointerEnteredPill()`, `PointerLeftPill()`, `PointerEnteredCard()`, `PointerLeftCard()` qui délèguent au `HoverController` ; `static readonly TimeSpan ClockInterval` (30 s).

Règles :
- Le constructeur s'abonne aux trois magasins, au `HoverController` et à une minuterie périodique de 30 s, puis calcule l'état initial. Chaque notification est relayée par `IUiDispatcher.Post` avant de relire `Current` / `Snapshot()` (contrat du Plan 1).
- Sur `SessionStore.Changed` : `TransitionWatcher.Observe` ; si au moins une transition, `AutoOpenCard` → `hover.Peek()`, `SoundEnabled` → un seul son, `AttentionSound` si une transition est Attention, sinon `DoneSound`.
- Thème effectif : `Theme.ForPreset(settings.ThemePreset, settings.CustomTheme, accent.AccentHex)`.
- `TrayText` = `"UsageNotch — " + provider.DisplayName + " " + Cell.PercentText`.

- [ ] **Step 1 : Écrire le répartiteur de test et les tests**

`tests/UsageNotch.Presentation.Tests/ImmediateDispatcher.cs` :
```csharp
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Tests;

public sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}
```

`tests/UsageNotch.Presentation.Tests/ViewModels/NotchViewModelTests.cs` :
```csharp
using System.ComponentModel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Behavior;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Services;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.Presentation.Tests.ViewModels;

public sealed class NotchViewModelTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeProvider : IUsageProvider
    {
        public string Id => "claude";
        public string DisplayName => "Claude";
        public string HeadlineWindowId => "session";
        public Task<FetchResult> FetchAsync(CancellationToken ct) => Task.FromResult<FetchResult>(new FetchResult.Failed("unused"));
    }

    private sealed class FakeFocus : ISessionFocus
    {
        public List<int?> Calls { get; } = [];
        public bool Focus(int? parentPid) { Calls.Add(parentPid); return true; }
    }

    private sealed class FakeSound : ISoundPlayer
    {
        public List<string> Played { get; } = [];
        public void Play(string soundName) => Played.Add(soundName);
    }

    private sealed class FakeAccent : IAccentColorSource
    {
        public string? AccentHex { get; set; }
    }

    private readonly TempDir _dir = new();
    private readonly FakeTimeProvider _time = new(Start);
    private readonly UsageStore _usage;
    private readonly SessionStore _sessions;
    private readonly SettingsStore _settings;
    private readonly HoverController _hover;
    private readonly FakeFocus _focus = new();
    private readonly FakeSound _sound = new();
    private readonly FakeAccent _accent = new();
    private int _refreshes;
    private readonly NotchViewModel _vm;

    public NotchViewModelTests()
    {
        _usage = new UsageStore(_dir.File("usage.json"), _time, NullLogger<UsageStore>.Instance);
        _sessions = new SessionStore(_time);
        _settings = new SettingsStore(_dir.File("settings.json"), NullLogger<SettingsStore>.Instance, _time);
        _settings.Load();
        _hover = new HoverController(_time);
        _vm = new NotchViewModel(_usage, _sessions, _settings, new FakeProvider(), () => _refreshes++, _hover,
            new ImmediateDispatcher(), _focus, _sound, _accent, _time, TimeZoneInfo.Utc);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _hover.Dispose();
        _dir.Dispose();
    }

    private static HookEvent Ev(string kind, string id = "s-1", int ppid = 4242) =>
        new(kind, id, ppid, @"C:\src\proj", "", "", "", "", "");

    private static LimitWindow Session(double used, DateTimeOffset resets) => new("session", "Session en cours", used, resets);

    [Fact]
    public void The_initial_state_waits_for_the_first_reading()
    {
        _vm.Cell.PercentText.Should().Be("…");
        _vm.TrayText.Should().Be("UsageNotch — Claude …");
        _vm.Card.Note.Should().Be("En attente de la première lecture…");
        _vm.CardVisible.Should().BeFalse();
    }

    [Fact]
    public void A_usage_reading_updates_the_cell_card_and_tray_text()
    {
        var raised = new List<string?>();
        ((INotifyPropertyChanged)_vm).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _usage.Apply(new FetchResult.Success([Session(0.73, Start.AddMinutes(51))]));

        _vm.Cell.PercentText.Should().Be("73" + FrenchText.Nbsp + "%");
        _vm.Card.Windows.Should().ContainSingle();
        _vm.TrayText.Should().Be("UsageNotch — Claude 73" + FrenchText.Nbsp + "%");
        raised.Should().Contain(new[] { nameof(NotchViewModel.Cell), nameof(NotchViewModel.Card), nameof(NotchViewModel.TrayText) });
    }

    [Fact]
    public void A_session_finishing_peeks_the_card_and_plays_the_done_sound()
    {
        _sessions.Apply(Ev(HookEvent.Running));
        _vm.CardVisible.Should().BeFalse();

        _sessions.Apply(Ev(HookEvent.Done));

        _vm.CardVisible.Should().BeTrue();
        _vm.Unfolded.Should().BeTrue();
        _sound.Played.Should().Equal("Asterisk");
        _vm.Card.Sessions.Should().ContainSingle(r => r.SessionId == "s-1");
    }

    [Fact]
    public void A_session_waiting_plays_the_attention_sound()
    {
        _sessions.Apply(Ev(HookEvent.Running));
        _sessions.Apply(Ev(HookEvent.Attention));
        _sound.Played.Should().Equal("Exclamation");
    }

    [Fact]
    public void Auto_open_and_sound_can_be_switched_off()
    {
        _settings.Save(_settings.Current with { AutoOpenCard = false, SoundEnabled = false });
        _sessions.Apply(Ev(HookEvent.Running));
        _sessions.Apply(Ev(HookEvent.Done));
        _vm.CardVisible.Should().BeFalse();
        _sound.Played.Should().BeEmpty();
    }

    [Fact]
    public void The_peek_closes_after_five_seconds()
    {
        _sessions.Apply(Ev(HookEvent.Running));
        _sessions.Apply(Ev(HookEvent.Done));
        _time.Advance(HoverController.PeekDuration + HoverController.CloseDelay);
        _vm.CardVisible.Should().BeFalse();
    }

    [Fact]
    public void A_settings_change_applies_the_new_theme_and_cell_content()
    {
        _settings.Save(_settings.Current with { ThemePreset = ThemePreset.Monochrome, CellContent = CellContent.RingOnly });
        _vm.Theme.Should().Be(Theme.Monochrome);
        _vm.Cell.TrackColor.Should().Be(Theme.Monochrome.RingTrack);
        _vm.Cell.ShowPercent.Should().BeFalse();
        _vm.Settings.CellContent.Should().Be(CellContent.RingOnly);
    }

    [Fact]
    public void The_system_accent_preset_uses_the_accent_colour()
    {
        _accent.AccentHex = "#0078D4";
        _settings.Save(_settings.Current with { ThemePreset = ThemePreset.SystemAccent });
        _vm.Theme.LevelAmple.Should().Be("#0078D4");
    }

    [Fact]
    public void Commands_refresh_lock_and_peek()
    {
        _vm.RefreshCommand.Execute(null);
        _refreshes.Should().Be(1);

        _vm.ToggleLockCommand.Execute(null);
        _vm.Locked.Should().BeTrue();
        _vm.CardVisible.Should().BeTrue();
        _vm.ToggleLockCommand.Execute(null);
        _vm.Locked.Should().BeFalse();

        _time.Advance(TimeSpan.FromSeconds(1));
        _vm.PeekCommand.Execute(null);
        _vm.CardVisible.Should().BeTrue();
    }

    [Fact]
    public void Session_commands_dismiss_and_focus_with_the_parent_pid()
    {
        _sessions.Apply(Ev(HookEvent.Running, "s-9", ppid: 777));

        _vm.FocusSessionCommand.Execute("s-9");
        _focus.Calls.Should().Equal(777);

        _vm.DismissSessionCommand.Execute("s-9");
        _sessions.Snapshot().Should().BeEmpty();
        _vm.Card.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void Pointer_methods_drive_the_hover_state()
    {
        _vm.PointerEnteredPill();
        _vm.CardVisible.Should().BeTrue();
        _vm.PointerLeftPill();
        _vm.PointerEnteredCard();
        _time.Advance(TimeSpan.FromSeconds(1));
        _vm.CardVisible.Should().BeTrue();
        _vm.PointerLeftCard();
        _time.Advance(HoverController.FoldDelay);
        _vm.CardVisible.Should().BeFalse();
        _vm.Unfolded.Should().BeFalse();
    }

    [Fact]
    public void The_clock_refreshes_relative_reset_texts()
    {
        _usage.Apply(new FetchResult.Success([Session(0.2, Start.AddMinutes(61))]));
        _vm.Card.Windows[0].ResetText.Should().Be("Réinitialisation à 13:01");

        _time.Advance(TimeSpan.FromMinutes(2));

        _vm.Card.Windows[0].ResetText.Should().Be("Réinitialisation dans 59 min");
    }

    [Fact]
    public void After_dispose_the_view_model_ignores_the_stores()
    {
        _vm.Dispose();
        _usage.Apply(new FetchResult.Success([Session(0.5, Start.AddHours(1))]));
        _vm.Cell.PercentText.Should().Be("…");
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter NotchViewModelTests`
Expected: erreur de compilation, `UsageNotch.Presentation.Services` et `ViewModels` inconnus.

- [ ] **Step 3 : Implémenter les interfaces**

`src/UsageNotch.Presentation/Services/IUiDispatcher.cs` :
```csharp
namespace UsageNotch.Presentation.Services;

/// <summary>Exécute une action sur le thread de l'interface. L'App fournit une implémentation WPF.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}
```

`src/UsageNotch.Presentation/Services/ISessionFocus.cs` :
```csharp
namespace UsageNotch.Presentation.Services;

/// <summary>Ramène au premier plan la fenêtre qui héberge la session. Faux si rien n'a été trouvé.</summary>
public interface ISessionFocus
{
    bool Focus(int? parentPid);
}
```

`src/UsageNotch.Presentation/Services/ISoundPlayer.cs` :
```csharp
namespace UsageNotch.Presentation.Services;

/// <summary>Joue un son système par son nom : Asterisk, Beep, Exclamation, Hand, Question.</summary>
public interface ISoundPlayer
{
    void Play(string soundName);
}
```

`src/UsageNotch.Presentation/Services/IAccentColorSource.cs` :
```csharp
namespace UsageNotch.Presentation.Services;

/// <summary>Couleur d'accentuation de Windows en <c>#RRGGBB</c>, ou null si elle est illisible.</summary>
public interface IAccentColorSource
{
    string? AccentHex { get; }
}
```

- [ ] **Step 4 : Implémenter le ViewModel**

`src/UsageNotch.Presentation/ViewModels/NotchViewModel.cs` :
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Behavior;
using UsageNotch.Presentation.Card;
using UsageNotch.Presentation.Pill;
using UsageNotch.Presentation.Services;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Presentation.ViewModels;

/// <summary>
/// État observable de la pilule et de la carte. Toutes les notifications des magasins passent par
/// <see cref="IUiDispatcher"/> avant de relire l'état courant ; les propriétés ne changent que sur le thread UI.
/// </summary>
public sealed class NotchViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(30);

    private readonly UsageStore _usage;
    private readonly SessionStore _sessions;
    private readonly SettingsStore _settingsStore;
    private readonly IUsageProvider _provider;
    private readonly HoverController _hover;
    private readonly IUiDispatcher _ui;
    private readonly ISessionFocus _focus;
    private readonly ISoundPlayer _sound;
    private readonly IAccentColorSource _accent;
    private readonly TimeProvider _time;
    private readonly TimeZoneInfo _zone;
    private readonly TransitionWatcher _watcher = new();
    private readonly ITimer _clock;

    private readonly Action<UsageSnapshot> _onUsage;
    private readonly Action _onSessions;
    private readonly Action<CoreSettings> _onSettings;
    private readonly Action _onHover;
    private bool _disposed;

    private CellModel _cell = null!;
    private CardModel _card = null!;
    private Theme _theme = Theme.Codenotch;
    private CoreSettings _settings = new();
    private bool _cardVisible;
    private bool _unfolded;
    private bool _locked;
    private string _trayText = "";

    public NotchViewModel(
        UsageStore usage,
        SessionStore sessions,
        SettingsStore settings,
        IUsageProvider provider,
        Action requestRefresh,
        HoverController hover,
        IUiDispatcher ui,
        ISessionFocus focus,
        ISoundPlayer sound,
        IAccentColorSource accent,
        TimeProvider time,
        TimeZoneInfo zone)
    {
        _usage = usage;
        _sessions = sessions;
        _settingsStore = settings;
        _provider = provider;
        _hover = hover;
        _ui = ui;
        _focus = focus;
        _sound = sound;
        _accent = accent;
        _time = time;
        _zone = zone;

        RefreshCommand = new RelayCommand(requestRefresh);
        ToggleLockCommand = new RelayCommand(_hover.ToggleLock);
        PeekCommand = new RelayCommand(_hover.Peek);
        DismissSessionCommand = new RelayCommand<string>(id => { if (id is not null) _sessions.Dismiss(id); });
        FocusSessionCommand = new RelayCommand<string>(id => { if (id is not null) _focus.Focus(_sessions.ParentPidOf(id)); });

        _onUsage = _ => Post(Recompute);
        _onSessions = () => Post(OnSessionsChanged);
        _onSettings = _ => Post(Recompute);
        _onHover = () => Post(SyncHover);

        _usage.Changed += _onUsage;
        _sessions.Changed += _onSessions;
        _settingsStore.Changed += _onSettings;
        _hover.Changed += _onHover;

        _watcher.Observe(_sessions.Snapshot());
        Recompute();
        SyncHover();

        _clock = _time.CreateTimer(_ => Post(Recompute), null, ClockInterval, ClockInterval);
    }

    public CellModel Cell { get => _cell; private set => SetProperty(ref _cell, value); }
    public CardModel Card { get => _card; private set => SetProperty(ref _card, value); }
    public Theme Theme { get => _theme; private set => SetProperty(ref _theme, value); }
    public CoreSettings Settings { get => _settings; private set => SetProperty(ref _settings, value); }
    public bool CardVisible { get => _cardVisible; private set => SetProperty(ref _cardVisible, value); }
    public bool Unfolded { get => _unfolded; private set => SetProperty(ref _unfolded, value); }
    public bool Locked { get => _locked; private set => SetProperty(ref _locked, value); }
    public string TrayText { get => _trayText; private set => SetProperty(ref _trayText, value); }

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand ToggleLockCommand { get; }
    public IRelayCommand PeekCommand { get; }
    public IRelayCommand<string> DismissSessionCommand { get; }
    public IRelayCommand<string> FocusSessionCommand { get; }

    public void PointerEnteredPill() => _hover.PointerEnteredPill();
    public void PointerLeftPill() => _hover.PointerLeftPill();
    public void PointerEnteredCard() => _hover.PointerEnteredCard();
    public void PointerLeftCard() => _hover.PointerLeftCard();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _clock.Dispose();
        _usage.Changed -= _onUsage;
        _sessions.Changed -= _onSessions;
        _settingsStore.Changed -= _onSettings;
        _hover.Changed -= _onHover;
    }

    private void Post(Action action) => _ui.Post(() => { if (!_disposed) action(); });

    private void OnSessionsChanged()
    {
        var transitions = _watcher.Observe(_sessions.Snapshot());
        if (transitions.Count > 0)
        {
            var s = _settingsStore.Current;
            if (s.AutoOpenCard) _hover.Peek();
            if (s.SoundEnabled)
            {
                _sound.Play(transitions.Any(t => t.Kind == TransitionKind.Attention) ? s.AttentionSound : s.DoneSound);
            }
        }
        Recompute();
    }

    private void Recompute()
    {
        var now = _time.GetUtcNow();
        var settings = _settingsStore.Current;
        var theme = Theme.ForPreset(settings.ThemePreset, settings.CustomTheme, _accent.AccentHex);
        var snapshot = _usage.Current;

        Settings = settings;
        Theme = theme;
        Cell = PillPresenter.Cell(snapshot, _provider.HeadlineWindowId, _sessions.Aggregate, theme, settings.CellContent, now);
        Card = CardPresenter.Build(snapshot, _provider.DisplayName, _sessions.Snapshot(), theme, now, _zone);
        TrayText = $"UsageNotch — {_provider.DisplayName} {Cell.PercentText}";
    }

    private void SyncHover()
    {
        CardVisible = _hover.CardVisible;
        Unfolded = _hover.Unfolded;
        Locked = _hover.Locked;
    }
}
```

- [ ] **Step 5 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter NotchViewModelTests`
Expected: 13 tests passés. Puis `dotnet test` à la racine, aucun avertissement.

- [ ] **Step 6 : Commit**

`feat(presentation): notch view model and app service interfaces`

---

### Task 7 : Arguments de ligne de commande et rapport de diagnostic

**Files:**
- Create: `src/UsageNotch.Presentation/AppArguments.cs`, `src/UsageNotch.Presentation/Diagnostics/DoctorReport.cs`
- Test: `tests/UsageNotch.Presentation.Tests/AppArgumentsTests.cs`, `tests/UsageNotch.Presentation.Tests/Diagnostics/DoctorReportTests.cs`

**Interfaces:**
- Consumes: `ClaudeCredential`, `MonitorInfo`, `PixelRect`, `UsageSnapshot`, `SnapshotStatus`, `Settings`, `ScreenEdge`, `VisibilityMode` (Core) ; `FrenchText` (Task 1).
- Produces:
  - `sealed record AppArguments(bool Doctor, bool Demo, bool FromHook)` avec `static AppArguments Parse(IReadOnlyList<string> args)`.
  - `sealed record DoctorInputs(string CredentialsDirectory, ClaudeCredential? Credential, string ClaudeSettingsPath, bool HooksInstalled, string HookExePath, bool HookExeExists, int Port, bool PortFree, IReadOnlyList<MonitorInfo> Monitors, string UsagePath, UsageSnapshot Usage, string SettingsPath, Settings Settings, DateTimeOffset Now, TimeZoneInfo Zone)`.
  - `static class DoctorReport` avec `string Build(DoctorInputs inputs)` ; les lignes sont séparées par `\n`.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/AppArgumentsTests.cs` :
```csharp
using FluentAssertions;

namespace UsageNotch.Presentation.Tests;

public class AppArgumentsTests
{
    [Fact]
    public void No_arguments_is_a_normal_start() =>
        AppArguments.Parse([]).Should().Be(new AppArguments(false, false, false));

    [Theory]
    [InlineData("doctor")]
    [InlineData("--doctor")]
    [InlineData("DOCTOR")]
    public void Doctor_is_recognised(string arg) => AppArguments.Parse([arg]).Doctor.Should().BeTrue();

    [Fact]
    public void Demo_and_from_hook_are_flags_and_unknown_arguments_are_ignored() =>
        AppArguments.Parse(["--demo", "--whatever", "--FROM-HOOK"]).Should().Be(new AppArguments(false, true, true));
}
```

`tests/UsageNotch.Presentation.Tests/Diagnostics/DoctorReportTests.cs` :
```csharp
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
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter "AppArgumentsTests|DoctorReportTests"`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Presentation/AppArguments.cs` :
```csharp
namespace UsageNotch.Presentation;

/// <summary><c>doctor</c> affiche le diagnostic et quitte ; <c>--demo</c> utilise des données fixes ; <c>--from-hook</c> signale un lancement par le hook.</summary>
public sealed record AppArguments(bool Doctor, bool Demo, bool FromHook)
{
    public static AppArguments Parse(IReadOnlyList<string> args)
    {
        bool Has(params string[] names) => args.Any(a => names.Any(n => string.Equals(a, n, StringComparison.OrdinalIgnoreCase)));
        return new AppArguments(Has("doctor", "--doctor"), Has("--demo"), Has("--from-hook"));
    }
}
```

`src/UsageNotch.Presentation/Diagnostics/DoctorReport.cs` :
```csharp
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
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter "AppArgumentsTests|DoctorReportTests"`
Expected: 10 tests passés (5 pour AppArguments dont 3 cas de Theory, 5 pour DoctorReport).

- [ ] **Step 5 : Commit**

`feat(presentation): command-line arguments and doctor report`

---

### Task 8 : Journal fichier quotidien

**Files:**
- Create: `src/UsageNotch.Core/Logging/FileLoggerProvider.cs`
- Test: `tests/UsageNotch.Core.Tests/Logging/FileLoggerProviderTests.cs`

**Interfaces:**
- Produces: `sealed class FileLoggerProvider(string directory, TimeProvider time, Func<LogLevel> minimumLevel) : ILoggerProvider` avec `const int RetentionDays = 7`, `static string FileNameFor(DateTimeOffset utc)` (`usagenotch-yyyyMMdd.log`). Ligne : `yyyy-MM-ddTHH:mm:ss.fffZ [INF] Catégorie: message`, suivie du `ToString()` de l'exception s'il y en a une. Les fichiers `usagenotch-*.log` de plus de 7 jours sont supprimés à la construction. Les erreurs d'écriture sont avalées : un journal ne doit jamais faire tomber l'application.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Core.Tests/Logging/FileLoggerProviderTests.cs` :
```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Logging;

namespace UsageNotch.Core.Tests.Logging;

public class FileLoggerProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 34, 56, 789, TimeSpan.Zero);

    [Fact]
    public void Writes_a_formatted_line_into_the_daily_file()
    {
        using var dir = new TempDir();
        using var provider = new FileLoggerProvider(dir.Path, new FakeTimeProvider(Now), () => LogLevel.Information);

        provider.CreateLogger("UsageNotch.Test").LogWarning("Port {Port} indisponible", 48666);

        var file = Path.Combine(dir.Path, "usagenotch-20260914.log");
        File.ReadAllLines(file).Should().Equal("2026-09-14T12:34:56.789Z [WRN] UsageNotch.Test: Port 48666 indisponible");
    }

    [Fact]
    public void The_minimum_level_is_read_on_every_call()
    {
        using var dir = new TempDir();
        var level = LogLevel.Information;
        using var provider = new FileLoggerProvider(dir.Path, new FakeTimeProvider(Now), () => level);
        var logger = provider.CreateLogger("C");

        logger.LogDebug("caché");
        level = LogLevel.Debug;
        logger.LogDebug("visible");

        File.ReadAllText(Path.Combine(dir.Path, FileLoggerProvider.FileNameFor(Now))).Should().NotContain("caché").And.Contain("[DBG] C: visible");
    }

    [Fact]
    public void An_exception_is_appended_after_the_message()
    {
        using var dir = new TempDir();
        using var provider = new FileLoggerProvider(dir.Path, new FakeTimeProvider(Now), () => LogLevel.Trace);

        provider.CreateLogger("C").LogError(new InvalidOperationException("boum"), "Échec");

        var text = File.ReadAllText(Path.Combine(dir.Path, FileLoggerProvider.FileNameFor(Now)));
        text.Should().Contain("[ERR] C: Échec").And.Contain("System.InvalidOperationException: boum");
    }

    [Fact]
    public void Old_log_files_are_purged_and_other_files_kept()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "usagenotch-20260906.log"), "8 jours");
        File.WriteAllText(Path.Combine(dir.Path, "usagenotch-20260908.log"), "6 jours");
        File.WriteAllText(Path.Combine(dir.Path, "usagenotch-garbage.log"), "nom illisible");
        File.WriteAllText(Path.Combine(dir.Path, "settings.json"), "{}");

        using var provider = new FileLoggerProvider(dir.Path, new FakeTimeProvider(Now), () => LogLevel.Information);

        Directory.GetFiles(dir.Path).Select(Path.GetFileName).Should().BeEquivalentTo(
            "usagenotch-20260908.log", "usagenotch-garbage.log", "settings.json");
    }

    [Fact]
    public void Concurrent_writes_keep_every_line_whole()
    {
        using var dir = new TempDir();
        using var provider = new FileLoggerProvider(dir.Path, new FakeTimeProvider(Now), () => LogLevel.Information);
        var logger = provider.CreateLogger("C");

        Parallel.For(0, 50, i => logger.LogInformation("ligne {Index}", i));

        var lines = File.ReadAllLines(Path.Combine(dir.Path, FileLoggerProvider.FileNameFor(Now)));
        lines.Should().HaveCount(50).And.OnlyContain(l => l.StartsWith("2026-09-14T12:34:56.789Z [INF] C: ligne "));
    }

    [Fact]
    public void None_is_never_enabled()
    {
        using var dir = new TempDir();
        using var provider = new FileLoggerProvider(dir.Path, new FakeTimeProvider(Now), () => LogLevel.Trace);
        provider.CreateLogger("C").IsEnabled(LogLevel.None).Should().BeFalse();
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Core.Tests --filter FileLoggerProviderTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Core/Logging/FileLoggerProvider.cs` :
```csharp
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Logging;

/// <summary>
/// Un fichier par jour UTC dans <paramref name="directory"/>, conservé 7 jours. Le niveau minimum est relu à chaque appel,
/// ce qui permet de basculer le mode Debug depuis les réglages sans redémarrer. N'écrit jamais d'exception vers l'appelant.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public const int RetentionDays = 7;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _directory;
    private readonly TimeProvider _time;
    private readonly Func<LogLevel> _minimumLevel;
    private readonly object _gate = new();

    public FileLoggerProvider(string directory, TimeProvider time, Func<LogLevel> minimumLevel)
    {
        _directory = directory;
        _time = time;
        _minimumLevel = minimumLevel;
        try
        {
            Directory.CreateDirectory(directory);
            Purge();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Journal indisponible : l'application continue sans.
        }
    }

    public static string FileNameFor(DateTimeOffset utc) =>
        "usagenotch-" + utc.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log";

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    internal bool IsEnabled(LogLevel level) => level != LogLevel.None && level >= _minimumLevel();

    internal void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var now = _time.GetUtcNow();
        var line = new StringBuilder()
            .Append(now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
            .Append(" [").Append(Abbreviation(level)).Append("] ")
            .Append(category).Append(": ").Append(message);
        if (exception is not null) line.Append(Environment.NewLine).Append(exception);
        line.Append(Environment.NewLine);

        try
        {
            lock (_gate)
            {
                File.AppendAllText(Path.Combine(_directory, FileNameFor(now)), line.ToString(), Utf8NoBom);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Voir la remarque du constructeur.
        }
    }

    private void Purge()
    {
        var cutoff = _time.GetUtcNow().UtcDateTime.Date.AddDays(-RetentionDays);
        foreach (var path in Directory.EnumerateFiles(_directory, "usagenotch-*.log"))
        {
            var stamp = Path.GetFileNameWithoutExtension(path)["usagenotch-".Length..];
            if (!DateTime.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) continue;
            if (day >= cutoff) continue;
            try { File.Delete(path); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    private static string Abbreviation(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "---",
    };

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            provider.Write(logLevel, category, formatter(state, exception), exception);
        }
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Core.Tests --filter FileLoggerProviderTests`
Expected: 6 tests passés. Puis `dotnet test` à la racine, aucun avertissement.

- [ ] **Step 5 : Commit**

`feat(core): daily file logger with seven-day retention`

---

### Task 9 : Projet WPF, interop Win32 et commande `doctor`

**Files:**
- Create: `src/UsageNotch.App/UsageNotch.App.csproj`, `src/UsageNotch.App/app.manifest`, `src/UsageNotch.App/App.xaml`, `src/UsageNotch.App/App.xaml.cs`
- Create: `src/UsageNotch.App/Hosting/AppPaths.cs`, `src/UsageNotch.App/Hosting/DoctorCommand.cs`
- Create: `src/UsageNotch.App/Interop/NativeMethods.cs`, `src/UsageNotch.App/Interop/MonitorService.cs`, `src/UsageNotch.App/Interop/WindowStyles.cs`
- Modify: `UsageNotch.sln` (ajout du projet)

**Interfaces:**
- Consumes: `AppArguments`, `DoctorReport`, `DoctorInputs` (Task 7) ; `SettingsStore`, `UsageStore`, `ClaudeCredentialReader`, `HookInstaller`, `MonitorInfo`, `PixelRect` (Core).
- Produces:
  - `sealed record AppPaths(string DataDirectory, string SettingsFile, string UsageFile, string LogsDirectory, string HookExe, string ClaudeSettingsFile)` avec `static AppPaths For(bool demo)`. Démo : tout dans `%TEMP%\UsageNotch-demo\`, y compris un faux `claude-settings.json` pour que l'installeur de hooks ne touche jamais la vraie configuration.
  - `static class NativeMethods` (internal) : toutes les déclarations Win32 du plan (liste complète ci-dessous ; les tâches suivantes n'en ajoutent pas).
  - `sealed class MonitorService` avec `IReadOnlyList<MonitorInfo> GetMonitors()`.
  - `static class WindowStyles` avec `void MakeToolWindowNoActivate(nint hwnd)`, `void MoveResize(nint hwnd, PixelRect rect)`, `PixelRect? GetRect(nint hwnd)`, `(int X, int Y) CursorPosition()`, `bool IsAltDown()`.
  - `static class DoctorCommand` avec `string Run(AppPaths paths, SettingsStore settings)` : écrit le rapport dans `logs\doctor.txt`, l'affiche sur la console parente et le renvoie.

- [ ] **Step 1 : Créer le projet**

```powershell
dotnet new wpf -n UsageNotch.App -o src/UsageNotch.App -f net10.0
dotnet sln add src/UsageNotch.App
Remove-Item src/UsageNotch.App/MainWindow.xaml, src/UsageNotch.App/MainWindow.xaml.cs, src/UsageNotch.App/AssemblyInfo.cs -ErrorAction SilentlyContinue
```

`src/UsageNotch.App/UsageNotch.App.csproj` (remplacer tout le contenu) :
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <AssemblyName>UsageNotch.App</AssemblyName>
    <RootNamespace>UsageNotch.App</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="H.NotifyIcon.Wpf" Version="2.4.1" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.12" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\UsageNotch.Core\UsageNotch.Core.csproj" />
    <ProjectReference Include="..\UsageNotch.Presentation\UsageNotch.Presentation.csproj" />
    <!-- Garantit que le hook est construit avant l'app ; ses fichiers sont copiés à côté de l'exe ci-dessous. -->
    <ProjectReference Include="..\UsageNotch.Hook\UsageNotch.Hook.csproj" ReferenceOutputAssembly="false" Private="false" />
  </ItemGroup>
  <Target Name="CopyHookNextToApp" AfterTargets="Build">
    <ItemGroup>
      <_HookFiles Include="..\UsageNotch.Hook\bin\$(Configuration)\net10.0\UsageNotch.Hook.*" />
    </ItemGroup>
    <Copy SourceFiles="@(_HookFiles)" DestinationFolder="$(OutDir)" SkipUnchangedFiles="true" />
  </Target>
</Project>
```

`src/UsageNotch.App/app.manifest` :
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="UsageNotch.App"/>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
</assembly>
```

`src/UsageNotch.App/App.xaml` :
```xml
<Application x:Class="UsageNotch.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
  <Application.Resources />
</Application>
```

`src/UsageNotch.App/App.xaml.cs` (version provisoire, remplacée en Task 10) :
```csharp
using System.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using UsageNotch.App.Hosting;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation;

namespace UsageNotch.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = AppArguments.Parse(e.Args);
        var paths = AppPaths.For(args.Demo);
        var settings = new SettingsStore(paths.SettingsFile, NullLogger<SettingsStore>.Instance);
        settings.Load();
        if (args.Doctor) DoctorCommand.Run(paths, settings);
        Shutdown(0);
    }
}
```

- [ ] **Step 2 : Chemins de données**

`src/UsageNotch.App/Hosting/AppPaths.cs` :
```csharp
using System.IO;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Settings;

namespace UsageNotch.App.Hosting;

public sealed record AppPaths(
    string DataDirectory,
    string SettingsFile,
    string UsageFile,
    string LogsDirectory,
    string HookExe,
    string ClaudeSettingsFile)
{
    /// <summary>En démo, tout vit dans %TEMP%\UsageNotch-demo : ni les réglages, ni la lecture, ni la config Claude Code réelles ne sont touchés.</summary>
    public static AppPaths For(bool demo)
    {
        var data = demo
            ? Path.Combine(Path.GetTempPath(), "UsageNotch-demo")
            : SettingsStore.DefaultDirectory;
        return new AppPaths(
            DataDirectory: data,
            SettingsFile: Path.Combine(data, "settings.json"),
            UsageFile: Path.Combine(data, "usage.json"),
            LogsDirectory: Path.Combine(data, "logs"),
            HookExe: Path.Combine(AppContext.BaseDirectory, "UsageNotch.Hook.exe"),
            ClaudeSettingsFile: demo ? Path.Combine(data, "claude-settings.json") : HookInstaller.DefaultSettingsPath);
    }
}
```

- [ ] **Step 3 : Déclarations Win32**

`src/UsageNotch.App/Interop/NativeMethods.cs` :
```csharp
using System.Runtime.InteropServices;

namespace UsageNotch.App.Interop;

/// <summary>Toutes les déclarations Win32 de l'application, en un seul endroit.</summary>
internal static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TOOLWINDOW = 0x00000080;
    public const long WS_EX_NOACTIVATE = 0x08000000;

    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int WM_DISPLAYCHANGE = 0x007E;
    public const int WM_SETTINGCHANGE = 0x001A;
    public const int WM_DPICHANGED = 0x02E0;
    public const int MA_NOACTIVATE = 3;

    public static readonly nint HWND_TOPMOST = -1;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public const uint MONITORINFOF_PRIMARY = 0x00000001;
    public const int MDT_EFFECTIVE_DPI = 0;

    public const uint TH32CS_SNAPPROCESS = 0x00000002;
    public static readonly nint INVALID_HANDLE_VALUE = -1;

    public const int SW_RESTORE = 9;
    public const uint FLASHW_ALL = 0x00000003;
    public const uint FLASHW_TIMERNOFG = 0x0000000C;

    public const int VK_MENU = 0x12;
    public const int ATTACH_PARENT_PROCESS = -1;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public nint th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FLASHWINFO
    {
        public uint cbSize;
        public nint hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public delegate bool MonitorEnumProc(nint hMonitor, nint hdc, ref RECT rect, nint data);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(nint hWnd, out RECT rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Process32FirstW(nint hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Process32NextW(nint hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(nint hObject);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLengthW(nint hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachConsole(int dwProcessId);
}
```

- [ ] **Step 4 : Écrans et styles de fenêtre**

`src/UsageNotch.App/Interop/MonitorService.cs` :
```csharp
using System.Runtime.InteropServices;
using UsageNotch.Core.Placement;

namespace UsageNotch.App.Interop;

/// <summary>Énumère les écrans en pixels physiques, avec leur facteur d'échelle effectif (1,5 = 150 %).</summary>
public sealed class MonitorService
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        NativeMethods.MonitorEnumProc callback = (nint hMonitor, nint hdc, ref NativeMethods.RECT rect, nint data) =>
        {
            var info = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>(), szDevice = "" };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info)) return true;

            var scale = NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0
                ? dpiX / 96.0
                : 1.0;

            list.Add(new MonitorInfo(
                info.szDevice,
                (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
                ToPixelRect(info.rcMonitor),
                ToPixelRect(info.rcWork),
                scale));
            return true;
        };

        NativeMethods.EnumDisplayMonitors(0, 0, callback, 0);
        GC.KeepAlive(callback);
        return list;
    }

    private static PixelRect ToPixelRect(NativeMethods.RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
}
```

`src/UsageNotch.App/Interop/WindowStyles.cs` :
```csharp
using UsageNotch.Core.Placement;

namespace UsageNotch.App.Interop;

public static class WindowStyles
{
    /// <summary>La fenêtre ne prend jamais le focus et n'apparaît ni dans la barre des tâches ni dans Alt+Tab.</summary>
    public static void MakeToolWindowNoActivate(nint hwnd)
    {
        var style = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, (nint)style);
    }

    /// <summary>Place et dimensionne en pixels physiques, au-dessus des autres fenêtres, sans activer.</summary>
    public static void MoveResize(nint hwnd, PixelRect rect) =>
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, rect.X, rect.Y, rect.Width, rect.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

    public static PixelRect? GetRect(nint hwnd) =>
        NativeMethods.GetWindowRect(hwnd, out var r)
            ? new PixelRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top)
            : null;

    public static (int X, int Y) CursorPosition() =>
        NativeMethods.GetCursorPos(out var p) ? (p.X, p.Y) : (int.MinValue, int.MinValue);

    public static bool IsAltDown() => NativeMethods.GetKeyState(NativeMethods.VK_MENU) < 0;
}
```

- [ ] **Step 5 : Commande `doctor`**

`src/UsageNotch.App/Hosting/DoctorCommand.cs` :
```csharp
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using UsageNotch.App.Interop;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Diagnostics;

namespace UsageNotch.App.Hosting;

public static class DoctorCommand
{
    public static string Run(AppPaths paths, SettingsStore settings)
    {
        var time = TimeProvider.System;
        var credentials = new ClaudeCredentialReader(ClaudeCredentialReader.DefaultDirectory, time);
        var installer = new HookInstaller(paths.ClaudeSettingsFile, paths.HookExe, time);
        var usage = new UsageStore(paths.UsageFile, time, NullLogger<UsageStore>.Instance);
        usage.Load();

        var port = settings.Current.Port;
        bool portFree;
        try
        {
            var probe = new TcpListener(IPAddress.Loopback, port);
            probe.Start();
            probe.Stop();
            portFree = true;
        }
        catch (SocketException)
        {
            portFree = false;
        }

        var text = DoctorReport.Build(new DoctorInputs(
            CredentialsDirectory: credentials.Directory,
            Credential: credentials.Read(),
            ClaudeSettingsPath: installer.SettingsPath,
            HooksInstalled: installer.IsInstalled(),
            HookExePath: paths.HookExe,
            HookExeExists: File.Exists(paths.HookExe),
            Port: port,
            PortFree: portFree,
            Monitors: new MonitorService().GetMonitors(),
            UsagePath: paths.UsageFile,
            Usage: usage.Current,
            SettingsPath: paths.SettingsFile,
            Settings: settings.Current,
            Now: time.GetUtcNow(),
            Zone: TimeZoneInfo.Local));

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        try
        {
            Directory.CreateDirectory(paths.LogsDirectory);
            File.WriteAllText(Path.Combine(paths.LogsDirectory, "doctor.txt"), text, utf8);
        }
        catch (IOException)
        {
            // Le rapport s'affiche quand même sur la console.
        }

        NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
        stdout.Write(text);
        return text;
    }
}
```

- [ ] **Step 6 : Construire et vérifier**

Run:
```powershell
dotnet build UsageNotch.sln
Test-Path src\UsageNotch.App\bin\Debug\net10.0-windows\UsageNotch.Hook.exe
& src\UsageNotch.App\bin\Debug\net10.0-windows\UsageNotch.App.exe doctor --demo | Out-String
Get-Content "$env:TEMP\UsageNotch-demo\logs\doctor.txt"
```
Expected : compilation sans avertissement ; `True` ; le rapport s'affiche et le fichier contient la ligne « Écrans (N) : » avec au moins un écran marqué « (principal) », la ligne « Exécutable hook : présent », et « Hooks Claude Code : non installés — …\UsageNotch-demo\claude-settings.json ». Si la sortie console est vide (sortie redirigée), le fichier `doctor.txt` fait foi.

- [ ] **Step 7 : Commit**

`feat(app): WPF project, Win32 interop and doctor command`

---

### Task 10 : Hébergement, instance unique, démo et services système

**Files:**
- Create: `src/UsageNotch.App/Hosting/AppHost.cs`, `Hosting/SingleInstance.cs`, `Hosting/WpfDispatcher.cs`, `Hosting/DemoMode.cs`, `Hosting/NotchShell.cs`
- Create: `src/UsageNotch.App/Interop/SystemAccentColor.cs`, `Interop/SystemSoundPlayer.cs`, `Interop/TerminalFocus.cs` (provisoire, complété en Task 15)
- Modify: `src/UsageNotch.App/App.xaml.cs` (version complète)

**Interfaces:**
- Consumes: tout le Core ; `NotchViewModel`, `HoverController`, services (Tasks 5-6) ; `AppPaths`, `DoctorCommand`, `MonitorService` (Task 9).
- Produces:
  - `static class AppHost` avec `IHost Build(AppArguments args, AppPaths paths, SettingsStore settings)`.
  - `sealed class SingleInstance(string name) : IDisposable` avec `bool IsFirst` et `static Task SignalExistingAsync(int port)`.
  - `sealed class WpfDispatcher(Dispatcher dispatcher) : IUiDispatcher`.
  - `sealed class DemoUsageProvider(TimeProvider time) : IUsageProvider` et `static class DemoMode` avec `IDisposable Start(SessionStore sessions, TimeProvider time)` (sessions de démonstration + une session qui alterne Running/Done toutes les 20 s, pour voir l'aperçu et entendre le son).
  - `sealed class NotchShell : IDisposable` avec `void Start()` : crée et relie tout ce qui s'affiche. Cette tâche n'y met que le lien « seconde instance → aperçu de la carte » ; chaque tâche suivante remplace ce fichier par sa version complète.
  - `sealed class SystemAccentColor : IAccentColorSource`, `sealed class SystemSoundPlayer : ISoundPlayer`, `sealed class TerminalFocus : ISessionFocus`.
  - `App` : `Task QuitAsync(bool userInitiated)` public, appelée par les menus.

- [ ] **Step 1 : Services système**

`src/UsageNotch.App/Interop/SystemAccentColor.cs` :
```csharp
using Microsoft.Win32;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Interop;

/// <summary>Lit HKCU\Software\Microsoft\Windows\DWM\AccentColor (DWORD au format ABGR).</summary>
public sealed class SystemAccentColor : IAccentColorSource
{
    public string? AccentHex
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
                if (key?.GetValue("AccentColor") is not int abgr) return null;
                var v = unchecked((uint)abgr);
                return $"#{v & 0xFF:X2}{(v >> 8) & 0xFF:X2}{(v >> 16) & 0xFF:X2}";
            }
            catch (System.Security.SecurityException)
            {
                return null;
            }
        }
    }
}
```

`src/UsageNotch.App/Interop/SystemSoundPlayer.cs` :
```csharp
using System.Media;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Interop;

public sealed class SystemSoundPlayer : ISoundPlayer
{
    public void Play(string soundName)
    {
        var sound = soundName switch
        {
            "Beep" => SystemSounds.Beep,
            "Exclamation" => SystemSounds.Exclamation,
            "Hand" => SystemSounds.Hand,
            "Question" => SystemSounds.Question,
            _ => SystemSounds.Asterisk,
        };
        sound.Play();
    }
}
```

`src/UsageNotch.App/Interop/TerminalFocus.cs` (provisoire) :
```csharp
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Interop;

/// <summary>Provisoire : le retour au terminal est implémenté en Task 15.</summary>
public sealed class TerminalFocus : ISessionFocus
{
    public bool Focus(int? parentPid) => false;
}
```

- [ ] **Step 2 : Répartiteur, instance unique, démo**

`src/UsageNotch.App/Hosting/WpfDispatcher.cs` :
```csharp
using System.Windows.Threading;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

public sealed class WpfDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public void Post(Action action)
    {
        if (dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(action);
    }
}
```

`src/UsageNotch.App/Hosting/SingleInstance.cs` :
```csharp
using System.Net.Http;

namespace UsageNotch.App.Hosting;

/// <summary>Mutex nommé de session. Libéré sur le thread qui l'a acquis (le thread UI).</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;

    public SingleInstance(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        IsFirst = createdNew;
    }

    public bool IsFirst { get; }

    /// <summary>Demande à l'instance existante de se montrer. Échec silencieux : l'autre instance peut être en train de démarrer.</summary>
    public static async Task SignalExistingAsync(int port)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await http.PostAsync($"http://127.0.0.1:{port}/open-settings", content: null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
        }
    }

    public void Dispose()
    {
        if (IsFirst)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { }
        }
        _mutex.Dispose();
    }
}
```

`src/UsageNotch.App/Hosting/DemoMode.cs` :
```csharp
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Usage;

namespace UsageNotch.App.Hosting;

/// <summary>Lecture fixe : session 73 %, hebdomadaire 21 %, Opus 52 %.</summary>
public sealed class DemoUsageProvider(TimeProvider time) : IUsageProvider
{
    public string Id => "claude";
    public string DisplayName => "Claude";
    public string HeadlineWindowId => "session";

    public Task<FetchResult> FetchAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        IReadOnlyList<LimitWindow> windows =
        [
            new("session", "Session en cours", 0.73, now.AddMinutes(51)),
            new("weekly_all", "Hebdomadaire (tous modèles)", 0.21, now.AddDays(3)),
            new("weekly_opus", "Hebdomadaire (Opus)", 0.52, now.AddDays(3)),
        ];
        return Task.FromResult<FetchResult>(new FetchResult.Success(windows));
    }
}

public static class DemoMode
{
    public static readonly TimeSpan CycleInterval = TimeSpan.FromSeconds(20);

    /// <summary>Crée trois sessions (en cours, en attente, terminée) puis fait alterner la première.</summary>
    public static IDisposable Start(SessionStore sessions, TimeProvider time)
    {
        static HookEvent Ev(string kind, string id, string cwd, string message = "", string tool = "", string cmd = "") =>
            new(kind, id, 0, cwd, "", message, tool, cmd, "");

        sessions.Apply(Ev(HookEvent.SessionStart, "demo-api-0001", @"C:\src\api"));
        sessions.Apply(Ev(HookEvent.Running, "demo-api-0001", @"C:\src\api", tool: "Bash", cmd: "dotnet test"));
        sessions.Apply(Ev(HookEvent.SessionStart, "demo-web-0002", @"C:\src\web"));
        sessions.Apply(Ev(HookEvent.Running, "demo-web-0002", @"C:\src\web"));
        sessions.Apply(Ev(HookEvent.Attention, "demo-web-0002", @"C:\src\web", message: "Autoriser Bash : npm install ?"));
        sessions.Apply(Ev(HookEvent.SessionStart, "demo-doc-0003", @"C:\src\docs"));
        sessions.Apply(Ev(HookEvent.Running, "demo-doc-0003", @"C:\src\docs"));
        sessions.Apply(Ev(HookEvent.Done, "demo-doc-0003", @"C:\src\docs"));

        var running = true;
        return time.CreateTimer(_ =>
        {
            running = !running;
            sessions.Apply(Ev(running ? HookEvent.Running : HookEvent.Done, "demo-api-0001", @"C:\src\api", tool: "Bash", cmd: "dotnet test"));
        }, null, CycleInterval, CycleInterval);
    }
}
```

- [ ] **Step 3 : Conteneur de services**

`src/UsageNotch.App/Hosting/AppHost.cs` :
```csharp
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UsageNotch.App.Interop;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Logging;
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation;
using UsageNotch.Presentation.Behavior;
using UsageNotch.Presentation.Services;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Hosting;

public static class AppHost
{
    public static IHost Build(AppArguments args, AppPaths paths, SettingsStore settings)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            ApplicationName = "UsageNotch",
        });

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddProvider(new FileLoggerProvider(paths.LogsDirectory, TimeProvider.System,
            () => settings.Current.DebugLogging ? LogLevel.Debug : LogLevel.Information));

        var s = builder.Services;
        s.AddSingleton(TimeProvider.System);
        s.AddSingleton(args);
        s.AddSingleton(paths);
        s.AddSingleton(settings);

        s.AddSingleton<SessionStore>();
        s.AddSingleton<ISessionActivity>(sp => sp.GetRequiredService<SessionStore>());
        s.AddSingleton(sp => new UsageStore(paths.UsageFile, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<UsageStore>>()));
        s.AddSingleton(sp => new ClaudeCredentialReader(ClaudeCredentialReader.DefaultDirectory, sp.GetRequiredService<TimeProvider>()));
        s.AddSingleton(_ => new HttpClient { Timeout = ClaudeUsageProvider.Timeout });
        if (args.Demo)
        {
            s.AddSingleton<IUsageProvider, DemoUsageProvider>();
        }
        else
        {
            s.AddSingleton<IUsageProvider, ClaudeUsageProvider>();
        }

        s.AddSingleton<UsagePoller>();
        s.AddHostedService(sp => sp.GetRequiredService<UsagePoller>());
        s.AddHostedService<SessionSweeper>();
        s.AddSingleton(sp => new HookListener(settings.Current.Port, sp.GetRequiredService<SessionStore>(), sp.GetRequiredService<ILogger<HookListener>>()));
        s.AddHostedService(sp => sp.GetRequiredService<HookListener>());
        s.AddSingleton(sp => new HookInstaller(paths.ClaudeSettingsFile, paths.HookExe, sp.GetRequiredService<TimeProvider>()));

        s.AddSingleton<MonitorService>();
        s.AddSingleton<HoverController>();
        s.AddSingleton<IUiDispatcher>(_ => new WpfDispatcher(Application.Current.Dispatcher));
        s.AddSingleton<ISessionFocus, TerminalFocus>();
        s.AddSingleton<ISoundPlayer, SystemSoundPlayer>();
        s.AddSingleton<IAccentColorSource, SystemAccentColor>();

        s.AddSingleton(sp => new NotchViewModel(
            sp.GetRequiredService<UsageStore>(),
            sp.GetRequiredService<SessionStore>(),
            settings,
            sp.GetRequiredService<IUsageProvider>(),
            () => sp.GetRequiredService<UsagePoller>().RequestRefresh(),
            sp.GetRequiredService<HoverController>(),
            sp.GetRequiredService<IUiDispatcher>(),
            sp.GetRequiredService<ISessionFocus>(),
            sp.GetRequiredService<ISoundPlayer>(),
            sp.GetRequiredService<IAccentColorSource>(),
            sp.GetRequiredService<TimeProvider>(),
            TimeZoneInfo.Local));

        s.AddSingleton<NotchShell>();
        return builder.Build();
    }
}
```

- [ ] **Step 4 : Coquille d'affichage (version de cette tâche)**

`src/UsageNotch.App/Hosting/NotchShell.cs` :
```csharp
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Hooks;
using UsageNotch.Presentation.Services;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Hosting;

/// <summary>Crée et relie tout ce qui s'affiche. Doit être démarrée avant host.StartAsync (contrat du Plan 1).</summary>
public sealed class NotchShell(
    NotchViewModel viewModel,
    HookListener listener,
    IUiDispatcher ui,
    ILogger<NotchShell> logger) : IDisposable
{
    private Action? _onOpenSettings;

    public void Start()
    {
        // Plan 2 : une seconde instance lancée à la main montre la carte ; le Plan 3 ouvrira la fenêtre de réglages.
        _onOpenSettings = () => ui.Post(() => viewModel.PeekCommand.Execute(null));
        listener.OpenSettingsRequested += _onOpenSettings;
        logger.LogInformation("Coquille démarrée");
    }

    public void Dispose()
    {
        if (_onOpenSettings is not null) listener.OpenSettingsRequested -= _onOpenSettings;
        viewModel.Dispose();
    }
}
```

- [ ] **Step 5 : Démarrage et arrêt de l'application**

`src/UsageNotch.App/App.xaml.cs` (version complète) :
```csharp
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UsageNotch.App.Hosting;
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation;

namespace UsageNotch.App;

public partial class App : Application
{
    private SingleInstance? _instance;
    private IHost? _host;
    private NotchShell? _shell;
    private IDisposable? _demo;
    private ILogger<App>? _log;
    private bool _quitting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = AppArguments.Parse(e.Args);
        var paths = AppPaths.For(args.Demo);
        var settings = new SettingsStore(paths.SettingsFile, NullLogger<SettingsStore>.Instance);
        settings.Load();

        if (args.Doctor)
        {
            DoctorCommand.Run(paths, settings);
            Shutdown(0);
            return;
        }

        _instance = new SingleInstance(@"Local\UsageNotch");
        if (!_instance.IsFirst)
        {
            if (!args.FromHook) await SingleInstance.SignalExistingAsync(settings.Current.Port);
            Shutdown(0);
            return;
        }

        // Contrat du Plan 1 : un démarrage manuel réautorise le hook à relancer l'application.
        if (!args.FromHook && !settings.Current.AutoLaunch)
        {
            settings.Save(settings.Current with { AutoLaunch = true });
        }

        _host = AppHost.Build(args, paths, settings);
        _log = _host.Services.GetRequiredService<ILogger<App>>();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
            _log?.LogCritical(ev.ExceptionObject as Exception, "Exception non gérée");
        TaskScheduler.UnobservedTaskException += (_, ev) =>
        {
            _log?.LogError(ev.Exception, "Tâche non observée");
            ev.SetObserved();
        };

        // Contrat du Plan 1 : tout ce qui s'abonne aux magasins est créé avant StartAsync.
        _shell = _host.Services.GetRequiredService<NotchShell>();
        _shell.Start();
        if (args.Demo)
        {
            _demo = DemoMode.Start(_host.Services.GetRequiredService<SessionStore>(), TimeProvider.System);
        }

        await _host.StartAsync();
        _log.LogInformation("UsageNotch démarré (démo : {Demo}, lancé par le hook : {FromHook})", args.Demo, args.FromHook);
    }

    /// <summary>« Quitter » : le hook ne relancera plus l'application jusqu'au prochain démarrage manuel.</summary>
    public async Task QuitAsync(bool userInitiated)
    {
        if (_quitting) return;
        _quitting = true;

        if (userInitiated && _host is not null)
        {
            var settings = _host.Services.GetRequiredService<SettingsStore>();
            settings.Save(settings.Current with { AutoLaunch = false });
        }

        _demo?.Dispose();
        _shell?.Dispose();
        if (_host is not null)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { await _host.StopAsync(timeout.Token); }
            catch (OperationCanceledException) { }
            _host.Dispose();
        }
        _log?.LogInformation("UsageNotch arrêté");
        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log?.LogError(e.Exception, "Exception non gérée sur le thread UI");
        e.Handled = true;
    }
}
```

- [ ] **Step 6 : Construire et vérifier**

Run: `dotnet build UsageNotch.sln` puis `dotnet test`
Expected : aucun avertissement, tous les tests passent.

Vérification manuelle (démo uniquement) :
```powershell
$exe = "src\UsageNotch.App\bin\Debug\net10.0-windows\UsageNotch.App.exe"
if (Get-NetTCPConnection -LocalPort 48667 -State Listen -ErrorAction SilentlyContinue) { throw "port 48667 occupé" }
$first = Start-Process $exe -ArgumentList "--demo" -PassThru
Start-Sleep -Seconds 3
$second = Start-Process $exe -ArgumentList "--demo","--from-hook" -PassThru
$second.WaitForExit(5000) | Out-Null
"second instance exited: $($second.HasExited) ; first alive: $(-not $first.HasExited)"
Invoke-WebRequest -Method Post -Uri "http://127.0.0.1:48667/event?e=running&ppid=1" -Body '{"session_id":"manual"}' -UseBasicParsing | Select-Object StatusCode
Get-Content "$env:TEMP\UsageNotch-demo\logs\usagenotch-$((Get-Date).ToUniversalTime().ToString('yyyyMMdd')).log"
Stop-Process -Id $first.Id
```
Expected : `second instance exited: True ; first alive: True` ; `StatusCode 200` ; le journal contient « Coquille démarrée », « Récepteur de hooks à l'écoute sur 127.0.0.1:48667 » et « UsageNotch démarré (démo : True, lancé par le hook : False) ». Aucune fenêtre n'est encore visible, c'est attendu.

- [ ] **Step 7 : Commit**

`feat(app): hosting, single instance, demo mode and system services`

---

### Task 11 : Fenêtre de la pilule

**Files:**
- Create: `src/UsageNotch.App/Converters/HexBrushConverter.cs`, `Converters/ActivityVisibilityConverter.cs`
- Create: `src/UsageNotch.App/Controls/ProgressRing.cs`, `Controls/PillShapeBuilder.cs`
- Create: `src/UsageNotch.App/Views/NotchPlacer.cs`, `Views/PillWindow.xaml`, `Views/PillWindow.xaml.cs`
- Modify: `src/UsageNotch.App/Hosting/NotchShell.cs` (version complète ci-dessous), `src/UsageNotch.App/Hosting/AppHost.cs` (une ligne : `s.AddSingleton<NotchPlacer>();` juste après `s.AddSingleton<MonitorService>();`)

**Interfaces:**
- Consumes: `NotchViewModel` (Task 6) ; `PillMetrics`, `CellModel`, `ActivityKind` (Task 2) ; `MonitorService`, `WindowStyles`, `NativeMethods` (Task 9) ; `PillPlacement`, `PixelRect`, `MonitorInfo`, `Settings`, `SettingsStore`, `ScreenEdge`, `VisibilityMode` (Core).
- Produces:
  - `sealed class HexBrushConverter : IValueConverter` avec `static SolidColorBrush ToBrush(string? hex)` (brosses gelées, mises en cache, transparent si invalide).
  - `sealed class ActivityVisibilityConverter : IValueConverter` (paramètre = nom d'`ActivityKind`).
  - `sealed class ProgressRing : FrameworkElement` : propriétés de dépendance `TargetFraction` (`double?`, anime `Fraction` en 300 ms), `Fraction` (`double`), `RingBrush`, `TrackBrush` (`Brush`), `RingThickness` (`double`, 5) ; `static Geometry ArcGeometry(Point center, double radius, double fraction)`.
  - `static class PillShapeBuilder` : `Geometry Pill(ScreenEdge edge, double thickness, double length, double radius, double fillet)`, `Rect Band(ScreenEdge edge, double thickness, double length, double band, double fillet)`, `Rect Body(ScreenEdge edge, double thickness, double length, double fillet)`, `Vector SlideOffset(ScreenEdge edge, double thickness)`.
  - `sealed record PlacementResult(MonitorInfo Monitor, PixelRect PillRect)` et `sealed class NotchPlacer(MonitorService monitors)` : `PlacementResult Compute(Settings s)`, `double FractionForCursor(PlacementResult p, ScreenEdge edge, int cursorX, int cursorY)`, `PixelRect CardRect(PlacementResult p, Settings s, int cardWidthPx, int cardHeightPx)`.
  - `partial class PillWindow : Window` : constructeur `(NotchViewModel vm, NotchPlacer placer, SettingsStore settings, Func<Task> quit, Action openSettings)` ; `nint Handle` ; `PlacementResult? Placement` ; `event Action? PlacementChanged` ; titre de fenêtre exact `UsageNotch — pilule`.

Règles de cette fenêtre :
- **Passage des clics.** La fenêtre utilise `AllowsTransparency="True"` et `Background="{x:Null}"`. Windows ignore les pixels entièrement transparents d'une fenêtre en couches lors du test de clic : les clics à côté de la forme dessinée atteignent l'application située derrière. Rien d'autre ne doit peindre la zone transparente (pas de fond à opacité faible). `WM_NCHITTEST` n'est pas utilisé : `HTTRANSPARENT` ne transmet le clic qu'aux fenêtres du même thread, pas aux autres applications. (Écart consigné par rapport à la spec §6.)
- **Pas d'activation.** Styles étendus `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` et réponse `MA_NOACTIVATE` à `WM_MOUSEACTIVATE`.
- **Placement.** Position et taille en pixels physiques par `SetWindowPos`, calculées par `NotchPlacer`. La géométrie dessinée est en DIP : épaisseur `PillMetrics.Thickness × Scale`, longueur `PillMetrics.WindowLength × Scale`. Après `WM_DISPLAYCHANGE`, `WM_SETTINGCHANGE` ou `WM_DPICHANGED`, WPF traite d'abord le message, puis le placement est réappliqué à la priorité `Background`.
- **Modes.** Déplié : pilule visible. Masqué : fenêtre cachée. Replié : si `Unfolded` est faux, la pilule glisse vers le bord en 200 ms puis passe en `Visibility.Hidden`, et seule la bande (épaisseur `FoldedThicknessPx` pixels physiques) est dessinée ; si `Unfolded` devient vrai, la pilule redevient visible et glisse vers sa place en 200 ms.
- **Souris.** Entrée/sortie → `PointerEnteredPill` / `PointerLeftPill`. Clic gauche → `ToggleLockCommand`. Alt + glisser → déplace la pilule le long du bord, enregistre `WithPosition` au relâchement. Clic droit → menu contextuel (Rafraîchir maintenant, Garder la carte ouverte, Réglages…, Quitter).

- [ ] **Step 1 : Convertisseurs**

`src/UsageNotch.App/Converters/HexBrushConverter.cs` :
```csharp
using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UsageNotch.App.Converters;

[ValueConversion(typeof(string), typeof(Brush))]
public sealed class HexBrushConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, SolidColorBrush> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static SolidColorBrush ToBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Brushes.Transparent;
        return Cache.GetOrAdd(hex, static h =>
        {
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(h));
                brush.Freeze();
                return brush;
            }
            catch (Exception e) when (e is FormatException or NotSupportedException)
            {
                return Brushes.Transparent;
            }
        });
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ToBrush(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
```

`src/UsageNotch.App/Converters/ActivityVisibilityConverter.cs` :
```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.App.Converters;

/// <summary>Visible si l'activité vaut le nom passé en paramètre (ex. « Running »), sinon Collapsed.</summary>
public sealed class ActivityVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ActivityKind kind
        && parameter is string name
        && Enum.TryParse<ActivityKind>(name, out var wanted)
        && kind == wanted
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
```

- [ ] **Step 2 : Anneau de progression**

`src/UsageNotch.App/Controls/ProgressRing.cs` :
```csharp
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace UsageNotch.App.Controls;

/// <summary>Anneau qui part de midi et tourne dans le sens horaire. Un changement de <see cref="TargetFraction"/> s'anime en 300 ms.</summary>
public sealed class ProgressRing : FrameworkElement
{
    public static readonly DependencyProperty TargetFractionProperty = DependencyProperty.Register(
        nameof(TargetFraction), typeof(double?), typeof(ProgressRing), new PropertyMetadata(null, OnTargetFractionChanged));

    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
        nameof(Fraction), typeof(double), typeof(ProgressRing), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingBrushProperty = DependencyProperty.Register(
        nameof(RingBrush), typeof(Brush), typeof(ProgressRing), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(ProgressRing), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingThicknessProperty = DependencyProperty.Register(
        nameof(RingThickness), typeof(double), typeof(ProgressRing), new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double? TargetFraction { get => (double?)GetValue(TargetFractionProperty); set => SetValue(TargetFractionProperty, value); }
    public double Fraction { get => (double)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public Brush RingBrush { get => (Brush)GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public double RingThickness { get => (double)GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }

    private static void OnTargetFractionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ring = (ProgressRing)d;
        var to = Math.Clamp((e.NewValue as double?) ?? 0.0, 0.0, 1.0);
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        ring.BeginAnimation(FractionProperty, animation);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= RingThickness) return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size - RingThickness) / 2;
        dc.DrawEllipse(null, new Pen(TrackBrush, RingThickness), center, radius, radius);

        var fraction = Math.Clamp(Fraction, 0.0, 1.0);
        if (fraction <= 0.0005) return;

        var pen = new Pen(RingBrush, RingThickness) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        if (fraction >= 0.9995)
        {
            dc.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        dc.DrawGeometry(null, pen, ArcGeometry(center, radius, fraction));
    }

    /// <summary>Arc de midi vers le sens horaire, pour 0 &lt; fraction &lt; 1. Partagé avec l'icône de notification.</summary>
    public static Geometry ArcGeometry(Point center, double radius, double fraction)
    {
        var angle = fraction * 2 * Math.PI;
        var start = new Point(center.X, center.Y - radius);
        var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.ArcTo(end, new Size(radius, radius), 0, isLargeArc: fraction > 0.5, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();
        return geometry;
    }
}
```

- [ ] **Step 3 : Géométrie de la pilule**

`src/UsageNotch.App/Controls/PillShapeBuilder.cs` :
```csharp
using System.Windows;
using System.Windows.Media;
using UsageNotch.Core.Settings;

namespace UsageNotch.App.Controls;

/// <summary>
/// Formes en DIP, dans le repère de la fenêtre. On les décrit dans un repère canonique (x : de l'intérieur de l'écran
/// vers le bord, 0 → épaisseur ; y : le long du bord, 0 → longueur, congés compris) puis on les projette selon le bord.
/// </summary>
public static class PillShapeBuilder
{
    /// <summary>
    /// Corps aux coins arrondis côté intérieur, plat côté bord, prolongé jusqu'au bord par deux congés concaves.
    /// Canonique : (T,0) → (T,L) le long du bord → congé → coin → côté intérieur → coin → congé → (T,0).
    /// </summary>
    public static Geometry Pill(ScreenEdge edge, double thickness, double length, double radius, double fillet)
    {
        var t = thickness;
        var l = length;
        var r = radius;
        var f = fillet;
        var flips = Flips(edge);
        var convex = flips ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;
        var concave = flips ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
        Point P(double x, double y) => Map(edge, x, y, t);

        var figure = new PathFigure { StartPoint = P(t, 0), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment(P(t, l), isStroked: true));
        figure.Segments.Add(new ArcSegment(P(t - f, l - f), new Size(f, f), 0, false, concave, true));
        figure.Segments.Add(new LineSegment(P(r, l - f), true));
        figure.Segments.Add(new ArcSegment(P(0, l - f - r), new Size(r, r), 0, false, convex, true));
        figure.Segments.Add(new LineSegment(P(0, f + r), true));
        figure.Segments.Add(new ArcSegment(P(r, f), new Size(r, r), 0, false, convex, true));
        figure.Segments.Add(new LineSegment(P(t - f, f), true));
        figure.Segments.Add(new ArcSegment(P(t, 0), new Size(f, f), 0, false, concave, true));

        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    /// <summary>La bande du mode Replié : collée au bord, sur la longueur du corps (congés exclus).</summary>
    public static Rect Band(ScreenEdge edge, double thickness, double length, double band, double fillet) =>
        new(Map(edge, thickness - band, fillet, thickness), Map(edge, thickness, length - fillet, thickness));

    /// <summary>Zone du contenu (anneau et pourcentage) : toute l'épaisseur, entre les deux congés.</summary>
    public static Rect Body(ScreenEdge edge, double thickness, double length, double fillet) =>
        new(Map(edge, 0, fillet, thickness), Map(edge, thickness, length - fillet, thickness));

    /// <summary>Déplacement qui pousse la pilule entièrement derrière le bord de l'écran.</summary>
    public static Vector SlideOffset(ScreenEdge edge, double thickness) => edge switch
    {
        ScreenEdge.Left => new Vector(-thickness, 0),
        ScreenEdge.Top => new Vector(0, -thickness),
        ScreenEdge.Bottom => new Vector(0, thickness),
        _ => new Vector(thickness, 0),
    };

    private static Point Map(ScreenEdge edge, double x, double y, double thickness) => edge switch
    {
        ScreenEdge.Left => new Point(thickness - x, y),
        ScreenEdge.Top => new Point(y, thickness - x),
        ScreenEdge.Bottom => new Point(y, x),
        _ => new Point(x, y),
    };

    /// <summary>Les projections gauche et bas sont des symétries : le sens des arcs s'inverse.</summary>
    private static bool Flips(ScreenEdge edge) => edge is ScreenEdge.Left or ScreenEdge.Bottom;
}
```

- [ ] **Step 4 : Placement physique**

`src/UsageNotch.App/Views/NotchPlacer.cs` :
```csharp
using UsageNotch.App.Interop;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.App.Views;

public sealed record PlacementResult(MonitorInfo Monitor, PixelRect PillRect);

/// <summary>Traduit les réglages en rectangles physiques sur l'écran choisi (repli sur l'écran principal s'il est absent).</summary>
public sealed class NotchPlacer(MonitorService monitors)
{
    public PlacementResult Compute(Settings s)
    {
        var monitor = PillPlacement.Choose(monitors.GetMonitors(), s.MonitorDeviceId);
        var factor = s.Scale * monitor.Scale;
        var thickness = (int)Math.Round(PillMetrics.Thickness * factor);
        var length = (int)Math.Round(PillMetrics.WindowLength * factor);
        var rect = PillPlacement.PillRect(monitor.Bounds, s.Edge, s.PositionFor(s.Edge), length, thickness);
        return new PlacementResult(monitor, rect);
    }

    /// <summary>Position le long du bord qui centre la pilule sur le curseur.</summary>
    public double FractionForCursor(PlacementResult p, ScreenEdge edge, int cursorX, int cursorY)
    {
        var r = p.PillRect;
        var moved = PillPlacement.IsVertical(edge)
            ? r with { Y = cursorY - r.Height / 2 }
            : r with { X = cursorX - r.Width / 2 };
        return PillPlacement.FractionOf(p.Monitor.Bounds, edge, moved);
    }

    public PixelRect CardRect(PlacementResult p, Settings s, int cardWidthPx, int cardHeightPx)
    {
        var gap = (int)Math.Round(PillMetrics.CardGap * s.Scale * p.Monitor.Scale);
        var margin = (int)Math.Round(PillMetrics.ScreenMargin * p.Monitor.Scale);
        return PillPlacement.CardRect(p.Monitor.Bounds, s.Edge, p.PillRect, p.PillRect, cardWidthPx, cardHeightPx, gap, margin);
    }
}
```

- [ ] **Step 5 : XAML de la pilule**

`src/UsageNotch.App/Views/PillWindow.xaml` :
```xml
<Window x:Class="UsageNotch.App.Views.PillWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:UsageNotch.App.Controls"
        xmlns:conv="clr-namespace:UsageNotch.App.Converters"
        Title="UsageNotch — pilule"
        WindowStyle="None" AllowsTransparency="True" Background="{x:Null}"
        Topmost="True" ShowInTaskbar="False" ShowActivated="False" ResizeMode="NoResize"
        Width="64" Height="136" UseLayoutRounding="True" SnapsToDevicePixels="True">
  <Window.Resources>
    <conv:HexBrushConverter x:Key="Hex" />
    <conv:ActivityVisibilityConverter x:Key="Activity" />
    <BooleanToVisibilityConverter x:Key="BoolVis" />
    <Storyboard x:Key="Spin" RepeatBehavior="Forever">
      <DoubleAnimation Storyboard.TargetName="SpinRotate" Storyboard.TargetProperty="Angle" From="0" To="360" Duration="0:0:1.2" />
    </Storyboard>
    <Storyboard x:Key="Pulse" RepeatBehavior="Forever" AutoReverse="True">
      <DoubleAnimation Storyboard.TargetName="AttentionRing" Storyboard.TargetProperty="Opacity" From="0.4" To="1" Duration="0:0:1" />
    </Storyboard>
  </Window.Resources>

  <Window.ContextMenu>
    <ContextMenu>
      <MenuItem Header="Rafraîchir maintenant" Command="{Binding RefreshCommand}" />
      <MenuItem Header="Garder la carte ouverte" IsChecked="{Binding Locked, Mode=OneWay}" Command="{Binding ToggleLockCommand}" />
      <Separator />
      <MenuItem Header="Réglages…" Click="OnSettingsClick" />
      <MenuItem Header="Quitter" Click="OnQuitClick" />
    </ContextMenu>
  </Window.ContextMenu>

  <Canvas x:Name="Root">
    <Path x:Name="BandPath" Visibility="Collapsed"
          Fill="{Binding Cell.BandColor, Converter={StaticResource Hex}}" />

    <Canvas x:Name="PillLayer">
      <Canvas.RenderTransform>
        <TranslateTransform x:Name="Slide" />
      </Canvas.RenderTransform>

      <Path x:Name="PillPath" StrokeThickness="1"
            Fill="{Binding Theme.PillBackground, Converter={StaticResource Hex}}"
            Stroke="{Binding Theme.PillBorder, Converter={StaticResource Hex}}" />

      <Grid x:Name="Body">
        <StackPanel x:Name="BodyStack" HorizontalAlignment="Center" VerticalAlignment="Center">
          <StackPanel.LayoutTransform>
            <ScaleTransform x:Name="BodyScale" ScaleX="1" ScaleY="1" />
          </StackPanel.LayoutTransform>

          <Grid x:Name="RingHost" Width="44" Height="44" Margin="0,0,0,4"
                Visibility="{Binding Cell.ShowRing, Converter={StaticResource BoolVis}}">
            <controls:ProgressRing RingThickness="5"
                                   TargetFraction="{Binding Cell.RingFraction}"
                                   RingBrush="{Binding Cell.RingColor, Converter={StaticResource Hex}}"
                                   TrackBrush="{Binding Cell.TrackColor, Converter={StaticResource Hex}}" />
            <Path x:Name="RunningArc" Width="44" Height="44" Stretch="None"
                  Data="M 22,8 A 14,14 0 0 1 36,22"
                  StrokeThickness="2.5" StrokeStartLineCap="Round" StrokeEndLineCap="Round"
                  RenderTransformOrigin="0.5,0.5"
                  Stroke="{Binding Cell.ActivityColor, Converter={StaticResource Hex}}"
                  Visibility="{Binding Cell.Activity, Converter={StaticResource Activity}, ConverterParameter=Running}">
              <Path.RenderTransform>
                <RotateTransform x:Name="SpinRotate" />
              </Path.RenderTransform>
            </Path>
            <Ellipse x:Name="AttentionRing" Width="28" Height="28" StrokeThickness="2.5"
                     Stroke="{Binding Cell.ActivityColor, Converter={StaticResource Hex}}"
                     Visibility="{Binding Cell.Activity, Converter={StaticResource Activity}, ConverterParameter=Attention}" />
            <Ellipse Width="8" Height="8"
                     Fill="{Binding Cell.ActivityColor, Converter={StaticResource Hex}}"
                     Visibility="{Binding Cell.Activity, Converter={StaticResource Activity}, ConverterParameter=Done}" />
          </Grid>

          <TextBlock x:Name="PercentText" FontFamily="Segoe UI" FontSize="14" FontWeight="SemiBold"
                     HorizontalAlignment="Center" VerticalAlignment="Center"
                     Text="{Binding Cell.PercentText}"
                     Foreground="{Binding Cell.TextColor, Converter={StaticResource Hex}}"
                     Visibility="{Binding Cell.ShowPercent, Converter={StaticResource BoolVis}}" />
        </StackPanel>
      </Grid>
    </Canvas>
  </Canvas>
</Window>
```

- [ ] **Step 6 : Code de la pilule**

`src/UsageNotch.App/Views/PillWindow.xaml.cs` :
```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using UsageNotch.App.Controls;
using UsageNotch.App.Interop;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Pill;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Views;

public partial class PillWindow : Window
{
    private static readonly TimeSpan FoldAnimation = TimeSpan.FromMilliseconds(200);

    private readonly NotchViewModel _vm;
    private readonly NotchPlacer _placer;
    private readonly SettingsStore _settings;
    private readonly Func<Task> _quit;
    private readonly Action _openSettings;

    private bool _initialized;
    private bool _dragging;
    private double _dragFraction;
    private double _thicknessDip = PillMetrics.Thickness;

    public PillWindow(NotchViewModel vm, NotchPlacer placer, SettingsStore settings, Func<Task> quit, Action openSettings)
    {
        _vm = vm;
        _placer = placer;
        _settings = settings;
        _quit = quit;
        _openSettings = openSettings;

        InitializeComponent();
        DataContext = vm;

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            ((Storyboard)Resources["Spin"]).Begin(this, isControllable: true);
            ((Storyboard)Resources["Pulse"]).Begin(this, isControllable: true);
        };
        MouseEnter += (_, _) => { if (!_dragging) _vm.PointerEnteredPill(); };
        MouseLeave += (_, _) => { if (!_dragging) _vm.PointerLeftPill(); };
        PreviewMouseLeftButtonDown += OnLeftButtonDown;
        PreviewMouseLeftButtonUp += OnLeftButtonUp;
        MouseMove += OnMouseMove;
        _vm.PropertyChanged += OnViewModelChanged;
    }

    public nint Handle { get; private set; }

    public PlacementResult? Placement { get; private set; }

    public event Action? PlacementChanged;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        Handle = new WindowInteropHelper(this).Handle;
        WindowStyles.MakeToolWindowNoActivate(Handle);
        HwndSource.FromHwnd(Handle)!.AddHook(WndProc);
        _initialized = true;
        ApplySettings(fromSourceInitialized: true);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_MOUSEACTIVATE:
                handled = true;
                return NativeMethods.MA_NOACTIVATE;
            case NativeMethods.WM_DISPLAYCHANGE:
            case NativeMethods.WM_SETTINGCHANGE:
            case NativeMethods.WM_DPICHANGED:
                // WPF traite d'abord le message (mise à l'échelle), puis on replace la pilule.
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => ApplySettings()));
                break;
        }
        return 0;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_initialized)
        {
            // Démarré en mode Masqué : la fenêtre n'a jamais été montrée ; la montrer déclenche SourceInitialized.
            if (e.PropertyName == nameof(NotchViewModel.Settings) && _vm.Settings.Visibility != VisibilityMode.Hidden) Show();
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(NotchViewModel.Settings):
                ApplySettings();
                break;
            case nameof(NotchViewModel.Unfolded):
                UpdateFold(animated: true);
                break;
            case nameof(NotchViewModel.Cell):
                BodyStack.Opacity = _vm.Cell.Dimmed ? 0.5 : 1.0;
                break;
        }
    }

    /// <summary>Recalcule forme, contenu et position à partir des réglages courants.</summary>
    private void ApplySettings(bool fromSourceInitialized = false)
    {
        if (!_initialized) return;
        var s = _vm.Settings;

        if (s.Visibility == VisibilityMode.Hidden)
        {
            Hide();
            return;
        }

        var placement = _placer.Compute(s);
        Placement = placement;

        var scale = s.Scale;
        _thicknessDip = PillMetrics.Thickness * scale;
        var length = PillMetrics.WindowLength * scale;
        var fillet = PillMetrics.Fillet * scale;
        var vertical = s.Edge is ScreenEdge.Right or ScreenEdge.Left;

        PillPath.Data = PillShapeBuilder.Pill(s.Edge, _thicknessDip, length, PillMetrics.CornerRadius * scale, fillet);
        var bandDip = s.FoldedThicknessPx / placement.Monitor.Scale;
        BandPath.Data = new RectangleGeometry(PillShapeBuilder.Band(s.Edge, _thicknessDip, length, bandDip, fillet));

        var body = PillShapeBuilder.Body(s.Edge, _thicknessDip, length, fillet);
        Canvas.SetLeft(Body, body.X);
        Canvas.SetTop(Body, body.Y);
        Body.Width = body.Width;
        Body.Height = body.Height;
        BodyScale.ScaleX = scale;
        BodyScale.ScaleY = scale;
        BodyStack.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        RingHost.Margin = vertical ? new Thickness(0, 0, 0, 4) : new Thickness(0, 0, 6, 0);
        BodyStack.Opacity = _vm.Cell.Dimmed ? 0.5 : 1.0;

        // Pendant SourceInitialized, Show() est déjà en cours : ne pas le rappeler.
        if (!fromSourceInitialized && !IsVisible) Show();
        WindowStyles.MoveResize(Handle, placement.PillRect);
        UpdateFold(animated: false);
        PlacementChanged?.Invoke();
    }

    private void UpdateFold(bool animated)
    {
        var s = _vm.Settings;
        var folded = s.Visibility == VisibilityMode.Folded && !_vm.Unfolded;
        var target = folded ? PillShapeBuilder.SlideOffset(s.Edge, _thicknessDip) : new Vector(0, 0);

        BandPath.Visibility = folded ? Visibility.Visible : Visibility.Collapsed;

        if (!animated)
        {
            Slide.BeginAnimation(TranslateTransform.XProperty, null);
            Slide.BeginAnimation(TranslateTransform.YProperty, null);
            Slide.X = target.X;
            Slide.Y = target.Y;
            PillLayer.Visibility = folded ? Visibility.Hidden : Visibility.Visible;
            return;
        }

        if (!folded) PillLayer.Visibility = Visibility.Visible;
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var x = new DoubleAnimation(target.X, FoldAnimation) { EasingFunction = ease };
        var y = new DoubleAnimation(target.Y, FoldAnimation) { EasingFunction = ease };
        if (folded)
        {
            x.Completed += (_, _) =>
            {
                if (_vm.Settings.Visibility == VisibilityMode.Folded && !_vm.Unfolded) PillLayer.Visibility = Visibility.Hidden;
            };
        }
        Slide.BeginAnimation(TranslateTransform.XProperty, x);
        Slide.BeginAnimation(TranslateTransform.YProperty, y);
    }

    private void OnLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Placement is null || !WindowStyles.IsAltDown()) return;
        _dragging = true;
        _dragFraction = _vm.Settings.PositionFor(_vm.Settings.Edge);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || Placement is null) return;
        var (cx, cy) = WindowStyles.CursorPosition();
        var edge = _vm.Settings.Edge;
        _dragFraction = _placer.FractionForCursor(Placement, edge, cx, cy);
        var preview = _placer.Compute(_vm.Settings.WithPosition(edge, _dragFraction));
        WindowStyles.MoveResize(Handle, preview.PillRect);
    }

    private void OnLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
            _settings.Save(_settings.Current.WithPosition(_vm.Settings.Edge, _dragFraction));
            if (!IsMouseOver) _vm.PointerLeftPill();
            e.Handled = true;
            return;
        }
        _vm.ToggleLockCommand.Execute(null);
        e.Handled = true;
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => _openSettings();

    private void OnQuitClick(object sender, RoutedEventArgs e) => _ = _quit();

    protected override void OnClosed(EventArgs e)
    {
        _vm.PropertyChanged -= OnViewModelChanged;
        base.OnClosed(e);
    }
}
```

- [ ] **Step 7 : Coquille (version de cette tâche)**

`src/UsageNotch.App/Hosting/NotchShell.cs` (remplacer tout le contenu) :
```csharp
using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.Logging;
using UsageNotch.App.Views;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Hosting;

/// <summary>Crée et relie tout ce qui s'affiche. Doit être démarrée avant host.StartAsync (contrat du Plan 1).</summary>
public sealed class NotchShell(
    NotchViewModel viewModel,
    HookListener listener,
    NotchPlacer placer,
    SettingsStore settings,
    AppPaths paths,
    IUiDispatcher ui,
    ILogger<NotchShell> logger) : IDisposable
{
    private Action? _onOpenSettings;
    private PillWindow? _pill;

    public void Start()
    {
        _onOpenSettings = () => ui.Post(() => viewModel.PeekCommand.Execute(null));
        listener.OpenSettingsRequested += _onOpenSettings;

        _pill = new PillWindow(viewModel, placer, settings, () => ((App)Application.Current).QuitAsync(userInitiated: true), OpenSettingsFile);
        if (settings.Current.Visibility != VisibilityMode.Hidden) _pill.Show();

        logger.LogInformation("Coquille démarrée");
    }

    /// <summary>Plan 2 : les réglages s'éditent dans settings.json ; le Plan 3 remplacera ceci par la fenêtre de réglages.</summary>
    private void OpenSettingsFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{paths.SettingsFile}\"") { UseShellExecute = false });
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            logger.LogWarning(e, "Impossible d'ouvrir {Path}", paths.SettingsFile);
        }
    }

    public void Dispose()
    {
        if (_onOpenSettings is not null) listener.OpenSettingsRequested -= _onOpenSettings;
        _pill?.Close();
        viewModel.Dispose();
    }
}
```

Note : la relecture à chaud de `settings.json` arrive en Task 13. Pour cette tâche, redémarrer l'application après chaque modification du fichier.

- [ ] **Step 8 : Construire et vérifier visuellement**

Run: `dotnet build UsageNotch.sln` — aucun avertissement.

Démarrer la démo et capturer l'écran avec la procédure « Capture d'écran » (section « Procédures de vérification visuelle ») :
```powershell
$exe = "src\UsageNotch.App\bin\Debug\net10.0-windows\UsageNotch.App.exe"
$app = Start-Process $exe -ArgumentList "--demo" -PassThru
Start-Sleep -Seconds 4
# … procédure « Capture d'écran » …
```
Expected sur la capture (lire l'image) : au milieu du bord droit de l'écran principal, une pilule noire soudée au bord avec ses deux congés concaves, un anneau jaune à 73 %, le texte « 73 % » en blanc dessous, et un anneau ambre qui pulse à l'intérieur (la démo contient une session en attente).

Vérifier le passage des clics avec la procédure « Test de clic » (section « Procédures de vérification visuelle ») : un point au centre de la pilule appartient à la fenêtre `UsageNotch — pilule`, le coin supérieur gauche de son rectangle (zone transparente) n'y appartient pas.

Vérifier les bords et le mode Replié : pour chaque variante, `Stop-Process -Id $app.Id`, modifier `%TEMP%\UsageNotch-demo\settings.json`, relancer, capturer :
- `"edge": "Left"`, puis `"Top"`, puis `"Bottom"` : la pilule est collée au bord correspondant, congés du bon côté, contenu côte à côte sur les bords haut et bas.
- `"visibility": "Folded"` avec `"edge": "Right"` : seule une bande de 4 px est visible au bord ; en déplaçant la souris dessus (procédure « Survol » (section « Procédures de vérification visuelle »)), la pilule glisse et apparaît.
- `"scale": 0.6` : pilule plus petite, proportions conservées.
Remettre `"edge": "Right"`, `"visibility": "Expanded"`, `"scale": 1` à la fin, puis `Stop-Process -Id $app.Id`.

- [ ] **Step 9 : Commit**

`feat(app): pill window with shape, ring, folded mode, placement and Alt-drag`

---

### Task 12 : Carte de détail et survol

**Files:**
- Create: `src/UsageNotch.App/Converters/NotNullVisibilityConverter.cs`
- Create: `src/UsageNotch.App/Views/CardWindow.xaml`, `Views/CardWindow.xaml.cs`
- Modify: `src/UsageNotch.App/Hosting/NotchShell.cs` (version complète ci-dessous)

**Interfaces:**
- Consumes: `NotchViewModel` (Task 6) ; `CardModel`, `WindowRow`, `SessionRow` (Task 3) ; `PillWindow`, `NotchPlacer`, `PlacementResult`, `HexBrushConverter` (Task 11) ; `WindowStyles`, `NativeMethods` (Task 9).
- Produces:
  - `sealed class NotNullVisibilityConverter : IValueConverter` (null ou chaîne vide → Collapsed).
  - `partial class CardWindow : Window` : constructeur `(NotchViewModel vm, NotchPlacer placer, PillWindow pill)` ; titre exact `UsageNotch — carte`.

Règles (spec §6 « Carte de détail », « Survol », « Animations ») :
- La carte est une fenêtre séparée, transparente hors de son dessin, non activable, cachée (`Hide()`) quand `CardVisible` est faux : elle n'occupe alors aucune surface.
- Ouverture : fondu 0 → 1 en 180 ms et glissement de 8 DIP depuis la pilule. Fermeture : fondu en 150 ms puis `Hide()` si `CardVisible` est toujours faux.
- Position : `NotchPlacer.CardRect` à partir du placement de la pilule (ou d'un placement calculé si le mode est Masqué), recalculée quand la taille de la carte change ou quand la pilule bouge. Taille physique = taille DIP × échelle de l'écran de la pilule.
- La flèche (10 DIP) est centrée sur le côté tourné vers la pilule.
- L'échelle globale `Settings.Scale` s'applique à toute la carte.
- Entrée/sortie du pointeur → `PointerEnteredCard` / `PointerLeftCard`. Filet de sécurité : tant que la carte est visible, toutes les 200 ms, si le curseur n'est ni dans le rectangle de la pilule ni dans celui de la carte → `PointerLeftPill()` et `PointerLeftCard()`.
- Clic sur une ligne de session → `FocusSessionCommand` ; bouton ✕ → `DismissSessionCommand`.

- [ ] **Step 1 : Convertisseur**

`src/UsageNotch.App/Converters/NotNullVisibilityConverter.cs` :
```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UsageNotch.App.Converters;

public sealed class NotNullVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null || (value is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
```

- [ ] **Step 2 : XAML de la carte**

`src/UsageNotch.App/Views/CardWindow.xaml` :
```xml
<Window x:Class="UsageNotch.App.Views.CardWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:conv="clr-namespace:UsageNotch.App.Converters"
        Title="UsageNotch — carte"
        WindowStyle="None" AllowsTransparency="True" Background="{x:Null}"
        Topmost="True" ShowInTaskbar="False" ShowActivated="False" ResizeMode="NoResize"
        SizeToContent="WidthAndHeight" UseLayoutRounding="True" SnapsToDevicePixels="True"
        FontFamily="Segoe UI">
  <Window.Resources>
    <conv:HexBrushConverter x:Key="Hex" />
    <conv:NotNullVisibilityConverter x:Key="NotNull" />
    <SolidColorBrush x:Key="Secondary" Color="#9A9A9A" />
    <Style x:Key="GhostButton" TargetType="Button">
      <Setter Property="Focusable" Value="False" />
      <Setter Property="Cursor" Value="Hand" />
      <Setter Property="Foreground" Value="#8A8A8A" />
      <Setter Property="FontSize" Value="12" />
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="Bg" Background="Transparent" CornerRadius="6" Padding="6,2">
              <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="Bg" Property="Background" Value="#33FFFFFF" />
                <Setter Property="Foreground" Value="#FFFFFF" />
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>

  <Grid x:Name="Root">
    <Grid.LayoutTransform>
      <ScaleTransform x:Name="RootScale" ScaleX="1" ScaleY="1" />
    </Grid.LayoutTransform>
    <Grid.RenderTransform>
      <TranslateTransform x:Name="Slide" />
    </Grid.RenderTransform>

    <Grid x:Name="CardHost" Margin="0,0,10,0">
      <Path x:Name="Arrow" Data="M0,0 L10,10 L0,20 Z"
            HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,-10,0"
            Fill="{Binding Theme.PillBackground, Converter={StaticResource Hex}}" />

      <Border Width="300" CornerRadius="14" Padding="16" BorderThickness="1"
              Background="{Binding Theme.PillBackground, Converter={StaticResource Hex}}"
              BorderBrush="{Binding Theme.PillBorder, Converter={StaticResource Hex}}">
        <StackPanel>
          <TextBlock Text="{Binding Card.Title}" FontSize="15" FontWeight="SemiBold"
                     Foreground="{Binding Theme.Text, Converter={StaticResource Hex}}" />
          <TextBlock Text="{Binding Card.Subtitle}" FontSize="11" Margin="0,2,0,0"
                     Foreground="{StaticResource Secondary}"
                     Visibility="{Binding Card.Subtitle, Converter={StaticResource NotNull}}" />

          <ItemsControl ItemsSource="{Binding Card.Windows}" Margin="0,12,0,0">
            <ItemsControl.ItemTemplate>
              <DataTemplate>
                <StackPanel Margin="0,0,0,12">
                  <DockPanel>
                    <TextBlock DockPanel.Dock="Right" Text="{Binding ResetText}" FontSize="11"
                               Foreground="{StaticResource Secondary}" />
                    <TextBlock Text="{Binding Label}" FontSize="12" TextTrimming="CharacterEllipsis"
                               Foreground="{Binding DataContext.Theme.Text, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource Hex}}" />
                  </DockPanel>
                  <Grid Height="4" Margin="0,6,0,4">
                    <Border CornerRadius="2"
                            Background="{Binding DataContext.Theme.RingTrack, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource Hex}}" />
                    <Border CornerRadius="2" RenderTransformOrigin="0,0.5"
                            Background="{Binding BarColor, Converter={StaticResource Hex}}">
                      <Border.RenderTransform>
                        <ScaleTransform ScaleX="{Binding Fraction}" ScaleY="1" />
                      </Border.RenderTransform>
                    </Border>
                  </Grid>
                  <TextBlock Text="{Binding UsedText}" FontSize="11" Foreground="{StaticResource Secondary}" />
                </StackPanel>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>

          <TextBlock Text="{Binding Card.Note}" FontSize="11" TextWrapping="Wrap" Margin="0,0,0,8"
                     Foreground="{StaticResource Secondary}"
                     Visibility="{Binding Card.Note, Converter={StaticResource NotNull}}" />

          <ItemsControl ItemsSource="{Binding Card.Sessions}">
            <ItemsControl.ItemTemplate>
              <DataTemplate>
                <Border x:Name="Row" Background="Transparent" CornerRadius="8" Padding="6,4" Cursor="Hand">
                  <Border.InputBindings>
                    <MouseBinding Gesture="LeftClick"
                                  Command="{Binding DataContext.FocusSessionCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                  CommandParameter="{Binding SessionId}" />
                  </Border.InputBindings>
                  <DockPanel LastChildFill="True">
                    <Button DockPanel.Dock="Right" Content="✕" ToolTip="Retirer cette session"
                            Style="{StaticResource GhostButton}"
                            Command="{Binding DataContext.DismissSessionCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                            CommandParameter="{Binding SessionId}" />
                    <Ellipse DockPanel.Dock="Left" Width="8" Height="8" Margin="0,0,8,0" VerticalAlignment="Center"
                             Fill="{Binding DotColor, Converter={StaticResource Hex}}" />
                    <StackPanel>
                      <TextBlock Text="{Binding Title}" FontSize="12" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"
                                 Foreground="{Binding DataContext.Theme.Text, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource Hex}}" />
                      <TextBlock Text="{Binding Detail}" FontSize="11" TextTrimming="CharacterEllipsis"
                                 Foreground="{StaticResource Secondary}" />
                    </StackPanel>
                  </DockPanel>
                </Border>
                <DataTemplate.Triggers>
                  <Trigger SourceName="Row" Property="IsMouseOver" Value="True">
                    <Setter TargetName="Row" Property="Background" Value="#1FFFFFFF" />
                  </Trigger>
                </DataTemplate.Triggers>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </StackPanel>
      </Border>
    </Grid>
  </Grid>
</Window>
```

- [ ] **Step 3 : Code de la carte**

`src/UsageNotch.App/Views/CardWindow.xaml.cs` :
```csharp
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

    private static nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
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

        switch (s.Edge)
        {
            case ScreenEdge.Left:
                CardHost.Margin = new Thickness(10, 0, 0, 0);
                Arrow.Data = Geometry.Parse("M10,0 L0,10 L10,20 Z");
                Arrow.HorizontalAlignment = HorizontalAlignment.Left;
                Arrow.VerticalAlignment = VerticalAlignment.Center;
                Arrow.Margin = new Thickness(-10, 0, 0, 0);
                break;
            case ScreenEdge.Top:
                CardHost.Margin = new Thickness(0, 10, 0, 0);
                Arrow.Data = Geometry.Parse("M0,10 L10,0 L20,10 Z");
                Arrow.HorizontalAlignment = HorizontalAlignment.Center;
                Arrow.VerticalAlignment = VerticalAlignment.Top;
                Arrow.Margin = new Thickness(0, -10, 0, 0);
                break;
            case ScreenEdge.Bottom:
                CardHost.Margin = new Thickness(0, 0, 0, 10);
                Arrow.Data = Geometry.Parse("M0,0 L10,10 L20,0 Z");
                Arrow.HorizontalAlignment = HorizontalAlignment.Center;
                Arrow.VerticalAlignment = VerticalAlignment.Bottom;
                Arrow.Margin = new Thickness(0, 0, 0, -10);
                break;
            default:
                CardHost.Margin = new Thickness(0, 0, 10, 0);
                Arrow.Data = Geometry.Parse("M0,0 L10,10 L0,20 Z");
                Arrow.HorizontalAlignment = HorizontalAlignment.Right;
                Arrow.VerticalAlignment = VerticalAlignment.Center;
                Arrow.Margin = new Thickness(0, 0, -10, 0);
                break;
        }
    }

    private void OpenCard()
    {
        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }
        Reposition();

        var from = SlideFrom(_vm.Settings.Edge);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(from.X, 0, OpenDuration) { EasingFunction = ease });
        Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(from.Y, 0, OpenDuration) { EasingFunction = ease });
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

        WindowStyles.MoveResize(_hwnd, _placer.CardRect(placement, s, width, height));
    }

    private void OnSafetyTick(object? sender, EventArgs e)
    {
        if (!_vm.CardVisible)
        {
            _safety.Stop();
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
        base.OnClosed(e);
    }
}
```

- [ ] **Step 4 : Coquille (version de cette tâche)**

`src/UsageNotch.App/Hosting/NotchShell.cs` (remplacer tout le contenu) :
```csharp
using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.Logging;
using UsageNotch.App.Views;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Hosting;

/// <summary>Crée et relie tout ce qui s'affiche. Doit être démarrée avant host.StartAsync (contrat du Plan 1).</summary>
public sealed class NotchShell(
    NotchViewModel viewModel,
    HookListener listener,
    NotchPlacer placer,
    SettingsStore settings,
    AppPaths paths,
    IUiDispatcher ui,
    ILogger<NotchShell> logger) : IDisposable
{
    private Action? _onOpenSettings;
    private PillWindow? _pill;
    private CardWindow? _card;

    public void Start()
    {
        _onOpenSettings = () => ui.Post(() => viewModel.PeekCommand.Execute(null));
        listener.OpenSettingsRequested += _onOpenSettings;

        _pill = new PillWindow(viewModel, placer, settings, QuitAsync, OpenSettingsFile);
        _card = new CardWindow(viewModel, placer, _pill);
        if (settings.Current.Visibility != VisibilityMode.Hidden) _pill.Show();

        logger.LogInformation("Coquille démarrée");
    }

    private static Task QuitAsync() => ((App)Application.Current).QuitAsync(userInitiated: true);

    /// <summary>Plan 2 : les réglages s'éditent dans settings.json ; le Plan 3 remplacera ceci par la fenêtre de réglages.</summary>
    private void OpenSettingsFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{paths.SettingsFile}\"") { UseShellExecute = false });
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            logger.LogWarning(e, "Impossible d'ouvrir {Path}", paths.SettingsFile);
        }
    }

    public void Dispose()
    {
        if (_onOpenSettings is not null) listener.OpenSettingsRequested -= _onOpenSettings;
        _card?.Close();
        _pill?.Close();
        viewModel.Dispose();
    }
}
```

- [ ] **Step 5 : Construire et vérifier**

Run: `dotnet build UsageNotch.sln` puis `dotnet test` — aucun avertissement, tout passe.

Démarrer la démo (`--demo`), puis :
1. Procédure « Survol » vers le centre de la pilule, attendre 500 ms, procédure « Capture d'écran ». Expected : à gauche de la pilule, une carte noire aux coins arrondis avec sa flèche vers la pilule : « Claude », trois blocs (Session en cours 73 % jaune avec « Réinitialisation dans 51 min », Hebdomadaire (tous modèles) 21 % vert, Hebdomadaire (Opus) 52 % jaune), puis les sessions « web · demo » en attente (« Autoriser Bash : npm install ? »), « api · demo » en cours (« 🔧 Bash : dotnet test »), « docs · demo » terminée.
2. Procédure « Survol » vers un point à 400 px à gauche de la carte, attendre 600 ms, capture. Expected : la carte a disparu. Procédure « Test de clic » au centre de l'ancienne position de la carte : le point n'appartient plus à `UsageNotch — carte`.
3. Attendre 20 à 25 s sans toucher la souris (la démo fait passer « api » en Terminé), capture. Expected : la carte s'est ouverte seule (aperçu) et un son système a retenti ; 6 s plus tard elle est refermée.
4. Survoler la pilule puis cliquer (procédure « Clic ») sur la pilule, écarter la souris, attendre 1 s, capture. Expected : la carte reste ouverte (verrou). Recliquer sur la pilule, écarter la souris, 1 s : la carte est fermée.

`Stop-Process` de l'application à la fin.

- [ ] **Step 6 : Commit**

`feat(app): detail card window with hover, lock and auto-open`

---

### Task 13 : Zone de notification, relecture des réglages et démarrage avec Windows

**Files:**
- Create: `src/UsageNotch.App/Tray/TrayIconService.cs`, `src/UsageNotch.App/Hosting/SettingsFileWatcher.cs`, `src/UsageNotch.App/Interop/AutoStart.cs`
- Modify: `src/UsageNotch.App/Hosting/NotchShell.cs` (version complète ci-dessous), `src/UsageNotch.App/Hosting/AppHost.cs` (ajouter `s.AddSingleton<TrayIconService>();` et `s.AddSingleton<SettingsFileWatcher>();` juste avant `s.AddSingleton<NotchShell>();`)

**Interfaces:**
- Consumes: `NotchViewModel` (Task 6) ; `ProgressRing.ArcGeometry`, `HexBrushConverter.ToBrush` (Task 11) ; `DoctorCommand`, `AppPaths` (Task 9) ; `IUiDispatcher` (Task 6) ; `HookInstaller`, `SettingsStore` (Core) ; `TaskbarIcon` (H.NotifyIcon.Wpf : `ToolTipText`, `IconSource`, `ContextMenu`, `Visibility`, événement `TrayLeftMouseUp`, `ForceCreate(bool)`, `Dispose()`).
- Produces:
  - `static class AutoStart` : `bool IsEnabled()`, `void Set(bool enabled, string exePath)` (valeur `UsageNotch` sous `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
  - `sealed class SettingsFileWatcher(SettingsStore store, AppPaths paths, IUiDispatcher ui, ILogger<SettingsFileWatcher> logger) : IDisposable` avec `void Start()`.
  - `sealed class TrayIconService(NotchViewModel vm, HookInstaller installer, AppPaths paths, SettingsStore settings, ILogger<TrayIconService> logger) : IDisposable` avec `void Start(Func<Task> quit, Action openSettings)`.

Règles :
- **Icône** : anneau 32×32 dessiné avec la couleur et la fraction de la cellule, point ambre au centre en Attention ; info-bulle `TrayText`. Visible si `TrayIconVisible` ou si le mode est Masqué. Clic gauche → `PeekCommand`.
- **Menu** : Rafraîchir maintenant · Garder la carte ouverte (coché selon `Locked`) · — · Hooks Claude Code installés (coché selon `IsInstalled()`, bascule installation/désinstallation, erreur affichée dans une boîte de message en français) · Démarrer avec Windows (coché, bascule) · — · Réglages… · Ouvrir le dossier de données · Diagnostic (lance `DoctorCommand` et ouvre `logs\doctor.txt`) · — · Quitter.
- **Relecture à chaud** : `FileSystemWatcher` sur `settings.json`, anti-rebond 300 ms ; si le texte du fichier diffère du dernier texte connu, `Load()` puis `Save()` du résultat (ce qui lève `Changed` et normalise le fichier). Le dernier texte connu est relu après chaque `Changed`, pour que les écritures de l'application elle-même ne relancent pas de cycle. Un changement de port est journalisé : il ne prend effet qu'au redémarrage.

- [ ] **Step 1 : Démarrage avec Windows**

`src/UsageNotch.App/Interop/AutoStart.cs` :
```csharp
using Microsoft.Win32;

namespace UsageNotch.App.Interop;

public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "UsageNotch";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void Set(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{exePath}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
```

- [ ] **Step 2 : Relecture à chaud**

`src/UsageNotch.App/Hosting/SettingsFileWatcher.cs` :
```csharp
using System.IO;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

/// <summary>Applique à chaud les modifications faites à la main dans settings.json.</summary>
public sealed class SettingsFileWatcher(SettingsStore store, AppPaths paths, IUiDispatcher ui, ILogger<SettingsFileWatcher> logger) : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private string? _lastText;
    private Action<Settings>? _onSaved;

    public void Start()
    {
        _lastText = ReadText();
        _onSaved = _ => _lastText = ReadText();
        store.Changed += _onSaved;

        _watcher = new FileSystemWatcher(paths.DataDirectory, Path.GetFileName(paths.SettingsFile))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;
        _debounce = new Timer(_ => ui.Post(Reload), null, Timeout.Infinite, Timeout.Infinite);
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) =>
        _debounce?.Change(Debounce, Timeout.InfiniteTimeSpan);

    private void Reload()
    {
        var text = ReadText();
        if (text is null || text == _lastText) return;

        var previousPort = store.Current.Port;
        var loaded = store.Load();
        store.Save(loaded);
        logger.LogInformation("Réglages relus depuis {Path}", paths.SettingsFile);
        if (loaded.Port != previousPort)
        {
            logger.LogWarning("Le port passe de {Old} à {New} : redémarrez UsageNotch pour l'appliquer", previousPort, loaded.Port);
        }
    }

    /// <summary>L'éditeur peut tenir le fichier ouvert un instant : quelques essais courts.</summary>
    private string? ReadText()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return File.Exists(paths.SettingsFile) ? File.ReadAllText(paths.SettingsFile) : null;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
        return null;
    }

    public void Dispose()
    {
        if (_onSaved is not null) store.Changed -= _onSaved;
        _watcher?.Dispose();
        _debounce?.Dispose();
    }
}
```

- [ ] **Step 3 : Icône de la zone de notification**

`src/UsageNotch.App/Tray/TrayIconService.cs` :
```csharp
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using UsageNotch.App.Converters;
using UsageNotch.App.Hosting;
using UsageNotch.App.Interop;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Pill;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Tray;

public sealed class TrayIconService(
    NotchViewModel vm,
    HookInstaller installer,
    AppPaths paths,
    SettingsStore settings,
    ILogger<TrayIconService> logger) : IDisposable
{
    private TaskbarIcon? _icon;
    private MenuItem? _lockItem;
    private MenuItem? _hooksItem;
    private MenuItem? _autoStartItem;

    public void Start(Func<Task> quit, Action openSettings)
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = vm.TrayText,
            IconSource = RenderIcon(vm.Cell),
            ContextMenu = BuildMenu(quit, openSettings),
        };
        _icon.TrayLeftMouseUp += (_, _) => vm.PeekCommand.Execute(null);
        _icon.ForceCreate(enablesEfficiencyMode: false);
        ApplyVisibility();
        vm.PropertyChanged += OnViewModelChanged;
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
                _icon.IconSource = RenderIcon(vm.Cell);
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
        _autoStartItem = Item("Démarrer avec Windows", ToggleAutoStart);
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new Separator());

        menu.Items.Add(Item("Réglages…", openSettings));
        menu.Items.Add(Item("Ouvrir le dossier de données", OpenDataDirectory));
        menu.Items.Add(Item("Diagnostic", OpenDiagnostic));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Quitter", () => _ = quit()));

        menu.Opened += (_, _) =>
        {
            _lockItem.IsChecked = vm.Locked;
            _hooksItem.IsChecked = SafeIsInstalled();
            _autoStartItem.IsChecked = AutoStart.IsEnabled();
        };
        return menu;
    }

    private static MenuItem Item(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        return item;
    }

    private bool SafeIsInstalled()
    {
        try { return installer.IsInstalled(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    private void ToggleHooks()
    {
        try
        {
            var message = SafeIsInstalled() ? installer.Uninstall() : installer.Install();
            logger.LogInformation("Hooks Claude Code : {Message}", message);
        }
        catch (FileNotFoundException)
        {
            Warn($"Exécutable hook introuvable : {paths.HookExe}");
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Warn(e.Message);
        }
    }

    private void ToggleAutoStart()
    {
        try
        {
            AutoStart.Set(!AutoStart.IsEnabled(), Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "UsageNotch.App.exe"));
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Warn("Impossible de modifier le démarrage avec Windows : " + e.Message);
        }
    }

    private void OpenDataDirectory() => StartProcess("explorer.exe", $"\"{paths.DataDirectory}\"");

    private void OpenDiagnostic()
    {
        DoctorCommand.Run(paths, settings);
        StartProcess("notepad.exe", $"\"{Path.Combine(paths.LogsDirectory, "doctor.txt")}\"");
    }

    private void StartProcess(string file, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false });
        }
        catch (Win32Exception e)
        {
            logger.LogWarning(e, "Impossible de lancer {File}", file);
        }
    }

    private void Warn(string message)
    {
        logger.LogWarning("{Message}", message);
        MessageBox.Show(message, "UsageNotch", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>Anneau 32×32 à la couleur du niveau, sur fond de pilule ; point ambre au centre si une session attend.</summary>
    private static ImageSource RenderIcon(CellModel cell)
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
        return bitmap;
    }

    public void Dispose()
    {
        vm.PropertyChanged -= OnViewModelChanged;
        _icon?.Dispose();
    }
}
```

- [ ] **Step 4 : Coquille (version de cette tâche)**

`src/UsageNotch.App/Hosting/NotchShell.cs` (remplacer tout le contenu) :
```csharp
using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.Logging;
using UsageNotch.App.Tray;
using UsageNotch.App.Views;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Hosting;

/// <summary>Crée et relie tout ce qui s'affiche. Doit être démarrée avant host.StartAsync (contrat du Plan 1).</summary>
public sealed class NotchShell(
    NotchViewModel viewModel,
    HookListener listener,
    NotchPlacer placer,
    SettingsStore settings,
    AppPaths paths,
    TrayIconService tray,
    SettingsFileWatcher watcher,
    IUiDispatcher ui,
    ILogger<NotchShell> logger) : IDisposable
{
    private Action? _onOpenSettings;
    private PillWindow? _pill;
    private CardWindow? _card;

    public void Start()
    {
        _onOpenSettings = () => ui.Post(() => viewModel.PeekCommand.Execute(null));
        listener.OpenSettingsRequested += _onOpenSettings;

        _pill = new PillWindow(viewModel, placer, settings, QuitAsync, OpenSettingsFile);
        _card = new CardWindow(viewModel, placer, _pill);
        if (settings.Current.Visibility != VisibilityMode.Hidden) _pill.Show();

        tray.Start(QuitAsync, OpenSettingsFile);
        watcher.Start();

        logger.LogInformation("Coquille démarrée");
    }

    private static Task QuitAsync() => ((App)Application.Current).QuitAsync(userInitiated: true);

    /// <summary>Plan 2 : les réglages s'éditent dans settings.json ; le Plan 3 remplacera ceci par la fenêtre de réglages.</summary>
    private void OpenSettingsFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{paths.SettingsFile}\"") { UseShellExecute = false });
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            logger.LogWarning(e, "Impossible d'ouvrir {Path}", paths.SettingsFile);
        }
    }

    public void Dispose()
    {
        if (_onOpenSettings is not null) listener.OpenSettingsRequested -= _onOpenSettings;
        watcher.Dispose();
        tray.Dispose();
        _card?.Close();
        _pill?.Close();
        viewModel.Dispose();
    }
}
```

- [ ] **Step 5 : Construire et vérifier**

Run: `dotnet build UsageNotch.sln` puis `dotnet test` — aucun avertissement, tout passe.

Démarrer la démo (`--demo`), puis :
1. Relecture à chaud : écrire `"edge": "Left"` dans `%TEMP%\UsageNotch-demo\settings.json` (lire le fichier, remplacer la valeur, réécrire en UTF-8), attendre 1 s, capture. Expected : la pilule est passée au bord gauche **sans redémarrage**. Remettre `"edge": "Right"`, attendre 1 s, capture : retour à droite. Passer `"visibility": "Hidden"` : la pilule disparaît ; remettre `"Expanded"` : elle réapparaît.
2. Journal : `logs\usagenotch-<date>.log` contient « Réglages relus depuis » deux à quatre fois, sans boucle (pas plus d'une ligne par modification).
3. Icône : procédure « Capture d'écran » de la zone de notification (coin inférieur droit). Expected : un petit anneau jaune. Si l'icône est rangée dans le menu de débordement, le noter dans le rapport sans le considérer comme un échec.
4. Menu « Hooks Claude Code installés » : **ne pas cliquer** sur la vraie configuration. En démo, l'installeur cible `%TEMP%\UsageNotch-demo\claude-settings.json` ; il est acceptable de le tester en ouvrant le menu de l'icône et en cliquant l'entrée si l'automatisation le permet, puis de vérifier que ce fichier de démo contient 7 entrées et que `%USERPROFILE%\.claude\settings.json` n'a pas changé (comparer sa date de modification avant/après). Sinon, se limiter aux étapes 1 à 3.
5. « Démarrer avec Windows » : ne pas l'activer pendant la vérification.

`Stop-Process` de l'application à la fin.

- [ ] **Step 6 : Commit**

`feat(app): tray icon, live settings reload and start with Windows`

---

### Task 14 : Retour au terminal d'une session

**Files:**
- Create: `src/UsageNotch.Presentation/Behavior/TerminalWindowChooser.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Behavior/TerminalWindowChooserTests.cs`
- Modify: `src/UsageNotch.App/Interop/TerminalFocus.cs` (remplace la version provisoire de la Task 10)

**Interfaces:**
- Consumes: `ISessionFocus` (Task 6) ; `NativeMethods` (Task 9).
- Produces:
  - `sealed record TopLevelWindow(nint Handle, int ProcessId)`.
  - `static class TerminalWindowChooser` : `const int MaxDepth = 8`, `IReadOnlyList<int> AncestorChain(int pid, IReadOnlyDictionary<int, int> parents)`, `nint? Choose(int pid, IReadOnlyDictionary<int, int> parents, IReadOnlyList<TopLevelWindow> windows, int selfPid)`.
  - `sealed class TerminalFocus(ILogger<TerminalFocus> logger) : ISessionFocus` (implémentation réelle).

Règle (spec §5 « Retour au terminal », affinée) : la chaîne part du PID envoyé par le hook (premier ancêtre qui n'est pas un shell) et remonte au plus 8 niveaux. Parmi les fenêtres visibles et titrées, on retient celle dont le processus — ou le parent de ce processus, cas `conhost` — est **le plus proche** dans la chaîne. Le plus proche plutôt que le plus lointain : plus haut, on tombe sur `explorer.exe`, qui possède toujours des fenêtres. Les fenêtres d'UsageNotch sont exclues.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/Behavior/TerminalWindowChooserTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Presentation.Behavior;

namespace UsageNotch.Presentation.Tests.Behavior;

public class TerminalWindowChooserTests
{
    // claude(100) → pwsh(200) → WindowsTerminal(300) → explorer(400)
    private static readonly Dictionary<int, int> Parents = new()
    {
        [100] = 200,
        [200] = 300,
        [300] = 400,
        [400] = 0,
        [500] = 200, // conhost dont le parent est le shell
        [900] = 1,   // UsageNotch lui-même
    };

    [Fact]
    public void The_chain_starts_at_the_pid_and_stops_at_zero() =>
        TerminalWindowChooser.AncestorChain(100, Parents).Should().Equal(100, 200, 300, 400);

    [Fact]
    public void The_chain_survives_a_cycle_and_a_missing_parent()
    {
        var cyclic = new Dictionary<int, int> { [1] = 2, [2] = 1 };
        TerminalWindowChooser.AncestorChain(1, cyclic).Should().Equal(1, 2);
        TerminalWindowChooser.AncestorChain(42, cyclic).Should().Equal(42);
    }

    [Fact]
    public void The_chain_is_limited_to_eight_ancestors()
    {
        var deep = Enumerable.Range(1, 20).ToDictionary(i => i, i => i + 1);
        TerminalWindowChooser.AncestorChain(1, deep).Should().HaveCount(TerminalWindowChooser.MaxDepth + 1);
    }

    [Fact]
    public void The_nearest_ancestor_window_wins_over_explorer()
    {
        var windows = new[] { new TopLevelWindow(4, 400), new TopLevelWindow(3, 300) };
        TerminalWindowChooser.Choose(100, Parents, windows, selfPid: 900).Should().Be((nint)3);
    }

    [Fact]
    public void A_conhost_window_whose_parent_is_in_the_chain_is_found()
    {
        var windows = new[] { new TopLevelWindow(4, 400), new TopLevelWindow(5, 500) };
        TerminalWindowChooser.Choose(100, Parents, windows, selfPid: 900).Should().Be((nint)5);
    }

    [Fact]
    public void Our_own_windows_are_never_chosen()
    {
        var parents = new Dictionary<int, int>(Parents) { [900] = 200 };
        var windows = new[] { new TopLevelWindow(9, 900), new TopLevelWindow(4, 400) };
        TerminalWindowChooser.Choose(100, parents, windows, selfPid: 900).Should().Be((nint)4);
    }

    [Fact]
    public void No_related_window_gives_null()
    {
        var windows = new[] { new TopLevelWindow(7, 777) };
        TerminalWindowChooser.Choose(100, Parents, windows, selfPid: 900).Should().BeNull();
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter TerminalWindowChooserTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter le choix**

`src/UsageNotch.Presentation/Behavior/TerminalWindowChooser.cs` :
```csharp
namespace UsageNotch.Presentation.Behavior;

public sealed record TopLevelWindow(nint Handle, int ProcessId);

/// <summary>Choisit la fenêtre qui héberge une session à partir de l'arbre des processus. Logique pure, sans appel système.</summary>
public static class TerminalWindowChooser
{
    public const int MaxDepth = 8;

    public static IReadOnlyList<int> AncestorChain(int pid, IReadOnlyDictionary<int, int> parents)
    {
        var chain = new List<int> { pid };
        var current = pid;
        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (!parents.TryGetValue(current, out var parent) || parent == 0 || chain.Contains(parent)) break;
            chain.Add(parent);
            current = parent;
        }
        return chain;
    }

    public static nint? Choose(int pid, IReadOnlyDictionary<int, int> parents, IReadOnlyList<TopLevelWindow> windows, int selfPid)
    {
        var chain = AncestorChain(pid, parents);
        nint? best = null;
        var bestIndex = int.MaxValue;

        foreach (var window in windows)
        {
            if (window.ProcessId == selfPid) continue;

            var index = IndexOf(chain, window.ProcessId);
            if (index < 0 && parents.TryGetValue(window.ProcessId, out var parent)) index = IndexOf(chain, parent);
            if (index < 0 || index >= bestIndex) continue;

            bestIndex = index;
            best = window.Handle;
        }
        return best;
    }

    private static int IndexOf(IReadOnlyList<int> chain, int pid)
    {
        for (var i = 0; i < chain.Count; i++)
        {
            if (chain[i] == pid) return i;
        }
        return -1;
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter TerminalWindowChooserTests`
Expected: 7 tests passés.

- [ ] **Step 5 : Implémenter l'interop**

`src/UsageNotch.App/Interop/TerminalFocus.cs` (remplacer tout le contenu) :
```csharp
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using UsageNotch.Presentation.Behavior;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Interop;

/// <summary>
/// Ramène au premier plan la fenêtre qui héberge la session et fait clignoter son bouton de barre des tâches.
/// Le clic de l'utilisateur sur la carte donne à UsageNotch le droit de changer la fenêtre active.
/// </summary>
public sealed class TerminalFocus(ILogger<TerminalFocus> logger) : ISessionFocus
{
    public bool Focus(int? parentPid)
    {
        if (parentPid is not int pid || pid <= 0) return false;

        var handle = TerminalWindowChooser.Choose(pid, ProcessParents(), VisibleTitledWindows(), Environment.ProcessId);
        if (handle is not nint hwnd)
        {
            logger.LogDebug("Aucune fenêtre trouvée pour le processus {Pid}", pid);
            return false;
        }

        if (NativeMethods.IsIconic(hwnd)) NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hwnd);
        var flash = new NativeMethods.FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.FLASHWINFO>(),
            hwnd = hwnd,
            dwFlags = NativeMethods.FLASHW_ALL | NativeMethods.FLASHW_TIMERNOFG,
            uCount = 3,
            dwTimeout = 0,
        };
        NativeMethods.FlashWindowEx(ref flash);
        return true;
    }

    private static Dictionary<int, int> ProcessParents()
    {
        var parents = new Dictionary<int, int>();
        var snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0);
        if (snapshot == NativeMethods.INVALID_HANDLE_VALUE || snapshot == 0) return parents;
        try
        {
            var entry = new NativeMethods.PROCESSENTRY32W
            {
                dwSize = (uint)Marshal.SizeOf<NativeMethods.PROCESSENTRY32W>(),
                szExeFile = "",
            };
            if (!NativeMethods.Process32FirstW(snapshot, ref entry)) return parents;
            do
            {
                parents[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
            }
            while (NativeMethods.Process32NextW(snapshot, ref entry));
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }
        return parents;
    }

    private static List<TopLevelWindow> VisibleTitledWindows()
    {
        var windows = new List<TopLevelWindow>();
        NativeMethods.EnumWindowsProc callback = (hwnd, _) =>
        {
            if (NativeMethods.IsWindowVisible(hwnd) && NativeMethods.GetWindowTextLengthW(hwnd) > 0)
            {
                NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
                windows.Add(new TopLevelWindow(hwnd, (int)processId));
            }
            return true;
        };
        NativeMethods.EnumWindows(callback, 0);
        GC.KeepAlive(callback);
        return windows;
    }
}
```

- [ ] **Step 6 : Construire et vérifier**

Run: `dotnet build UsageNotch.sln` puis `dotnet test` — aucun avertissement, tout passe.

Vérification manuelle (démo) :
```powershell
$exe = "src\UsageNotch.App\bin\Debug\net10.0-windows\UsageNotch.App.exe"
$app = Start-Process $exe -ArgumentList "--demo" -PassThru
Start-Sleep -Seconds 3
$np = Start-Process notepad.exe -PassThru
Start-Sleep -Seconds 1
Invoke-WebRequest -Method Post -Uri "http://127.0.0.1:48667/event?e=running&ppid=$($np.Id)" -Body '{"session_id":"focus-test-0001","cwd":"C:\\tmp\\focus"}' -UseBasicParsing | Out-Null
Invoke-WebRequest -Method Post -Uri "http://127.0.0.1:48667/event?e=attention&ppid=$($np.Id)" -Body '{"session_id":"focus-test-0001","message":"Test du retour"}' -UseBasicParsing | Out-Null
```
Puis procédure « Survol » sur la pilule, capture pour repérer la ligne « focus · focu » dans la carte, procédure « Clic » sur cette ligne, attendre 500 ms, et lire la fenêtre au premier plan :
```powershell
Add-Type -Namespace Probe -Name Fg -MemberDefinition '[DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow(); [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr h, out uint pid);'
$pidOut = 0; [Probe.Fg]::GetWindowThreadProcessId([Probe.Fg]::GetForegroundWindow(), [ref]$pidOut) | Out-Null
"foreground pid = $pidOut ; notepad pid = $($np.Id)"
Stop-Process -Id $np.Id; Stop-Process -Id $app.Id
```
Expected : les deux PID sont égaux. Si Windows refuse le changement de premier plan (le bouton du Bloc-notes clignote dans la barre des tâches à la place), le noter comme préoccupation dans le rapport au lieu d'un échec.

- [ ] **Step 7 : Commit**

`feat(app): return to the session's terminal window`

---

### Task 15 : Publication, vérification de bout en bout et notes

**Files:**
- Create: `scripts/publish.ps1`
- Modify: `.gitignore` (ajouter `publish/` s'il n'y est pas déjà — il y est depuis le Plan 1 : vérifier seulement)
- Modify: `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md` (nouvelle section « Plan 2 »)

**Interfaces:**
- Consumes: tous les projets.
- Produces: `scripts/publish.ps1 [-Output <dossier>]` — publie l'application (dépendante du framework .NET 10, win-x64) et le hook en Native AOT dans le même dossier, `publish\` par défaut.

- [ ] **Step 1 : Script de publication**

`scripts/publish.ps1` :
```powershell
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\publish')
)
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = [IO.Path]::GetFullPath($Output)

# La détection de Visual Studio par le compilateur AOT échoue si cette variable est définie (voir les notes du Plan 1).
if (Test-Path Env:NoDefaultCurrentDirectoryInExePath) { Remove-Item Env:NoDefaultCurrentDirectoryInExePath }

if (Test-Path $out) { Remove-Item -Recurse -Force $out }

dotnet publish (Join-Path $root 'src\UsageNotch.App') -c Release -r win-x64 --self-contained false -o $out
if ($LASTEXITCODE -ne 0) { throw "Échec de la publication de l'application" }

$hookOut = Join-Path $out 'hook-aot'
dotnet publish (Join-Path $root 'src\UsageNotch.Hook') -c Release -r win-x64 -o $hookOut
if ($LASTEXITCODE -ne 0) { throw 'Échec de la publication du hook' }

Get-ChildItem $out -Filter 'UsageNotch.Hook.*' -File | Remove-Item
Copy-Item (Join-Path $hookOut 'UsageNotch.Hook.exe') $out
Remove-Item -Recurse -Force $hookOut

Write-Host "UsageNotch publié dans $out"
```

- [ ] **Step 2 : Vérification complète**

Run :
```powershell
dotnet build UsageNotch.sln
dotnet test
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
Get-ChildItem publish -Filter 'UsageNotch.*.exe' | Select-Object Name, Length
& publish\UsageNotch.App.exe doctor --demo | Out-String
```
Expected :
- compilation sans avertissement, tous les tests passent (Core, Presentation) ;
- `publish\UsageNotch.App.exe` et `publish\UsageNotch.Hook.exe` présents, le hook pèse environ 2 Mo (natif, sans `.dll` à côté) ;
- le diagnostic indique « Exécutable hook : présent — …\publish\UsageNotch.Hook.exe ».

Puis démarrer `publish\UsageNotch.App.exe --demo`, procédure « Capture d'écran » : la pilule est visible comme en Task 11. Envoyer un événement à la démo (port 48667) :
```powershell
Invoke-WebRequest -Method Post -Uri "http://127.0.0.1:48667/event?e=running&ppid=1" -Body '{"session_id":"publish-check-01","cwd":"C:\\tmp\\publish"}' -UseBasicParsing | Out-Null
```
Procédure « Survol » sur la pilule, capture : la carte liste « publish · publ » en cours. `Stop-Process` de l'application.

Le hook publié vise le port réel 48666 (il lit les réglages réels, absents ici). Le vérifier contre un récepteur PowerShell, sans application ni configuration Claude Code :
```powershell
if (Get-NetTCPConnection -LocalPort 48666 -State Listen -ErrorAction SilentlyContinue) { throw "port 48666 occupé" }
$l = New-Object System.Net.HttpListener; $l.Prefixes.Add("http://127.0.0.1:48666/"); $l.Start()
$job = Start-Job -ScriptBlock { param($e) '{"session_id":"publish-check-02"}' | & $e done } -ArgumentList (Resolve-Path publish\UsageNotch.Hook.exe).Path
$task = $l.GetContextAsync()
if ($task.Wait(5000)) { $c = $task.Result; $b = (New-Object IO.StreamReader($c.Request.InputStream)).ReadToEnd(); "$($c.Request.HttpMethod) $($c.Request.RawUrl) body=$b"; $c.Response.StatusCode = 400; $c.Response.Close() } else { "aucune requête en 5 s" }
$l.Stop(); Wait-Job $job -Timeout 5 | Out-Null; Remove-Job $job -Force
```
Attendu : `POST /event?e=done&ppid=<nombre> body={"session_id":"publish-check-02"}`.

- [ ] **Step 3 : Notes du Plan 2**

Ajouter à la fin de `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md` :
```markdown
## Plan 2 — application

### Utilisation

- Publier : `powershell -ExecutionPolicy Bypass -File scripts\publish.ps1` (sortie dans `publish\`).
- Lancer : `publish\UsageNotch.App.exe`. Démonstration sans toucher à la vraie configuration : `--demo`. Diagnostic : `doctor`.
- Installer les hooks Claude Code : menu de l'icône de notification › « Hooks Claude Code installés ». Une sauvegarde horodatée de `~/.claude/settings.json` est écrite avant la modification.
- Réglages : menu › « Réglages… » ouvre `settings.json` ; les changements s'appliquent à l'enregistrement (sauf le port, au redémarrage).

### Écarts par rapport à la spec

- Passage des clics par la transparence par pixel des fenêtres en couches, pas par `WM_NCHITTEST` : `HTTRANSPARENT` ne transmet le clic qu'aux fenêtres du même thread.
- Déclarations Win32 écrites à la main (`NativeMethods`) au lieu de CsWin32.
- Une seule cellule : le clic gauche sur la pilule verrouille la carte ; « Rafraîchir maintenant » est dans le menu contextuel.
- Clic gauche sur l'icône de notification : aperçu de la carte 5 s (fenêtre de réglages au Plan 3). Une seconde instance lancée à la main fait de même.
- Mode Replié par glissement du contenu dans une fenêtre de taille fixe.
- Retour au terminal : la fenêtre retenue est l'ancêtre le plus proche, pas le plus lointain.

### Reste à faire (Plan 3)

- Fenêtre de réglages en cinq pages avec aperçu en direct (spec §7).
```

- [ ] **Step 4 : Commit**

`chore: publish script and Plan 2 notes`

Le premier lancement réel (sans `--demo`), l'installation des hooks sur la vraie configuration Claude Code et « Démarrer avec Windows » restent à l'utilisateur : ils modifient sa configuration personnelle.

---

## Auto-relecture

**Couverture de la spec.**
- §3 : Presentation + App ; hébergement et services de fond (Task 10).
- §5 : affichage des sessions et transitions (Tasks 3, 4, 6), auto-ouverture et son (Task 6, vérifié en Task 12), retour au terminal (Task 14).
- §6 : deux fenêtres (Tasks 11, 12), non-activation (Tasks 9, 11, 12), passage des clics (écart consigné, vérifié en Task 11), survol et filet de sécurité (Tasks 5, 12), verrou (Tasks 5, 11), menu contextuel (Task 11), animations (Tasks 11, 12), DPI par écran (Tasks 9, 11), icône de notification (Task 13).
- §7 : thèmes et seuils (Task 6, via Core), échelle et densité (Tasks 2, 11), modes de visibilité (Tasks 5, 11, 13), placement et glisser Alt (Task 11).
  La fenêtre de réglages est au Plan 3.
- §8 : écrans par identifiant et repli (Task 11 via Core), changements d'affichage (Task 11), instance unique (Task 10), démarrage avec Windows (Task 13).
- §9 : journal fichier (Task 8), `doctor` et `--demo` (Tasks 7, 9, 10), exceptions non gérées journalisées (Task 10).

**Cohérence des types.**
- `CellModel` et `CardModel` (Tasks 2, 3) sont liés tels quels par les fenêtres (Tasks 11, 12).
- `NotchViewModel` (Task 6) expose exactement les membres utilisés par `PillWindow`, `CardWindow` et `TrayIconService`.
- `PlacementResult` et `NotchPlacer` (Task 11) sont consommés par `CardWindow` (Task 12).
- `ProgressRing.ArcGeometry` (Task 11) sert à `TrayIconService` (Task 13).
- `TerminalFocus` garde la même signature entre les Tasks 10 et 14 ; seul son constructeur gagne un `ILogger`, résolu par le conteneur.
- `NotchShell` est donné en entier à chaque changement (Tasks 10, 11, 12, 13).
