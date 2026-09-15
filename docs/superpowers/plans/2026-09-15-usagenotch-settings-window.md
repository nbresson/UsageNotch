# UsageNotch — Plan 3 : la fenêtre de réglages

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Livrer la fenêtre de réglages de la spec §7 : navigation latérale, cinq pages (Apparence, Position, Comportement, Claude Code, À propos) et un aperçu en direct de la pilule. Chaque modification s'applique à la vraie pilule sans redémarrage, sauf le port.

**Architecture:**
- **Présentation.** Toute la logique vit dans `UsageNotch.Presentation` (net10.0, sans WPF), testée par xUnit :
  - un brouillon de réglages (`SettingsDraft`) qui applique chaque modification comme une fonction sur les réglages enregistrés et enregistre au plus tard 250 ms après ;
  - un ViewModel par page et un ViewModel racine ;
  - le modèle d'aperçu et la miniature des écrans.
- **Application.** `UsageNotch.App` ne reçoit que les vues WPF, deux contrôles dessinés en code (aperçu, miniature des écrans) et les services système (sélecteur de couleur Windows, démarrage avec Windows, hooks, ouverture de fichiers). Tout se vérifie en `--demo`, par UI Automation et captures d'écran.

**Tech Stack:** .NET 10 (SDK 10.0.401), C# 14, WPF, CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 10.0.12, H.NotifyIcon.Wpf 2.4.1, xUnit 2.9.2, FluentAssertions 7.2.0, Microsoft.Extensions.TimeProvider.Testing 10.10.0. Aucune nouvelle dépendance.

**Spec:** `docs/superpowers/specs/2026-09-14-usagenotch-design.md` (§6 « Icône de zone de notification », §7 en entier, §8 « Démarrage et instance unique », §9 « Journalisation »).
**Contrats et arbitrages hérités :** `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md` (sections Plan 1 et Plan 2, dont « Reste à faire (Plan 3) »).

## Découpage

Tâches 1 à 7 : bibliothèque Presentation, entièrement testée (TDD). Tâches 8 à 11 : application WPF, vérifiée à l'exécution en démo. Tâche 12 : vérification de bout en bout, publication et notes.

## Écarts assumés par rapport à la spec

- **Aperçu partagé.** L'aperçu est en haut de la fenêtre, commun aux cinq pages, plutôt que répété dans chaque page. Il montre trois pilules d'exemple, une par niveau d'usage (modéré, vigilance, critique), avec les trois états de session. En mode Replié, la bande de chaque exemple est dessinée à côté de sa pilule. L'aperçu est statique : aucune animation en boucle, donc aucun coût processeur au repos.
- **Opacité.** `Theme.PillOpacity` s'applique à la forme de la pilule (fond et contour), pas au contenu ni à la carte : l'anneau, le pourcentage et la carte restent lisibles. Le Plan 2 ne l'appliquait nulle part ; la Task 8 corrige ce manque.
- **Opacité, seuils et couleurs relèvent du thème.** Les modifier depuis un préréglage passe au thème Personnalisé, initialisé depuis le thème affiché (règle de la spec pour les couleurs, étendue à l'opacité et aux seuils, qui font partie de `Theme`).
- **Sélecteur de couleur.** Champ `#RRGGBB` éditable plus la boîte de dialogue « Couleurs » de Windows (`ChooseColorW`), au lieu d'un sélecteur dessiné en WPF.
- **Port.** Un changement de port est enregistré puis annoncé « utilisé au prochain démarrage » : le récepteur de hooks n'est pas relancé à chaud.
- **Enregistrement.** Chaque modification apparaît aussitôt dans l'aperçu et arrive à la vraie pilule en 250 ms au plus (enregistrement groupé pendant un glisser de curseur). La fermeture ou la perte de focus de la fenêtre enregistre immédiatement.
- **Clic gauche sur l'icône de notification.** Il ouvre les réglages, comme le prévoit la spec §6 ; l'aperçu de carte 5 s du Plan 2 disparaît de ce clic (l'ouverture automatique sur transition reste).
- **Diagnostic depuis l'interface.** Le menu de l'icône et la page À propos écrivent `logs\doctor.txt` et l'ouvrent, sans s'attacher à une console (point laissé en l'état au Plan 2).

## Global Constraints

- Cibles : `net10.0` pour `UsageNotch.Presentation` et ses tests ; `net10.0-windows` avec `UseWPF` pour `UsageNotch.App`. SDK épinglé par `global.json` (10.0.401). `Directory.Build.props` impose `Nullable`, `ImplicitUsings`, `LangVersion 14`, `TreatWarningsAsErrors`.
- `UsageNotch.Presentation` ne référence ni WPF ni `System.Windows` : couleurs en chaînes `#RRGGBB`, temps via `TimeProvider`, services système derrière des interfaces de `UsageNotch.Presentation.Services`.
- Dans `UsageNotch.Presentation`, le type `UsageNotch.Core.Settings.Settings` s'écrit via l'alias `using CoreSettings = UsageNotch.Core.Settings.Settings;` (le nom `Settings` est aussi un espace de noms). Aucun espace de noms nommé `Settings` n'est créé : les réglages de l'interface vivent dans `UsageNotch.Presentation.Preferences` et `UsageNotch.App.Views.Preferences`.
- Tous les textes affichés sont en français. Pourcentages : nombre entier, espace insécable U+00A0, `%` (écrire `FrenchText.Nbsp` dans le code et les tests, jamais le caractère littéral).
- Aucun jeton, aucun contenu de prompt dans les journaux ni dans l'interface.
- Réglages : lecture de `SettingsStore.Current`, écriture uniquement par `SettingsStore.Save`, sur le thread UI. Chaque écriture de la fenêtre passe par `SettingsDraft`.
- Toutes les modifications s'appliquent sans redémarrage, sauf le port (spec §7, écart consigné).
- Contrats des Plans 1 et 2 :
  - « Quitter » met `AutoLaunch` à `false` et rien ne doit l'écraser ensuite.
  - Les abonnements aux magasins sont faits avant `host.StartAsync`.
  - Les événements des magasins et du récepteur arrivent hors du thread UI et passent par `IUiDispatcher`.
  - La démo est isolée (données `%TEMP%\UsageNotch-demo`, mutex `Local\UsageNotch-demo`, port 48667) et ne touche jamais la valeur Run de HKCU.
- Fenêtre de réglages : titre exact `UsageNotch — réglages` (tiret cadratin U+2014), fenêtre classique activable, une seule instance.
- Constantes :

  | Réglage | Valeur |
  |---|---|
  | Délai d'enregistrement du brouillon | 250 ms |
  | Miniature des écrans | 360 × 150 DIP, marge 6 |
  | Échelle | 40 % à 150 % |
  | Bande repliée | 2 à 12 px |
  | Opacité | 0,2 à 1 |
  | Seuil de vigilance | 0,05 à 0,95 |
  | Seuil critique | au moins vigilance + 0,05, au plus 1 |
  | Port | 1024 à 65535 |
- Version de l'application : `0.3.0` (`<Version>` du projet App), affichée « UsageNotch 0.3.0 ».
- Sécurité des vérifications manuelles :
  - N'exécuter l'application qu'avec `--demo` (ou `doctor --demo`).
  - Ne jamais lire ni modifier `%USERPROFILE%\.claude\settings.json` ni `.credentials.json`.
  - Ne jamais cocher « Démarrer avec Windows ».
  - Installer les hooks uniquement quand le chemin affiché est sous `%TEMP%\UsageNotch-demo`.
  - Fermer tout processus lancé (application, Bloc-notes) et laisser les ports 48666 et 48667 libres.
  - Supprimer `%TEMP%\UsageNotch-demo\settings.json` à la fin de chaque tâche visuelle.
- Commits fréquents, un par tâche au minimum, messages en anglais `type(scope): description`, fichier de message en UTF-8 sans BOM, terminés par :
  ```
  Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01DhmM4QvqWKwELXSCEZcweW
  ```

## Procédures de vérification

Les tâches 8 à 12 se vérifient à l'exécution, en PowerShell (Windows PowerShell 5.1). Enregistrer un script `.ps1` en UTF-8 **avec** BOM, sinon le tiret cadratin et les accents sont mal lus. Captures et scripts vont dans le dossier scratchpad de la session, jamais dans le dépôt. Lire les captures avec l'outil de lecture d'image.

**Préparation** (une fois par session PowerShell) :
```powershell
Add-Type -Namespace Verify -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern System.IntPtr SetThreadDpiAwarenessContext(System.IntPtr ctx);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern System.IntPtr FindWindow(string cls, string title);
[DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr h, out RECT r);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@
[Verify.Win]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null   # coordonnées physiques
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$scratch = '<dossier scratchpad de la session>'
$exe = (Resolve-Path 'src\UsageNotch.App\bin\Debug\net10.0-windows\UsageNotch.App.exe').Path
$demoDir = Join-Path $env:TEMP 'UsageNotch-demo'
$UIA = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]

function Get-WindowRect([string]$title) {
    $h = [Verify.Win]::FindWindow([NullString]::Value, $title)   # $null deviendrait "" en PowerShell 5.1
    $r = New-Object Verify.Win+RECT
    [Verify.Win]::GetWindowRect($h, [ref]$r) | Out-Null
    [pscustomobject]@{ Handle = $h; Left = $r.Left; Top = $r.Top; Right = $r.Right; Bottom = $r.Bottom;
                       Width = $r.Right - $r.Left; Height = $r.Bottom - $r.Top }
}
function Save-Shot([string]$name, $rect) {
    $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size); $g.Dispose()
    if ($rect) {
        $crop = New-Object System.Drawing.Rectangle ($rect.Left - $b.Left - 20), ($rect.Top - $b.Top - 20), ($rect.Width + 40), ($rect.Height + 40)
        $crop.Intersect((New-Object System.Drawing.Rectangle 0, 0, $b.Width, $b.Height))
        $part = $bmp.Clone($crop, $bmp.PixelFormat); $bmp.Dispose(); $bmp = $part
    }
    $bmp.Save((Join-Path $scratch "shot-$name.png")); $bmp.Dispose()
}
function Get-SettingsWindow {
    $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::NameProperty, 'UsageNotch — réglages')
    for ($i = 0; $i -lt 40; $i++) { $w = $UIA::RootElement.FindFirst($Scope::Children, $cond); if ($w) { return $w }; Start-Sleep -Milliseconds 250 }
    throw 'fenêtre de réglages introuvable'
}
function Find-Ui([string]$id) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::AutomationIdProperty, $id)
    $e = (Get-SettingsWindow).FindFirst($Scope::Descendants, $cond)
    if (-not $e) { throw "élément $id introuvable" }
    $e
}
function Find-Text([string]$text) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::NameProperty, $text)
    (Get-SettingsWindow).FindFirst($Scope::Descendants, $cond)
}
function Select-Page([string]$title) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::NameProperty, $title)
    $item = (Find-Ui 'Nav').FindFirst($Scope::Children, $cond)
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 400
}
function Set-Combo([string]$id, [string]$label) {
    $c = Find-Ui $id
    $ec = $c.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $ec.Expand(); Start-Sleep -Milliseconds 250
    $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::NameProperty, $label)
    $item = $c.FindFirst($Scope::Descendants, $cond)
    if (-not $item) { throw "choix « $label » introuvable dans $id" }
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $ec.Collapse(); Start-Sleep -Milliseconds 600
}
function Get-ComboText([string]$id) {
    (Find-Ui $id).GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection() | ForEach-Object { $_.Current.Name }
}
function Set-Range([string]$id, [double]$value) {
    (Find-Ui $id).GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue($value); Start-Sleep -Milliseconds 600
}
function Set-Text([string]$id, [string]$text) {
    (Find-Ui $id).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text)
    (Find-Ui 'Nav').SetFocus()   # la perte de focus pousse la saisie vers le ViewModel
    Start-Sleep -Milliseconds 600
}
function Get-Text([string]$id) { (Find-Ui $id).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value }
function Invoke-Ui([string]$id) { (Find-Ui $id).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 600 }
function Switch-Ui([string]$id) { (Find-Ui $id).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep -Milliseconds 600 }
function Close-Settings { (Get-SettingsWindow).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close(); Start-Sleep -Milliseconds 500 }
function Get-DemoSettings { Get-Content (Join-Path $demoDir 'settings.json') -Raw -Encoding UTF8 | ConvertFrom-Json }
```

**Démarrer la démo, ouvrir les réglages, arrêter** :
```powershell
foreach ($p in 48666, 48667) { if (Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue) { throw "port $p occupé" } }
Remove-Item (Join-Path $demoDir 'settings.json') -ErrorAction SilentlyContinue   # départ des réglages de démo par défaut
$app = Start-Process $exe -ArgumentList '--demo' -PassThru
Start-Sleep -Seconds 4
Start-Process $exe -ArgumentList '--demo' -Wait   # seconde instance : demande à la première d'ouvrir les réglages, puis quitte
$w = Get-SettingsWindow
# … étapes …
Stop-Process -Id $app.Id
Remove-Item (Join-Path $demoDir 'settings.json') -ErrorAction SilentlyContinue
```
Avant la Task 9, la seconde instance n'ouvre encore aucune fenêtre : la Task 8 démarre la démo sans la lancer.

**Mesure processeur** (20 s, en % d'un cœur) :
```powershell
$t0 = (Get-Process -Id $app.Id).TotalProcessorTime; Start-Sleep -Seconds 20
$cpu = ((Get-Process -Id $app.Id).TotalProcessorTime - $t0).TotalMilliseconds / 20000 * 100
"{0:N2} % d'un cœur" -f $cpu
```

Pendant les vérifications, ne jamais invoquer « Ouvrir le dossier… » ni « Ouvrir settings.json », sauf mention contraire de l'étape. Ils ouvrent l'Explorateur ou le Bloc-notes, difficiles à refermer proprement. Ne jamais cliquer sur l'icône de notification.

## Structure des fichiers

```
src/UsageNotch.Presentation/
  Formatting/HexColor.cs                 saisie #RRGGBB, conversion COLORREF
  Preferences/Choices.cs                 Choice<T> et listes de choix en français
  Preferences/MonitorChoices.cs          liste « Écran principal » + écrans numérotés
  Preferences/SettingsDraft.cs           brouillon : modifications rejouées, enregistrement groupé
  Preferences/SettingsPreview.cs         PreviewModel : trois pilules d'exemple
  Preferences/MonitorMap.cs              miniature des écrans et repère de la pilule
  Preferences/ColorSlot.cs               une couleur éditable du thème
  Preferences/AppearancePageViewModel.cs
  Preferences/PositionPageViewModel.cs
  Preferences/BehaviorPageViewModel.cs
  Preferences/ClaudeCodePageViewModel.cs
  Preferences/SettingsEnvironment.cs     informations en lecture seule (version, dossiers, port)
  Preferences/AboutPageViewModel.cs
  Preferences/SettingsViewModel.cs       pages, page choisie, aperçu, fabrique
  Services/IColorPicker.cs
  Services/IMonitorSource.cs
  Services/IAutoStart.cs
  Services/IHookSetup.cs                 et HookSetupResult
  Services/IShellActions.cs
src/UsageNotch.App/
  UsageNotch.App.csproj                  <Version>0.3.0</Version>
  Interop/NativeMethods.cs               + ChooseColorW, AllowSetForegroundWindow
  Interop/MonitorService.cs              implémente IMonitorSource
  Interop/NativeColorPicker.cs           IColorPicker
  Hosting/AutoStartService.cs            IAutoStart (indisponible en démo)
  Hosting/HookSetupService.cs            IHookSetup (erreurs traduites en messages)
  Hosting/ShellActions.cs                IShellActions
  Hosting/DoctorCommand.cs               WriteReport séparé de l'affichage console
  Hosting/SingleInstance.cs              autorise l'instance existante à passer au premier plan
  Hosting/SettingsWindowHost.cs          une seule fenêtre de réglages
  Hosting/NotchShell.cs                  routes vers la fenêtre de réglages
  Hosting/AppHost.cs                     enregistrements DI
  Tray/TrayIconService.cs                hooks et diagnostic via les services, clic gauche → réglages
  Controls/PillPreview.cs                aperçu statique des pilules d'exemple
  Controls/MonitorMapView.cs             miniature cliquable des écrans
  Views/PillWindow.xaml                  opacité du thème sur la forme
  Views/Preferences/PreferencesStyles.xaml
  Views/Preferences/SettingsWindow.xaml(.cs)
  Views/Preferences/AppearancePage.xaml(.cs)
  Views/Preferences/PositionPage.xaml(.cs)
  Views/Preferences/BehaviorPage.xaml(.cs)
  Views/Preferences/ClaudeCodePage.xaml(.cs)
  Views/Preferences/AboutPage.xaml(.cs)
tests/UsageNotch.Presentation.Tests/
  Formatting/HexColorTests.cs
  Preferences/ChoicesTests.cs
  Preferences/MonitorChoicesTests.cs
  Preferences/SettingsDraftTests.cs
  Preferences/SettingsPreviewTests.cs
  Preferences/MonitorMapTests.cs
  Preferences/DraftFixture.cs
  Preferences/Fakes.cs                   faux services partagés
  Preferences/AppearancePageViewModelTests.cs
  Preferences/PositionPageViewModelTests.cs
  Preferences/BehaviorPageViewModelTests.cs
  Preferences/ClaudeCodePageViewModelTests.cs
  Preferences/AboutPageViewModelTests.cs
  Preferences/SettingsViewModelTests.cs
docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md   section « Plan 3 »
```

Tous les faux services de test sont dans `Preferences/Fakes.cs`, créé en entier dès la Task 4, pour qu'aucune tâche n'ait à modifier un fichier de test d'une autre. Les interfaces qu'ils implémentent sont donc toutes créées en Task 4.

---

### Task 1 : Couleurs saisies et listes de choix

**Files:**
- Create: `src/UsageNotch.Presentation/Formatting/HexColor.cs`
- Create: `src/UsageNotch.Presentation/Preferences/Choices.cs`
- Create: `src/UsageNotch.Presentation/Preferences/MonitorChoices.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Formatting/HexColorTests.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Preferences/ChoicesTests.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Preferences/MonitorChoicesTests.cs`

**Interfaces:**
- Consumes: `ThemePreset`, `CellContent`, `ScreenEdge`, `VisibilityMode` (Core.Settings) ; `MonitorInfo`, `PixelRect` (Core.Placement) ; `FrenchText.Nbsp` (Presentation.Formatting).
- Produces :
  - `static class HexColor` : `bool TryNormalize(string? input, out string hex)`, `uint ToColorRef(string hex)`, `string FromColorRef(uint colorRef)`.
  - `sealed record Choice<T>(T Value, string Label)` dont `ToString()` rend `Label`.
  - `static class Choices` : `ThemePresets`, `CellContents`, `Edges`, `Visibilities` (listes `IReadOnlyList<Choice<…>>`), `Sounds` (`IReadOnlyList<Choice<string>>`), `string SoundLabel(string name)`.
  - `static class MonitorChoices` : `const string PrimaryKey = ""`, `IReadOnlyList<MonitorInfo> Ordered(IReadOnlyList<MonitorInfo>)`, `string Describe(MonitorInfo, int number)`, `IReadOnlyList<Choice<string>> Build(IReadOnlyList<MonitorInfo>, string? selectedDeviceId)`, `string KeyFor(IReadOnlyList<Choice<string>> choices, string? deviceId)`, `string? DeviceIdFor(string? key)`.

Les listes déroulantes WPF ne savent pas sélectionner une valeur `null` : « Écran principal » a donc la clé `""`, traduite en `MonitorDeviceId = null`. `Choice<T>.ToString()` rend le libellé, que l'accessibilité (UI Automation) lit comme nom de l'élément.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/Formatting/HexColorTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Tests.Formatting;

public class HexColorTests
{
    [Theory]
    [InlineData("#28e07b", "#28E07B")]
    [InlineData("28E07B", "#28E07B")]
    [InlineData("#abc", "#AABBCC")]
    [InlineData("fff", "#FFFFFF")]
    [InlineData("  #FF4500 ", "#FF4500")]
    public void Valid_entries_are_normalised_to_uppercase_six_digits(string input, string expected)
    {
        HexColor.TryNormalize(input, out var hex).Should().BeTrue();
        hex.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGGGGG")]
    [InlineData("rouge")]
    public void Invalid_entries_are_rejected(string? input)
    {
        HexColor.TryNormalize(input, out var hex).Should().BeFalse();
        hex.Should().BeEmpty();
    }

    [Fact]
    public void Colorref_is_blue_green_red()
    {
        HexColor.ToColorRef("#FF4500").Should().Be(0x000045FFu);
        HexColor.FromColorRef(0x000045FFu).Should().Be("#FF4500");
    }

    [Fact]
    public void Colorref_round_trips()
    {
        HexColor.FromColorRef(HexColor.ToColorRef("#28e07b")).Should().Be("#28E07B");
    }

    [Fact]
    public void Colorref_of_an_invalid_colour_throws()
    {
        var act = () => HexColor.ToColorRef("rouge");
        act.Should().Throw<ArgumentException>();
    }
}
```

`tests/UsageNotch.Presentation.Tests/Preferences/ChoicesTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class ChoicesTests
{
    [Fact]
    public void A_choice_reads_as_its_label()
    {
        new Choice<ScreenEdge>(ScreenEdge.Top, "Haut").ToString().Should().Be("Haut");
    }

    [Fact]
    public void Enumerations_are_listed_in_spec_order_with_french_labels()
    {
        Choices.ThemePresets.Select(c => c.Value).Should().Equal(ThemePreset.Codenotch, ThemePreset.Monochrome, ThemePreset.SystemAccent, ThemePreset.Custom);
        Choices.ThemePresets.Select(c => c.Label).Should().Equal("Codenotch", "Monochrome", "Accent système", "Personnalisé");
        Choices.CellContents.Select(c => c.Label).Should().Equal("Anneau et pourcentage", "Anneau seul", "Pourcentage seul");
        Choices.Edges.Select(c => c.Value).Should().Equal(ScreenEdge.Right, ScreenEdge.Left, ScreenEdge.Top, ScreenEdge.Bottom);
        Choices.Edges.Select(c => c.Label).Should().Equal("Droite", "Gauche", "Haut", "Bas");
        Choices.Visibilities.Select(c => c.Label).Should().Equal("Déplié", "Replié", "Masqué");
    }

    [Fact]
    public void Sounds_are_the_five_windows_system_sounds()
    {
        Choices.Sounds.Select(c => c.Value).Should().Equal("Asterisk", "Beep", "Exclamation", "Hand", "Question");
        Choices.Sounds.Select(c => c.Label).Should().Equal("Astérisque", "Bip", "Exclamation", "Arrêt critique", "Question");
    }

    [Fact]
    public void An_unknown_sound_keeps_its_name_as_label()
    {
        Choices.SoundLabel("Hand").Should().Be("Arrêt critique");
        Choices.SoundLabel("Tada").Should().Be("Tada");
    }
}
```

`tests/UsageNotch.Presentation.Tests/Preferences/MonitorChoicesTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Placement;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class MonitorChoicesTests
{
    private static MonitorInfo M(string id, bool primary, int x, int y, int w, int h, double scale) =>
        new(id, primary, new PixelRect(x, y, w, h), new PixelRect(x, y, w, h - 48), scale);

    private static readonly MonitorInfo Main = M(@"\\.\DISPLAY1", true, 0, 0, 2560, 1440, 1.5);
    private static readonly MonitorInfo Side = M(@"\\.\DISPLAY2", false, 2560, 0, 1920, 1080, 1.0);

    [Fact]
    public void Primary_comes_first_then_monitors_from_left_to_right()
    {
        var choices = MonitorChoices.Build([Side, Main], null);

        choices.Select(c => c.Value).Should().Equal("", @"\\.\DISPLAY1", @"\\.\DISPLAY2");
        choices.Select(c => c.Label).Should().Equal(
            "Écran principal",
            "Écran 1 — 2560 × 1440, 150" + FrenchText.Nbsp + "% (principal)",
            "Écran 2 — 1920 × 1080, 100" + FrenchText.Nbsp + "%");
    }

    [Fact]
    public void A_monitor_left_of_the_primary_is_numbered_first()
    {
        var left = M(@"\\.\DISPLAY3", false, -1920, 0, 1920, 1080, 1.0);
        MonitorChoices.Ordered([Main, left]).Select(m => m.DeviceId).Should().Equal(@"\\.\DISPLAY3", @"\\.\DISPLAY1");
    }

    [Fact]
    public void An_absent_saved_monitor_stays_listed()
    {
        var choices = MonitorChoices.Build([Main], @"\\.\DISPLAY9");

        choices.Should().HaveCount(3);
        choices[^1].Value.Should().Be(@"\\.\DISPLAY9");
        choices[^1].Label.Should().Be(@"Écran absent (\\.\DISPLAY9) — pilule sur l'écran principal");
    }

    [Fact]
    public void A_saved_monitor_with_another_case_is_not_listed_twice()
    {
        var choices = MonitorChoices.Build([Main, Side], @"\\.\display2");

        choices.Should().HaveCount(3);
        MonitorChoices.KeyFor(choices, @"\\.\display2").Should().Be(@"\\.\DISPLAY2");
    }

    [Fact]
    public void Keys_and_device_ids_map_the_primary_choice_to_null()
    {
        var choices = MonitorChoices.Build([Main], null);

        MonitorChoices.KeyFor(choices, null).Should().Be(MonitorChoices.PrimaryKey);
        MonitorChoices.DeviceIdFor(MonitorChoices.PrimaryKey).Should().BeNull();
        MonitorChoices.DeviceIdFor(null).Should().BeNull();
        MonitorChoices.DeviceIdFor(@"\\.\DISPLAY1").Should().Be(@"\\.\DISPLAY1");
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~HexColorTests|FullyQualifiedName~ChoicesTests"`
Expected: erreur de compilation (types absents). Le filtre `ChoicesTests` couvre aussi `MonitorChoicesTests`.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Presentation/Formatting/HexColor.cs` :
```csharp
using System.Globalization;

namespace UsageNotch.Presentation.Formatting;

/// <summary>Couleurs saisies par l'utilisateur. Forme normalisée : <c>#RRGGBB</c> en majuscules.</summary>
public static class HexColor
{
    /// <summary>Accepte « #RRGGBB », « RRGGBB », « #RGB » ou « RGB », espaces autour ignorés, casse indifférente.</summary>
    public static bool TryNormalize(string? input, out string hex)
    {
        hex = "";
        if (input is null) return false;

        var digits = input.Trim();
        if (digits.StartsWith('#')) digits = digits[1..];
        if (digits.Length == 3) digits = string.Concat(digits.Select(c => new string(c, 2)));
        if (digits.Length != 6 || !digits.All(char.IsAsciiHexDigit)) return false;

        hex = "#" + digits.ToUpperInvariant();
        return true;
    }

    /// <summary>COLORREF Win32 : <c>0x00BBGGRR</c>.</summary>
    /// <exception cref="ArgumentException">La couleur n'est pas lisible par <see cref="TryNormalize"/>.</exception>
    public static uint ToColorRef(string hex)
    {
        if (!TryNormalize(hex, out var normalized))
        {
            throw new ArgumentException($"Couleur invalide : {hex}", nameof(hex));
        }

        var r = uint.Parse(normalized.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = uint.Parse(normalized.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = uint.Parse(normalized.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return r | (g << 8) | (b << 16);
    }

    public static string FromColorRef(uint colorRef) =>
        $"#{colorRef & 0xFF:X2}{(colorRef >> 8) & 0xFF:X2}{(colorRef >> 16) & 0xFF:X2}";
}
```

`src/UsageNotch.Presentation/Preferences/Choices.cs` :
```csharp
using UsageNotch.Core.Settings;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Une valeur et son libellé français. <see cref="ToString"/> rend le libellé, lu par les listes et l'accessibilité.</summary>
public sealed record Choice<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Les listes de choix de la fenêtre de réglages, dans l'ordre d'affichage.</summary>
public static class Choices
{
    public static IReadOnlyList<Choice<ThemePreset>> ThemePresets { get; } =
    [
        new(ThemePreset.Codenotch, "Codenotch"),
        new(ThemePreset.Monochrome, "Monochrome"),
        new(ThemePreset.SystemAccent, "Accent système"),
        new(ThemePreset.Custom, "Personnalisé"),
    ];

    public static IReadOnlyList<Choice<CellContent>> CellContents { get; } =
    [
        new(CellContent.RingAndPercent, "Anneau et pourcentage"),
        new(CellContent.RingOnly, "Anneau seul"),
        new(CellContent.PercentOnly, "Pourcentage seul"),
    ];

    public static IReadOnlyList<Choice<ScreenEdge>> Edges { get; } =
    [
        new(ScreenEdge.Right, "Droite"),
        new(ScreenEdge.Left, "Gauche"),
        new(ScreenEdge.Top, "Haut"),
        new(ScreenEdge.Bottom, "Bas"),
    ];

    public static IReadOnlyList<Choice<VisibilityMode>> Visibilities { get; } =
    [
        new(VisibilityMode.Expanded, "Déplié"),
        new(VisibilityMode.Folded, "Replié"),
        new(VisibilityMode.Hidden, "Masqué"),
    ];

    /// <summary>Noms acceptés par <c>ISoundPlayer.Play</c>.</summary>
    public static IReadOnlyList<Choice<string>> Sounds { get; } =
    [
        new("Asterisk", "Astérisque"),
        new("Beep", "Bip"),
        new("Exclamation", "Exclamation"),
        new("Hand", "Arrêt critique"),
        new("Question", "Question"),
    ];

    public static string SoundLabel(string name) => Sounds.FirstOrDefault(s => s.Value == name)?.Label ?? name;
}
```

`src/UsageNotch.Presentation/Preferences/MonitorChoices.cs` :
```csharp
using UsageNotch.Core.Placement;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Preferences;

/// <summary>La liste « écran d'ancrage » : « Écran principal » puis chaque écran numéroté de gauche à droite.</summary>
public static class MonitorChoices
{
    /// <summary>Clé de « Écran principal » : une liste déroulante WPF ne sait pas sélectionner une valeur null.</summary>
    public const string PrimaryKey = "";

    /// <summary>Ordre d'affichage stable : de gauche à droite, puis de haut en bas. Il sert à numéroter les écrans.</summary>
    public static IReadOnlyList<MonitorInfo> Ordered(IReadOnlyList<MonitorInfo> monitors) =>
        monitors.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).ToList();

    public static string Describe(MonitorInfo monitor, int number)
    {
        var percent = (int)Math.Round(monitor.Scale * 100, MidpointRounding.AwayFromZero);
        var label = $"Écran {number} — {monitor.Bounds.Width} × {monitor.Bounds.Height}, {percent}{FrenchText.Nbsp}%";
        return monitor.IsPrimary ? label + " (principal)" : label;
    }

    /// <summary>Un écran mémorisé mais absent reste dans la liste : son choix n'est jamais perdu (spec §8).</summary>
    public static IReadOnlyList<Choice<string>> Build(IReadOnlyList<MonitorInfo> monitors, string? selectedDeviceId)
    {
        var ordered = Ordered(monitors);
        var choices = new List<Choice<string>> { new(PrimaryKey, "Écran principal") };
        for (var i = 0; i < ordered.Count; i++)
        {
            choices.Add(new Choice<string>(ordered[i].DeviceId, Describe(ordered[i], i + 1)));
        }

        if (selectedDeviceId is not null
            && !ordered.Any(m => m.DeviceId.Equals(selectedDeviceId, StringComparison.OrdinalIgnoreCase)))
        {
            choices.Add(new Choice<string>(selectedDeviceId, $"Écran absent ({selectedDeviceId}) — pilule sur l'écran principal"));
        }
        return choices;
    }

    /// <summary>La clé à sélectionner pour un identifiant mémorisé, sans tenir compte de la casse.</summary>
    public static string KeyFor(IReadOnlyList<Choice<string>> choices, string? deviceId)
    {
        if (deviceId is null) return PrimaryKey;
        return choices.FirstOrDefault(c => c.Value.Length > 0 && c.Value.Equals(deviceId, StringComparison.OrdinalIgnoreCase))?.Value
            ?? deviceId;
    }

    public static string? DeviceIdFor(string? key) => string.IsNullOrEmpty(key) ? null : key;
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~HexColorTests|FullyQualifiedName~ChoicesTests"`
Expected: 25 tests passés (HexColor 16, Choices 4, MonitorChoices 5).

- [ ] **Step 5 : Commit**

`feat(presentation): colour entry and settings choice lists`

---

### Task 2 : Brouillon de réglages

**Files:**
- Create: `src/UsageNotch.Presentation/Preferences/SettingsDraft.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Preferences/SettingsDraftTests.cs`

**Interfaces:**
- Consumes: `SettingsStore` (`Current`, `Save`, `event Action<Settings> Changed`), `Settings.Clamp()` (Core) ; `IUiDispatcher` (Presentation.Services) ; `TempDir`, `ImmediateDispatcher` (tests).
- Produces: `sealed class SettingsDraft : IDisposable` avec `static readonly TimeSpan CommitDelay` (250 ms), constructeur `(SettingsStore store, IUiDispatcher ui, TimeProvider time)`, `CoreSettings Value { get; }`, `bool HasPendingEdits { get; }`, `event Action? Changed`, `void Edit(Func<CoreSettings, CoreSettings> edit)`, `void Flush()`, `void Dispose()`.

Règles :
- **Une modification est une fonction, pas une valeur.** `Value` vaut les modifications en attente appliquées à `store.Current`, puis `Clamp()`.
- **Rien n'est écrasé.** Une modification enregistrée ailleurs est préservée : glisser Alt, relecture de settings.json, « Quitter » qui met `AutoLaunch` à false. Les modifications en attente sont rejouées par-dessus.
- **Enregistrement groupé.** La première modification non enregistrée programme l'enregistrement dans `CommitDelay` ; les suivantes ne le repoussent pas. Pendant un glisser de curseur, la vraie pilule suit donc quatre fois par seconde.
- **`Flush` enregistre tout de suite.** `Dispose` enregistre, puis cesse de suivre le magasin et ignore toute modification ultérieure.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/Preferences/SettingsDraftTests.cs` :
```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public sealed class SettingsDraftTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly SettingsStore _store;
    private int _saves;

    public SettingsDraftTests()
    {
        _store = new SettingsStore(_dir.File("settings.json"), NullLogger<SettingsStore>.Instance, _time);
        _store.Load();
        _store.Changed += _ => _saves++;
    }

    public void Dispose() => _dir.Dispose();

    private SettingsDraft NewDraft() => new(_store, new ImmediateDispatcher(), _time);

    [Fact]
    public void Starts_from_the_saved_settings()
    {
        using var draft = NewDraft();

        draft.Value.Should().Be(_store.Current);
        draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void An_edit_is_visible_at_once_and_saved_after_the_commit_delay()
    {
        using var draft = NewDraft();
        var changed = 0;
        draft.Changed += () => changed++;

        draft.Edit(s => s with { Scale = 1.2 });

        draft.Value.Scale.Should().Be(1.2);
        draft.HasPendingEdits.Should().BeTrue();
        changed.Should().Be(1);
        _store.Current.Scale.Should().Be(1.0);

        _time.Advance(SettingsDraft.CommitDelay - TimeSpan.FromMilliseconds(1));
        _saves.Should().Be(0);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        _saves.Should().Be(1);
        _store.Current.Scale.Should().Be(1.2);
        draft.HasPendingEdits.Should().BeFalse();

        var reread = new SettingsStore(_dir.File("settings.json"), NullLogger<SettingsStore>.Instance, _time);
        reread.Load().Scale.Should().Be(1.2);
    }

    [Fact]
    public void A_burst_of_edits_is_saved_together_at_most_every_commit_delay()
    {
        using var draft = NewDraft();

        draft.Edit(s => s with { Scale = 1.1 });
        _time.Advance(TimeSpan.FromMilliseconds(100));
        draft.Edit(s => s with { Scale = 1.2 });
        _time.Advance(TimeSpan.FromMilliseconds(100));
        draft.Edit(s => s with { Scale = 1.3 });
        _time.Advance(TimeSpan.FromMilliseconds(50));

        _saves.Should().Be(1);
        _store.Current.Scale.Should().Be(1.3);

        draft.Edit(s => s with { Scale = 1.4 });
        _time.Advance(SettingsDraft.CommitDelay - TimeSpan.FromMilliseconds(1));
        _saves.Should().Be(1);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        _saves.Should().Be(2);
        _store.Current.Scale.Should().Be(1.4);
    }

    [Fact]
    public void A_change_saved_elsewhere_keeps_the_pending_edits()
    {
        using var draft = NewDraft();

        draft.Edit(s => s with { Scale = 1.2 });
        _store.Save(_store.Current with { Edge = ScreenEdge.Left });

        draft.Value.Edge.Should().Be(ScreenEdge.Left);
        draft.Value.Scale.Should().Be(1.2);

        _time.Advance(SettingsDraft.CommitDelay);
        _store.Current.Edge.Should().Be(ScreenEdge.Left);
        _store.Current.Scale.Should().Be(1.2);
    }

    [Fact]
    public void Without_pending_edits_an_outside_change_replaces_the_value_and_notifies()
    {
        using var draft = NewDraft();
        var changed = 0;
        draft.Changed += () => changed++;

        _store.Save(_store.Current with { AutoOpenCard = false });

        draft.Value.AutoOpenCard.Should().BeFalse();
        changed.Should().Be(1);
    }

    [Fact]
    public void Quit_turning_auto_launch_off_survives_a_later_flush()
    {
        using var draft = NewDraft();

        draft.Edit(s => s with { Scale = 1.2 });
        _store.Save(_store.Current with { AutoLaunch = false });
        draft.Flush();

        _store.Current.AutoLaunch.Should().BeFalse();
        _store.Current.Scale.Should().Be(1.2);
    }

    [Fact]
    public void Flush_saves_at_once_and_cancels_the_timer()
    {
        using var draft = NewDraft();

        draft.Edit(s => s with { Scale = 1.2 });
        draft.Flush();

        _saves.Should().Be(1);
        _store.Current.Scale.Should().Be(1.2);

        _time.Advance(SettingsDraft.CommitDelay * 4);
        _saves.Should().Be(1);
    }

    [Fact]
    public void Flush_without_pending_edits_saves_nothing()
    {
        using var draft = NewDraft();

        draft.Flush();

        _saves.Should().Be(0);
    }

    [Fact]
    public void Values_are_clamped_as_they_will_be_when_saved()
    {
        using var draft = NewDraft();

        draft.Edit(s => s with { Scale = 9.0, FoldedThicknessPx = 0 });

        draft.Value.Scale.Should().Be(Settings.ScaleMax);
        draft.Value.FoldedThicknessPx.Should().Be(Settings.FoldedThicknessMin);
    }

    [Fact]
    public void Dispose_saves_pending_edits_then_stops_following_the_store()
    {
        var draft = NewDraft();
        draft.Edit(s => s with { Scale = 1.2 });

        draft.Dispose();

        _store.Current.Scale.Should().Be(1.2);
        _saves.Should().Be(1);

        var changed = 0;
        draft.Changed += () => changed++;
        _store.Save(_store.Current with { Edge = ScreenEdge.Top });
        draft.Edit(s => s with { Scale = 0.5 });
        _time.Advance(SettingsDraft.CommitDelay * 2);

        changed.Should().Be(0);
        _store.Current.Scale.Should().Be(1.2);
        draft.Dispose();
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter SettingsDraftTests`
Expected: erreur de compilation (`SettingsDraft` absent).

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Presentation/Preferences/SettingsDraft.cs` :
```csharp
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Presentation.Preferences;

/// <summary>
/// Réglages en cours d'édition dans la fenêtre de réglages. Chaque modification est une fonction appliquée aux réglages
/// enregistrés : <see cref="Value"/> change aussitôt (aperçu) et l'enregistrement suit au plus tard <see cref="CommitDelay"/>
/// après la première modification non enregistrée. Une modification enregistrée ailleurs (glisser Alt, settings.json édité
/// à la main, Quitter) n'est jamais écrasée : les modifications en attente sont rejouées par-dessus.
/// À utiliser depuis le thread UI.
/// </summary>
public sealed class SettingsDraft : IDisposable
{
    public static readonly TimeSpan CommitDelay = TimeSpan.FromMilliseconds(250);

    private readonly SettingsStore _store;
    private readonly IUiDispatcher _ui;
    private readonly ITimer _timer;
    private readonly Action<CoreSettings> _onStoreChanged;
    private Func<CoreSettings, CoreSettings>? _pending;
    private bool _scheduled;
    private bool _disposed;

    public SettingsDraft(SettingsStore store, IUiDispatcher ui, TimeProvider time)
    {
        _store = store;
        _ui = ui;
        Value = store.Current;
        _timer = time.CreateTimer(_ => _ui.Post(OnTimer), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _onStoreChanged = _ => _ui.Post(Recompute);
        _store.Changed += _onStoreChanged;
    }

    /// <summary>Les réglages tels qu'ils seront enregistrés, bornes appliquées.</summary>
    public CoreSettings Value { get; private set; }

    public bool HasPendingEdits => _pending is not null;

    /// <summary>Levé sur le thread UI quand <see cref="Value"/> change, que la modification vienne d'ici ou d'ailleurs.</summary>
    public event Action? Changed;

    public void Edit(Func<CoreSettings, CoreSettings> edit)
    {
        if (_disposed) return;

        var previous = _pending;
        _pending = previous is null ? edit : s => edit(previous(s));
        Recompute();

        if (_scheduled) return;
        _scheduled = true;
        _timer.Change(CommitDelay, Timeout.InfiniteTimeSpan);
    }

    public void Flush()
    {
        if (_pending is null) return;

        var pending = _pending;
        _pending = null;
        _scheduled = false;
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _store.Save(pending(_store.Current));
        Recompute();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Flush();
        _disposed = true;
        _store.Changed -= _onStoreChanged;
        _timer.Dispose();
    }

    private void OnTimer()
    {
        if (!_disposed) Flush();
    }

    private void Recompute()
    {
        if (_disposed) return;

        var next = _pending is null ? _store.Current : _pending(_store.Current).Clamp();
        if (next == Value) return;
        Value = next;
        Changed?.Invoke();
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter SettingsDraftTests`
Expected: 10 tests passés.

- [ ] **Step 5 : Commit**

`feat(presentation): settings draft with grouped saves`

---

### Task 3 : Aperçu et miniature des écrans

**Files:**
- Create: `src/UsageNotch.Presentation/Preferences/SettingsPreview.cs`
- Create: `src/UsageNotch.Presentation/Preferences/MonitorMap.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Preferences/SettingsPreviewTests.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Preferences/MonitorMapTests.cs`

**Interfaces:**
- Consumes: `PillPresenter.Cell`, `CellModel`, `ActivityKind`, `PillMetrics` (Presentation.Pill) ; `Theme.ForPreset`, `PillPlacement.PillRect`, `PillPlacement.Choose`, `UsageSnapshot`, `LimitWindow`, `SessionState` (Core) ; `MonitorChoices.Ordered` (Task 1).
- Produces :
  - `sealed record PreviewSample(string Caption, CellModel Cell)`.
  - `sealed record PreviewModel(Theme Theme, ScreenEdge Edge, double Scale, VisibilityMode Visibility, int FoldedThicknessPx, IReadOnlyList<PreviewSample> Samples)`.
  - `static class SettingsPreview` avec `PreviewModel Build(CoreSettings settings, string? accentHex)`.
  - `readonly record struct MapRect(double X, double Y, double Width, double Height)`.
  - `sealed record MonitorTile(string DeviceId, int Number, bool IsPrimary, bool IsSelected, MapRect Rect)`.
  - `sealed record MonitorMapModel(IReadOnlyList<MonitorTile> Tiles, MapRect? PillMarker)` avec `static MonitorMapModel Empty`.
  - `static class MonitorMap` avec `const double MinMarkerSize = 3` et `MonitorMapModel Layout(IReadOnlyList<MonitorInfo> monitors, CoreSettings settings, double width, double height, double padding)`.

Les trois exemples d'aperçu :

| Exemple | Fraction | Session | Légende |
|---|---|---|---|
| Modéré | vigilance / 2 | en cours | « Modéré · en cours » |
| Vigilance | (vigilance + critique) / 2 | en attente | « Vigilance · en attente » |
| Critique | (critique + 1) / 2 | terminée | « Critique · terminé » |

Chaque exemple suit les seuils du thème et se construit avec `PillPresenter.Cell`, comme la vraie pilule.

La miniature réduit le bureau virtuel dans la zone demandée en gardant les proportions et le centre. Le repère de la pilule est calculé comme la vraie pilule : écran choisi, bord, position du bord, échelle de l'écran fois échelle des réglages. Sa plus petite dimension vaut au moins 3 DIP, collée au bord de l'écran.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/Preferences/SettingsPreviewTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Pill;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class SettingsPreviewTests
{
    private static string Pct(int value) => value + FrenchText.Nbsp.ToString() + "%";

    [Fact]
    public void Three_samples_show_each_usage_level_and_session_state()
    {
        var model = SettingsPreview.Build(new Settings(), accentHex: null);
        var theme = Theme.Codenotch;

        model.Theme.Should().Be(theme);
        model.Samples.Select(s => s.Caption).Should().Equal("Modéré · en cours", "Vigilance · en attente", "Critique · terminé");
        model.Samples.Select(s => s.Cell.PercentText).Should().Equal(Pct(25), Pct(65), Pct(90));
        model.Samples.Select(s => s.Cell.RingColor).Should().Equal(theme.LevelAmple, theme.LevelWatch, theme.LevelCritical);
        model.Samples.Select(s => s.Cell.Activity).Should().Equal(ActivityKind.Running, ActivityKind.Attention, ActivityKind.Done);
        model.Samples.Should().OnlyContain(s => !s.Cell.Dimmed);
    }

    [Fact]
    public void Samples_follow_custom_thresholds()
    {
        var settings = new Settings
        {
            ThemePreset = ThemePreset.Custom,
            CustomTheme = Theme.Codenotch with { ThresholdWatch = 0.3, ThresholdCritical = 0.6 },
        };

        var model = SettingsPreview.Build(settings, accentHex: null);

        model.Samples[0].Cell.RingFraction.Should().BeApproximately(0.15, 1e-9);
        model.Samples[1].Cell.RingFraction.Should().BeApproximately(0.45, 1e-9);
        model.Samples[2].Cell.RingFraction.Should().BeApproximately(0.8, 1e-9);
        model.Samples.Select(s => s.Cell.RingColor).Should().Equal(Theme.Codenotch.LevelAmple, Theme.Codenotch.LevelWatch, Theme.Codenotch.LevelCritical);
    }

    [Fact]
    public void The_theme_follows_the_preset_and_the_system_accent()
    {
        var model = SettingsPreview.Build(new Settings { ThemePreset = ThemePreset.SystemAccent }, "#0078D4");

        model.Theme.LevelAmple.Should().Be("#0078D4");
        model.Samples[0].Cell.RingColor.Should().Be("#0078D4");
    }

    [Fact]
    public void Cell_content_is_honoured()
    {
        var model = SettingsPreview.Build(new Settings { CellContent = CellContent.PercentOnly }, accentHex: null);

        model.Samples.Should().OnlyContain(s => !s.Cell.ShowRing && s.Cell.ShowPercent);
    }

    [Fact]
    public void Placement_and_visibility_settings_are_passed_through()
    {
        var settings = new Settings { Edge = ScreenEdge.Top, Scale = 0.8, Visibility = VisibilityMode.Folded, FoldedThicknessPx = 7 };

        var model = SettingsPreview.Build(settings, accentHex: null);

        model.Edge.Should().Be(ScreenEdge.Top);
        model.Scale.Should().Be(0.8);
        model.Visibility.Should().Be(VisibilityMode.Folded);
        model.FoldedThicknessPx.Should().Be(7);
    }
}
```

`tests/UsageNotch.Presentation.Tests/Preferences/MonitorMapTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class MonitorMapTests
{
    private static MonitorInfo M(string id, bool primary, int x, int y, int w, int h, double scale = 1.0) =>
        new(id, primary, new PixelRect(x, y, w, h), new PixelRect(x, y, w, h - 48), scale);

    private static readonly MonitorInfo Main = M(@"\\.\DISPLAY1", true, 0, 0, 1920, 1080);
    private static readonly MonitorInfo Side = M(@"\\.\DISPLAY2", false, 1920, 0, 1920, 1080);

    private static void ShouldBe(MapRect actual, double x, double y, double w, double h)
    {
        actual.X.Should().BeApproximately(x, 1e-6);
        actual.Y.Should().BeApproximately(y, 1e-6);
        actual.Width.Should().BeApproximately(w, 1e-6);
        actual.Height.Should().BeApproximately(h, 1e-6);
    }

    [Fact]
    public void The_virtual_desktop_is_scaled_and_centred()
    {
        // Bureau 3840 × 1080 dans 200 × 100 avec une marge de 4 : facteur 0,05, contenu 192 × 54 centré.
        var map = MonitorMap.Layout([Side, Main], new Settings(), 200, 100, 4);

        map.Tiles.Select(t => t.Number).Should().Equal(1, 2);
        map.Tiles[0].DeviceId.Should().Be(@"\\.\DISPLAY1");
        map.Tiles[0].IsPrimary.Should().BeTrue();
        ShouldBe(map.Tiles[0].Rect, 4, 23, 96, 54);
        ShouldBe(map.Tiles[1].Rect, 100, 23, 96, 54);
    }

    [Fact]
    public void The_primary_monitor_is_selected_by_default_and_carries_the_pill_marker()
    {
        var map = MonitorMap.Layout([Main, Side], new Settings(), 200, 100, 4);

        map.Tiles.Select(t => t.IsSelected).Should().Equal(true, false);
        // Pilule 64 × 136 au bord droit, centrée : (1856, 472) sur l'écran principal.
        ShouldBe(map.PillMarker!.Value, 4 + 1856 * 0.05, 23 + 472 * 0.05, 64 * 0.05, 136 * 0.05);
    }

    [Fact]
    public void The_chosen_monitor_edge_and_position_move_the_marker()
    {
        var settings = new Settings { MonitorDeviceId = @"\\.\DISPLAY2", Edge = ScreenEdge.Left, PositionLeft = 0 };

        var map = MonitorMap.Layout([Main, Side], settings, 200, 100, 4);

        map.Tiles.Select(t => t.IsSelected).Should().Equal(false, true);
        ShouldBe(map.PillMarker!.Value, 100, 23, 64 * 0.05, 136 * 0.05);
    }

    [Fact]
    public void An_absent_monitor_falls_back_to_the_primary()
    {
        var map = MonitorMap.Layout([Main, Side], new Settings { MonitorDeviceId = @"\\.\DISPLAY9" }, 200, 100, 4);

        map.Tiles.Select(t => t.IsSelected).Should().Equal(true, false);
    }

    [Fact]
    public void Monitor_and_settings_scales_size_the_marker()
    {
        var hiDpi = M(@"\\.\DISPLAY1", true, 0, 0, 1920, 1080, 1.5);

        var map = MonitorMap.Layout([hiDpi], new Settings { Scale = 0.5 }, 200, 100, 4);

        // Échelle physique 1,5 × 0,5 = 0,75 : épaisseur round(64 × 0,75) = 48, longueur round(136 × 0,75) = 102.
        var factor = Math.Min(192.0 / 1920, 92.0 / 1080);
        map.PillMarker!.Value.Width.Should().BeApproximately(48 * factor, 1e-6);
        map.PillMarker!.Value.Height.Should().BeApproximately(102 * factor, 1e-6);
    }

    [Fact]
    public void A_tiny_marker_is_enlarged_against_its_edge()
    {
        var uhd = M(@"\\.\DISPLAY1", true, 0, 0, 3840, 2160);

        var map = MonitorMap.Layout([uhd], new Settings(), 100, 60, 0);

        var marker = map.PillMarker!.Value;
        marker.Width.Should().Be(MonitorMap.MinMarkerSize);
        (marker.X + marker.Width).Should().BeApproximately(100, 1e-6);
    }

    [Fact]
    public void No_monitor_gives_an_empty_map()
    {
        MonitorMap.Layout([], new Settings(), 200, 100, 4).Should().Be(MonitorMapModel.Empty);
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~SettingsPreviewTests|FullyQualifiedName~MonitorMapTests"`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Presentation/Preferences/SettingsPreview.cs` :
```csharp
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Pill;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Presentation.Preferences;

public sealed record PreviewSample(string Caption, CellModel Cell);

public sealed record PreviewModel(
    Theme Theme,
    ScreenEdge Edge,
    double Scale,
    VisibilityMode Visibility,
    int FoldedThicknessPx,
    IReadOnlyList<PreviewSample> Samples);

/// <summary>Trois pilules d'exemple, une par niveau d'usage, calculées exactement comme la vraie pilule.</summary>
public static class SettingsPreview
{
    public static readonly DateTimeOffset SampleTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static PreviewModel Build(CoreSettings settings, string? accentHex)
    {
        var theme = Theme.ForPreset(settings.ThemePreset, settings.CustomTheme, accentHex);
        var content = settings.CellContent;
        PreviewSample[] samples =
        [
            Sample("Modéré · en cours", theme.ThresholdWatch / 2, SessionState.Running, theme, content),
            Sample("Vigilance · en attente", (theme.ThresholdWatch + theme.ThresholdCritical) / 2, SessionState.Attention, theme, content),
            Sample("Critique · terminé", (theme.ThresholdCritical + 1.0) / 2, SessionState.Done, theme, content),
        ];
        return new PreviewModel(theme, settings.Edge, settings.Scale, settings.Visibility, settings.FoldedThicknessPx, samples);
    }

    private static PreviewSample Sample(string caption, double fraction, SessionState state, Theme theme, CellContent content)
    {
        var snapshot = new UsageSnapshot(
            SnapshotStatus.Ok,
            [new LimitWindow("session", "Session en cours", fraction, SampleTime.AddHours(3))],
            SampleTime,
            "",
            null);
        return new PreviewSample(caption, PillPresenter.Cell(snapshot, "session", state, theme, content, SampleTime));
    }
}
```

`src/UsageNotch.Presentation/Preferences/MonitorMap.cs` :
```csharp
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Pill;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Presentation.Preferences;

public readonly record struct MapRect(double X, double Y, double Width, double Height);

public sealed record MonitorTile(string DeviceId, int Number, bool IsPrimary, bool IsSelected, MapRect Rect);

public sealed record MonitorMapModel(IReadOnlyList<MonitorTile> Tiles, MapRect? PillMarker)
{
    public static MonitorMapModel Empty { get; } = new([], null);
}

/// <summary>Miniature des écrans : le bureau virtuel réduit et centré dans une zone donnée, avec le repère de la pilule.</summary>
public static class MonitorMap
{
    public const double MinMarkerSize = 3;

    public static MonitorMapModel Layout(IReadOnlyList<MonitorInfo> monitors, CoreSettings settings, double width, double height, double padding)
    {
        if (monitors.Count == 0) return MonitorMapModel.Empty;

        var ordered = MonitorChoices.Ordered(monitors);
        var left = ordered.Min(m => m.Bounds.X);
        var top = ordered.Min(m => m.Bounds.Y);
        var desktopWidth = ordered.Max(m => m.Bounds.Right) - left;
        var desktopHeight = ordered.Max(m => m.Bounds.Bottom) - top;
        var factor = Math.Min((width - 2 * padding) / desktopWidth, (height - 2 * padding) / desktopHeight);
        var offsetX = (width - desktopWidth * factor) / 2;
        var offsetY = (height - desktopHeight * factor) / 2;

        MapRect Map(PixelRect r) => new(offsetX + (r.X - left) * factor, offsetY + (r.Y - top) * factor, r.Width * factor, r.Height * factor);

        var selected = PillPlacement.Choose(ordered, settings.MonitorDeviceId);
        var tiles = ordered
            .Select((m, i) => new MonitorTile(m.DeviceId, i + 1, m.IsPrimary, ReferenceEquals(m, selected), Map(m.Bounds)))
            .ToList();

        var physical = selected.Scale * settings.Scale;
        var pill = PillPlacement.PillRect(
            selected.Bounds,
            settings.Edge,
            settings.PositionFor(settings.Edge),
            (int)Math.Round(PillMetrics.WindowLength * physical),
            (int)Math.Round(PillMetrics.Thickness * physical));

        return new MonitorMapModel(tiles, Enlarge(Map(pill), settings.Edge));
    }

    /// <summary>Un repère trop fin est élargi à <see cref="MinMarkerSize"/>, en restant collé au bord de l'écran.</summary>
    private static MapRect Enlarge(MapRect r, ScreenEdge edge)
    {
        if (r.Width < MinMarkerSize)
        {
            r = edge == ScreenEdge.Right
                ? r with { X = r.X + r.Width - MinMarkerSize, Width = MinMarkerSize }
                : r with { Width = MinMarkerSize };
        }
        if (r.Height < MinMarkerSize)
        {
            r = edge == ScreenEdge.Bottom
                ? r with { Y = r.Y + r.Height - MinMarkerSize, Height = MinMarkerSize }
                : r with { Height = MinMarkerSize };
        }
        return r;
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~SettingsPreviewTests|FullyQualifiedName~MonitorMapTests"`
Expected: 12 tests passés (aperçu 5, miniature 7).

- [ ] **Step 5 : Commit**

`feat(presentation): settings preview and monitor map models`

---

### Task 4 : Services de la fenêtre de réglages et page Apparence

**Files:**
- Create: `src/UsageNotch.Presentation/Services/IColorPicker.cs`
- Create: `src/UsageNotch.Presentation/Services/IMonitorSource.cs`
- Create: `src/UsageNotch.Presentation/Services/IAutoStart.cs`
- Create: `src/UsageNotch.Presentation/Services/IHookSetup.cs`
- Create: `src/UsageNotch.Presentation/Services/IShellActions.cs`
- Create: `src/UsageNotch.Presentation/Preferences/ColorSlot.cs`
- Create: `src/UsageNotch.Presentation/Preferences/AppearancePageViewModel.cs`
- Create: `tests/UsageNotch.Presentation.Tests/Preferences/Fakes.cs`
- Create: `tests/UsageNotch.Presentation.Tests/Preferences/DraftFixture.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Preferences/AppearancePageViewModelTests.cs`

**Interfaces:**
- Consumes: `SettingsDraft` (Task 2) ; `Choice<T>`, `Choices`, `HexColor` (Task 1) ; `IAccentColorSource`, `ISoundPlayer` (Presentation.Services, Plan 2) ; `Theme`, `ThemePreset`, `CellContent` (Core) ; `FrenchText` ; `TempDir`, `ImmediateDispatcher` (tests).
- Produces (services, utilisés par les Tasks 5 à 11) :
  - `interface IColorPicker { string? Pick(string initialHex); }`
  - `interface IMonitorSource { IReadOnlyList<MonitorInfo> GetMonitors(); }`
  - `interface IAutoStart { bool IsAvailable { get; } bool IsEnabled(); string? TrySet(bool enabled); }`
  - `sealed record HookSetupResult(bool Succeeded, string Message)` et `interface IHookSetup { string SettingsPath { get; } string HookExePath { get; } bool HookExeExists { get; } bool IsInstalled(); HookSetupResult Install(); HookSetupResult Uninstall(); }`
  - `interface IShellActions { void OpenFolder(string path); void OpenFile(string path); string? RunDoctor(); }`
- Produces (tests, utilisés par les Tasks 5 à 7) : `FakeColorPicker`, `FakeAccent`, `FakeMonitors`, `FakeSound`, `FakeAutoStart`, `FakeHooks`, `FakeShell` dans `Fakes.cs` ; `DraftFixture` avec `Dir`, `Time`, `Store`, `Draft`.
- Produces (page) :
  - `sealed class ColorSlot : ObservableObject` : `string Key`, `string Label`, `string Hex { get; set; }`, `string Error`, `IRelayCommand PickCommand`.
  - `sealed class AppearancePageViewModel : ObservableObject, IDisposable`, constructeur `(SettingsDraft draft, IAccentColorSource accent, IColorPicker picker)` :
    - `const string CustomHint` ;
    - `Presets`, `CellContents`, `IReadOnlyList<ColorSlot> Colors`, `Theme Theme` ;
    - `ThemePreset Preset`, `double PillOpacity`, `string PillOpacityText`, `double ThresholdWatch`, `string ThresholdWatchText`, `double ThresholdCritical`, `string ThresholdCriticalText`, `double Scale`, `string ScaleText`, `CellContent CellContent`.

Règles de la page (spec §7 et écart consigné) :
- **Choisir un préréglage** change `ThemePreset` sans toucher `CustomTheme`. Passer à « Personnalisé » depuis un autre préréglage copie d'abord le thème affiché dans `CustomTheme`.
- **Modifier une couleur, l'opacité ou un seuil** passe à « Personnalisé ». Le point de départ est le thème personnalisé si l'on y est déjà, sinon le thème affiché.
- **Une couleur mal saisie** affiche une erreur qui cite la saisie, et rien n'est modifié.
- **Les setters ignorent une valeur identique.** Une liaison WPF qui réécrit la même valeur ne crée donc pas de modification.
- **Toute modification du brouillon** rafraîchit toutes les propriétés : `PropertyChanged` avec un nom vide, plus chaque `ColorSlot`.

- [ ] **Step 1 : Services**

`src/UsageNotch.Presentation/Services/IColorPicker.cs` :
```csharp
namespace UsageNotch.Presentation.Services;

/// <summary>Sélecteur de couleur du système. Rend <c>#RRGGBB</c>, ou null si l'utilisateur annule.</summary>
public interface IColorPicker
{
    string? Pick(string initialHex);
}
```

`src/UsageNotch.Presentation/Services/IMonitorSource.cs` :
```csharp
using UsageNotch.Core.Placement;

namespace UsageNotch.Presentation.Services;

/// <summary>Les écrans connectés, en pixels physiques.</summary>
public interface IMonitorSource
{
    IReadOnlyList<MonitorInfo> GetMonitors();
}
```

`src/UsageNotch.Presentation/Services/IAutoStart.cs` :
```csharp
namespace UsageNotch.Presentation.Services;

/// <summary>« Démarrer avec Windows » (valeur Run de HKCU).</summary>
public interface IAutoStart
{
    /// <summary>Faux en mode démo : la vraie valeur Run ne doit jamais être lue ni modifiée.</summary>
    bool IsAvailable { get; }

    bool IsEnabled();

    /// <summary>Null si la modification a réussi, sinon un message d'erreur en français.</summary>
    string? TrySet(bool enabled);
}
```

`src/UsageNotch.Presentation/Services/IHookSetup.cs` :
```csharp
namespace UsageNotch.Presentation.Services;

public sealed record HookSetupResult(bool Succeeded, string Message);

/// <summary>Installation des hooks dans settings.json de Claude Code. Ne lève pas : les échecs deviennent des messages.</summary>
public interface IHookSetup
{
    string SettingsPath { get; }
    string HookExePath { get; }
    bool HookExeExists { get; }
    bool IsInstalled();
    HookSetupResult Install();
    HookSetupResult Uninstall();
}
```

`src/UsageNotch.Presentation/Services/IShellActions.cs` :
```csharp
namespace UsageNotch.Presentation.Services;

/// <summary>Ouvertures de dossiers et de fichiers, et diagnostic, pour les pages de réglages.</summary>
public interface IShellActions
{
    void OpenFolder(string path);

    void OpenFile(string path);

    /// <summary>Écrit le diagnostic dans le dossier des journaux et l'ouvre. Null si tout s'est bien passé, sinon un message.</summary>
    string? RunDoctor();
}
```

- [ ] **Step 2 : Faux services et fixture de test**

`tests/UsageNotch.Presentation.Tests/Preferences/Fakes.cs` :
```csharp
using UsageNotch.Core.Placement;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Tests.Preferences;

public sealed class FakeColorPicker : IColorPicker
{
    public string? Result { get; set; }
    public List<string> Requests { get; } = [];

    public string? Pick(string initialHex)
    {
        Requests.Add(initialHex);
        return Result;
    }
}

public sealed class FakeAccent : IAccentColorSource
{
    public string? AccentHex { get; set; }
}

public sealed class FakeMonitors : IMonitorSource
{
    public List<MonitorInfo> Monitors { get; } = [];
    public IReadOnlyList<MonitorInfo> GetMonitors() => Monitors.ToList();
}

public sealed class FakeSound : ISoundPlayer
{
    public List<string> Played { get; } = [];
    public void Play(string soundName) => Played.Add(soundName);
}

public sealed class FakeAutoStart : IAutoStart
{
    public bool IsAvailable { get; set; } = true;
    public bool Enabled { get; set; }
    public string? Error { get; set; }
    public int Reads { get; private set; }
    public List<bool> Writes { get; } = [];

    public bool IsEnabled()
    {
        Reads++;
        return Enabled;
    }

    public string? TrySet(bool enabled)
    {
        Writes.Add(enabled);
        if (Error is not null) return Error;
        Enabled = enabled;
        return null;
    }
}

public sealed class FakeHooks : IHookSetup
{
    public string SettingsPath { get; set; } = @"C:\Users\test\.claude\settings.json";
    public string HookExePath { get; set; } = @"C:\apps\UsageNotch\UsageNotch.Hook.exe";
    public bool HookExeExists { get; set; } = true;
    public bool Installed { get; set; }
    public HookSetupResult? NextResult { get; set; }

    public bool IsInstalled() => Installed;

    public HookSetupResult Install() => Apply(installed: true, "7 hooks écrits");

    public HookSetupResult Uninstall() => Apply(installed: false, "7 hooks retirés");

    private HookSetupResult Apply(bool installed, string message)
    {
        var result = NextResult ?? new HookSetupResult(true, message);
        if (result.Succeeded) Installed = installed;
        return result;
    }
}

public sealed class FakeShell : IShellActions
{
    public List<string> Folders { get; } = [];
    public List<string> Files { get; } = [];
    public int DoctorRuns { get; private set; }
    public string? DoctorError { get; set; }

    public void OpenFolder(string path) => Folders.Add(path);
    public void OpenFile(string path) => Files.Add(path);

    public string? RunDoctor()
    {
        DoctorRuns++;
        return DoctorError;
    }
}
```

`tests/UsageNotch.Presentation.Tests/Preferences/DraftFixture.cs` :
```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

/// <summary>Un magasin de réglages sur disque temporaire, éventuellement prérempli, et son brouillon.</summary>
public sealed class DraftFixture : IDisposable
{
    public DraftFixture(Func<Settings, Settings>? initial = null)
    {
        Store = new SettingsStore(Dir.File("settings.json"), NullLogger<SettingsStore>.Instance, Time);
        Store.Load();
        if (initial is not null) Store.Save(initial(Store.Current));
        Draft = new SettingsDraft(Store, new ImmediateDispatcher(), Time);
    }

    public TempDir Dir { get; } = new();
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
    public SettingsStore Store { get; }
    public SettingsDraft Draft { get; }

    public void Dispose()
    {
        Draft.Dispose();
        Dir.Dispose();
    }
}
```

Les initialiseurs de propriétés auto (`Dir`, `Time`) s'exécutent avant le corps du constructeur : `Store` peut donc les utiliser.

- [ ] **Step 3 : Écrire les tests de la page**

`tests/UsageNotch.Presentation.Tests/Preferences/AppearancePageViewModelTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class AppearancePageViewModelTests
{
    private static string Pct(int value) => value + FrenchText.Nbsp.ToString() + "%";

    private static (DraftFixture F, AppearancePageViewModel Vm, FakeColorPicker Picker, FakeAccent Accent) Create(
        Func<Settings, Settings>? initial = null)
    {
        var f = new DraftFixture(initial);
        var picker = new FakeColorPicker();
        var accent = new FakeAccent();
        return (f, new AppearancePageViewModel(f.Draft, accent, picker), picker, accent);
    }

    private static ColorSlot Slot(AppearancePageViewModel vm, string key) => vm.Colors.Single(c => c.Key == key);

    [Fact]
    public void Lists_presets_contents_and_the_ten_theme_colours_in_order()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.Presets.Should().BeSameAs(Choices.ThemePresets);
        vm.CellContents.Should().BeSameAs(Choices.CellContents);
        vm.Colors.Select(c => c.Key).Should().Equal(
            "PillBackground", "PillBorder", "RingTrack", "LevelAmple", "LevelWatch", "LevelCritical", "Running", "Attention", "Done", "Text");
        vm.Colors.Select(c => c.Label).Should().Equal(
            "Fond de la pilule", "Contour de la pilule", "Piste de l'anneau", "Niveau modéré", "Niveau vigilance",
            "Niveau critique", "Session en cours", "Session en attente", "Session terminée", "Texte");
        Slot(vm, "LevelAmple").Hex.Should().Be(Theme.Codenotch.LevelAmple);
    }

    [Fact]
    public void Choosing_a_preset_edits_the_draft_and_refreshes_the_colours()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;
        var names = new List<string?>();
        vm.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        var slotNames = new List<string?>();
        Slot(vm, "PillBackground").PropertyChanged += (_, e) => slotNames.Add(e.PropertyName);

        vm.Preset = ThemePreset.Monochrome;

        f.Draft.Value.ThemePreset.Should().Be(ThemePreset.Monochrome);
        vm.Theme.Should().Be(Theme.Monochrome);
        Slot(vm, "PillBackground").Hex.Should().Be(Theme.Monochrome.PillBackground);
        names.Should().Contain(string.Empty);
        slotNames.Should().Contain(nameof(ColorSlot.Hex));
    }

    [Fact]
    public void Choosing_the_same_preset_again_changes_nothing()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.Preset = ThemePreset.Codenotch;

        f.Draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void Switching_to_custom_starts_from_the_theme_being_left()
    {
        var (f, vm, _, _) = Create(s => s with
        {
            ThemePreset = ThemePreset.Monochrome,
            CustomTheme = Theme.Codenotch with { Text = "#123456" },
        });
        using var _f = f;

        vm.Preset = ThemePreset.Custom;

        f.Draft.Value.ThemePreset.Should().Be(ThemePreset.Custom);
        f.Draft.Value.CustomTheme.Should().Be(Theme.Monochrome);
    }

    [Fact]
    public void Editing_a_colour_on_a_preset_switches_to_custom_with_only_that_colour_changed()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        Slot(vm, "Text").Hex = "#abc";

        f.Draft.Value.ThemePreset.Should().Be(ThemePreset.Custom);
        f.Draft.Value.CustomTheme.Should().Be(Theme.Codenotch with { Text = "#AABBCC" });
        Slot(vm, "Text").Hex.Should().Be("#AABBCC");
        Slot(vm, "Text").Error.Should().BeEmpty();
        vm.Preset.Should().Be(ThemePreset.Custom);
    }

    [Fact]
    public void Editing_a_custom_theme_keeps_its_other_colours()
    {
        var (f, vm, _, _) = Create(s => s with
        {
            ThemePreset = ThemePreset.Custom,
            CustomTheme = Theme.Codenotch with { Text = "#123456" },
        });
        using var _f = f;

        Slot(vm, "Done").Hex = "#00FF00";

        f.Draft.Value.CustomTheme.Should().Be(Theme.Codenotch with { Text = "#123456", Done = "#00FF00" });
    }

    [Fact]
    public void An_invalid_colour_shows_an_error_and_changes_nothing()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        Slot(vm, "Text").Hex = "rouge";

        Slot(vm, "Text").Error.Should().Be("Couleur invalide : « rouge ». Format attendu : #RRGGBB.");
        Slot(vm, "Text").Hex.Should().Be(Theme.Codenotch.Text);
        f.Draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void A_valid_entry_clears_the_previous_error()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        Slot(vm, "Text").Hex = "rouge";
        Slot(vm, "Text").Hex = "#FF0000";

        Slot(vm, "Text").Error.Should().BeEmpty();
        f.Draft.Value.CustomTheme.Text.Should().Be("#FF0000");
    }

    [Fact]
    public void Picking_a_colour_applies_it_and_cancelling_changes_nothing()
    {
        var (f, vm, picker, _) = Create();
        using var _f = f;

        picker.Result = null;
        Slot(vm, "LevelAmple").PickCommand.Execute(null);
        f.Draft.HasPendingEdits.Should().BeFalse();

        picker.Result = "#0078d4";
        Slot(vm, "LevelAmple").PickCommand.Execute(null);

        picker.Requests.Should().Equal(Theme.Codenotch.LevelAmple, Theme.Codenotch.LevelAmple);
        f.Draft.Value.CustomTheme.LevelAmple.Should().Be("#0078D4");
    }

    [Fact]
    public void Opacity_is_a_theme_setting_and_is_clamped()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.PillOpacity = 0.1;

        f.Draft.Value.ThemePreset.Should().Be(ThemePreset.Custom);
        f.Draft.Value.CustomTheme.PillOpacity.Should().Be(0.2);
        vm.PillOpacity.Should().Be(0.2);
        vm.PillOpacityText.Should().Be(Pct(20));
    }

    [Fact]
    public void Raising_the_watch_threshold_pushes_the_critical_threshold_up()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.ThresholdWatch = 0.9;

        vm.ThresholdWatch.Should().Be(0.9);
        vm.ThresholdCritical.Should().BeApproximately(0.95, 1e-9);
        vm.ThresholdWatchText.Should().Be(Pct(90));
        vm.ThresholdCriticalText.Should().Be(Pct(95));
    }

    [Fact]
    public void Scale_and_cell_content_edit_the_draft()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.Scale = 1.25;
        vm.CellContent = CellContent.RingOnly;

        f.Draft.Value.Scale.Should().Be(1.25);
        f.Draft.Value.CellContent.Should().Be(CellContent.RingOnly);
        vm.ScaleText.Should().Be(Pct(125));
        f.Draft.Value.ThemePreset.Should().Be(ThemePreset.Codenotch);
    }

    [Fact]
    public void The_system_accent_preset_shows_the_windows_accent()
    {
        var (f, vm, _, accent) = Create();
        using var _f = f;
        accent.AccentHex = "#0078D4";

        vm.Preset = ThemePreset.SystemAccent;

        vm.Theme.LevelAmple.Should().Be("#0078D4");
        Slot(vm, "LevelAmple").Hex.Should().Be("#0078D4");
    }

    [Fact]
    public void Dispose_stops_following_the_draft()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;
        var count = 0;
        vm.PropertyChanged += (_, _) => count++;

        vm.Dispose();
        f.Draft.Edit(s => s with { Scale = 1.3 });

        count.Should().Be(0);
    }
}
```

- [ ] **Step 4 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter AppearancePageViewModelTests`
Expected: erreur de compilation (`AppearancePageViewModel` et `ColorSlot` absents).

- [ ] **Step 5 : Implémenter**

`src/UsageNotch.Presentation/Preferences/ColorSlot.cs` :
```csharp
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
```

`src/UsageNotch.Presentation/Preferences/AppearancePageViewModel.cs` :
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Services;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Page « Apparence » : préréglage, couleurs, opacité, seuils, échelle, contenu de la cellule.</summary>
public sealed class AppearancePageViewModel : ObservableObject, IDisposable
{
    public const string CustomHint =
        "Modifier une couleur, l'opacité ou un seuil passe au thème Personnalisé, initialisé depuis le thème affiché.";

    private readonly SettingsDraft _draft;
    private readonly IAccentColorSource _accent;

    public AppearancePageViewModel(SettingsDraft draft, IAccentColorSource accent, IColorPicker picker)
    {
        _draft = draft;
        _accent = accent;

        ColorSlot Slot(string key, string label, Func<Theme, string> get, Func<Theme, string, Theme> set) =>
            new(key, label, get, set, () => Theme, EditTheme, picker);

        Colors =
        [
            Slot("PillBackground", "Fond de la pilule", t => t.PillBackground, (t, h) => t with { PillBackground = h }),
            Slot("PillBorder", "Contour de la pilule", t => t.PillBorder, (t, h) => t with { PillBorder = h }),
            Slot("RingTrack", "Piste de l'anneau", t => t.RingTrack, (t, h) => t with { RingTrack = h }),
            Slot("LevelAmple", "Niveau modéré", t => t.LevelAmple, (t, h) => t with { LevelAmple = h }),
            Slot("LevelWatch", "Niveau vigilance", t => t.LevelWatch, (t, h) => t with { LevelWatch = h }),
            Slot("LevelCritical", "Niveau critique", t => t.LevelCritical, (t, h) => t with { LevelCritical = h }),
            Slot("Running", "Session en cours", t => t.Running, (t, h) => t with { Running = h }),
            Slot("Attention", "Session en attente", t => t.Attention, (t, h) => t with { Attention = h }),
            Slot("Done", "Session terminée", t => t.Done, (t, h) => t with { Done = h }),
            Slot("Text", "Texte", t => t.Text, (t, h) => t with { Text = h }),
        ];

        _draft.Changed += OnDraftChanged;
    }

    public IReadOnlyList<Choice<ThemePreset>> Presets => Choices.ThemePresets;

    public IReadOnlyList<Choice<CellContent>> CellContents => Choices.CellContents;

    public IReadOnlyList<ColorSlot> Colors { get; }

    /// <summary>Le thème affiché : celui du préréglage choisi, avec l'accent système lu maintenant.</summary>
    public Theme Theme => EffectiveTheme(_draft.Value, _accent.AccentHex);

    public ThemePreset Preset
    {
        get => _draft.Value.ThemePreset;
        set
        {
            if (value == Preset) return;
            var accent = _accent.AccentHex;
            _draft.Edit(s => value == ThemePreset.Custom && s.ThemePreset != ThemePreset.Custom
                ? s with { ThemePreset = ThemePreset.Custom, CustomTheme = EffectiveTheme(s, accent) }
                : s with { ThemePreset = value });
        }
    }

    public double PillOpacity
    {
        get => Theme.PillOpacity;
        set
        {
            if (Same(value, PillOpacity)) return;
            EditTheme(t => t with { PillOpacity = value });
        }
    }

    public string PillOpacityText => FrenchText.Percent(PillOpacity);

    public double ThresholdWatch
    {
        get => Theme.ThresholdWatch;
        set
        {
            if (Same(value, ThresholdWatch)) return;
            EditTheme(t => t with { ThresholdWatch = value });
        }
    }

    public string ThresholdWatchText => FrenchText.Percent(ThresholdWatch);

    public double ThresholdCritical
    {
        get => Theme.ThresholdCritical;
        set
        {
            if (Same(value, ThresholdCritical)) return;
            EditTheme(t => t with { ThresholdCritical = value });
        }
    }

    public string ThresholdCriticalText => FrenchText.Percent(ThresholdCritical);

    public double Scale
    {
        get => _draft.Value.Scale;
        set
        {
            if (Same(value, Scale)) return;
            _draft.Edit(s => s with { Scale = value });
        }
    }

    /// <summary>L'échelle dépasse 100 % : pas de <see cref="FrenchText.Percent"/>, qui borne à 1.</summary>
    public string ScaleText => $"{(int)Math.Round(Scale * 100, MidpointRounding.AwayFromZero)}{FrenchText.Nbsp}%";

    public CellContent CellContent
    {
        get => _draft.Value.CellContent;
        set
        {
            if (value == CellContent) return;
            _draft.Edit(s => s with { CellContent = value });
        }
    }

    public void Dispose() => _draft.Changed -= OnDraftChanged;

    private static Theme EffectiveTheme(CoreSettings s, string? accent) => Theme.ForPreset(s.ThemePreset, s.CustomTheme, accent);

    /// <summary>Toute retouche du thème passe au thème Personnalisé, initialisé depuis le thème affiché.</summary>
    private void EditTheme(Func<Theme, Theme> change)
    {
        var accent = _accent.AccentHex;
        _draft.Edit(s => s with
        {
            ThemePreset = ThemePreset.Custom,
            CustomTheme = change(s.ThemePreset == ThemePreset.Custom ? s.CustomTheme : EffectiveTheme(s, accent)),
        });
    }

    private static bool Same(double a, double b) => Math.Abs(a - b) < 1e-9;

    private void OnDraftChanged()
    {
        OnPropertyChanged(string.Empty);
        foreach (var slot in Colors) slot.Refresh();
    }
}
```

`Theme` est à la fois le nom d'une propriété et de son type : l'accès statique `Theme.ForPreset` reste valide (règle « Color Color » de C#).

- [ ] **Step 6 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter AppearancePageViewModelTests`
Expected: 14 tests passés.

- [ ] **Step 7 : Commit**

`feat(presentation): settings services and appearance page view model`

---

### Task 5 : Page Position

**Files:**
- Create: `src/UsageNotch.Presentation/Preferences/PositionPageViewModel.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Preferences/PositionPageViewModelTests.cs`

**Interfaces:**
- Consumes: `SettingsDraft` (Task 2) ; `Choices`, `MonitorChoices` (Task 1) ; `MonitorMap`, `MonitorMapModel` (Task 3) ; `IMonitorSource` (Task 4) ; `FakeMonitors`, `DraftFixture` (tests, Task 4) ; `Settings.PositionFor`, `Settings.WithPosition` (Core).
- Produces: `sealed class PositionPageViewModel : ObservableObject, IDisposable`, constructeur `(SettingsDraft draft, IMonitorSource monitors)` :
  - constantes `MapWidth = 360`, `MapHeight = 150`, `MapPadding = 6`, `DragHint`, `FoldedHint`, `HiddenHint` ;
  - écran : `IReadOnlyList<Choice<string>> Monitors`, `string MonitorKey { get; set; }`, `MonitorMapModel Map` ;
  - bord et position : `Edges`, `ScreenEdge Edge`, `double Position`, `string PositionText` ;
  - visibilité : `Visibilities`, `VisibilityMode Visibility`, `string VisibilityHint`, `double FoldedThickness`, `string FoldedThicknessText`, `bool FoldedThicknessEnabled` ;
  - commandes : `IRelayCommand RecenterCommand`, `IRelayCommand<string> SelectMonitorCommand`, `IRelayCommand RefreshMonitorsCommand`.

Règles :
- **La liste des écrans garde la même instance tant que son contenu ne change pas.** Remplacer `ItemsSource` ferait réécrire la sélection par WPF. Une valeur `null` venue de la liste est ignorée.
- **`Position` lit et écrit la position du bord courant.** Changer de bord ne perd pas la position des autres (spec §7 : mémorisée par bord).
- **« Recentrer » met la position du bord courant à 0,5.**
- **`FoldedThickness` est exposée en `double`** pour la liaison au curseur, puis arrondie à l'entier.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/Preferences/PositionPageViewModelTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class PositionPageViewModelTests
{
    private static MonitorInfo M(string id, bool primary, int x, int w, int h, double scale) =>
        new(id, primary, new PixelRect(x, 0, w, h), new PixelRect(x, 0, w, h - 48), scale);

    private static readonly MonitorInfo Main = M(@"\\.\DISPLAY1", true, 0, 2560, 1440, 1.5);
    private static readonly MonitorInfo Side = M(@"\\.\DISPLAY2", false, 2560, 1920, 1080, 1.0);

    private static (DraftFixture F, PositionPageViewModel Vm, FakeMonitors Monitors) Create(Func<Settings, Settings>? initial = null)
    {
        var f = new DraftFixture(initial);
        var monitors = new FakeMonitors();
        monitors.Monitors.AddRange([Main, Side]);
        return (f, new PositionPageViewModel(f.Draft, monitors), monitors);
    }

    [Fact]
    public void Lists_the_primary_choice_then_each_monitor()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.Monitors.Select(c => c.Value).Should().Equal("", @"\\.\DISPLAY1", @"\\.\DISPLAY2");
        vm.MonitorKey.Should().Be(MonitorChoices.PrimaryKey);
        vm.Edges.Should().BeSameAs(Choices.Edges);
        vm.Visibilities.Should().BeSameAs(Choices.Visibilities);
    }

    [Fact]
    public void Choosing_a_monitor_stores_its_id_and_the_primary_choice_stores_null()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.MonitorKey = @"\\.\DISPLAY2";
        f.Draft.Value.MonitorDeviceId.Should().Be(@"\\.\DISPLAY2");
        vm.Map.Tiles.Select(t => t.IsSelected).Should().Equal(false, true);

        vm.MonitorKey = MonitorChoices.PrimaryKey;
        f.Draft.Value.MonitorDeviceId.Should().BeNull();
    }

    [Fact]
    public void A_null_selection_from_the_list_is_ignored()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.MonitorKey = null!;

        f.Draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void Clicking_a_tile_selects_its_monitor()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.SelectMonitorCommand.Execute(@"\\.\DISPLAY2");

        f.Draft.Value.MonitorDeviceId.Should().Be(@"\\.\DISPLAY2");
    }

    [Fact]
    public void An_absent_saved_monitor_stays_selected_while_the_map_shows_the_fallback()
    {
        var (f, vm, _) = Create(s => s with { MonitorDeviceId = @"\\.\DISPLAY9" });
        using var _f = f;

        vm.Monitors.Should().HaveCount(4);
        vm.MonitorKey.Should().Be(@"\\.\DISPLAY9");
        vm.Map.Tiles.Select(t => t.IsSelected).Should().Equal(true, false);
    }

    [Fact]
    public void A_saved_monitor_id_with_another_case_selects_the_listed_monitor()
    {
        var (f, vm, _) = Create(s => s with { MonitorDeviceId = @"\\.\display2" });
        using var _f = f;

        vm.Monitors.Should().HaveCount(3);
        vm.MonitorKey.Should().Be(@"\\.\DISPLAY2");
    }

    [Fact]
    public void Each_edge_keeps_its_own_position()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.Position = 0.2;
        vm.Edge = ScreenEdge.Top;

        vm.Position.Should().Be(0.5);
        vm.Position = 0.9;

        f.Draft.Value.PositionRight.Should().Be(0.2);
        f.Draft.Value.PositionTop.Should().Be(0.9);
        vm.PositionText.Should().Be("90" + FrenchText.Nbsp + "%");
    }

    [Fact]
    public void Recenter_puts_the_pill_back_in_the_middle_of_the_current_edge()
    {
        var (f, vm, _) = Create(s => s with { Edge = ScreenEdge.Bottom, PositionBottom = 0.1, PositionRight = 0.3 });
        using var _f = f;

        vm.RecenterCommand.Execute(null);

        f.Draft.Value.PositionBottom.Should().Be(0.5);
        f.Draft.Value.PositionRight.Should().Be(0.3);
    }

    [Fact]
    public void Folded_thickness_is_enabled_only_in_folded_mode_and_clamped()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.FoldedThicknessEnabled.Should().BeFalse();
        vm.VisibilityHint.Should().BeEmpty();

        vm.Visibility = VisibilityMode.Folded;
        vm.FoldedThickness = 20;

        vm.FoldedThicknessEnabled.Should().BeTrue();
        vm.VisibilityHint.Should().Be(PositionPageViewModel.FoldedHint);
        f.Draft.Value.FoldedThicknessPx.Should().Be(12);
        vm.FoldedThickness.Should().Be(12);
        vm.FoldedThicknessText.Should().Be("12 px");

        vm.FoldedThickness = 6.6;
        f.Draft.Value.FoldedThicknessPx.Should().Be(7);
    }

    [Fact]
    public void Hidden_mode_explains_that_only_the_tray_icon_remains()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.Visibility = VisibilityMode.Hidden;

        vm.VisibilityHint.Should().Be(PositionPageViewModel.HiddenHint);
        vm.FoldedThicknessEnabled.Should().BeFalse();
    }

    [Fact]
    public void Refreshing_monitors_rebuilds_the_list_only_when_it_changed()
    {
        var (f, vm, monitors) = Create();
        using var _f = f;
        var before = vm.Monitors;

        vm.RefreshMonitorsCommand.Execute(null);
        vm.Monitors.Should().BeSameAs(before);

        monitors.Monitors.Add(M(@"\\.\DISPLAY3", false, 4480, 1920, 1080, 1.0));
        var names = new List<string?>();
        vm.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        vm.RefreshMonitorsCommand.Execute(null);

        vm.Monitors.Should().HaveCount(4);
        names.Should().Contain(nameof(PositionPageViewModel.Monitors)).And.Contain(nameof(PositionPageViewModel.Map));
    }

    [Fact]
    public void The_map_follows_the_draft()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.Edge = ScreenEdge.Left;

        vm.Map.PillMarker!.Value.X.Should().BeApproximately(vm.Map.Tiles[0].Rect.X, 1e-6);
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter PositionPageViewModelTests`
Expected: erreur de compilation (`PositionPageViewModel` absent).

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Presentation/Preferences/PositionPageViewModel.cs` :
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Page « Position » : écran d'ancrage, bord, position le long du bord, mode de visibilité, bande repliée.</summary>
public sealed class PositionPageViewModel : ObservableObject, IDisposable
{
    public const double MapWidth = 360;
    public const double MapHeight = 150;
    public const double MapPadding = 6;
    public const string DragHint = "Astuce : Alt + glisser la pilule la déplace le long du bord.";
    public const string FoldedHint = "En mode Replié, seule une fine bande reste au bord ; la pilule se déplie au survol.";
    public const string HiddenHint = "En mode Masqué, rien n'est affiché au bord de l'écran ; l'icône de notification reste visible.";

    private readonly SettingsDraft _draft;
    private readonly IMonitorSource _source;
    private IReadOnlyList<MonitorInfo> _monitors;
    private IReadOnlyList<Choice<string>> _choices;

    public PositionPageViewModel(SettingsDraft draft, IMonitorSource monitors)
    {
        _draft = draft;
        _source = monitors;
        _monitors = monitors.GetMonitors();
        _choices = MonitorChoices.Build(_monitors, draft.Value.MonitorDeviceId);

        RecenterCommand = new RelayCommand(() => Position = 0.5);
        SelectMonitorCommand = new RelayCommand<string>(key => { if (key is not null) MonitorKey = key; });
        RefreshMonitorsCommand = new RelayCommand(RefreshMonitors);

        _draft.Changed += OnDraftChanged;
    }

    public IReadOnlyList<Choice<string>> Monitors => _choices;

    public IReadOnlyList<Choice<ScreenEdge>> Edges => Choices.Edges;

    public IReadOnlyList<Choice<VisibilityMode>> Visibilities => Choices.Visibilities;

    public string MonitorKey
    {
        get => MonitorChoices.KeyFor(_choices, _draft.Value.MonitorDeviceId);
        set
        {
            // Une liste déroulante WPF écrit null quand sa sélection disparaît : ce n'est pas un choix de l'utilisateur.
            if (value is null || value == MonitorKey) return;
            var deviceId = MonitorChoices.DeviceIdFor(value);
            _draft.Edit(s => s with { MonitorDeviceId = deviceId });
        }
    }

    public MonitorMapModel Map => MonitorMap.Layout(_monitors, _draft.Value, MapWidth, MapHeight, MapPadding);

    public ScreenEdge Edge
    {
        get => _draft.Value.Edge;
        set
        {
            if (value == Edge) return;
            _draft.Edit(s => s with { Edge = value });
        }
    }

    public double Position
    {
        get => _draft.Value.PositionFor(_draft.Value.Edge);
        set
        {
            if (Math.Abs(value - Position) < 1e-9) return;
            _draft.Edit(s => s.WithPosition(s.Edge, value));
        }
    }

    public string PositionText => FrenchText.Percent(Position);

    public VisibilityMode Visibility
    {
        get => _draft.Value.Visibility;
        set
        {
            if (value == Visibility) return;
            _draft.Edit(s => s with { Visibility = value });
        }
    }

    public string VisibilityHint => Visibility switch
    {
        VisibilityMode.Folded => FoldedHint,
        VisibilityMode.Hidden => HiddenHint,
        _ => "",
    };

    public double FoldedThickness
    {
        get => _draft.Value.FoldedThicknessPx;
        set
        {
            var px = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            if (px == _draft.Value.FoldedThicknessPx) return;
            _draft.Edit(s => s with { FoldedThicknessPx = px });
        }
    }

    public string FoldedThicknessText => $"{_draft.Value.FoldedThicknessPx} px";

    public bool FoldedThicknessEnabled => Visibility == VisibilityMode.Folded;

    public IRelayCommand RecenterCommand { get; }

    public IRelayCommand<string> SelectMonitorCommand { get; }

    public IRelayCommand RefreshMonitorsCommand { get; }

    public void Dispose() => _draft.Changed -= OnDraftChanged;

    private void RefreshMonitors()
    {
        _monitors = _source.GetMonitors();
        RebuildChoices();
        OnPropertyChanged(nameof(Map));
    }

    /// <summary>Remplace la liste seulement si son contenu change : WPF réécrirait la sélection à chaque nouvelle instance.</summary>
    private void RebuildChoices()
    {
        var next = MonitorChoices.Build(_monitors, _draft.Value.MonitorDeviceId);
        if (next.SequenceEqual(_choices)) return;
        _choices = next;
        OnPropertyChanged(nameof(Monitors));
    }

    private void OnDraftChanged()
    {
        RebuildChoices();
        OnPropertyChanged(string.Empty);
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter PositionPageViewModelTests`
Expected: 12 tests passés.

- [ ] **Step 5 : Commit**

`feat(presentation): position page view model`

---

### Task 6 : Pages Comportement et Claude Code

**Files:**
- Create: `src/UsageNotch.Presentation/Preferences/SettingsEnvironment.cs`
- Create: `src/UsageNotch.Presentation/Preferences/BehaviorPageViewModel.cs`
- Create: `src/UsageNotch.Presentation/Preferences/ClaudeCodePageViewModel.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Preferences/BehaviorPageViewModelTests.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Preferences/ClaudeCodePageViewModelTests.cs`

**Interfaces:**
- Consumes: `SettingsDraft` (Task 2) ; `Choices` (Task 1) ; `ISoundPlayer` (Plan 2), `IAutoStart`, `IHookSetup`, `HookSetupResult`, `IShellActions` (Task 4) ; `FakeSound`, `FakeAutoStart`, `FakeHooks`, `FakeShell`, `DraftFixture` (tests, Task 4).
- Produces :
  - `sealed record SettingsEnvironment(string Version, bool Demo, string DataDirectory, string LogsDirectory, string SettingsFile, int ListeningPort, bool Listening)`.
  - `sealed class BehaviorPageViewModel : ObservableObject, IDisposable`, constructeur `(SettingsDraft draft, ISoundPlayer sound, IAutoStart autoStart)` :
    - constantes `AutoStartUnavailableNote`, `TrayLockedNote` ;
    - carte et sons : `bool AutoOpenCard`, `bool SoundEnabled`, `Sounds`, `string DoneSound`, `string AttentionSound`, `IRelayCommand PlayDoneSoundCommand`, `IRelayCommand PlayAttentionSoundCommand` ;
    - démarrage avec Windows : `bool AutoStartAvailable`, `bool AutoStartEnabled`, `string AutoStartNote`, `string AutoStartError` ;
    - icône de notification : `bool TrayIconVisible`, `bool TrayIconEditable`, `string TrayIconNote`.
  - `sealed class ClaudeCodePageViewModel : ObservableObject, IDisposable`, constructeur `(SettingsDraft draft, IHookSetup hooks, IShellActions shell, SettingsEnvironment environment)` :
    - constantes `PortRangeError`, `RestartNote`, `DemoNote` ;
    - hooks : `bool HooksInstalled`, `string HooksStatus`, `string LastMessage`, `bool LastActionFailed`, `string ClaudeSettingsPath`, `string HookExePath`, `string HookExeStatus`, `string DemoHint` ;
    - port : `string PortText { get; set; }`, `string PortError`, `string PortNote` ;
    - commandes : `IRelayCommand InstallCommand`, `IRelayCommand UninstallCommand`, `IRelayCommand RefreshCommand`, `IRelayCommand OpenClaudeFolderCommand`.

Règles :
- **Écoute des sons.** Les boutons d'écoute jouent le son choisi même quand les sons sont désactivés : l'utilisateur choisit avant d'activer.
- **« Démarrer avec Windows »** n'est pas un réglage de settings.json, c'est la valeur Run de HKCU.
  - En démo, `IAutoStart.IsAvailable` est faux : la page ne lit ni n'écrit rien.
  - Un échec garde l'état précédent et affiche le message.
- **Icône de notification.** En mode Masqué, elle est forcée visible par `Settings.Clamp` et la case n'est pas modifiable.
- **Port.**
  - La saisie se valide à la perte de focus.
  - Un texte hors de 1024 à 65535 affiche une erreur sans rien modifier.
  - Un port différent de celui qu'écoute l'application annonce le redémarrage.
  - Si le port écouté n'a pas pu être ouvert, la note le dit.
- **Hooks.** L'état des hooks est relu après chaque action. Installer demande l'exécutable du hook ; désinstaller demande des hooks installés.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/Preferences/BehaviorPageViewModelTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class BehaviorPageViewModelTests
{
    private static (DraftFixture F, BehaviorPageViewModel Vm, FakeSound Sound, FakeAutoStart AutoStart) Create(
        FakeAutoStart? autoStart = null)
    {
        var f = new DraftFixture();
        var sound = new FakeSound();
        autoStart ??= new FakeAutoStart();
        return (f, new BehaviorPageViewModel(f.Draft, sound, autoStart), sound, autoStart);
    }

    [Fact]
    public void Card_and_sound_switches_edit_the_draft()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.AutoOpenCard = false;
        vm.SoundEnabled = false;

        f.Draft.Value.AutoOpenCard.Should().BeFalse();
        f.Draft.Value.SoundEnabled.Should().BeFalse();
    }

    [Fact]
    public void Sound_choices_are_the_windows_sounds_and_a_selection_edits_the_draft()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.Sounds.Should().BeSameAs(Choices.Sounds);
        vm.DoneSound.Should().Be("Asterisk");

        vm.DoneSound = "Hand";
        vm.AttentionSound = "Question";

        f.Draft.Value.DoneSound.Should().Be("Hand");
        f.Draft.Value.AttentionSound.Should().Be("Question");
    }

    [Fact]
    public void An_empty_sound_selection_is_ignored()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.DoneSound = null!;
        vm.AttentionSound = "";

        f.Draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void Preview_buttons_play_the_selected_sound_even_when_sounds_are_off()
    {
        var (f, vm, sound, _) = Create();
        using var _f = f;

        vm.SoundEnabled = false;
        vm.DoneSound = "Question";
        vm.PlayDoneSoundCommand.Execute(null);
        vm.PlayAttentionSoundCommand.Execute(null);

        sound.Played.Should().Equal("Question", "Exclamation");
    }

    [Fact]
    public void Auto_start_reflects_and_changes_the_registry_value()
    {
        var (f, vm, _, autoStart) = Create();
        using var _f = f;

        vm.AutoStartAvailable.Should().BeTrue();
        vm.AutoStartEnabled.Should().BeFalse();
        vm.AutoStartNote.Should().BeEmpty();

        vm.AutoStartEnabled = true;

        autoStart.Writes.Should().Equal(true);
        vm.AutoStartEnabled.Should().BeTrue();
        vm.AutoStartError.Should().BeEmpty();
        f.Draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void A_failed_auto_start_change_keeps_the_previous_state_and_shows_the_error()
    {
        var (f, vm, _, _) = Create(new FakeAutoStart { Error = "Impossible de modifier le démarrage avec Windows : accès refusé" });
        using var _f = f;
        var names = new List<string?>();
        vm.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        vm.AutoStartEnabled = true;

        vm.AutoStartEnabled.Should().BeFalse();
        vm.AutoStartError.Should().Be("Impossible de modifier le démarrage avec Windows : accès refusé");
        names.Should().Contain(nameof(BehaviorPageViewModel.AutoStartEnabled));
    }

    [Fact]
    public void Demo_mode_never_reads_or_writes_auto_start()
    {
        var autoStart = new FakeAutoStart { IsAvailable = false, Enabled = true };
        var (f, vm, _, _) = Create(autoStart);
        using var _f = f;

        vm.AutoStartEnabled = true;
        vm.AutoStartEnabled = false;

        vm.AutoStartAvailable.Should().BeFalse();
        vm.AutoStartEnabled.Should().BeFalse();
        vm.AutoStartNote.Should().Be(BehaviorPageViewModel.AutoStartUnavailableNote);
        autoStart.Reads.Should().Be(0);
        autoStart.Writes.Should().BeEmpty();
    }

    [Fact]
    public void The_tray_icon_can_be_hidden_except_in_hidden_mode()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.TrayIconEditable.Should().BeTrue();
        vm.TrayIconVisible = false;
        f.Draft.Value.TrayIconVisible.Should().BeFalse();

        f.Draft.Edit(s => s with { Visibility = VisibilityMode.Hidden });
        f.Draft.Flush();

        vm.TrayIconVisible.Should().BeTrue();
        vm.TrayIconEditable.Should().BeFalse();
        vm.TrayIconNote.Should().Be(BehaviorPageViewModel.TrayLockedNote);

        vm.TrayIconVisible = false;
        f.Draft.HasPendingEdits.Should().BeFalse();
    }
}
```

`tests/UsageNotch.Presentation.Tests/Preferences/ClaudeCodePageViewModelTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Presentation.Preferences;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Tests.Preferences;

public class ClaudeCodePageViewModelTests
{
    private static SettingsEnvironment Env(int port = 48666, bool listening = true, bool demo = false) =>
        new("0.3.0", demo, @"C:\data", @"C:\data\logs", @"C:\data\settings.json", port, listening);

    private static (DraftFixture F, ClaudeCodePageViewModel Vm, FakeHooks Hooks, FakeShell Shell) Create(
        FakeHooks? hooks = null, SettingsEnvironment? env = null)
    {
        var f = new DraftFixture();
        hooks ??= new FakeHooks();
        var shell = new FakeShell();
        return (f, new ClaudeCodePageViewModel(f.Draft, hooks, shell, env ?? Env()), hooks, shell);
    }

    [Fact]
    public void Shows_the_hook_status_and_paths()
    {
        var (f, vm, hooks, _) = Create();
        using var _f = f;

        vm.HooksInstalled.Should().BeFalse();
        vm.HooksStatus.Should().Be("Non installés");
        vm.ClaudeSettingsPath.Should().Be(hooks.SettingsPath);
        vm.HookExePath.Should().Be(hooks.HookExePath);
        vm.HookExeStatus.Should().Be("Présent");
        vm.LastMessage.Should().BeEmpty();
        vm.PortText.Should().Be("48666");
        vm.PortNote.Should().BeEmpty();
        vm.DemoHint.Should().BeEmpty();
    }

    [Fact]
    public void Install_writes_the_hooks_and_reports_the_message()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.InstallCommand.CanExecute(null).Should().BeTrue();
        vm.UninstallCommand.CanExecute(null).Should().BeFalse();

        vm.InstallCommand.Execute(null);

        vm.HooksInstalled.Should().BeTrue();
        vm.HooksStatus.Should().Be("Installés");
        vm.LastMessage.Should().Be("7 hooks écrits");
        vm.LastActionFailed.Should().BeFalse();
        vm.InstallCommand.CanExecute(null).Should().BeFalse();
        vm.UninstallCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void A_failed_install_shows_the_error_and_keeps_the_status()
    {
        var hooks = new FakeHooks { NextResult = new HookSetupResult(false, "Impossible de modifier les hooks Claude Code : fichier illisible") };
        var (f, vm, _, _) = Create(hooks);
        using var _f = f;

        vm.InstallCommand.Execute(null);

        vm.HooksInstalled.Should().BeFalse();
        vm.LastActionFailed.Should().BeTrue();
        vm.LastMessage.Should().Be("Impossible de modifier les hooks Claude Code : fichier illisible");
    }

    [Fact]
    public void Install_is_disabled_without_the_hook_executable()
    {
        var (f, vm, _, _) = Create(new FakeHooks { HookExeExists = false });
        using var _f = f;

        vm.InstallCommand.CanExecute(null).Should().BeFalse();
        vm.HookExeStatus.Should().Be("Introuvable : les hooks ne peuvent pas être installés.");
    }

    [Fact]
    public void Uninstall_removes_the_hooks()
    {
        var (f, vm, _, _) = Create(new FakeHooks { Installed = true });
        using var _f = f;

        vm.UninstallCommand.Execute(null);

        vm.HooksInstalled.Should().BeFalse();
        vm.LastMessage.Should().Be("7 hooks retirés");
    }

    [Fact]
    public void Refresh_rereads_the_hook_status()
    {
        var (f, vm, hooks, _) = Create();
        using var _f = f;

        hooks.Installed = true;
        vm.RefreshCommand.Execute(null);

        vm.HooksInstalled.Should().BeTrue();
    }

    [Fact]
    public void A_valid_port_edits_the_draft_and_announces_a_restart()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.PortText = " 48700 ";

        f.Draft.Value.Port.Should().Be(48700);
        vm.PortError.Should().BeEmpty();
        vm.PortNote.Should().Be(ClaudeCodePageViewModel.RestartNote);
    }

    [Theory]
    [InlineData("80")]
    [InlineData("70000")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("-48666")]
    public void An_invalid_port_shows_an_error_and_changes_nothing(string text)
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.PortText = text;

        vm.PortError.Should().Be(ClaudeCodePageViewModel.PortRangeError);
        vm.PortText.Should().Be(text);
        f.Draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void Going_back_to_the_listening_port_clears_the_note()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.PortText = "48700";
        vm.PortText = "48666";

        vm.PortNote.Should().BeEmpty();
        f.Draft.Value.Port.Should().Be(48666);
    }

    [Fact]
    public void An_unavailable_listening_port_is_explained()
    {
        var (f, vm, _, _) = Create(env: Env(listening: false));
        using var _f = f;

        vm.PortNote.Should().Be("Le port 48666 est indisponible : une autre application l'utilise peut-être. Choisissez-en un autre, puis redémarrez UsageNotch.");
    }

    [Fact]
    public void An_outside_port_change_updates_the_text()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        f.Store.Save(f.Store.Current with { Port = 49000 });

        vm.PortText.Should().Be("49000");
    }

    [Fact]
    public void Demo_mode_says_hooks_go_to_a_test_file()
    {
        var (f, vm, _, _) = Create(env: Env(demo: true));
        using var _f = f;

        vm.DemoHint.Should().Be(ClaudeCodePageViewModel.DemoNote);
    }

    [Fact]
    public void Open_folder_opens_the_claude_settings_directory()
    {
        var (f, vm, _, shell) = Create();
        using var _f = f;

        vm.OpenClaudeFolderCommand.Execute(null);

        shell.Folders.Should().Equal(@"C:\Users\test\.claude");
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~BehaviorPageViewModelTests|FullyQualifiedName~ClaudeCodePageViewModelTests"`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Presentation/Preferences/SettingsEnvironment.cs` :
```csharp
namespace UsageNotch.Presentation.Preferences;

/// <summary>Ce que la fenêtre de réglages affiche sans pouvoir le modifier. <paramref name="ListeningPort"/> : port réellement écouté.</summary>
public sealed record SettingsEnvironment(
    string Version,
    bool Demo,
    string DataDirectory,
    string LogsDirectory,
    string SettingsFile,
    int ListeningPort,
    bool Listening);
```

`src/UsageNotch.Presentation/Preferences/BehaviorPageViewModel.cs` :
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Page « Comportement » : auto-ouverture de la carte, sons, démarrer avec Windows, icône de notification.</summary>
public sealed class BehaviorPageViewModel : ObservableObject, IDisposable
{
    public const string AutoStartUnavailableNote = "Indisponible en mode démo.";
    public const string TrayLockedNote = "Toujours visible en mode Masqué : c'est alors le seul accès à UsageNotch.";

    private readonly SettingsDraft _draft;
    private readonly IAutoStart _autoStart;
    private bool _autoStartEnabled;
    private string _autoStartError = "";

    public BehaviorPageViewModel(SettingsDraft draft, ISoundPlayer sound, IAutoStart autoStart)
    {
        _draft = draft;
        _autoStart = autoStart;
        _autoStartEnabled = autoStart.IsAvailable && autoStart.IsEnabled();

        PlayDoneSoundCommand = new RelayCommand(() => sound.Play(DoneSound));
        PlayAttentionSoundCommand = new RelayCommand(() => sound.Play(AttentionSound));

        _draft.Changed += OnDraftChanged;
    }

    public bool AutoOpenCard
    {
        get => _draft.Value.AutoOpenCard;
        set
        {
            if (value == AutoOpenCard) return;
            _draft.Edit(s => s with { AutoOpenCard = value });
        }
    }

    public bool SoundEnabled
    {
        get => _draft.Value.SoundEnabled;
        set
        {
            if (value == SoundEnabled) return;
            _draft.Edit(s => s with { SoundEnabled = value });
        }
    }

    public IReadOnlyList<Choice<string>> Sounds => Choices.Sounds;

    public string DoneSound
    {
        get => _draft.Value.DoneSound;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == DoneSound) return;
            _draft.Edit(s => s with { DoneSound = value });
        }
    }

    public string AttentionSound
    {
        get => _draft.Value.AttentionSound;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == AttentionSound) return;
            _draft.Edit(s => s with { AttentionSound = value });
        }
    }

    public IRelayCommand PlayDoneSoundCommand { get; }

    public IRelayCommand PlayAttentionSoundCommand { get; }

    public bool AutoStartAvailable => _autoStart.IsAvailable;

    public string AutoStartNote => AutoStartAvailable ? "" : AutoStartUnavailableNote;

    public bool AutoStartEnabled
    {
        get => _autoStartEnabled;
        set
        {
            if (!AutoStartAvailable || value == _autoStartEnabled) return;

            var error = _autoStart.TrySet(value);
            if (error is null)
            {
                _autoStartEnabled = _autoStart.IsEnabled();
                AutoStartError = "";
            }
            else
            {
                AutoStartError = error;
            }
            // Toujours notifier : en cas d'échec, la case cochée par l'utilisateur doit revenir à l'état réel.
            OnPropertyChanged();
        }
    }

    public string AutoStartError
    {
        get => _autoStartError;
        private set => SetProperty(ref _autoStartError, value);
    }

    public bool TrayIconVisible
    {
        get => _draft.Value.TrayIconVisible;
        set
        {
            if (value == TrayIconVisible) return;
            if (!TrayIconEditable)
            {
                OnPropertyChanged();
                return;
            }
            _draft.Edit(s => s with { TrayIconVisible = value });
        }
    }

    public bool TrayIconEditable => _draft.Value.Visibility != VisibilityMode.Hidden;

    public string TrayIconNote => TrayIconEditable ? "" : TrayLockedNote;

    public void Dispose() => _draft.Changed -= OnDraftChanged;

    private void OnDraftChanged() => OnPropertyChanged(string.Empty);
}
```

`src/UsageNotch.Presentation/Preferences/ClaudeCodePageViewModel.cs` :
```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Page « Claude Code » : état et installation des hooks, port du récepteur, chemins utiles.</summary>
public sealed class ClaudeCodePageViewModel : ObservableObject, IDisposable
{
    public const string PortRangeError = "Le port doit être un nombre entre 1024 et 65535.";
    public const string RestartNote = "Le nouveau port sera utilisé au prochain démarrage d'UsageNotch.";
    public const string DemoNote = "Mode démo : les hooks sont écrits dans un fichier de test, jamais dans la configuration de Claude Code.";

    private readonly SettingsDraft _draft;
    private readonly IHookSetup _hooks;
    private readonly SettingsEnvironment _environment;
    private bool _installed;
    private string _lastMessage = "";
    private bool _lastActionFailed;
    private string _portText;
    private string _portError = "";

    public ClaudeCodePageViewModel(SettingsDraft draft, IHookSetup hooks, IShellActions shell, SettingsEnvironment environment)
    {
        _draft = draft;
        _hooks = hooks;
        _environment = environment;
        _installed = hooks.IsInstalled();
        _portText = FormatPort(draft.Value.Port);

        InstallCommand = new RelayCommand(() => Apply(_hooks.Install()), () => !_installed && _hooks.HookExeExists);
        UninstallCommand = new RelayCommand(() => Apply(_hooks.Uninstall()), () => _installed);
        RefreshCommand = new RelayCommand(Refresh);
        OpenClaudeFolderCommand = new RelayCommand(() => shell.OpenFolder(Path.GetDirectoryName(_hooks.SettingsPath) ?? _hooks.SettingsPath));

        _draft.Changed += OnDraftChanged;
    }

    public bool HooksInstalled => _installed;

    public string HooksStatus => _installed ? "Installés" : "Non installés";

    public string LastMessage
    {
        get => _lastMessage;
        private set => SetProperty(ref _lastMessage, value);
    }

    public bool LastActionFailed
    {
        get => _lastActionFailed;
        private set => SetProperty(ref _lastActionFailed, value);
    }

    public string ClaudeSettingsPath => _hooks.SettingsPath;

    public string HookExePath => _hooks.HookExePath;

    public string HookExeStatus => _hooks.HookExeExists ? "Présent" : "Introuvable : les hooks ne peuvent pas être installés.";

    public string DemoHint => _environment.Demo ? DemoNote : "";

    /// <summary>Texte saisi ; un port valide modifie le brouillon, sinon <see cref="PortError"/> explique pourquoi.</summary>
    public string PortText
    {
        get => _portText;
        set
        {
            _portText = value ?? "";
            if (int.TryParse(_portText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 1024 and <= 65535)
            {
                PortError = "";
                if (port != _draft.Value.Port) _draft.Edit(s => s with { Port = port });
            }
            else
            {
                PortError = PortRangeError;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(PortNote));
        }
    }

    public string PortError
    {
        get => _portError;
        private set => SetProperty(ref _portError, value);
    }

    public string PortNote =>
        _draft.Value.Port != _environment.ListeningPort ? RestartNote
        : !_environment.Listening ? $"Le port {_environment.ListeningPort} est indisponible : une autre application l'utilise peut-être. Choisissez-en un autre, puis redémarrez UsageNotch."
        : "";

    public IRelayCommand InstallCommand { get; }

    public IRelayCommand UninstallCommand { get; }

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand OpenClaudeFolderCommand { get; }

    public void Dispose() => _draft.Changed -= OnDraftChanged;

    private static string FormatPort(int port) => port.ToString(CultureInfo.InvariantCulture);

    private void Apply(HookSetupResult result)
    {
        LastMessage = result.Message;
        LastActionFailed = !result.Succeeded;
        Refresh();
    }

    private void Refresh()
    {
        _installed = _hooks.IsInstalled();
        OnPropertyChanged(nameof(HooksInstalled));
        OnPropertyChanged(nameof(HooksStatus));
        OnPropertyChanged(nameof(HookExeStatus));
        InstallCommand.NotifyCanExecuteChanged();
        UninstallCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Une saisie invalide en cours n'est pas remplacée ; sinon le texte suit le port enregistré ailleurs.</summary>
    private void OnDraftChanged()
    {
        if (PortError.Length == 0) _portText = FormatPort(_draft.Value.Port);
        OnPropertyChanged(string.Empty);
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~BehaviorPageViewModelTests|FullyQualifiedName~ClaudeCodePageViewModelTests"`
Expected: 25 tests passés (Comportement 8, Claude Code 17).

- [ ] **Step 5 : Commit**

`feat(presentation): behaviour and Claude Code page view models`

---

### Task 7 : Page À propos et ViewModel de la fenêtre

**Files:**
- Create: `src/UsageNotch.Presentation/Preferences/AboutPageViewModel.cs`
- Create: `src/UsageNotch.Presentation/Preferences/SettingsViewModel.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Preferences/AboutPageViewModelTests.cs`
- Test: `tests/UsageNotch.Presentation.Tests/Preferences/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: tout ce qui précède (Tasks 2 à 6) ; `SettingsStore` (Core) ; `IUiDispatcher`, `IAccentColorSource`, `ISoundPlayer` (Plan 2) ; les faux services (Task 4).
- Produces :
  - `sealed class AboutPageViewModel : ObservableObject, IDisposable`, constructeur `(SettingsDraft draft, IShellActions shell, SettingsEnvironment environment)` :
    - constante `DebugNote` ;
    - informations : `string VersionText`, `string DataDirectory`, `string LogsDirectory`, `string SettingsFile`, `string DemoHint` ;
    - journalisation et diagnostic : `bool DebugLogging`, `string DoctorError` ;
    - commandes : `IRelayCommand OpenDataFolderCommand`, `IRelayCommand OpenLogsFolderCommand`, `IRelayCommand OpenSettingsFileCommand`, `IRelayCommand RunDoctorCommand`.
  - `enum SettingsPageKind { Appearance, Position, Behavior, ClaudeCode, About }`.
  - `sealed record SettingsPage(SettingsPageKind Kind, string Title, object ViewModel)` dont `ToString()` rend `Title`.
  - `sealed class SettingsViewModel : ObservableObject, IDisposable` :
    - constructeur `(SettingsDraft draft, IAccentColorSource accent, AppearancePageViewModel appearance, PositionPageViewModel position, BehaviorPageViewModel behavior, ClaudeCodePageViewModel claudeCode, AboutPageViewModel about)` ;
    - pages : propriétés `Appearance`, `Position`, `Behavior`, `ClaudeCode`, `About`, `IReadOnlyList<SettingsPage> Pages`, `SettingsPage SelectedPage { get; set; }`, `void Select(SettingsPageKind kind)` ;
    - aperçu : `PreviewModel Preview` ;
    - enregistrement : `void Flush()`, `void Dispose()` ;
    - fabrique statique `SettingsViewModel Create(SettingsStore store, IUiDispatcher ui, TimeProvider time, IAccentColorSource accent, IColorPicker picker, IMonitorSource monitors, ISoundPlayer sound, IAutoStart autoStart, IHookSetup hooks, IShellActions shell, SettingsEnvironment environment)`.

Le ViewModel racine possède le brouillon. `Dispose` libère d'abord les pages (désabonnement), puis le brouillon, ce qui enregistre les modifications en attente.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Presentation.Tests/Preferences/AboutPageViewModelTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class AboutPageViewModelTests
{
    private static SettingsEnvironment Env(bool demo = false) =>
        new("0.3.0", demo, @"C:\data", @"C:\data\logs", @"C:\data\settings.json", 48666, true);

    private static (DraftFixture F, AboutPageViewModel Vm, FakeShell Shell) Create(bool demo = false)
    {
        var f = new DraftFixture();
        var shell = new FakeShell();
        return (f, new AboutPageViewModel(f.Draft, shell, Env(demo)), shell);
    }

    [Fact]
    public void Shows_version_and_folders()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.VersionText.Should().Be("UsageNotch 0.3.0");
        vm.DataDirectory.Should().Be(@"C:\data");
        vm.LogsDirectory.Should().Be(@"C:\data\logs");
        vm.SettingsFile.Should().Be(@"C:\data\settings.json");
        vm.DemoHint.Should().BeEmpty();
    }

    [Fact]
    public void Demo_mode_says_where_test_data_lives()
    {
        var (f, vm, _) = Create(demo: true);
        using var _f = f;

        vm.DemoHint.Should().Be(@"Mode démo : réglages, lecture et journaux de test dans C:\data.");
    }

    [Fact]
    public void Debug_logging_edits_the_draft()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.DebugLogging = true;

        f.Draft.Value.DebugLogging.Should().BeTrue();
    }

    [Fact]
    public void Open_commands_open_the_folders_and_the_settings_file()
    {
        var (f, vm, shell) = Create();
        using var _f = f;

        vm.OpenDataFolderCommand.Execute(null);
        vm.OpenLogsFolderCommand.Execute(null);
        vm.OpenSettingsFileCommand.Execute(null);

        shell.Folders.Should().Equal(@"C:\data", @"C:\data\logs");
        shell.Files.Should().Equal(@"C:\data\settings.json");
    }

    [Fact]
    public void Doctor_errors_are_shown_and_cleared_on_success()
    {
        var (f, vm, shell) = Create();
        using var _f = f;

        shell.DoctorError = "Diagnostic impossible : disque plein";
        vm.RunDoctorCommand.Execute(null);
        vm.DoctorError.Should().Be("Diagnostic impossible : disque plein");

        shell.DoctorError = null;
        vm.RunDoctorCommand.Execute(null);
        vm.DoctorError.Should().BeEmpty();
        shell.DoctorRuns.Should().Be(2);
    }
}
```

`tests/UsageNotch.Presentation.Tests/Preferences/SettingsViewModelTests.cs` :
```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly SettingsStore _store;

    public SettingsViewModelTests()
    {
        _store = new SettingsStore(_dir.File("settings.json"), NullLogger<SettingsStore>.Instance, _time);
        _store.Load();
    }

    public void Dispose() => _dir.Dispose();

    private SettingsViewModel Create(FakeMonitors? monitors = null) => SettingsViewModel.Create(
        _store,
        new ImmediateDispatcher(),
        _time,
        new FakeAccent(),
        new FakeColorPicker(),
        monitors ?? new FakeMonitors(),
        new FakeSound(),
        new FakeAutoStart(),
        new FakeHooks(),
        new FakeShell(),
        new SettingsEnvironment("0.3.0", false, @"C:\data", @"C:\data\logs", @"C:\data\settings.json", 48666, true));

    [Fact]
    public void The_five_spec_pages_are_listed_in_order_and_the_first_is_selected()
    {
        using var vm = Create();

        vm.Pages.Select(p => p.Title).Should().Equal("Apparence", "Position", "Comportement", "Claude Code", "À propos");
        vm.Pages.Select(p => p.Kind).Should().Equal(
            SettingsPageKind.Appearance, SettingsPageKind.Position, SettingsPageKind.Behavior, SettingsPageKind.ClaudeCode, SettingsPageKind.About);
        vm.Pages.Select(p => p.ViewModel).Should().Equal(vm.Appearance, vm.Position, vm.Behavior, vm.ClaudeCode, vm.About);
        vm.Pages[3].ToString().Should().Be("Claude Code");
        vm.SelectedPage.Should().Be(vm.Pages[0]);
    }

    [Fact]
    public void A_page_can_be_selected_by_kind_and_a_null_selection_is_ignored()
    {
        using var vm = Create();

        vm.Select(SettingsPageKind.ClaudeCode);
        vm.SelectedPage.Kind.Should().Be(SettingsPageKind.ClaudeCode);

        vm.SelectedPage = null!;
        vm.SelectedPage.Kind.Should().Be(SettingsPageKind.ClaudeCode);
    }

    [Fact]
    public void The_preview_follows_every_edit()
    {
        using var vm = Create();
        var names = new List<string?>();
        vm.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        vm.Appearance.Preset = ThemePreset.Monochrome;
        vm.Position.Edge = ScreenEdge.Bottom;

        vm.Preview.Theme.Should().Be(Theme.Monochrome);
        vm.Preview.Edge.Should().Be(ScreenEdge.Bottom);
        names.Should().Contain(nameof(SettingsViewModel.Preview));
    }

    [Fact]
    public void Flush_saves_at_once()
    {
        using var vm = Create();

        vm.Appearance.Scale = 1.3;
        vm.Flush();

        _store.Current.Scale.Should().Be(1.3);
    }

    [Fact]
    public void Dispose_saves_pending_edits_and_detaches_the_pages()
    {
        var vm = Create();
        vm.Appearance.Scale = 1.3;
        var pageNotifications = 0;
        vm.Appearance.PropertyChanged += (_, _) => pageNotifications++;

        vm.Dispose();
        _store.Save(_store.Current with { Edge = ScreenEdge.Top });

        _store.Current.Scale.Should().Be(1.3);
        pageNotifications.Should().Be(0);
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~AboutPageViewModelTests|FullyQualifiedName~SettingsViewModelTests"`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Presentation/Preferences/AboutPageViewModel.cs` :
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Page « À propos » : version, dossiers, niveau de journalisation, diagnostic.</summary>
public sealed class AboutPageViewModel : ObservableObject, IDisposable
{
    public const string DebugNote =
        "Le niveau Debug ajoute le détail des requêtes et des événements de hooks, jamais de jeton ni de contenu de prompt.";

    private readonly SettingsDraft _draft;
    private readonly SettingsEnvironment _environment;
    private string _doctorError = "";

    public AboutPageViewModel(SettingsDraft draft, IShellActions shell, SettingsEnvironment environment)
    {
        _draft = draft;
        _environment = environment;

        OpenDataFolderCommand = new RelayCommand(() => shell.OpenFolder(environment.DataDirectory));
        OpenLogsFolderCommand = new RelayCommand(() => shell.OpenFolder(environment.LogsDirectory));
        OpenSettingsFileCommand = new RelayCommand(() => shell.OpenFile(environment.SettingsFile));
        RunDoctorCommand = new RelayCommand(() => DoctorError = shell.RunDoctor() ?? "");

        _draft.Changed += OnDraftChanged;
    }

    public string VersionText => $"UsageNotch {_environment.Version}";

    public string DataDirectory => _environment.DataDirectory;

    public string LogsDirectory => _environment.LogsDirectory;

    public string SettingsFile => _environment.SettingsFile;

    public string DemoHint => _environment.Demo
        ? $"Mode démo : réglages, lecture et journaux de test dans {_environment.DataDirectory}."
        : "";

    public bool DebugLogging
    {
        get => _draft.Value.DebugLogging;
        set
        {
            if (value == DebugLogging) return;
            _draft.Edit(s => s with { DebugLogging = value });
        }
    }

    public string DoctorError
    {
        get => _doctorError;
        private set => SetProperty(ref _doctorError, value);
    }

    public IRelayCommand OpenDataFolderCommand { get; }

    public IRelayCommand OpenLogsFolderCommand { get; }

    public IRelayCommand OpenSettingsFileCommand { get; }

    public IRelayCommand RunDoctorCommand { get; }

    public void Dispose() => _draft.Changed -= OnDraftChanged;

    private void OnDraftChanged() => OnPropertyChanged(nameof(DebugLogging));
}
```

`src/UsageNotch.Presentation/Preferences/SettingsViewModel.cs` :
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Preferences;

public enum SettingsPageKind
{
    Appearance,
    Position,
    Behavior,
    ClaudeCode,
    About,
}

/// <summary>Une entrée de la navigation latérale. <see cref="ToString"/> rend le titre, lu par l'accessibilité.</summary>
public sealed record SettingsPage(SettingsPageKind Kind, string Title, object ViewModel)
{
    public override string ToString() => Title;
}

/// <summary>La fenêtre de réglages : navigation entre les cinq pages (spec §7), aperçu commun, brouillon partagé.</summary>
public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly SettingsDraft _draft;
    private readonly IAccentColorSource _accent;
    private SettingsPage _selectedPage;
    private PreviewModel _preview;
    private bool _disposed;

    public SettingsViewModel(
        SettingsDraft draft,
        IAccentColorSource accent,
        AppearancePageViewModel appearance,
        PositionPageViewModel position,
        BehaviorPageViewModel behavior,
        ClaudeCodePageViewModel claudeCode,
        AboutPageViewModel about)
    {
        _draft = draft;
        _accent = accent;
        Appearance = appearance;
        Position = position;
        Behavior = behavior;
        ClaudeCode = claudeCode;
        About = about;

        Pages =
        [
            new SettingsPage(SettingsPageKind.Appearance, "Apparence", appearance),
            new SettingsPage(SettingsPageKind.Position, "Position", position),
            new SettingsPage(SettingsPageKind.Behavior, "Comportement", behavior),
            new SettingsPage(SettingsPageKind.ClaudeCode, "Claude Code", claudeCode),
            new SettingsPage(SettingsPageKind.About, "À propos", about),
        ];
        _selectedPage = Pages[0];
        _preview = SettingsPreview.Build(draft.Value, accent.AccentHex);

        _draft.Changed += OnDraftChanged;
    }

    public static SettingsViewModel Create(
        SettingsStore store,
        IUiDispatcher ui,
        TimeProvider time,
        IAccentColorSource accent,
        IColorPicker picker,
        IMonitorSource monitors,
        ISoundPlayer sound,
        IAutoStart autoStart,
        IHookSetup hooks,
        IShellActions shell,
        SettingsEnvironment environment)
    {
        var draft = new SettingsDraft(store, ui, time);
        return new SettingsViewModel(
            draft,
            accent,
            new AppearancePageViewModel(draft, accent, picker),
            new PositionPageViewModel(draft, monitors),
            new BehaviorPageViewModel(draft, sound, autoStart),
            new ClaudeCodePageViewModel(draft, hooks, shell, environment),
            new AboutPageViewModel(draft, shell, environment));
    }

    public AppearancePageViewModel Appearance { get; }

    public PositionPageViewModel Position { get; }

    public BehaviorPageViewModel Behavior { get; }

    public ClaudeCodePageViewModel ClaudeCode { get; }

    public AboutPageViewModel About { get; }

    public IReadOnlyList<SettingsPage> Pages { get; }

    public SettingsPage SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (value is null) return;
            SetProperty(ref _selectedPage, value);
        }
    }

    public PreviewModel Preview
    {
        get => _preview;
        private set => SetProperty(ref _preview, value);
    }

    public void Select(SettingsPageKind kind) => SelectedPage = Pages.First(p => p.Kind == kind);

    /// <summary>Enregistre tout de suite les modifications en attente (perte de focus de la fenêtre).</summary>
    public void Flush() => _draft.Flush();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _draft.Changed -= OnDraftChanged;
        Appearance.Dispose();
        Position.Dispose();
        Behavior.Dispose();
        ClaudeCode.Dispose();
        About.Dispose();
        _draft.Dispose();
    }

    private void OnDraftChanged() => Preview = SettingsPreview.Build(_draft.Value, _accent.AccentHex);
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~AboutPageViewModelTests|FullyQualifiedName~SettingsViewModelTests"`
Expected: 10 tests passés (À propos 5, fenêtre 5).

- [ ] **Step 5 : Suite complète**

Run: `dotnet build UsageNotch.sln` puis `dotnet test`
Expected: 0 avertissement ; tous les tests passent (225 Core ; Presentation 95 + 108 = 203, soit Tasks 1 à 7 : 25 + 10 + 12 + 14 + 12 + 25 + 10).

- [ ] **Step 6 : Commit**

`feat(presentation): about page and settings window view model`

---

### Task 8 : Services système de l'application

**Files:**
- Modify: `src/UsageNotch.App/UsageNotch.App.csproj`
- Modify: `src/UsageNotch.App/Interop/NativeMethods.cs`
- Modify: `src/UsageNotch.App/Interop/MonitorService.cs`
- Create: `src/UsageNotch.App/Interop/NativeColorPicker.cs`
- Create: `src/UsageNotch.App/Hosting/AutoStartService.cs`
- Create: `src/UsageNotch.App/Hosting/HookSetupService.cs`
- Create: `src/UsageNotch.App/Hosting/ShellActions.cs`
- Modify: `src/UsageNotch.App/Hosting/DoctorCommand.cs` (remplacé en entier)
- Modify: `src/UsageNotch.App/Hosting/SingleInstance.cs`
- Modify: `src/UsageNotch.App/Hosting/AppHost.cs`
- Modify: `src/UsageNotch.App/Tray/TrayIconService.cs` (remplacé en entier)
- Modify: `src/UsageNotch.App/Views/PillWindow.xaml`

**Interfaces:**
- Consumes: `IColorPicker`, `IMonitorSource`, `IAutoStart`, `IHookSetup`, `HookSetupResult`, `IShellActions` (Task 4) ; `HexColor` (Task 1) ; `HookInstaller`, `SettingsStore` (Core) ; `AutoStart`, `AppPaths`, `AppArguments`, `DoctorReport`, `NotchViewModel` (Plan 2).
- Produces :
  - `MonitorService : IMonitorSource`.
  - `NativeColorPicker : IColorPicker, IDisposable` avec `nint Owner { get; set; }`.
  - `AutoStartService : IAutoStart`, `HookSetupService : IHookSetup`, `ShellActions : IShellActions`.
  - `sealed record DoctorOutput(string Text, string? FilePath)` et `DoctorCommand.WriteReport(AppPaths, SettingsStore)` ; `DoctorCommand.Run` garde sa signature.
  - `TrayIconService`, dont le constructeur devient `(NotchViewModel vm, IHookSetup hooks, IShellActions shell, IAutoStart autoStart, AppPaths paths, ILogger<TrayIconService> logger)`. `Start(Func<Task> quit, Action openSettings)` ne change pas, et le clic gauche appelle `openSettings`.
  - Enregistrements DI de tous ces services.

Cette tâche prépare l'application sans encore créer de fenêtre.
- **Icône de notification.** Elle passe par les mêmes services que la future fenêtre, sans code dupliqué : hooks, démarrage avec Windows, diagnostic sans console.
- **Pilule.** Elle applique enfin `Theme.PillOpacity` à sa forme.
- **Instance déjà lancée.** Une seconde instance l'autorise à passer au premier plan, pour que la fenêtre de réglages de la Task 9 s'ouvre devant les autres.

- [ ] **Step 1 : Version et déclarations Win32**

Dans `src/UsageNotch.App/UsageNotch.App.csproj`, ajouter dans le premier `<PropertyGroup>`, après `<ApplicationManifest>app.manifest</ApplicationManifest>` :
```xml
    <Version>0.3.0</Version>
```

Dans `src/UsageNotch.App/Interop/NativeMethods.cs`, ajouter après la ligne `public const int ATTACH_PARENT_PROCESS = -1;` :
```csharp

    public const int ASFW_ANY = -1;
    public const uint CC_RGBINIT = 0x00000001;
    public const uint CC_FULLOPEN = 0x00000002;
```
puis, juste avant l'accolade fermante de la classe (après la déclaration de `AttachConsole`) :
```csharp

    [StructLayout(LayoutKind.Sequential)]
    public struct CHOOSECOLOR
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        public uint rgbResult;
        public nint lpCustColors;
        public uint Flags;
        public nint lCustData;
        public nint lpfnHook;
        public nint lpTemplateName;
    }

    [DllImport("comdlg32.dll", EntryPoint = "ChooseColorW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ChooseColor(ref CHOOSECOLOR lpcc);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AllowSetForegroundWindow(int dwProcessId);
```

- [ ] **Step 2 : Écrans et sélecteur de couleur**

Dans `src/UsageNotch.App/Interop/MonitorService.cs`, ajouter `using UsageNotch.Presentation.Services;` sous `using UsageNotch.Core.Placement;` et remplacer `public sealed class MonitorService` par :
```csharp
public sealed class MonitorService : IMonitorSource
```

`src/UsageNotch.App/Interop/NativeColorPicker.cs` :
```csharp
using System.Runtime.InteropServices;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Interop;

/// <summary>Boîte de dialogue « Couleurs » de Windows. Ses 16 couleurs personnalisées sont gardées pendant la session.</summary>
public sealed class NativeColorPicker : IColorPicker, IDisposable
{
    private const int CustomColorCount = 16;

    private nint _customColors;

    public NativeColorPicker()
    {
        _customColors = Marshal.AllocHGlobal(CustomColorCount * sizeof(uint));
        for (var i = 0; i < CustomColorCount; i++)
        {
            Marshal.WriteInt32(_customColors, i * sizeof(uint), 0x00FFFFFF);
        }
    }

    /// <summary>Fenêtre propriétaire de la boîte (la fenêtre de réglages) ; 0 si aucune.</summary>
    public nint Owner { get; set; }

    public string? Pick(string initialHex)
    {
        ObjectDisposedException.ThrowIf(_customColors == 0, this);

        var dialog = new NativeMethods.CHOOSECOLOR
        {
            lStructSize = Marshal.SizeOf<NativeMethods.CHOOSECOLOR>(),
            hwndOwner = Owner,
            rgbResult = HexColor.TryNormalize(initialHex, out var hex) ? HexColor.ToColorRef(hex) : 0,
            lpCustColors = _customColors,
            Flags = NativeMethods.CC_RGBINIT | NativeMethods.CC_FULLOPEN,
        };
        return NativeMethods.ChooseColor(ref dialog) ? HexColor.FromColorRef(dialog.rgbResult) : null;
    }

    public void Dispose()
    {
        if (_customColors == 0) return;
        Marshal.FreeHGlobal(_customColors);
        _customColors = 0;
    }
}
```

- [ ] **Step 3 : Démarrage avec Windows, hooks, ouvertures**

`src/UsageNotch.App/Hosting/AutoStartService.cs` :
```csharp
using System.IO;
using System.Security;
using UsageNotch.App.Interop;
using UsageNotch.Presentation;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

/// <summary>« Démarrer avec Windows ». En démo, la vraie valeur Run de HKCU n'est jamais lue ni modifiée.</summary>
public sealed class AutoStartService(AppArguments args) : IAutoStart
{
    public bool IsAvailable => !args.Demo;

    public bool IsEnabled()
    {
        if (!IsAvailable) return false;
        try
        {
            return AutoStart.IsEnabled();
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    public string? TrySet(bool enabled)
    {
        if (!IsAvailable) return "Démarrer avec Windows est indisponible en mode démo.";
        try
        {
            AutoStart.Set(enabled, Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "UsageNotch.App.exe"));
            return null;
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            return "Impossible de modifier le démarrage avec Windows : " + e.Message;
        }
    }
}
```

`src/UsageNotch.App/Hosting/HookSetupService.cs` :
```csharp
using System.IO;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Hooks;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

/// <summary>Installation des hooks : les exceptions de <see cref="HookInstaller"/> deviennent des messages en français.</summary>
public sealed class HookSetupService(HookInstaller installer, ILogger<HookSetupService> logger) : IHookSetup
{
    private const string FailurePrefix = "Impossible de modifier les hooks Claude Code : ";

    public string SettingsPath => installer.SettingsPath;

    public string HookExePath => installer.HookExePath;

    public bool HookExeExists => File.Exists(installer.HookExePath);

    public bool IsInstalled()
    {
        try
        {
            return installer.IsInstalled();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public HookSetupResult Install() => Run("installation", installer.Install);

    public HookSetupResult Uninstall() => Run("désinstallation", installer.Uninstall);

    private HookSetupResult Run(string action, Func<string> operation)
    {
        try
        {
            var message = operation();
            logger.LogInformation("Hooks Claude Code, {Action} : {Message}", action, message);
            return new HookSetupResult(true, message);
        }
        catch (FileNotFoundException)
        {
            return Fail(action, $"exécutable hook introuvable : {installer.HookExePath}");
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Fail(action, e.Message);
        }
    }

    private HookSetupResult Fail(string action, string reason)
    {
        logger.LogWarning("Hooks Claude Code, échec de la {Action} : {Reason}", action, reason);
        return new HookSetupResult(false, FailurePrefix + reason);
    }
}
```

`src/UsageNotch.App/Hosting/ShellActions.cs` :
```csharp
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

public sealed class ShellActions(AppPaths paths, SettingsStore settings, ILogger<ShellActions> logger) : IShellActions
{
    public void OpenFolder(string path) => Start("explorer.exe", $"\"{path}\"");

    public void OpenFile(string path) => Start("notepad.exe", $"\"{path}\"");

    public string? RunDoctor()
    {
        DoctorOutput output;
        try
        {
            output = DoctorCommand.WriteReport(paths, settings);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Diagnostic impossible");
            return "Diagnostic impossible : " + e.Message;
        }

        if (output.FilePath is null) return $"Le diagnostic n'a pas pu être écrit dans {paths.LogsDirectory}.";
        OpenFile(output.FilePath);
        return null;
    }

    private void Start(string file, string arguments)
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
}
```

- [ ] **Step 4 : Diagnostic sans console**

Remplacer `src/UsageNotch.App/Hosting/DoctorCommand.cs` par :
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

/// <summary><paramref name="FilePath"/> : chemin de doctor.txt, ou null si l'écriture a échoué.</summary>
public sealed record DoctorOutput(string Text, string? FilePath);

public static class DoctorCommand
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary><c>UsageNotch.App.exe doctor</c> : écrit le rapport puis l'affiche sur la console qui a lancé l'application.</summary>
    public static string Run(AppPaths paths, SettingsStore settings)
    {
        var output = WriteReport(paths, settings);
        NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), Utf8) { AutoFlush = true };
        stdout.Write(output.Text);
        return output.Text;
    }

    /// <summary>Construit le rapport et l'écrit dans logs\doctor.txt, sans console : utilisable depuis l'interface.</summary>
    public static DoctorOutput WriteReport(AppPaths paths, SettingsStore settings)
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

        var file = Path.Combine(paths.LogsDirectory, "doctor.txt");
        try
        {
            Directory.CreateDirectory(paths.LogsDirectory);
            File.WriteAllText(file, text, Utf8);
            return new DoctorOutput(text, file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Le rapport reste affichable sur la console.
            return new DoctorOutput(text, null);
        }
    }
}
```

- [ ] **Step 5 : Premier plan pour l'instance existante**

Dans `src/UsageNotch.App/Hosting/SingleInstance.cs`, ajouter `using UsageNotch.App.Interop;` sous `using System.Net.Http;` et, au début de `SignalExistingAsync`, avant le `try` :
```csharp
        // Windows n'autorise un processus d'arrière-plan à passer au premier plan que si le processus au premier plan
        // (celui-ci, lancé par l'utilisateur) le lui permet : la fenêtre de réglages s'ouvre ainsi devant les autres.
        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
```

- [ ] **Step 6 : Icône de notification via les services**

Remplacer `src/UsageNotch.App/Tray/TrayIconService.cs` par :
```csharp
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

        menu.Opened += (_, _) =>
        {
            _lockItem.IsChecked = vm.Locked;
            _hooksItem.IsChecked = hooks.IsInstalled();
            if (autoStart.IsAvailable) _autoStartItem.IsChecked = autoStart.IsEnabled();
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
```

`using System.Diagnostics` et `using System.IO` ont disparu avec le code qu'ils servaient. `ActivityKind` n'est donc plus ambigu avec `System.Diagnostics.ActivityKind` et s'écrit sans qualification. S'il reste ambigu malgré tout, qualifier en `UsageNotch.Presentation.Pill.ActivityKind` et le signaler.

- [ ] **Step 7 : Opacité de la pilule**

Dans `src/UsageNotch.App/Views/PillWindow.xaml`, sur l'élément `<Path x:Name="PillPath" …>`, ajouter l'attribut :
```xml
            Opacity="{Binding Theme.PillOpacity}"
```
Le contenu (anneau, pourcentage) et la bande repliée ne sont pas concernés.

- [ ] **Step 8 : Enregistrements DI**

Dans `src/UsageNotch.App/Hosting/AppHost.cs`, après la ligne `s.AddSingleton<IAccentColorSource, SystemAccentColor>();`, ajouter :
```csharp
        s.AddSingleton<IMonitorSource>(sp => sp.GetRequiredService<MonitorService>());
        s.AddSingleton<NativeColorPicker>();
        s.AddSingleton<IColorPicker>(sp => sp.GetRequiredService<NativeColorPicker>());
        s.AddSingleton<IAutoStart, AutoStartService>();
        s.AddSingleton<IHookSetup, HookSetupService>();
        s.AddSingleton<IShellActions, ShellActions>();
```
Les types sont déjà importés (`UsageNotch.App.Interop`, `UsageNotch.Presentation.Services`).

- [ ] **Step 9 : Compiler et tester**

Run: `dotnet build UsageNotch.sln` puis `dotnet test`
Expected: 0 avertissement ; tous les tests passent (225 Core, 203 Presentation).

- [ ] **Step 10 : Vérifier à l'exécution**

Procédure « Préparation ». Puis :

1. Diagnostic en console, toujours fonctionnel :
   ```powershell
   & $exe doctor --demo | Out-String
   Test-Path (Join-Path $demoDir 'logs\doctor.txt')
   ```
   Attendu : le rapport commence par « UsageNotch — diagnostic du », puis `True`.
2. Pilule de référence. Démarrer la démo (première ligne de la procédure, sans la seconde instance). Capture `task8-opaque`, recadrée sur `Get-WindowRect 'UsageNotch — pilule'`. `Stop-Process -Id $app.Id`.
3. Pilule translucide. Écrire des réglages de démo au thème personnalisé d'opacité 0,35, démarrer la démo, capture `task8-translucent` sur la pilule :
   ```powershell
   New-Item -ItemType Directory -Force $demoDir | Out-Null
   $json = '{ "port": 48667, "themePreset": "Custom", "customTheme": { "pillBackground": "#000000", "pillBorder": "#2E2E2E", "ringTrack": "#3A3A3A", "levelAmple": "#28E07B", "levelWatch": "#F5E400", "levelCritical": "#FF4500", "running": "#28E07B", "attention": "#FFBF00", "done": "#57C7FF", "text": "#FFFFFF", "pillOpacity": 0.35, "thresholdWatch": 0.5, "thresholdCritical": 0.8 } }'
   [IO.File]::WriteAllText((Join-Path $demoDir 'settings.json'), $json)
   $app = Start-Process $exe -ArgumentList '--demo' -PassThru; Start-Sleep -Seconds 4
   ```
   Attendu : le fond de la pilule laisse voir le bureau ; l'anneau et « 73 % » restent opaques. `Stop-Process -Id $app.Id`, puis supprimer `settings.json` de la démo.
4. Aucun processus `UsageNotch.App` restant ; ports 48666 et 48667 libres.

- [ ] **Step 11 : Commit**

`feat(app): system services for the settings window`

---

### Task 9 : Fenêtre de réglages, aperçu et page Apparence

**Files:**
- Create: `src/UsageNotch.App/Views/Preferences/PreferencesStyles.xaml`
- Create: `src/UsageNotch.App/Controls/PillPreview.cs`
- Create: `src/UsageNotch.App/Views/Preferences/AppearancePage.xaml`
- Create: `src/UsageNotch.App/Views/Preferences/AppearancePage.xaml.cs`
- Create: `src/UsageNotch.App/Views/Preferences/SettingsWindow.xaml`
- Create: `src/UsageNotch.App/Views/Preferences/SettingsWindow.xaml.cs`
- Create: `src/UsageNotch.App/Hosting/SettingsWindowHost.cs`
- Modify: `src/UsageNotch.App/Hosting/NotchShell.cs` (remplacé en entier)
- Modify: `src/UsageNotch.App/Hosting/AppHost.cs`

**Interfaces:**
- Consumes: `SettingsViewModel`, `SettingsPageKind`, `PreviewModel`, `PreviewSample`, `AppearancePageViewModel`, `ColorSlot`, `SettingsEnvironment` (Tasks 3 à 7) ; services de la Task 8 ; `PillShapeBuilder`, `ProgressRing`, `HexBrushConverter`, `PillWindow`, `CardWindow`, `NotchPlacer`, `TrayIconService`, `SettingsFileWatcher` (Plan 2) ; `HookListener` (`Port`, `IsListening`, `OpenSettingsRequested`).
- Produces :
  - `SettingsWindow(SettingsViewModel vm, NativeColorPicker picker)`, titre « UsageNotch — réglages ».
  - `PillPreview : ContentControl` avec la propriété de dépendance `PreviewModel? Model`.
  - `SettingsWindowHost` avec `void Show(SettingsPageKind? page = null)` et `Dispose()`.
  - `NotchShell` : le menu de la pilule, le menu et le clic gauche de l'icône, et la seconde instance ouvrent tous la fenêtre de réglages.
  - Dictionnaire de styles partagé par les pages (`PageTitle`, `SectionTitle`, `FieldLabel`, `NoteText`, `ErrorText`, `PathText`, `NavItem`, styles implicites).
  - Identifiants d'automatisation : `Nav`, `Preview`, `Preset`, `Color_<Key>`, `Pick_<Key>`, `PillOpacity`, `ThresholdWatch`, `ThresholdCritical`, `Scale`, `CellContent`.

Règles de la fenêtre :
- **Une seule fenêtre.** La redemander la ramène au premier plan, restaurée si elle était réduite.
- **Un ViewModel neuf à chaque ouverture.** La fermeture le libère, ce qui enregistre les modifications en attente.
- **Perte de focus.** La fenêtre enregistre aussitôt.
- **Écrans.** À chaque activation, la liste des écrans est relue.
- **Champs de texte.** Ils poussent leur saisie à la perte de focus ou sur Entrée, pour que le texte ne soit pas réécrit pendant la frappe.
- **Sections suivantes.** Les pages Position, Comportement, Claude Code et À propos arrivent aux Tasks 10 et 11 ; en attendant, leur contenu affiche le nom de leur type.
- **Styles dans chaque page.** Chaque page fusionne `PreferencesStyles.xaml` dans ses propres ressources. Une `StaticResource` est résolue pendant `InitializeComponent`, avant que la page soit dans la fenêtre : elle ne verrait pas les ressources de la fenêtre.

- [ ] **Step 1 : Styles partagés**

`src/UsageNotch.App/Views/Preferences/PreferencesStyles.xaml` :
```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style x:Key="PageTitle" TargetType="TextBlock">
    <Setter Property="FontSize" Value="22" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Margin" Value="0,4,0,4" />
  </Style>
  <Style x:Key="SectionTitle" TargetType="TextBlock">
    <Setter Property="FontSize" Value="15" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Margin" Value="0,18,0,8" />
  </Style>
  <Style x:Key="FieldLabel" TargetType="TextBlock">
    <Setter Property="VerticalAlignment" Value="Center" />
    <Setter Property="Margin" Value="0,0,12,0" />
  </Style>
  <Style x:Key="NoteText" TargetType="TextBlock">
    <Setter Property="Foreground" Value="#5C5C5C" />
    <Setter Property="TextWrapping" Value="Wrap" />
    <Setter Property="Margin" Value="0,4,0,0" />
    <Style.Triggers>
      <Trigger Property="Text" Value="">
        <Setter Property="Visibility" Value="Collapsed" />
      </Trigger>
    </Style.Triggers>
  </Style>
  <Style x:Key="ErrorText" TargetType="TextBlock" BasedOn="{StaticResource NoteText}">
    <Setter Property="Foreground" Value="#C42B1C" />
  </Style>
  <Style x:Key="PathText" TargetType="TextBox">
    <Setter Property="IsReadOnly" Value="True" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="TextWrapping" Value="Wrap" />
    <Setter Property="Margin" Value="0,2,0,0" />
  </Style>
  <Style x:Key="NavItem" TargetType="ListBoxItem">
    <Setter Property="Padding" Value="16,9" />
    <Setter Property="FontSize" Value="14" />
    <Setter Property="AutomationProperties.Name" Value="{Binding Title}" />
  </Style>

  <Style TargetType="ComboBox">
    <Setter Property="MinWidth" Value="240" />
    <Setter Property="HorizontalAlignment" Value="Left" />
    <Setter Property="Padding" Value="8,4" />
  </Style>
  <!-- Toutes les listes déroulantes des réglages portent des Choice<T> : leur libellé sert de nom d'accessibilité. -->
  <Style TargetType="ComboBoxItem">
    <Setter Property="AutomationProperties.Name" Value="{Binding Label}" />
  </Style>
  <Style TargetType="Button">
    <Setter Property="Padding" Value="12,4" />
    <Setter Property="MinWidth" Value="90" />
    <Setter Property="HorizontalAlignment" Value="Left" />
  </Style>
  <Style TargetType="CheckBox">
    <Setter Property="Margin" Value="0,4" />
  </Style>
  <Style TargetType="Slider">
    <Setter Property="Width" Value="260" />
    <Setter Property="VerticalAlignment" Value="Center" />
    <Setter Property="IsSnapToTickEnabled" Value="True" />
  </Style>
</ResourceDictionary>
```

- [ ] **Step 2 : Aperçu des pilules**

`src/UsageNotch.App/Controls/PillPreview.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using UsageNotch.App.Converters;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Pill;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.App.Controls;

/// <summary>
/// Aperçu statique des pilules d'exemple : même forme, mêmes couleurs, même échelle que la vraie pilule, sans animation en
/// boucle (aucun coût processeur au repos). En mode Replié, la bande de chaque exemple est dessinée à côté de sa pilule.
/// </summary>
public sealed class PillPreview : ContentControl
{
    public const string HiddenNote = "Mode Masqué : la pilule n'est pas affichée ; l'icône de notification reste disponible.";

    private const double SampleSpacing = 18;
    private const double BandGap = 10;
    private const double EdgeLineThickness = 2;

    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(PreviewModel), typeof(PillPreview), new PropertyMetadata(null, (d, _) => ((PillPreview)d).Rebuild()));

    private static readonly SolidColorBrush CaptionBrush = Frozen(Color.FromRgb(0x9A, 0x9A, 0x9A));
    private static readonly SolidColorBrush EdgeBrush = Frozen(Color.FromRgb(0x80, 0x80, 0x80));
    private static readonly SolidColorBrush NoteBackground = Frozen(Color.FromArgb(0xB0, 0x00, 0x00, 0x00));
    private static readonly Geometry RunningArc = FrozenGeometry("M 22,8 A 14,14 0 0 1 36,22");

    public PillPreview()
    {
        Focusable = false;
        IsTabStop = false;
    }

    public PreviewModel? Model
    {
        get => (PreviewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private void Rebuild()
    {
        if (Model is not { } model)
        {
            Content = null;
            return;
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var sample in model.Samples) row.Children.Add(BuildSample(model, sample));

        var root = new StackPanel();
        root.Children.Add(row);
        if (model.Visibility == VisibilityMode.Hidden)
        {
            root.Children.Add(new Border
            {
                Background = NoteBackground,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock { Text = HiddenNote, Foreground = Brushes.White, FontSize = 12 },
            });
        }
        Content = root;
    }

    private static StackPanel BuildSample(PreviewModel model, PreviewSample sample)
    {
        var vertical = model.Edge is ScreenEdge.Right or ScreenEdge.Left;
        var thickness = PillMetrics.Thickness * model.Scale;
        var length = PillMetrics.WindowLength * model.Scale;
        var fillet = PillMetrics.Fillet * model.Scale;

        var shapes = new StackPanel
        {
            Orientation = vertical ? Orientation.Horizontal : Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        shapes.Children.Add(BuildPill(model, sample.Cell, thickness, length, fillet, vertical));
        if (model.Visibility == VisibilityMode.Folded)
        {
            var band = BuildBand(model, sample.Cell, thickness, length, fillet, vertical);
            band.Margin = vertical ? new Thickness(BandGap, 0, 0, 0) : new Thickness(0, BandGap, 0, 0);
            shapes.Children.Add(band);
        }

        var column = new StackPanel { Margin = new Thickness(SampleSpacing, 0, SampleSpacing, 0), VerticalAlignment = VerticalAlignment.Center };
        column.Children.Add(shapes);
        column.Children.Add(new TextBlock
        {
            Text = sample.Caption,
            FontSize = 11,
            Foreground = CaptionBrush,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        return column;
    }

    private static Canvas BuildPill(PreviewModel model, CellModel cell, double thickness, double length, double fillet, bool vertical)
    {
        var theme = model.Theme;
        var canvas = new Canvas { Width = vertical ? thickness : length, Height = vertical ? length : thickness };
        canvas.Children.Add(new Path
        {
            Data = PillShapeBuilder.Pill(model.Edge, thickness, length, PillMetrics.CornerRadius * model.Scale, fillet),
            Fill = HexBrushConverter.ToBrush(theme.PillBackground),
            Stroke = HexBrushConverter.ToBrush(theme.PillBorder),
            StrokeThickness = 1,
            Opacity = theme.PillOpacity,
        });

        var bodyRect = PillShapeBuilder.Body(model.Edge, thickness, length, fillet);
        var body = new Grid { Width = bodyRect.Width, Height = bodyRect.Height, Opacity = cell.Dimmed ? 0.5 : 1.0 };
        Canvas.SetLeft(body, bodyRect.X);
        Canvas.SetTop(body, bodyRect.Y);

        var stack = new StackPanel
        {
            Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LayoutTransform = new ScaleTransform(model.Scale, model.Scale),
        };
        if (cell.ShowRing) stack.Children.Add(BuildRing(cell, vertical));
        if (cell.ShowPercent)
        {
            stack.Children.Add(new TextBlock
            {
                Text = cell.PercentText,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = HexBrushConverter.ToBrush(cell.TextColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        body.Children.Add(stack);
        canvas.Children.Add(body);
        canvas.Children.Add(EdgeLine(model.Edge, canvas.Width, canvas.Height));
        return canvas;
    }

    private static Grid BuildRing(CellModel cell, bool vertical)
    {
        var host = new Grid
        {
            Width = PillMetrics.RingSize,
            Height = PillMetrics.RingSize,
            Margin = vertical ? new Thickness(0, 0, 0, 4) : new Thickness(0, 0, 6, 0),
        };
        host.Children.Add(new ProgressRing
        {
            RingThickness = 5,
            RingBrush = HexBrushConverter.ToBrush(cell.RingColor),
            TrackBrush = HexBrushConverter.ToBrush(cell.TrackColor),
            // Fraction d'abord : l'animation lancée par TargetFraction part alors de la valeur finale, sans balayage à chaque retouche.
            Fraction = cell.RingFraction ?? 0,
            TargetFraction = cell.RingFraction,
        });

        var activity = HexBrushConverter.ToBrush(cell.ActivityColor);
        switch (cell.Activity)
        {
            case ActivityKind.Running:
                host.Children.Add(new Path
                {
                    Width = PillMetrics.RingSize,
                    Height = PillMetrics.RingSize,
                    Stretch = Stretch.None,
                    Data = RunningArc,
                    StrokeThickness = 2.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Stroke = activity,
                });
                break;
            case ActivityKind.Attention:
                host.Children.Add(new Ellipse { Width = 28, Height = 28, StrokeThickness = 2.5, Stroke = activity });
                break;
            case ActivityKind.Done:
                host.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = activity });
                break;
        }
        return host;
    }

    private static Canvas BuildBand(PreviewModel model, CellModel cell, double thickness, double length, double fillet, bool vertical)
    {
        var canvas = new Canvas { Width = vertical ? thickness : length, Height = vertical ? length : thickness };
        var band = PillShapeBuilder.Band(model.Edge, thickness, length, model.FoldedThicknessPx, fillet);
        var rect = new Rectangle { Width = band.Width, Height = band.Height, Fill = HexBrushConverter.ToBrush(cell.BandColor) };
        Canvas.SetLeft(rect, band.X);
        Canvas.SetTop(rect, band.Y);
        canvas.Children.Add(rect);
        canvas.Children.Add(EdgeLine(model.Edge, canvas.Width, canvas.Height));
        return canvas;
    }

    /// <summary>Un trait gris figure le bord de l'écran, pour lire l'orientation de la pilule.</summary>
    private static Rectangle EdgeLine(ScreenEdge edge, double width, double height)
    {
        var alongHeight = edge is ScreenEdge.Right or ScreenEdge.Left;
        var line = new Rectangle
        {
            Fill = EdgeBrush,
            Width = alongHeight ? EdgeLineThickness : width,
            Height = alongHeight ? height : EdgeLineThickness,
        };
        Canvas.SetLeft(line, edge switch { ScreenEdge.Right => width, ScreenEdge.Left => -EdgeLineThickness, _ => 0 });
        Canvas.SetTop(line, edge switch { ScreenEdge.Bottom => height, ScreenEdge.Top => -EdgeLineThickness, _ => 0 });
        return line;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Geometry FrozenGeometry(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }
}
```

Les champs statiques sont initialisés dans l'ordre du texte : `ModelProperty` n'utilise aucun des pinceaux, qui sont prêts avant toute instance.

- [ ] **Step 3 : Page Apparence**

`src/UsageNotch.App/Views/Preferences/AppearancePage.xaml` :
```xml
<UserControl x:Class="UsageNotch.App.Views.Preferences.AppearancePage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:conv="clr-namespace:UsageNotch.App.Converters"
             xmlns:pref="clr-namespace:UsageNotch.Presentation.Preferences;assembly=UsageNotch.Presentation">
  <UserControl.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="PreferencesStyles.xaml" />
      </ResourceDictionary.MergedDictionaries>
      <conv:HexBrushConverter x:Key="Hex" />
    </ResourceDictionary>
  </UserControl.Resources>

  <StackPanel>
    <TextBlock Text="Apparence" Style="{StaticResource PageTitle}" />

    <TextBlock Text="Thème" Style="{StaticResource SectionTitle}" />
    <ComboBox AutomationProperties.AutomationId="Preset" AutomationProperties.Name="Thème"
              ItemsSource="{Binding Presets}" DisplayMemberPath="Label" SelectedValuePath="Value"
              SelectedValue="{Binding Preset}" />
    <TextBlock Text="{x:Static pref:AppearancePageViewModel.CustomHint}" Style="{StaticResource NoteText}" />

    <TextBlock Text="Couleurs" Style="{StaticResource SectionTitle}" />
    <ItemsControl ItemsSource="{Binding Colors}" Focusable="False">
      <ItemsControl.ItemTemplate>
        <DataTemplate>
          <StackPanel Margin="0,3">
            <StackPanel Orientation="Horizontal">
              <TextBlock Text="{Binding Label}" Width="180" Style="{StaticResource FieldLabel}" />
              <Border Width="34" Height="22" CornerRadius="3" BorderBrush="#8A8A8A" BorderThickness="1" Margin="0,0,8,0"
                      Background="{Binding Hex, Converter={StaticResource Hex}}" />
              <TextBox Width="92" VerticalContentAlignment="Center"
                       Text="{Binding Hex, UpdateSourceTrigger=LostFocus}"
                       AutomationProperties.AutomationId="{Binding Key, StringFormat='Color_{0}'}"
                       AutomationProperties.Name="{Binding Label}" />
              <Button Content="Choisir…" Margin="8,0,0,0" Command="{Binding PickCommand}"
                      AutomationProperties.AutomationId="{Binding Key, StringFormat='Pick_{0}'}" />
            </StackPanel>
            <TextBlock Text="{Binding Error}" Style="{StaticResource ErrorText}" Margin="180,2,0,0" />
          </StackPanel>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>

    <TextBlock Text="Opacité et seuils" Style="{StaticResource SectionTitle}" />
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="180" />
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="Auto" />
      </Grid.ColumnDefinitions>
      <Grid.RowDefinitions>
        <RowDefinition Height="34" />
        <RowDefinition Height="34" />
        <RowDefinition Height="34" />
      </Grid.RowDefinitions>

      <TextBlock Text="Opacité du fond" Style="{StaticResource FieldLabel}" />
      <Slider Grid.Column="1" AutomationProperties.AutomationId="PillOpacity" AutomationProperties.Name="Opacité du fond"
              Minimum="0.2" Maximum="1" TickFrequency="0.05" Value="{Binding PillOpacity}" />
      <TextBlock Grid.Column="2" Text="{Binding PillOpacityText}" Margin="12,0,0,0" VerticalAlignment="Center" />

      <TextBlock Grid.Row="1" Text="Seuil de vigilance" Style="{StaticResource FieldLabel}" />
      <Slider Grid.Row="1" Grid.Column="1" AutomationProperties.AutomationId="ThresholdWatch" AutomationProperties.Name="Seuil de vigilance"
              Minimum="0.05" Maximum="0.95" TickFrequency="0.05" Value="{Binding ThresholdWatch}" />
      <TextBlock Grid.Row="1" Grid.Column="2" Text="{Binding ThresholdWatchText}" Margin="12,0,0,0" VerticalAlignment="Center" />

      <TextBlock Grid.Row="2" Text="Seuil critique" Style="{StaticResource FieldLabel}" />
      <Slider Grid.Row="2" Grid.Column="1" AutomationProperties.AutomationId="ThresholdCritical" AutomationProperties.Name="Seuil critique"
              Minimum="0.1" Maximum="1" TickFrequency="0.05" Value="{Binding ThresholdCritical}" />
      <TextBlock Grid.Row="2" Grid.Column="2" Text="{Binding ThresholdCriticalText}" Margin="12,0,0,0" VerticalAlignment="Center" />
    </Grid>

    <TextBlock Text="Taille et contenu" Style="{StaticResource SectionTitle}" />
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="180" />
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="Auto" />
      </Grid.ColumnDefinitions>
      <Grid.RowDefinitions>
        <RowDefinition Height="34" />
        <RowDefinition Height="38" />
      </Grid.RowDefinitions>

      <TextBlock Text="Échelle" Style="{StaticResource FieldLabel}" />
      <Slider Grid.Column="1" AutomationProperties.AutomationId="Scale" AutomationProperties.Name="Échelle"
              Minimum="0.4" Maximum="1.5" TickFrequency="0.05" Value="{Binding Scale}" />
      <TextBlock Grid.Column="2" Text="{Binding ScaleText}" Margin="12,0,0,0" VerticalAlignment="Center" />

      <TextBlock Grid.Row="1" Text="Contenu de la cellule" Style="{StaticResource FieldLabel}" />
      <ComboBox Grid.Row="1" Grid.Column="1" Grid.ColumnSpan="2" VerticalAlignment="Center"
                AutomationProperties.AutomationId="CellContent" AutomationProperties.Name="Contenu de la cellule"
                ItemsSource="{Binding CellContents}" DisplayMemberPath="Label" SelectedValuePath="Value"
                SelectedValue="{Binding CellContent}" />
    </Grid>
  </StackPanel>
</UserControl>
```

`src/UsageNotch.App/Views/Preferences/AppearancePage.xaml.cs` :
```csharp
using System.Windows.Controls;

namespace UsageNotch.App.Views.Preferences;

public partial class AppearancePage : UserControl
{
    public AppearancePage() => InitializeComponent();
}
```

- [ ] **Step 4 : Fenêtre**

`src/UsageNotch.App/Views/Preferences/SettingsWindow.xaml` :
```xml
<Window x:Class="UsageNotch.App.Views.Preferences.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:UsageNotch.App.Controls"
        xmlns:views="clr-namespace:UsageNotch.App.Views.Preferences"
        xmlns:pref="clr-namespace:UsageNotch.Presentation.Preferences;assembly=UsageNotch.Presentation"
        Title="UsageNotch — réglages"
        Width="940" Height="760" MinWidth="780" MinHeight="580"
        WindowStartupLocation="CenterScreen" ShowInTaskbar="True"
        FontFamily="Segoe UI" FontSize="13" Background="#F3F3F3">
  <Window.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="PreferencesStyles.xaml" />
      </ResourceDictionary.MergedDictionaries>
      <!-- Une page par type de ViewModel ; les Tasks 10 et 11 ajoutent les autres modèles ici. -->
      <DataTemplate DataType="{x:Type pref:AppearancePageViewModel}">
        <views:AppearancePage />
      </DataTemplate>
    </ResourceDictionary>
  </Window.Resources>

  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="200" />
      <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>

    <Border Background="#E9E9E9" BorderBrush="#D6D6D6" BorderThickness="0,0,1,0">
      <DockPanel>
        <TextBlock DockPanel.Dock="Top" Text="UsageNotch" FontSize="18" FontWeight="SemiBold" Margin="16,18,16,14" />
        <ListBox AutomationProperties.AutomationId="Nav" AutomationProperties.Name="Pages des réglages"
                 ItemsSource="{Binding Pages}" SelectedItem="{Binding SelectedPage}" DisplayMemberPath="Title"
                 ItemContainerStyle="{StaticResource NavItem}" Background="Transparent" BorderThickness="0" />
      </DockPanel>
    </Border>

    <Grid Grid.Column="1">
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
      </Grid.RowDefinitions>

      <!-- Moitié sombre, moitié claire : l'opacité et les couleurs se jugent sur les deux fonds. -->
      <Border Margin="20,16,20,8" Height="260" CornerRadius="8" BorderBrush="#D6D6D6" BorderThickness="1">
        <Border.Background>
          <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
            <GradientStop Color="#1E1E1E" Offset="0" />
            <GradientStop Color="#1E1E1E" Offset="0.5" />
            <GradientStop Color="#E8E8E8" Offset="0.5" />
            <GradientStop Color="#E8E8E8" Offset="1" />
          </LinearGradientBrush>
        </Border.Background>
        <Grid>
          <TextBlock Text="Aperçu" Margin="10,6" FontSize="11" Foreground="#9A9A9A" />
          <Viewbox Margin="16,24,16,12" Stretch="Uniform" StretchDirection="DownOnly">
            <controls:PillPreview AutomationProperties.AutomationId="Preview" Model="{Binding Preview}" />
          </Viewbox>
        </Grid>
      </Border>

      <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" Padding="20,4,20,20" Focusable="False">
        <ContentControl Content="{Binding SelectedPage.ViewModel}" Focusable="False" />
      </ScrollViewer>
    </Grid>
  </Grid>
</Window>
```

`src/UsageNotch.App/Views/Preferences/SettingsWindow.xaml.cs` :
```csharp
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
```

- [ ] **Step 5 : Hôte de la fenêtre**

`src/UsageNotch.App/Hosting/SettingsWindowHost.cs` :
```csharp
using System.Reflection;
using System.Windows;
using Microsoft.Extensions.Logging;
using UsageNotch.App.Interop;
using UsageNotch.App.Views.Preferences;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation;
using UsageNotch.Presentation.Preferences;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

/// <summary>
/// Une seule fenêtre de réglages à la fois : la redemander la ramène au premier plan. Chaque ouverture crée un ViewModel
/// neuf ; la fermeture le libère, ce qui enregistre les modifications en attente. À utiliser depuis le thread UI.
/// </summary>
public sealed class SettingsWindowHost(
    SettingsStore settings,
    IUiDispatcher ui,
    TimeProvider time,
    IAccentColorSource accent,
    NativeColorPicker picker,
    IMonitorSource monitors,
    ISoundPlayer sound,
    IAutoStart autoStart,
    IHookSetup hooks,
    IShellActions shell,
    AppPaths paths,
    AppArguments args,
    HookListener listener,
    ILogger<SettingsWindowHost> logger) : IDisposable
{
    private SettingsWindow? _window;
    private SettingsViewModel? _vm;

    public void Show(SettingsPageKind? page = null)
    {
        if (_window is null || _vm is null)
        {
            _vm = SettingsViewModel.Create(settings, ui, time, accent, picker, monitors, sound, autoStart, hooks, shell, BuildEnvironment());
            _window = new SettingsWindow(_vm, picker);
            _window.Closed += OnClosed;
            _window.Show();
            logger.LogInformation("Fenêtre de réglages ouverte");
        }

        if (page is { } kind) _vm.Select(kind);
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void Dispose() => _window?.Close();

    private SettingsEnvironment BuildEnvironment() => new(
        Version: Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?",
        Demo: args.Demo,
        DataDirectory: paths.DataDirectory,
        LogsDirectory: paths.LogsDirectory,
        SettingsFile: paths.SettingsFile,
        ListeningPort: listener.Port,
        Listening: listener.IsListening);

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_window is not null) _window.Closed -= OnClosed;
        _window = null;
        var vm = _vm;
        _vm = null;
        vm?.Dispose();
        logger.LogInformation("Fenêtre de réglages fermée");
    }
}
```

- [ ] **Step 6 : Routes vers la fenêtre**

Remplacer `src/UsageNotch.App/Hosting/NotchShell.cs` par :
```csharp
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
    TrayIconService tray,
    SettingsFileWatcher watcher,
    SettingsWindowHost settingsWindow,
    IUiDispatcher ui,
    ILogger<NotchShell> logger) : IDisposable
{
    private Action? _onOpenSettings;
    private PillWindow? _pill;
    private CardWindow? _card;

    public void Start()
    {
        // Seconde instance lancée à la main (spec §8) : l'événement arrive sur un thread du récepteur.
        _onOpenSettings = () => ui.Post(OpenSettings);
        listener.OpenSettingsRequested += _onOpenSettings;

        _pill = new PillWindow(viewModel, placer, settings, QuitAsync, OpenSettings);
        _card = new CardWindow(viewModel, placer, _pill);
        if (settings.Current.Visibility != VisibilityMode.Hidden) _pill.Show();

        tray.Start(QuitAsync, OpenSettings);
        watcher.Start();

        logger.LogInformation("Coquille démarrée");
    }

    private static Task QuitAsync() => ((App)Application.Current).QuitAsync(userInitiated: true);

    private void OpenSettings() => settingsWindow.Show();

    public void Dispose()
    {
        if (_onOpenSettings is not null) listener.OpenSettingsRequested -= _onOpenSettings;
        // D'abord la fenêtre de réglages : sa fermeture enregistre ses modifications par-dessus l'état courant
        // (dont AutoLaunch = false posé par Quitter).
        settingsWindow.Dispose();
        watcher.Dispose();
        tray.Dispose();
        _card?.Close();
        _pill?.Close();
        viewModel.Dispose();
    }
}
```

Dans `src/UsageNotch.App/Hosting/AppHost.cs`, ajouter après `s.AddSingleton<SettingsFileWatcher>();` :
```csharp
        s.AddSingleton<SettingsWindowHost>();
```

- [ ] **Step 7 : Compiler et tester**

Run: `dotnet build UsageNotch.sln` puis `dotnet test`
Expected: 0 avertissement ; tous les tests passent.

- [ ] **Step 8 : Vérifier à l'exécution**

Procédure « Préparation », puis « Démarrer la démo, ouvrir les réglages » (avec la seconde instance). Enregistrer un script en UTF-8 avec BOM.

1. **Ouverture par la seconde instance.** `Get-SettingsWindow` trouve la fenêtre. Capture `task9-window` recadrée sur `Get-WindowRect 'UsageNotch — réglages'`.
   Attendu : navigation à gauche avec les cinq titres, aperçu de trois pilules sur fond moitié sombre moitié clair, page Apparence.
2. **Une seule fenêtre.** Relancer `Start-Process $exe -ArgumentList '--demo' -Wait`. Attendu :
   ```powershell
   $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::NameProperty, 'UsageNotch — réglages')
   $UIA::RootElement.FindAll($Scope::Children, $cond).Count   # 1
   ```
3. **Préréglage.** `Set-Combo 'Preset' 'Monochrome'`. Attendu : `(Get-DemoSettings).themePreset` vaut `Monochrome`. Capture `task9-monochrome` de la pilule (`Get-WindowRect 'UsageNotch — pilule'`) : anneau gris.
4. **Couleur saisie.** `Set-Text 'Color_Text' '#ff4500'`. Attendu : `themePreset` vaut `Custom`, `customTheme.text` vaut `#FF4500`, `customTheme.pillBackground` vaut `#111111` (repris de Monochrome). Capture `task9-preview` de la fenêtre : pourcentages orange dans l'aperçu.
5. **Saisie invalide.** `Set-Text 'Color_Text' 'rouge'`. Attendu : `Find-Text 'Couleur invalide : « rouge ». Format attendu : #RRGGBB.'` n'est pas nul ; `(Get-DemoSettings).customTheme.text` vaut toujours `#FF4500`.
6. **Opacité.** `Set-Range 'PillOpacity' 0.4`. Attendu : `customTheme.pillOpacity` vaut `0.4`. Capture `task9-opacity` de la pilule.
7. **Échelle.** Relever `$h1 = (Get-WindowRect 'UsageNotch — pilule').Height`, puis `Set-Range 'Scale' 1.5`. Attendu : `scale` vaut `1.5` et la nouvelle hauteur vaut `$h1 * 1.5` à 1 pixel près. Remettre `Set-Range 'Scale' 1`.
8. **Contenu.** `Set-Combo 'CellContent' 'Pourcentage seul'`. Attendu : `cellContent` vaut `PercentOnly`. Capture `task9-percent-only` de la fenêtre : l'aperçu n'a plus d'anneau.
9. **Seuils.** `Set-Range 'ThresholdWatch' 0.9`. Attendu : `customTheme.thresholdWatch` vaut `0.9` et `customTheme.thresholdCritical` vaut `0.95`.
10. **Fermeture et réouverture.** `Close-Settings`, puis relancer la seconde instance. Attendu : `Get-ComboText 'Preset'` vaut `Personnalisé` et `Get-Text 'Color_Text'` vaut `#FF4500`.
11. **Processeur.** Pointeur hors de la pilule. Procédure « Mesure processeur » une première fois fenêtre ouverte, puis `Close-Settings` et une seconde fois fenêtre fermée. Attendu : l'écart entre les deux mesures ne dépasse pas 1 point (l'aperçu n'a aucune animation en boucle). Rouvrir ensuite la fenêtre par la seconde instance.
12. **Nettoyage.** Arrêter la démo et supprimer les réglages de démo. Vérifier qu'aucun processus `UsageNotch.App` ne reste et que les ports 48666 et 48667 sont libres.

- [ ] **Step 9 : Commit**

`feat(app): settings window with live preview and appearance page`

---

### Task 10 : Page Position et miniature des écrans

**Files:**
- Create: `src/UsageNotch.App/Controls/MonitorMapView.cs`
- Create: `src/UsageNotch.App/Views/Preferences/PositionPage.xaml`
- Create: `src/UsageNotch.App/Views/Preferences/PositionPage.xaml.cs`
- Modify: `src/UsageNotch.App/Views/Preferences/SettingsWindow.xaml`

**Interfaces:**
- Consumes: `PositionPageViewModel` (Task 5), `MonitorMapModel`, `MonitorTile`, `MapRect` (Task 3) ; styles de `PreferencesStyles.xaml` et `SettingsWindow` (Task 9).
- Produces :
  - `MonitorMapView : Canvas` avec les propriétés de dépendance `MonitorMapModel? Model` et `ICommand? SelectCommand`.
  - La page Position.
  - Identifiants d'automatisation : `MonitorMap`, `MonitorTile<N>`, `Monitor`, `RefreshMonitors`, `Edge`, `Position`, `Recenter`, `Visibility`, `FoldedThickness`.

- [ ] **Step 1 : Miniature des écrans**

`src/UsageNotch.App/Controls/MonitorMapView.cs` :
```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.App.Controls;

/// <summary>Miniature cliquable des écrans : un bouton par écran, l'écran choisi en surbrillance, la pilule en orange.</summary>
public sealed class MonitorMapView : Canvas
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(MonitorMapModel), typeof(MonitorMapView), new PropertyMetadata(null, OnInputChanged));

    public static readonly DependencyProperty SelectCommandProperty = DependencyProperty.Register(
        nameof(SelectCommand), typeof(ICommand), typeof(MonitorMapView), new PropertyMetadata(null, OnInputChanged));

    private static readonly ControlTemplate TileTemplate = (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>"
        + "<Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' "
        + "BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='3'>"
        + "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' /></Border></ControlTemplate>");

    private static readonly SolidColorBrush NormalFill = Frozen(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush NormalBorder = Frozen(Color.FromRgb(0x8A, 0x8A, 0x8A));
    private static readonly SolidColorBrush SelectedFill = Frozen(Color.FromRgb(0xDC, 0xEB, 0xFA));
    private static readonly SolidColorBrush SelectedBorder = Frozen(Color.FromRgb(0x00, 0x67, 0xC0));
    private static readonly SolidColorBrush MarkerFill = Frozen(Color.FromRgb(0xFF, 0x45, 0x00));

    public MonitorMapView()
    {
        Width = PositionPageViewModel.MapWidth;
        Height = PositionPageViewModel.MapHeight;
        ClipToBounds = true;
    }

    public MonitorMapModel? Model
    {
        get => (MonitorMapModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public ICommand? SelectCommand
    {
        get => (ICommand?)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    private static void OnInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MonitorMapView)d).Rebuild();

    private void Rebuild()
    {
        Children.Clear();
        if (Model is not { } model) return;

        foreach (var tile in model.Tiles)
        {
            var name = tile.IsPrimary ? $"Écran {tile.Number} (principal)" : $"Écran {tile.Number}";
            var button = new Button
            {
                Template = TileTemplate,
                // Valeurs locales : elles l'emportent sur le style implicite des boutons de la fenêtre (MinWidth 90).
                MinWidth = 0,
                Padding = new Thickness(0),
                Width = tile.Rect.Width,
                Height = tile.Rect.Height,
                Background = tile.IsSelected ? SelectedFill : NormalFill,
                BorderBrush = tile.IsSelected ? SelectedBorder : NormalBorder,
                BorderThickness = new Thickness(tile.IsSelected ? 2 : 1),
                Command = SelectCommand,
                CommandParameter = tile.DeviceId,
                Cursor = Cursors.Hand,
                ToolTip = $"{name} — {tile.DeviceId}",
                Content = new TextBlock
                {
                    Text = tile.IsPrimary ? $"{tile.Number.ToString(CultureInfo.InvariantCulture)} (principal)" : tile.Number.ToString(CultureInfo.InvariantCulture),
                    FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            };
            AutomationProperties.SetAutomationId(button, $"MonitorTile{tile.Number.ToString(CultureInfo.InvariantCulture)}");
            AutomationProperties.SetName(button, name);
            SetLeft(button, tile.Rect.X);
            SetTop(button, tile.Rect.Y);
            Children.Add(button);
        }

        if (model.PillMarker is { } marker)
        {
            var rect = new Rectangle { Width = marker.Width, Height = marker.Height, Fill = MarkerFill, IsHitTestVisible = false };
            SetLeft(rect, marker.X);
            SetTop(rect, marker.Y);
            Children.Add(rect);
        }
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
```

- [ ] **Step 2 : Page Position**

`src/UsageNotch.App/Views/Preferences/PositionPage.xaml` :
```xml
<UserControl x:Class="UsageNotch.App.Views.Preferences.PositionPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="clr-namespace:UsageNotch.App.Controls"
             xmlns:pref="clr-namespace:UsageNotch.Presentation.Preferences;assembly=UsageNotch.Presentation">
  <UserControl.Resources>
    <ResourceDictionary Source="PreferencesStyles.xaml" />
  </UserControl.Resources>

  <StackPanel>
    <TextBlock Text="Position" Style="{StaticResource PageTitle}" />

    <TextBlock Text="Écran d'ancrage" Style="{StaticResource SectionTitle}" />
    <Border HorizontalAlignment="Left" Padding="4" CornerRadius="6" BorderBrush="#D6D6D6" BorderThickness="1" Background="#FAFAFA">
      <controls:MonitorMapView AutomationProperties.AutomationId="MonitorMap" AutomationProperties.Name="Miniature des écrans"
                               Model="{Binding Map}" SelectCommand="{Binding SelectMonitorCommand}" />
    </Border>
    <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
      <ComboBox MinWidth="400" AutomationProperties.AutomationId="Monitor" AutomationProperties.Name="Écran d'ancrage"
                ItemsSource="{Binding Monitors}" DisplayMemberPath="Label" SelectedValuePath="Value"
                SelectedValue="{Binding MonitorKey}" />
      <Button Content="Actualiser" Margin="10,0,0,0" Command="{Binding RefreshMonitorsCommand}"
              AutomationProperties.AutomationId="RefreshMonitors" />
    </StackPanel>

    <TextBlock Text="Bord et position" Style="{StaticResource SectionTitle}" />
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="200" />
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="Auto" />
      </Grid.ColumnDefinitions>
      <Grid.RowDefinitions>
        <RowDefinition Height="38" />
        <RowDefinition Height="38" />
      </Grid.RowDefinitions>

      <TextBlock Text="Bord" Style="{StaticResource FieldLabel}" />
      <ComboBox Grid.Column="1" VerticalAlignment="Center" AutomationProperties.AutomationId="Edge" AutomationProperties.Name="Bord"
                ItemsSource="{Binding Edges}" DisplayMemberPath="Label" SelectedValuePath="Value" SelectedValue="{Binding Edge}" />

      <TextBlock Grid.Row="1" Text="Position le long du bord" Style="{StaticResource FieldLabel}" />
      <Slider Grid.Row="1" Grid.Column="1" AutomationProperties.AutomationId="Position" AutomationProperties.Name="Position le long du bord"
              Minimum="0" Maximum="1" TickFrequency="0.01" Value="{Binding Position}" />
      <StackPanel Grid.Row="1" Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center">
        <TextBlock Text="{Binding PositionText}" Width="48" Margin="12,0,0,0" VerticalAlignment="Center" />
        <Button Content="Recentrer" Command="{Binding RecenterCommand}" AutomationProperties.AutomationId="Recenter" />
      </StackPanel>
    </Grid>
    <TextBlock Text="{x:Static pref:PositionPageViewModel.DragHint}" Style="{StaticResource NoteText}" />

    <TextBlock Text="Visibilité" Style="{StaticResource SectionTitle}" />
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="200" />
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="Auto" />
      </Grid.ColumnDefinitions>
      <Grid.RowDefinitions>
        <RowDefinition Height="38" />
        <RowDefinition Height="38" />
      </Grid.RowDefinitions>

      <TextBlock Text="Mode" Style="{StaticResource FieldLabel}" />
      <ComboBox Grid.Column="1" VerticalAlignment="Center" AutomationProperties.AutomationId="Visibility" AutomationProperties.Name="Mode de visibilité"
                ItemsSource="{Binding Visibilities}" DisplayMemberPath="Label" SelectedValuePath="Value" SelectedValue="{Binding Visibility}" />

      <TextBlock Grid.Row="1" Text="Épaisseur de la bande repliée" Style="{StaticResource FieldLabel}" />
      <Slider Grid.Row="1" Grid.Column="1" AutomationProperties.AutomationId="FoldedThickness" AutomationProperties.Name="Épaisseur de la bande repliée"
              Minimum="2" Maximum="12" TickFrequency="1" Value="{Binding FoldedThickness}" IsEnabled="{Binding FoldedThicknessEnabled}" />
      <TextBlock Grid.Row="1" Grid.Column="2" Text="{Binding FoldedThicknessText}" Margin="12,0,0,0" VerticalAlignment="Center" />
    </Grid>
    <TextBlock Text="{Binding VisibilityHint}" Style="{StaticResource NoteText}" />
  </StackPanel>
</UserControl>
```

`src/UsageNotch.App/Views/Preferences/PositionPage.xaml.cs` :
```csharp
using System.Windows.Controls;

namespace UsageNotch.App.Views.Preferences;

public partial class PositionPage : UserControl
{
    public PositionPage() => InitializeComponent();
}
```

Dans `src/UsageNotch.App/Views/Preferences/SettingsWindow.xaml`, ajouter après le `DataTemplate` d'`AppearancePageViewModel` :
```xml
      <DataTemplate DataType="{x:Type pref:PositionPageViewModel}">
        <views:PositionPage />
      </DataTemplate>
```

- [ ] **Step 3 : Compiler et tester**

Run: `dotnet build UsageNotch.sln` puis `dotnet test`
Expected: 0 avertissement ; tous les tests passent.

- [ ] **Step 4 : Vérifier à l'exécution**

Procédure « Préparation », puis « Démarrer la démo, ouvrir les réglages ». `Select-Page 'Position'`.

1. **Page.** Capture `task10-position` de la fenêtre. Attendu : miniature avec un bouton par écran, l'écran principal en surbrillance et un repère orange au bord droit, liste « Écran principal », bord « Droite », position « 50 % », mode « Déplié ».
2. **Un bouton par écran.** `$count = [System.Windows.Forms.Screen]::AllScreens.Count`, puis `Find-Ui "MonitorTile$n"` pour chaque `$n` de 1 à `$count`. Attendu : aucun échec.
3. **Bord.** `$right = Get-WindowRect 'UsageNotch — pilule'`, puis `Set-Combo 'Edge' 'Haut'`. Attendu : `edge` vaut `Top` ; la pilule a `Width` supérieur à `Height`. Capture `task10-top` de la pilule.
4. **Position et Recentrer.** `$mid = (Get-WindowRect 'UsageNotch — pilule').Left`, puis `Set-Range 'Position' 0`. Attendu : `positionTop` vaut `0` et la nouvelle valeur de `Left` est inférieure à `$mid`. `Invoke-Ui 'Recenter'`. Attendu : `positionTop` vaut `0.5` et `Left` revient à `$mid`.
5. **Retour à droite.** `Set-Combo 'Edge' 'Droite'`. Attendu : la pilule retrouve le rectangle `$right` (position du bord droit conservée).
6. **Mode Replié.** `Set-Combo 'Visibility' 'Replié'`. Attendu : `(Find-Ui 'FoldedThickness').Current.IsEnabled` vaut `True`. `Set-Range 'FoldedThickness' 12`. Attendu : `foldedThicknessPx` vaut `12`. Capture `task10-folded` de la pilule : seule une bande épaisse est visible au bord.
7. **Mode Masqué.** `Set-Combo 'Visibility' 'Masqué'`. Attendu : `Find-Text` du texte de `PositionPageViewModel.HiddenHint` n'est pas nul ; l'aperçu affiche la note du mode Masqué (capture `task10-hidden` de la fenêtre). `Set-Combo 'Visibility' 'Déplié'`. Attendu : la case d'épaisseur redevient inactive et la pilule réapparaît.
8. **Écran par bouton.** `Invoke-Ui 'MonitorTile1'`. Attendu : `monitorDeviceId` commence par `\\.\DISPLAY` et `Get-ComboText 'Monitor'` commence par « Écran 1 ». Puis `Set-Combo 'Monitor' 'Écran principal'`. Attendu : `monitorDeviceId` est `$null`.
   Avec plusieurs écrans, `Invoke-Ui 'MonitorTile2'` doit déplacer la pilule sur l'écran 2 (capture `task10-monitor2`), puis revenir à « Écran principal ». Avec un seul écran, le noter dans le rapport.
9. **Nettoyage.** Arrêter la démo, supprimer les réglages de démo, vérifier les processus et les ports.

- [ ] **Step 5 : Commit**

`feat(app): position page with monitor map`

---

### Task 11 : Pages Comportement, Claude Code et À propos

**Files:**
- Create: `src/UsageNotch.App/Views/Preferences/BehaviorPage.xaml`
- Create: `src/UsageNotch.App/Views/Preferences/BehaviorPage.xaml.cs`
- Create: `src/UsageNotch.App/Views/Preferences/ClaudeCodePage.xaml`
- Create: `src/UsageNotch.App/Views/Preferences/ClaudeCodePage.xaml.cs`
- Create: `src/UsageNotch.App/Views/Preferences/AboutPage.xaml`
- Create: `src/UsageNotch.App/Views/Preferences/AboutPage.xaml.cs`
- Modify: `src/UsageNotch.App/Views/Preferences/SettingsWindow.xaml`

**Interfaces:**
- Consumes: `BehaviorPageViewModel`, `ClaudeCodePageViewModel` (Task 6), `AboutPageViewModel` (Task 7) ; styles (Task 9).
- Produces : les trois pages. Identifiants d'automatisation :

  | Page | Identifiants |
  |---|---|
  | Comportement | `AutoOpenCard`, `SoundEnabled`, `DoneSound`, `PlayDoneSound`, `AttentionSound`, `PlayAttentionSound`, `AutoStart`, `TrayIconVisible` |
  | Claude Code | `HooksStatus`, `InstallHooks`, `UninstallHooks`, `RefreshHooks`, `HooksMessage`, `ClaudeSettingsPath`, `OpenClaudeFolder`, `HookExePath`, `Port` |
  | À propos | `Version`, `DataDirectory`, `OpenDataFolder`, `OpenSettingsFile`, `LogsDirectory`, `OpenLogsFolder`, `DebugLogging`, `RunDoctor` |

- [ ] **Step 1 : Page Comportement**

`src/UsageNotch.App/Views/Preferences/BehaviorPage.xaml` :
```xml
<UserControl x:Class="UsageNotch.App.Views.Preferences.BehaviorPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <ResourceDictionary Source="PreferencesStyles.xaml" />
  </UserControl.Resources>

  <StackPanel>
    <TextBlock Text="Comportement" Style="{StaticResource PageTitle}" />

    <TextBlock Text="Carte de détail" Style="{StaticResource SectionTitle}" />
    <CheckBox Content="Ouvrir la carte 5 s quand une session se termine ou attend une réponse"
              IsChecked="{Binding AutoOpenCard}" AutomationProperties.AutomationId="AutoOpenCard" />

    <TextBlock Text="Sons" Style="{StaticResource SectionTitle}" />
    <CheckBox Content="Jouer un son lors de ces transitions" IsChecked="{Binding SoundEnabled}"
              AutomationProperties.AutomationId="SoundEnabled" />
    <Grid Margin="0,6,0,0">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="200" />
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="Auto" />
      </Grid.ColumnDefinitions>
      <Grid.RowDefinitions>
        <RowDefinition Height="38" />
        <RowDefinition Height="38" />
      </Grid.RowDefinitions>

      <TextBlock Text="Session terminée" Style="{StaticResource FieldLabel}" />
      <ComboBox Grid.Column="1" VerticalAlignment="Center" AutomationProperties.AutomationId="DoneSound" AutomationProperties.Name="Son de session terminée"
                ItemsSource="{Binding Sounds}" DisplayMemberPath="Label" SelectedValuePath="Value" SelectedValue="{Binding DoneSound}" />
      <Button Grid.Column="2" Content="Écouter" Margin="10,0,0,0" VerticalAlignment="Center"
              Command="{Binding PlayDoneSoundCommand}" AutomationProperties.AutomationId="PlayDoneSound" />

      <TextBlock Grid.Row="1" Text="Session en attente" Style="{StaticResource FieldLabel}" />
      <ComboBox Grid.Row="1" Grid.Column="1" VerticalAlignment="Center" AutomationProperties.AutomationId="AttentionSound" AutomationProperties.Name="Son de session en attente"
                ItemsSource="{Binding Sounds}" DisplayMemberPath="Label" SelectedValuePath="Value" SelectedValue="{Binding AttentionSound}" />
      <Button Grid.Row="1" Grid.Column="2" Content="Écouter" Margin="10,0,0,0" VerticalAlignment="Center"
              Command="{Binding PlayAttentionSoundCommand}" AutomationProperties.AutomationId="PlayAttentionSound" />
    </Grid>

    <TextBlock Text="Démarrage" Style="{StaticResource SectionTitle}" />
    <CheckBox Content="Démarrer avec Windows" IsChecked="{Binding AutoStartEnabled}" IsEnabled="{Binding AutoStartAvailable}"
              AutomationProperties.AutomationId="AutoStart" />
    <TextBlock Text="{Binding AutoStartNote}" Style="{StaticResource NoteText}" />
    <TextBlock Text="{Binding AutoStartError}" Style="{StaticResource ErrorText}" />

    <TextBlock Text="Icône de notification" Style="{StaticResource SectionTitle}" />
    <CheckBox Content="Afficher l'icône dans la zone de notification" IsChecked="{Binding TrayIconVisible}"
              IsEnabled="{Binding TrayIconEditable}" AutomationProperties.AutomationId="TrayIconVisible" />
    <TextBlock Text="{Binding TrayIconNote}" Style="{StaticResource NoteText}" />
  </StackPanel>
</UserControl>
```

`src/UsageNotch.App/Views/Preferences/BehaviorPage.xaml.cs` :
```csharp
using System.Windows.Controls;

namespace UsageNotch.App.Views.Preferences;

public partial class BehaviorPage : UserControl
{
    public BehaviorPage() => InitializeComponent();
}
```

- [ ] **Step 2 : Page Claude Code**

`src/UsageNotch.App/Views/Preferences/ClaudeCodePage.xaml` :
```xml
<UserControl x:Class="UsageNotch.App.Views.Preferences.ClaudeCodePage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <ResourceDictionary Source="PreferencesStyles.xaml" />
  </UserControl.Resources>

  <StackPanel>
    <TextBlock Text="Claude Code" Style="{StaticResource PageTitle}" />
    <TextBlock Text="{Binding DemoHint}" Style="{StaticResource NoteText}" />

    <TextBlock Text="Hooks" Style="{StaticResource SectionTitle}" />
    <StackPanel Orientation="Horizontal">
      <TextBlock Text="État :" Style="{StaticResource FieldLabel}" Margin="0,0,6,0" />
      <TextBlock Text="{Binding HooksStatus}" FontWeight="SemiBold" VerticalAlignment="Center"
                 AutomationProperties.AutomationId="HooksStatus" />
    </StackPanel>
    <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
      <Button Content="Installer les hooks" Command="{Binding InstallCommand}" AutomationProperties.AutomationId="InstallHooks" />
      <Button Content="Désinstaller" Margin="10,0,0,0" Command="{Binding UninstallCommand}" AutomationProperties.AutomationId="UninstallHooks" />
      <Button Content="Actualiser" Margin="10,0,0,0" Command="{Binding RefreshCommand}" AutomationProperties.AutomationId="RefreshHooks" />
    </StackPanel>
    <TextBlock Text="{Binding LastMessage}" AutomationProperties.AutomationId="HooksMessage">
      <TextBlock.Style>
        <Style TargetType="TextBlock" BasedOn="{StaticResource NoteText}">
          <Style.Triggers>
            <DataTrigger Binding="{Binding LastActionFailed}" Value="True">
              <Setter Property="Foreground" Value="#C42B1C" />
            </DataTrigger>
          </Style.Triggers>
        </Style>
      </TextBlock.Style>
    </TextBlock>
    <TextBlock Text="Une copie horodatée de settings.json est enregistrée avant chaque modification." Style="{StaticResource NoteText}" />

    <TextBlock Text="Fichiers" Style="{StaticResource SectionTitle}" />
    <TextBlock Text="settings.json de Claude Code" />
    <TextBox Text="{Binding ClaudeSettingsPath, Mode=OneWay}" Style="{StaticResource PathText}"
             AutomationProperties.AutomationId="ClaudeSettingsPath" AutomationProperties.Name="settings.json de Claude Code" />
    <Button Content="Ouvrir le dossier" Margin="0,4,0,0" Command="{Binding OpenClaudeFolderCommand}"
            AutomationProperties.AutomationId="OpenClaudeFolder" />
    <TextBlock Text="Exécutable du hook" Margin="0,14,0,0" />
    <TextBox Text="{Binding HookExePath, Mode=OneWay}" Style="{StaticResource PathText}"
             AutomationProperties.AutomationId="HookExePath" AutomationProperties.Name="Exécutable du hook" />
    <TextBlock Text="{Binding HookExeStatus}" Style="{StaticResource NoteText}" />

    <TextBlock Text="Récepteur local" Style="{StaticResource SectionTitle}" />
    <StackPanel Orientation="Horizontal">
      <TextBlock Text="Port" Width="60" Style="{StaticResource FieldLabel}" />
      <TextBox Width="90" VerticalContentAlignment="Center" Text="{Binding PortText, UpdateSourceTrigger=LostFocus}"
               AutomationProperties.AutomationId="Port" AutomationProperties.Name="Port" />
    </StackPanel>
    <TextBlock Text="{Binding PortError}" Style="{StaticResource ErrorText}" />
    <TextBlock Text="{Binding PortNote}" Style="{StaticResource NoteText}" />
  </StackPanel>
</UserControl>
```

`src/UsageNotch.App/Views/Preferences/ClaudeCodePage.xaml.cs` :
```csharp
using System.Windows.Controls;

namespace UsageNotch.App.Views.Preferences;

public partial class ClaudeCodePage : UserControl
{
    public ClaudeCodePage() => InitializeComponent();
}
```

- [ ] **Step 3 : Page À propos**

`src/UsageNotch.App/Views/Preferences/AboutPage.xaml` :
```xml
<UserControl x:Class="UsageNotch.App.Views.Preferences.AboutPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:pref="clr-namespace:UsageNotch.Presentation.Preferences;assembly=UsageNotch.Presentation">
  <UserControl.Resources>
    <ResourceDictionary Source="PreferencesStyles.xaml" />
  </UserControl.Resources>

  <StackPanel>
    <TextBlock Text="À propos" Style="{StaticResource PageTitle}" />
    <TextBlock Text="{Binding VersionText}" FontSize="15" AutomationProperties.AutomationId="Version" />
    <TextBlock Text="Usage de Claude Code et état des sessions au bord de l'écran. Inspiré de codenotch (licence MIT)."
               Style="{StaticResource NoteText}" />
    <TextBlock Text="{Binding DemoHint}" Style="{StaticResource NoteText}" />

    <TextBlock Text="Dossiers" Style="{StaticResource SectionTitle}" />
    <TextBlock Text="Données" />
    <TextBox Text="{Binding DataDirectory, Mode=OneWay}" Style="{StaticResource PathText}"
             AutomationProperties.AutomationId="DataDirectory" AutomationProperties.Name="Dossier de données" />
    <StackPanel Orientation="Horizontal" Margin="0,4,0,0">
      <Button Content="Ouvrir le dossier de données" Command="{Binding OpenDataFolderCommand}" AutomationProperties.AutomationId="OpenDataFolder" />
      <Button Content="Ouvrir settings.json" Margin="10,0,0,0" Command="{Binding OpenSettingsFileCommand}" AutomationProperties.AutomationId="OpenSettingsFile" />
    </StackPanel>
    <TextBlock Text="Journaux" Margin="0,14,0,0" />
    <TextBox Text="{Binding LogsDirectory, Mode=OneWay}" Style="{StaticResource PathText}"
             AutomationProperties.AutomationId="LogsDirectory" AutomationProperties.Name="Dossier des journaux" />
    <Button Content="Ouvrir le dossier des journaux" Margin="0,4,0,0" Command="{Binding OpenLogsFolderCommand}"
            AutomationProperties.AutomationId="OpenLogsFolder" />

    <TextBlock Text="Journalisation" Style="{StaticResource SectionTitle}" />
    <CheckBox Content="Journalisation détaillée (niveau Debug)" IsChecked="{Binding DebugLogging}"
              AutomationProperties.AutomationId="DebugLogging" />
    <TextBlock Text="{x:Static pref:AboutPageViewModel.DebugNote}" Style="{StaticResource NoteText}" />

    <TextBlock Text="Diagnostic" Style="{StaticResource SectionTitle}" />
    <Button Content="Lancer le diagnostic" Command="{Binding RunDoctorCommand}" AutomationProperties.AutomationId="RunDoctor" />
    <TextBlock Text="Écrit logs\doctor.txt puis l'ouvre : identifiants (sans le jeton), hooks, port, écrans, dernière lecture."
               Style="{StaticResource NoteText}" />
    <TextBlock Text="{Binding DoctorError}" Style="{StaticResource ErrorText}" />
  </StackPanel>
</UserControl>
```

`src/UsageNotch.App/Views/Preferences/AboutPage.xaml.cs` :
```csharp
using System.Windows.Controls;

namespace UsageNotch.App.Views.Preferences;

public partial class AboutPage : UserControl
{
    public AboutPage() => InitializeComponent();
}
```

Dans `src/UsageNotch.App/Views/Preferences/SettingsWindow.xaml`, ajouter après le `DataTemplate` de `PositionPageViewModel`, puis retirer la phrase « les Tasks 10 et 11 ajoutent les autres modèles ici » du commentaire, qui devient `<!-- Une page par type de ViewModel. -->` :
```xml
      <DataTemplate DataType="{x:Type pref:BehaviorPageViewModel}">
        <views:BehaviorPage />
      </DataTemplate>
      <DataTemplate DataType="{x:Type pref:ClaudeCodePageViewModel}">
        <views:ClaudeCodePage />
      </DataTemplate>
      <DataTemplate DataType="{x:Type pref:AboutPageViewModel}">
        <views:AboutPage />
      </DataTemplate>
```

- [ ] **Step 4 : Compiler et tester**

Run: `dotnet build UsageNotch.sln` puis `dotnet test`
Expected: 0 avertissement ; tous les tests passent.

- [ ] **Step 5 : Vérifier à l'exécution**

Procédure « Préparation », puis « Démarrer la démo, ouvrir les réglages ».

**Comportement.** `Select-Page 'Comportement'`, capture `task11-behavior` de la fenêtre.
1. `Switch-Ui 'AutoOpenCard'` → `autoOpenCard` vaut `False` ; `Switch-Ui 'AutoOpenCard'` → `True`.
2. **Ne jamais basculer `AutoStart`.** Attendu : `(Find-Ui 'AutoStart').Current.IsEnabled` vaut `False` et `Find-Text 'Indisponible en mode démo.'` n'est pas nul.
3. `Set-Combo 'DoneSound' 'Bip'` → `doneSound` vaut `Beep`. `Invoke-Ui 'PlayDoneSound'` : la fenêtre reste présente (`Get-SettingsWindow` réussit).
4. `Switch-Ui 'TrayIconVisible'` → `trayIconVisible` vaut `False` ; `Switch-Ui 'TrayIconVisible'` → `True`.

**Claude Code.** `Select-Page 'Claude Code'`, capture `task11-claude` de la fenêtre.
5. Garde de sécurité, **à exécuter avant toute action sur les hooks** :
   ```powershell
   $claudePath = Get-Text 'ClaudeSettingsPath'
   if (-not $claudePath.StartsWith($demoDir, [StringComparison]::OrdinalIgnoreCase)) { throw "ARRÊT : chemin hors démo : $claudePath" }
   ```
   Attendu : aucun arrêt ; `Find-Text` du texte de `ClaudeCodePageViewModel.DemoNote` n'est pas nul.
6. Si `(Find-Ui 'HooksStatus').Current.Name` vaut `Installés` (reste d'une vérification précédente), `Invoke-Ui 'UninstallHooks'` d'abord.
7. `Invoke-Ui 'InstallHooks'`. Attendu : statut `Installés` ; `Select-String -Path $claudePath -SimpleMatch 'UsageNotch.Hook.exe' -Quiet` vaut `True`.
8. `Invoke-Ui 'UninstallHooks'`. Attendu : statut `Non installés` ; la même recherche vaut `False`.
9. **Port.**
   - `Set-Text 'Port' '48668'` → `port` vaut `48668` et le texte de `RestartNote` est trouvé.
   - `Set-Text 'Port' 'abc'` → le texte de `PortRangeError` est trouvé et `port` vaut toujours `48668`.
   - `Set-Text 'Port' '48667'` → `port` vaut `48667` et le texte de `RestartNote` n'est plus trouvé.

**À propos.** `Select-Page 'À propos'`, capture `task11-about` de la fenêtre.
10. Attendu : `(Find-Ui 'Version').Current.Name` vaut `UsageNotch 0.3.0` ; `Get-Text 'DataDirectory'` vaut `$demoDir`.
11. `Switch-Ui 'DebugLogging'` → `debugLogging` vaut `True` ; `Switch-Ui 'DebugLogging'` → `False`.
12. **Diagnostic, seulement si aucun Bloc-notes n'est ouvert.** Sinon, sauter l'étape et le signaler : le Bloc-notes de Windows 11 ouvrirait un onglet dans la fenêtre de l'utilisateur.
    ```powershell
    if (Get-Process notepad -ErrorAction SilentlyContinue) { 'diagnostic sauté : Bloc-notes déjà ouvert' } else {
        Invoke-Ui 'RunDoctor'; Start-Sleep -Seconds 2
        $doctor = Get-Item (Join-Path $demoDir 'logs\doctor.txt')
        ((Get-Date) - $doctor.LastWriteTime).TotalSeconds -lt 30
        Get-Process notepad -ErrorAction SilentlyContinue | Stop-Process
    }
    ```
    Attendu : `True`, puis le Bloc-notes est fermé.
13. **Nettoyage.** Arrêter la démo, supprimer les réglages de démo, vérifier les processus et les ports. Supprimer aussi le faux settings.json de Claude Code et ses sauvegardes : `Remove-Item (Join-Path $demoDir 'claude-settings.json*')`.

- [ ] **Step 6 : Commit**

`feat(app): behaviour, Claude Code and about pages`

---

### Task 12 : Vérification de bout en bout, publication et notes

**Files:**
- Modify: `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md` (nouvelle section « Plan 3 », remplacement de la ligne « Réglages » du Plan 2)

**Interfaces:**
- Consumes: tout le plan ; `scripts/publish.ps1` (Plan 2).
- Produces : l'application publiée vérifiée et les notes du Plan 3.

- [ ] **Step 1 : Suite complète et publication**

Run :
```powershell
dotnet build UsageNotch.sln
dotnet test
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
(Get-Item publish\UsageNotch.App.exe).VersionInfo.ProductVersion
```
Expected : 0 avertissement ; 225 tests Core et 203 tests Presentation passés ; publication réussie ; version commençant par `0.3.0`.

- [ ] **Step 2 : Parcours complet sur l'application publiée**

Procédure « Préparation » avec `$exe = (Resolve-Path 'publish\UsageNotch.App.exe').Path`, puis « Démarrer la démo, ouvrir les réglages ».

1. **Pages.** Capture de la fenêtre pour chaque page : `task12-appearance`, `task12-position`, `task12-behavior`, `task12-claude`, `task12-about`. Relire chaque capture. Attendu : aucun texte tronqué ni chevauchement, aperçu visible en haut.
2. **Menu de la pilule.** `Close-Settings`. Ouvrir le menu contextuel de la pilule et choisir « Réglages… » :
   ```powershell
   Add-Type -Namespace Verify -Name Mouse -MemberDefinition @'
   [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
   [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, System.UIntPtr extra);
   '@
   $p = Get-WindowRect 'UsageNotch — pilule'
   [Verify.Mouse]::SetCursorPos([int](($p.Left + $p.Right) / 2), [int](($p.Top + $p.Bottom) / 2)) | Out-Null
   Start-Sleep -Milliseconds 300
   [Verify.Mouse]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero); [Verify.Mouse]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero)
   Start-Sleep -Milliseconds 600
   $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::NameProperty, 'Réglages…')
   $item = $UIA::RootElement.FindFirst($Scope::Descendants, $cond)
   if ($item) { $item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() } else { 'menu introuvable' }
   [Verify.Mouse]::SetCursorPos(10, 10) | Out-Null
   ```
   Attendu : `Get-SettingsWindow` réussit. Si le menu reste introuvable (recouvrement par une fenêtre système), le signaler avec une capture : la route est la même méthode `OpenSettings` que la seconde instance.
3. **« Quitter » ne perd ni `AutoLaunch = false` ni une modification en attente.** Page Apparence. Poser l'échelle sans attendre l'enregistrement, puis quitter tout de suite par le menu de la pilule :
   ```powershell
   Select-Page 'Apparence'
   (Find-Ui 'Scale').GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(1.2)
   [Verify.Mouse]::SetCursorPos([int](($p.Left + $p.Right) / 2), [int](($p.Top + $p.Bottom) / 2)) | Out-Null
   Start-Sleep -Milliseconds 300
   [Verify.Mouse]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero); [Verify.Mouse]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero)
   Start-Sleep -Milliseconds 600
   $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::NameProperty, 'Quitter')
   $UIA::RootElement.FindFirst($Scope::Descendants, $cond).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
   Start-Sleep -Seconds 3
   $s = Get-DemoSettings; "autoLaunch=$($s.autoLaunch) scale=$($s.scale)"
   Get-Process -Id $app.Id -ErrorAction SilentlyContinue
   ```
   Attendu : `autoLaunch=False scale=1.2`, et le processus est terminé. Si le menu est introuvable, arrêter la démo par `Stop-Process` et le signaler.
4. **Nettoyage.** Supprimer les réglages de démo ; aucun processus `UsageNotch.App` ; ports 48666 et 48667 libres ; `Get-ItemProperty HKCU:\Software\Microsoft\Windows\CurrentVersion\Run -Name UsageNotch -ErrorAction SilentlyContinue` ne rend rien.

- [ ] **Step 3 : Notes du Plan 3**

Dans `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md`, section « Plan 2 — application › Utilisation », remplacer la ligne qui commence par « - Réglages : menu › « Réglages… » ouvre `settings.json` » par :
```markdown
- Réglages : voir « Plan 3 — fenêtre de réglages ».
```
puis ajouter à la fin du fichier :
```markdown
## Plan 3 — fenêtre de réglages

### Utilisation

- Ouvrir les réglages de trois façons :
  - clic gauche sur l'icône de notification ;
  - « Réglages… » dans le menu de la pilule ou de l'icône ;
  - relancer `UsageNotch.App.exe` pendant qu'il tourne.
- Il n'y a pas de bouton « Appliquer ». La pilule suit en 250 ms au plus, et fermer la fenêtre ou la quitter enregistre tout.
- Le port se change dans la page Claude Code et s'applique au prochain démarrage.
- `settings.json` reste modifiable à la main : la fenêtre ouverte suit ces modifications et ne les écrase pas.

### Écarts par rapport à la spec

- Aperçu commun aux cinq pages, en haut de la fenêtre : trois pilules d'exemple (modéré, vigilance, critique) et leur bande en mode Replié. Il est statique, sans animation en boucle.
- `PillOpacity` s'applique à la forme de la pilule seulement ; la carte reste opaque.
- Modifier une couleur, l'opacité ou un seuil passe au thème Personnalisé, initialisé depuis le thème affiché.
- Sélecteur de couleur : champ `#RRGGBB` et boîte « Couleurs » de Windows.
- Changement de port appliqué au redémarrage.
- Clic gauche sur l'icône : réglages (spec §6) au lieu de l'aperçu de carte 5 s du Plan 2.
- Diagnostic depuis l'icône et la page À propos : `logs\doctor.txt` ouvert dans le Bloc-notes, sans console.

### Reste à faire

- Vérifier la pilule, la carte et la miniature des écrans sur un écran à une autre mise à l'échelle (DPI mixte).
- Tests d'interface automatisés (FlaUI, spec §9) : la vérification reste manuelle, par UI Automation en démo.
```

- [ ] **Step 4 : Commit**

`docs: Plan 3 notes`

Les actions sur la configuration réelle restent à l'utilisateur :
- ouvrir les réglages hors démo ;
- installer les hooks sur la vraie configuration Claude Code ;
- cocher « Démarrer avec Windows ».

---

## Auto-relecture

**Couverture de la spec.**
- §7, « Tout s'applique immédiatement » : `SettingsDraft` (Task 2), routes vers la pilule via `SettingsStore.Changed` (Plan 2). Seule exception, le port (écart consigné, Task 6).
- §7, Thème : préréglages, Personnalisé initialisé depuis le courant, un sélecteur par couleur, opacité, seuils (Tasks 1, 4, 8, 9).
- §7, Taille et densité : échelle 40 à 150 %, contenu de cellule (Tasks 4, 9).
- §7, Modes de visibilité et largeur de bande : Tasks 5, 10 ; aperçu du mode Replié et du mode Masqué (Tasks 3, 9).
- §7, Placement : bord, position par bord, curseur, Recentrer, écran d'ancrage (Tasks 1, 3, 5, 10).
- §7, Fenêtre de réglages : navigation latérale, cinq pages, aperçu en direct (Tasks 7, 9, 10, 11).
  - Apparence : Task 9.
  - Position, avec la miniature des écrans : Task 10.
  - Comportement (auto-ouverture, son avec choix et écoute, démarrer avec Windows, icône) : Tasks 6, 11.
  - Claude Code (état, installer, désinstaller, port, chemins) : Tasks 6, 11.
  - À propos (version, dossier de données, journaux, niveau, `doctor`) : Tasks 7, 11.
- §6, `SettingsWindow` classique activable ; icône « Clic gauche → Réglages » et menu identique : Tasks 8, 9.
- §8, écran absent conservé : Tasks 1, 5. Second lancement → « ouvrir les réglages » : Tasks 8 (premier plan) et 9 (route).
- §9, Debug activable dans les réglages : Tasks 7, 11. `doctor` accessible : Tasks 8, 11.

**Cohérence des types.**
- `Choice<T>` (Task 1) est lié par `DisplayMemberPath="Label"` et `SelectedValuePath="Value"` dans toutes les pages. Le style implicite de `ComboBoxItem` (Task 9) lit `Label`.
- `SettingsDraft` (Task 2) est le premier argument de chaque ViewModel de page (Tasks 4 à 7). `SettingsViewModel.Create` (Task 7) est la seule fabrique, appelée par `SettingsWindowHost` (Task 9) avec les services enregistrés en Task 8.
- `IColorPicker`, `IMonitorSource`, `IAutoStart`, `IHookSetup`, `IShellActions` (Task 4) sont implémentés par `NativeColorPicker`, `MonitorService`, `AutoStartService`, `HookSetupService`, `ShellActions` (Task 8).
  - `SettingsWindow` et `SettingsWindowHost` reçoivent `NativeColorPicker` concret pour régler `Owner`.
- `PositionPageViewModel.MapWidth` et `MapHeight` (Task 5) dimensionnent `MonitorMapView` (Task 10) ; `MonitorMap.Layout` (Task 3) reçoit les mêmes valeurs.
- `PreviewModel` (Task 3) est la propriété `SettingsViewModel.Preview` (Task 7) liée à `PillPreview.Model` (Task 9).
- `TrayIconService.Start(Func<Task>, Action)` garde sa signature ; seul son constructeur change (Task 8), résolu par le conteneur.
- `NotchShell` est donné en entier en Task 9, seule tâche qui le modifie.
- Les identifiants d'automatisation utilisés par les vérifications des Tasks 9 à 12 sont ceux déclarés dans les XAML des mêmes tâches.
