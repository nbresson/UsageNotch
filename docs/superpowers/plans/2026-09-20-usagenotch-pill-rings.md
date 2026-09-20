# Pilule à trois anneaux — Plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal :** la pilule affiche trois anneaux concentriques — session, hebdomadaire tous modèles, hebdomadaire par modèle — au lieu du seul anneau de session.

**Architecture :** travail de présentation uniquement. Les trois fenêtres de limite sont déjà lues par `ClaudeUsageParser` et déjà affichées par la carte ; seule la pilule se limitait à une fenêtre. Le rendu réutilise le contrôle `ProgressRing` existant, instancié trois fois à des tailles différentes dans le `Grid` qui accueille déjà l'anneau unique. Le contrat `CellModel` passe d'un anneau à une liste de trois, en deux temps pour que la compilation et les tests restent verts à chaque commit.

**Tech Stack :** C# / .NET 10, WPF (`net10.0-windows`), CommunityToolkit.Mvvm, xUnit + FluentAssertions.

**Spec :** `docs/superpowers/specs/2026-09-20-usagenotch-pill-rings-design.md`

## Global Constraints

- Interface **en français** : tout libellé, texte de page et message visible par l'utilisateur.
- Compilation **sans aucun avertissement** (`TreatWarningsAsErrors` dans `Directory.Build.props`).
- La logique vit dans `UsageNotch.Core` et `UsageNotch.Presentation`, **sans référence à WPF**, et se développe en TDD. `UsageNotch.App` (WPF, interop Win32) se vérifie à l'exécution.
- Messages de commit **en anglais**, forme `type(scope): description`, fichier de message en UTF-8 **sans BOM**.
- Branche de travail : `feat/pill-rings`. Une branche pour tout le chantier, fusionnée dans `main` à la fin.
- Cotes en DIP à l'échelle 100 %, multipliées par `Settings.Scale` à l'exécution.
- Ne **jamais** arrêter l'instance réelle de l'utilisateur (`publish\UsageNotch.App.exe`, port 48666, mutex `Local\UsageNotch`). Les vérifications se font en `--demo` (port 48667, mutex `Local\UsageNotch-demo`), et les fenêtres de la démo se retrouvent par identifiant de processus.
- Ne pas republier dans `publish\` pendant le chantier.

### Cycle de vérification, identique à chaque tâche

```powershell
dotnet build UsageNotch.sln
dotnet test
```

Référence de départ : **489 tests verts** (237 Core, 252 Presentation), zéro avertissement.

---

## Structure des fichiers

**Créés :**

| Fichier | Responsabilité |
|---|---|
| `src/UsageNotch.Core/Settings/RingColoring.cs` | L'enum du mode de coloration des anneaux |
| `src/UsageNotch.Core/Usage/RingWindows.cs` | Les trois groupes d'alias de `kind`, dans l'ordre extérieur → intérieur |
| `src/UsageNotch.Presentation/Pill/RingModel.cs` | Ce qu'un anneau dessine : fraction, couleur, couleur de piste |
| `tests/UsageNotch.Core.Tests/Settings/ThemeTests.cs` | Les couleurs d'anneaux des préréglages et leurs replis |
| `tests/UsageNotch.Core.Tests/Usage/RingWindowsTests.cs` | Résolution des alias sur un `UsageSnapshot` |

**Modifiés :**

| Fichier | Nature du changement |
|---|---|
| `src/UsageNotch.Core/Settings/Theme.cs` | Trois couleurs d'anneaux, leurs replis, l'accent système |
| `src/UsageNotch.Core/Settings/Settings.cs` | `Coloring`, version 2, remappage de `PercentOnly` |
| `src/UsageNotch.Core/Usage/UsageSnapshot.cs` | Surcharge `Window(IEnumerable<string>)` |
| `src/UsageNotch.Core/Usage/IUsageProvider.cs` | `RingWindowIds` ajouté, `HeadlineWindowId` retiré en tâche 6 |
| `src/UsageNotch.Core/Usage/ClaudeUsageProvider.cs` | Implémente `RingWindowIds` |
| `src/UsageNotch.Presentation/Pill/CellModel.cs` | `Rings` ajouté, anciens champs retirés en tâche 6 |
| `src/UsageNotch.Presentation/Pill/PillPresenter.cs` | Construit les trois anneaux, applique le mode de coloration |
| `src/UsageNotch.Presentation/Pill/PillMetrics.cs` | Cotes de la pile et des voyants d'activité |
| `src/UsageNotch.Presentation/ViewModels/NotchViewModel.cs` | Nouvel appel au présentateur |
| `src/UsageNotch.Presentation/Preferences/SettingsPreview.cs` | Échantillons d'aperçu à trois fenêtres |
| `src/UsageNotch.Presentation/Preferences/Choices.cs` | `RingColorings`, `CellContents` réduit |
| `src/UsageNotch.Presentation/Preferences/AppearancePageViewModel.cs` | Trois `ColorSlot`, propriété `Coloring` |
| `src/UsageNotch.App/Views/PillWindow.xaml` | Trois `ProgressRing`, voyants d'activité agrandis |
| `src/UsageNotch.App/Views/PillWindow.xaml.cs` | `UpdateAnimations` cesse de consulter `ShowRing` |
| `src/UsageNotch.App/Controls/PillPreview.cs` | Même pile dans l'aperçu des réglages |
| `src/UsageNotch.App/Hosting/DemoMode.cs` | `RingWindowIds`, `weekly_scoped` |
| `src/UsageNotch.App/Views/Preferences/AppearancePage.xaml` | Liste déroulante « Coloration des anneaux » |
| `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md` | Journal de bord du chantier |
| `docs/REPRISE.md` | Backlog : l'idée 1 est livrée |

---

## Task 0 : Branche

- [ ] **Step 1 : Créer la branche de travail**

```powershell
git switch -c feat/pill-rings
```

- [ ] **Step 2 : Vérifier la référence de départ**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : compilation sans avertissement, **489 tests verts**.

Si ce nombre diffère, s'arrêter et le signaler avant toute modification : le plan s'appuie dessus pour détecter les régressions.

---

## Task 1 : Les trois couleurs d'anneaux dans le thème

**Files :**
- Modify : `src/UsageNotch.Core/Settings/Theme.cs`
- Test : `tests/UsageNotch.Core.Tests/Settings/ThemeTests.cs` (créer)

**Interfaces :**
- Consomme : rien.
- Produit : `Theme.RingSession`, `Theme.RingWeeklyAll`, `Theme.RingWeeklyScoped` — trois `string` au format `#RRGGBB`, présentes dans les deux préréglages, repliées par `Clamp()`, et dont `RingSession` reçoit l'accent système en préréglage `SystemAccent`.

- [ ] **Step 1 : Écrire le test qui échoue**

Créer `tests/UsageNotch.Core.Tests/Settings/ThemeTests.cs` :

```csharp
using FluentAssertions;
using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Tests.Settings;

public class ThemeTests
{
    [Fact]
    public void The_codenotch_preset_gives_each_ring_its_own_colour()
    {
        var t = Theme.Codenotch;
        t.RingSession.Should().Be("#28E07B");
        t.RingWeeklyAll.Should().Be("#57C7FF");
        t.RingWeeklyScoped.Should().Be("#C792EA");
    }

    [Fact]
    public void The_monochrome_preset_separates_the_rings_by_lightness()
    {
        var t = Theme.Monochrome;
        new[] { t.RingSession, t.RingWeeklyAll, t.RingWeeklyScoped }
            .Should().Equal("#E0E0E0", "#A0A0A0", "#707070");
    }

    [Fact]
    public void Every_preset_keeps_its_three_ring_colours_distinct()
    {
        foreach (var t in new[] { Theme.Codenotch, Theme.Monochrome })
        {
            new[] { t.RingSession, t.RingWeeklyAll, t.RingWeeklyScoped }
                .Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void A_theme_missing_its_ring_colours_falls_back_to_codenotch()
    {
        var partial = Theme.Monochrome with { RingSession = "", RingWeeklyAll = null!, RingWeeklyScoped = "" };

        var clamped = partial.Clamp();

        clamped.RingSession.Should().Be(Theme.Codenotch.RingSession);
        clamped.RingWeeklyAll.Should().Be(Theme.Codenotch.RingWeeklyAll);
        clamped.RingWeeklyScoped.Should().Be(Theme.Codenotch.RingWeeklyScoped);
    }

    [Fact]
    public void The_system_accent_preset_tints_the_session_ring_only()
    {
        var t = Theme.ForPreset(ThemePreset.SystemAccent, Theme.Codenotch, "#0078D4");

        t.RingSession.Should().Be("#0078D4");
        t.RingWeeklyAll.Should().Be(Theme.Codenotch.RingWeeklyAll);
        t.RingWeeklyScoped.Should().Be(Theme.Codenotch.RingWeeklyScoped);
    }
}
```

- [ ] **Step 2 : Lancer le test pour vérifier qu'il échoue**

Run : `dotnet test tests/UsageNotch.Core.Tests --filter "FullyQualifiedName~ThemeTests"`
Expected : échec de compilation, `'Theme' does not contain a definition for 'RingSession'`.

- [ ] **Step 3 : Ajouter les trois couleurs au record**

Dans `src/UsageNotch.Core/Settings/Theme.cs`, ajouter trois paramètres au record **après** `LevelCritical` et avant `Running` :

```csharp
    string LevelCritical,
    string RingSession,
    string RingWeeklyAll,
    string RingWeeklyScoped,
    string Running,
```

- [ ] **Step 4 : Renseigner les deux préréglages**

```csharp
    public static Theme Codenotch { get; } = new(
        PillBackground: "#000000", PillBorder: "#2E2E2E", RingTrack: "#3A3A3A",
        LevelAmple: "#28E07B", LevelWatch: "#F5E400", LevelCritical: "#FF4500",
        RingSession: "#28E07B", RingWeeklyAll: "#57C7FF", RingWeeklyScoped: "#C792EA",
        Running: "#28E07B", Attention: "#FFBF00", Done: "#57C7FF",
        Text: "#FFFFFF", PillOpacity: 1.0, ThresholdWatch: 0.50, ThresholdCritical: 0.80);

    public static Theme Monochrome { get; } = new(
        PillBackground: "#111111", PillBorder: "#3A3A3A", RingTrack: "#333333",
        LevelAmple: "#E0E0E0", LevelWatch: "#A0A0A0", LevelCritical: "#FFFFFF",
        RingSession: "#E0E0E0", RingWeeklyAll: "#A0A0A0", RingWeeklyScoped: "#707070",
        Running: "#E0E0E0", Attention: "#FFFFFF", Done: "#B0B0B0",
        Text: "#FFFFFF", PillOpacity: 0.9, ThresholdWatch: 0.50, ThresholdCritical: 0.80);
```

- [ ] **Step 5 : Tinter l'anneau de session avec l'accent système**

Dans `ForPreset`, la branche `SystemAccent` devient :

```csharp
        ThemePreset.SystemAccent => systemAccentHex is null
            ? Codenotch
            : Codenotch with { LevelAmple = systemAccentHex, Running = systemAccentHex, RingSession = systemAccentHex },
```

- [ ] **Step 6 : Ajouter les trois replis dans `Clamp()`**

Dans le bloc `return this with { … }`, après la ligne `LevelCritical = …` :

```csharp
            RingSession = Or(RingSession, d.RingSession),
            RingWeeklyAll = Or(RingWeeklyAll, d.RingWeeklyAll),
            RingWeeklyScoped = Or(RingWeeklyScoped, d.RingWeeklyScoped),
```

- [ ] **Step 7 : Lancer les tests**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : les 5 nouveaux tests passent, **494 tests verts**, zéro avertissement.

- [ ] **Step 8 : Commit**

```powershell
git add src/UsageNotch.Core/Settings/Theme.cs tests/UsageNotch.Core.Tests/Settings/ThemeTests.cs
git commit -m "feat(core): a theme colour per usage ring"
```

---

## Task 2 : Mode de coloration, version 2 des réglages, retrait du pourcentage seul

**Files :**
- Create : `src/UsageNotch.Core/Settings/RingColoring.cs`
- Modify : `src/UsageNotch.Core/Settings/Settings.cs`
- Test : `tests/UsageNotch.Core.Tests/Settings/SettingsStoreTests.cs`

**Interfaces :**
- Consomme : rien de la tâche 1.
- Produit : `enum RingColoring { PerRing, ByLevel }` et `Settings.Coloring` (défaut `PerRing`). `Settings.CurrentVersion` vaut 2. `CellContent.PercentOnly` existe toujours dans l'enum mais `Settings.Clamp()` le remplace par `CellContent.RingAndPercent`.

**Pourquoi `PercentOnly` survit dans l'enum.** `SettingsStore` sérialise les enums avec `JsonStringEnumConverter`, qui lève `JsonException` sur une valeur inconnue. `Load()` traite alors le fichier **entier** comme corrompu, le copie en `settings.json.corrupt-<secondes unix>` et repart sur les défauts. Retirer le membre effacerait les réglages de quiconque avait choisi ce mode.

- [ ] **Step 1 : Écrire les tests qui échouent**

Ajouter à `tests/UsageNotch.Core.Tests/Settings/SettingsStoreTests.cs` :

```csharp
    [Fact]
    public void A_version_1_file_loads_into_version_2_without_losing_its_values()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), """
            { "version": 1, "port": 49000, "edge": "Top", "scale": 0.75, "cellContent": "RingOnly" }
            """);

        var s = Build(dir).Load();

        s.Version.Should().Be(2);
        s.Port.Should().Be(49000);
        s.Edge.Should().Be(ScreenEdge.Top);
        s.Scale.Should().Be(0.75);
        s.CellContent.Should().Be(CellContent.RingOnly);
        s.Coloring.Should().Be(RingColoring.PerRing);
    }

    [Fact]
    public void The_retired_percent_only_mode_is_remapped_without_condemning_the_file()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), """
            { "version": 1, "port": 49000, "cellContent": "PercentOnly" }
            """);

        var s = Build(dir).Load();

        s.CellContent.Should().Be(CellContent.RingAndPercent);
        s.Port.Should().Be(49000);
        Directory.GetFiles(dir.Path).Should().NotContain(f => f.Contains(".corrupt-"));
    }

    [Fact]
    public void The_ring_colouring_mode_round_trips()
    {
        using var dir = new TempDir();
        var store = Build(dir);

        store.Save(new UsageNotch.Core.Settings.Settings { Coloring = RingColoring.ByLevel });

        Build(dir).Load().Coloring.Should().Be(RingColoring.ByLevel);
    }
```

- [ ] **Step 2 : Lancer les tests pour vérifier qu'ils échouent**

Run : `dotnet test tests/UsageNotch.Core.Tests --filter "FullyQualifiedName~SettingsStoreTests"`
Expected : échec de compilation, `The type or namespace name 'RingColoring' could not be found`.

- [ ] **Step 3 : Créer l'enum**

Créer `src/UsageNotch.Core/Settings/RingColoring.cs` :

```csharp
namespace UsageNotch.Core.Settings;

/// <summary>Ce qui décide de la couleur d'un anneau : sa couleur propre, ou le niveau de sa fraction.</summary>
public enum RingColoring { PerRing, ByLevel }
```

- [ ] **Step 4 : Étendre les réglages**

Dans `src/UsageNotch.Core/Settings/Settings.cs` :

```csharp
    public const int CurrentVersion = 2;
```

Ajouter la propriété juste après `CellContent` :

```csharp
    public CellContent CellContent { get; init; } = CellContent.RingAndPercent;
    /// <summary>Couleur propre à chaque anneau, ou couleur du niveau de chacun.</summary>
    public RingColoring Coloring { get; init; } = RingColoring.PerRing;
```

Dans `Clamp()`, ajouter après la ligne `Version = CurrentVersion,` :

```csharp
        // « Pourcentage seul » a été retiré des choix : le membre survit dans l'enum pour que la relecture
        // d'un fichier de version 1 ne lève pas et ne condamne pas tout le fichier.
        CellContent = CellContent == CellContent.PercentOnly ? CellContent.RingAndPercent : CellContent,
```

- [ ] **Step 5 : Lancer les tests**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : les 3 nouveaux tests passent, **497 tests verts**.

Le test existant `Save_then_Load_round_trips_every_field` affecte `CellContent = CellContent.PercentOnly` ; il devient faux. Le corriger en `CellContent.RingOnly`, valeur qui survit au clamp et reste distincte du défaut.

- [ ] **Step 6 : Commit**

```powershell
git add src/UsageNotch.Core/Settings tests/UsageNotch.Core.Tests/Settings
git commit -m "feat(core): ring colouring mode, settings version 2"
```

---

## Task 3 : Alias de fenêtres et `RingWindowIds`

**Files :**
- Create : `src/UsageNotch.Core/Usage/RingWindows.cs`
- Modify : `src/UsageNotch.Core/Usage/UsageSnapshot.cs`, `src/UsageNotch.Core/Usage/IUsageProvider.cs`, `src/UsageNotch.Core/Usage/ClaudeUsageProvider.cs`, `src/UsageNotch.App/Hosting/DemoMode.cs`
- Test : `tests/UsageNotch.Core.Tests/Usage/RingWindowsTests.cs` (créer)

**Interfaces :**
- Consomme : rien des tâches 1 et 2.
- Produit :
  - `RingWindows.Claude` de type `IReadOnlyList<IReadOnlyList<string>>` — trois groupes d'alias, extérieur → intérieur ;
  - `UsageSnapshot.Window(IEnumerable<string> aliases)` → `LimitWindow?` ;
  - `IUsageProvider.RingWindowIds` de type `IReadOnlyList<IReadOnlyList<string>>`.

`HeadlineWindowId` **reste** sur l'interface pour cette tâche : `NotchViewModel` s'en sert encore. Il disparaît en tâche 6.

Pas d'ambiguïté de surcharge entre `Window(string)` et `Window(IEnumerable<string>)` : `string` implémente `IEnumerable<char>`, pas `IEnumerable<string>`.

- [ ] **Step 1 : Écrire le test qui échoue**

Créer `tests/UsageNotch.Core.Tests/Usage/RingWindowsTests.cs` :

```csharp
using FluentAssertions;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class RingWindowsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static UsageSnapshot Snap(params (string Id, double Used)[] windows) =>
        new(SnapshotStatus.Ok,
            windows.Select(w => new LimitWindow(w.Id, w.Id, w.Used, Now.AddHours(1))).ToList(),
            Now, "", null);

    [Fact]
    public void The_three_groups_are_ordered_outer_to_inner()
    {
        RingWindows.Claude.Should().HaveCount(3);
        RingWindows.Claude[0].Should().Equal("session", "five_hour");
        RingWindows.Claude[1].Should().Equal("weekly_all", "seven_day", "weekly");
        RingWindows.Claude[2].Should().Equal("weekly_scoped", "weekly_opus", "seven_day_opus");
    }

    [Fact]
    public void An_alias_group_finds_its_window_whichever_name_the_api_used()
    {
        Snap(("five_hour", 0.3)).Window(RingWindows.Claude[0])!.UsedFraction.Should().Be(0.3);
        Snap(("seven_day", 0.4)).Window(RingWindows.Claude[1])!.UsedFraction.Should().Be(0.4);
        Snap(("weekly_opus", 0.5)).Window(RingWindows.Claude[2])!.UsedFraction.Should().Be(0.5);
        Snap(("weekly_scoped", 0.6)).Window(RingWindows.Claude[2])!.UsedFraction.Should().Be(0.6);
    }

    [Fact]
    public void The_first_alias_of_the_group_wins_when_several_are_present()
    {
        var s = Snap(("weekly_opus", 0.2), ("weekly_scoped", 0.9));
        s.Window(RingWindows.Claude[2])!.UsedFraction.Should().Be(0.9);
    }

    [Fact]
    public void A_group_with_no_match_gives_null()
    {
        Snap(("session", 0.3)).Window(RingWindows.Claude[2]).Should().BeNull();
    }

    [Fact]
    public void A_group_never_borrows_another_rings_window()
    {
        Snap(("weekly_all", 0.7)).Window(RingWindows.Claude[0]).Should().BeNull();
    }
}
```

- [ ] **Step 2 : Lancer le test pour vérifier qu'il échoue**

Run : `dotnet test tests/UsageNotch.Core.Tests --filter "FullyQualifiedName~RingWindowsTests"`
Expected : échec de compilation, `The type or namespace name 'RingWindows' could not be found`.

- [ ] **Step 3 : Créer les groupes d'alias**

Créer `src/UsageNotch.Core/Usage/RingWindows.cs` :

```csharp
namespace UsageNotch.Core.Usage;

/// <summary>
/// Les fenêtres que la pilule dessine, de l'anneau extérieur à l'intérieur. Chaque groupe liste les <c>kind</c>
/// acceptés dans l'ordre d'essai : l'API n'emploie pas le même d'un compte et d'une version à l'autre.
/// </summary>
public static class RingWindows
{
    public static readonly IReadOnlyList<string> Session = ["session", "five_hour"];
    public static readonly IReadOnlyList<string> WeeklyAll = ["weekly_all", "seven_day", "weekly"];
    public static readonly IReadOnlyList<string> WeeklyScoped = ["weekly_scoped", "weekly_opus", "seven_day_opus"];

    public static readonly IReadOnlyList<IReadOnlyList<string>> Claude = [Session, WeeklyAll, WeeklyScoped];
}
```

- [ ] **Step 4 : Ajouter la surcharge de recherche**

Dans `src/UsageNotch.Core/Usage/UsageSnapshot.cs`, sous le `Window(string id)` existant :

```csharp
    /// <summary>La première fenêtre dont l'identifiant figure dans le groupe d'alias, ou null.</summary>
    public LimitWindow? Window(IEnumerable<string> aliases) =>
        aliases.Select(Window).FirstOrDefault(w => w is not null);
```

- [ ] **Step 5 : Étendre l'interface et les deux fournisseurs**

Dans `src/UsageNotch.Core/Usage/IUsageProvider.cs`, à la suite de `HeadlineWindowId` :

```csharp
    /// <summary>Les fenêtres des trois anneaux, de l'extérieur à l'intérieur, chacune par son groupe d'alias.</summary>
    IReadOnlyList<IReadOnlyList<string>> RingWindowIds { get; }
```

Dans `src/UsageNotch.Core/Usage/ClaudeUsageProvider.cs`, sous `HeadlineWindowId` :

```csharp
    public IReadOnlyList<IReadOnlyList<string>> RingWindowIds => RingWindows.Claude;
```

Dans `src/UsageNotch.App/Hosting/DemoMode.cs`, sous `HeadlineWindowId` de `DemoUsageProvider` :

```csharp
    public IReadOnlyList<IReadOnlyList<string>> RingWindowIds => RingWindows.Claude;
```

Et, dans le même fichier, la fenêtre Opus devient la fenêtre par modèle — corriger aussi le résumé de la classe :

```csharp
/// <summary>Lecture fixe : session 73 %, hebdomadaire 21 %, par modèle 52 %.</summary>
```

```csharp
            new("weekly_scoped", "Hebdomadaire (par modèle)", 0.52, now.AddDays(3)),
```

- [ ] **Step 6 : Compléter les deux doubles de test**

Deux fichiers implémentent `IUsageProvider` et cesseront de compiler :

- `tests/UsageNotch.Core.Tests/Usage/UsagePollerTests.cs`
- `tests/UsageNotch.Presentation.Tests/ViewModels/NotchViewModelTests.cs`

Ajouter la propriété à chacun des doubles qu'ils déclarent :

```csharp
    public IReadOnlyList<IReadOnlyList<string>> RingWindowIds => RingWindows.Claude;
```

- [ ] **Step 7 : Lancer les tests**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : les 5 nouveaux tests passent, **502 tests verts**.

- [ ] **Step 8 : Commit**

```powershell
git add src/UsageNotch.Core/Usage src/UsageNotch.App/Hosting/DemoMode.cs tests
git commit -m "feat(core): resolve each ring window through an alias group"
```

---

## Task 4 : Trois anneaux dans le modèle de cellule

**Files :**
- Create : `src/UsageNotch.Presentation/Pill/RingModel.cs`
- Modify : `src/UsageNotch.Presentation/Pill/CellModel.cs`, `src/UsageNotch.Presentation/Pill/PillPresenter.cs`, `src/UsageNotch.Presentation/ViewModels/NotchViewModel.cs`, `src/UsageNotch.Presentation/Preferences/SettingsPreview.cs`
- Test : `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs`

**Interfaces :**
- Consomme : `Theme.RingSession` / `RingWeeklyAll` / `RingWeeklyScoped` (tâche 1), `RingColoring` (tâche 2), `RingWindows.Claude` et `UsageSnapshot.Window(IEnumerable<string>)` (tâche 3).
- Produit :
  - `record RingModel(double? Fraction, string Color, string TrackColor)` ;
  - `CellModel.Rings` de type `IReadOnlyList<RingModel>`, toujours **trois** éléments, extérieur → intérieur ;
  - la nouvelle signature `PillPresenter.Cell(UsageSnapshot snapshot, IReadOnlyList<IReadOnlyList<string>> ringWindowIds, SessionState aggregate, Theme theme, CellContent content, RingColoring coloring, DateTimeOffset now)`.

`RingFraction`, `RingColor` et `ShowRing` restent sur `CellModel`, dérivés de `Rings[0]`, pour que `PillWindow.xaml` et `PillPreview` continuent de compiler et de fonctionner. Ils disparaissent en tâche 6.

- [ ] **Step 1 : Écrire les tests qui échouent**

Dans `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs`, remplacer les deux fabriques d'en-tête par :

```csharp
    private static UsageSnapshot Snap(
        SnapshotStatus status,
        double? session,
        double? weeklyAll = null,
        double? weeklyScoped = null,
        DateTimeOffset? fetched = null,
        string note = "",
        string scopedId = "weekly_scoped")
    {
        var windows = new List<LimitWindow>();
        if (session is { } s) windows.Add(new LimitWindow("session", "Session en cours", s, Now.AddHours(2)));
        if (weeklyAll is { } a) windows.Add(new LimitWindow("weekly_all", "Hebdomadaire (tous modèles)", a, Now.AddDays(3)));
        if (weeklyScoped is { } p) windows.Add(new LimitWindow(scopedId, "Hebdomadaire (par modèle)", p, Now.AddDays(3)));
        return new UsageSnapshot(status, windows, fetched ?? Now, note, null);
    }

    private static CellModel Cell(
        UsageSnapshot s,
        SessionState agg = SessionState.Idle,
        CellContent content = CellContent.RingAndPercent,
        RingColoring coloring = RingColoring.PerRing) =>
        PillPresenter.Cell(s, RingWindows.Claude, agg, Theme, content, coloring, Now);
```

Ajouter `using UsageNotch.Core.Usage;` s'il manque, puis ajouter ces tests :

```csharp
    [Fact]
    public void The_three_rings_are_ordered_session_then_weekly_then_scoped()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.73, 0.21, 0.52));

        c.Rings.Should().HaveCount(3);
        c.Rings[0].Fraction.Should().BeApproximately(0.73, 1e-9);
        c.Rings[1].Fraction.Should().BeApproximately(0.21, 1e-9);
        c.Rings[2].Fraction.Should().BeApproximately(0.52, 1e-9);
        c.Rings.Should().OnlyContain(r => r.TrackColor == Theme.RingTrack);
    }

    [Fact]
    public void A_legacy_opus_window_still_feeds_the_inner_ring()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.1, 0.2, 0.52, scopedId: "weekly_opus"));

        c.Rings[2].Fraction.Should().BeApproximately(0.52, 1e-9);
    }

    [Fact]
    public void Per_ring_colouring_gives_each_ring_its_own_theme_colour()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.95, 0.05, 0.5));

        c.Rings[0].Color.Should().Be(Theme.RingSession);
        c.Rings[1].Color.Should().Be(Theme.RingWeeklyAll);
        c.Rings[2].Color.Should().Be(Theme.RingWeeklyScoped);
    }

    [Fact]
    public void By_level_colouring_grades_each_ring_on_its_own_fraction()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.95, 0.05, 0.6), coloring: RingColoring.ByLevel);

        c.Rings[0].Color.Should().Be(Theme.LevelCritical);
        c.Rings[1].Color.Should().Be(Theme.LevelAmple);
        c.Rings[2].Color.Should().Be(Theme.LevelWatch);
    }

    [Fact]
    public void A_missing_window_draws_its_track_alone_without_moving_the_others()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.73, weeklyScoped: 0.52));

        c.Rings.Should().HaveCount(3);
        c.Rings[1].Fraction.Should().BeNull();
        c.Rings[1].Color.Should().Be(Theme.RingTrack);
        c.Rings[2].Fraction.Should().BeApproximately(0.52, 1e-9);
    }

    [Fact]
    public void Needs_auth_leaves_the_three_rings_as_bare_tracks()
    {
        var c = Cell(Snap(SnapshotStatus.NeedsAuth, 0.4, 0.4, 0.4, note: "Identifiant refusé"));

        c.Rings.Should().OnlyContain(r => r.Fraction == null && r.Color == Theme.RingTrack);
        c.PercentText.Should().Be("—");
    }

    [Fact]
    public void Exhausted_and_the_percent_still_speak_for_the_session_alone()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 1.0, 0.1, 0.1));

        c.Exhausted.Should().BeTrue();
        c.PercentText.Should().Be("100" + FrenchText.Nbsp + "%");

        Cell(Snap(SnapshotStatus.Ok, 0.1, 1.0, 1.0)).Exhausted.Should().BeFalse();
    }
```

Corriger enfin les tests existants que le nouveau défaut `PerRing` rend faux :

- `An_ok_reading_shows_the_headline_fraction_percent_and_level_colour` : renommer en `An_ok_reading_shows_the_session_fraction_percent_and_colour`, et attendre `Theme.RingSession` pour `RingColor` et `BandColor`.
- `The_ring_colour_follows_the_theme_thresholds` : passer `coloring: RingColoring.ByLevel` à `Cell`.
- `A_missing_headline_window_is_a_dash_not_another_window` : inchangé, il vérifie toujours que l'absence de session donne un tiret.

- [ ] **Step 2 : Lancer les tests pour vérifier qu'ils échouent**

Run : `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~PillPresenterTests"`
Expected : échec de compilation, `No overload for method 'Cell' takes 7 arguments`.

- [ ] **Step 3 : Créer le modèle d'anneau**

Créer `src/UsageNotch.Presentation/Pill/RingModel.cs` :

```csharp
namespace UsageNotch.Presentation.Pill;

/// <summary>
/// Ce qu'un anneau dessine. <see cref="Fraction"/> null = pas de lecture exploitable : la piste seule.
/// Couleurs en <c>#RRGGBB</c>.
/// </summary>
public sealed record RingModel(double? Fraction, string Color, string TrackColor);
```

- [ ] **Step 4 : Ajouter la liste au modèle de cellule**

Remplacer tout le contenu de `src/UsageNotch.Presentation/Pill/CellModel.cs` par :

```csharp
namespace UsageNotch.Presentation.Pill;

/// <summary>
/// Tout ce que la cellule dessine. <see cref="Rings"/> porte toujours trois anneaux, de l'extérieur vers
/// l'intérieur. <see cref="RingFraction"/> et <see cref="RingColor"/> reprennent l'anneau de session : ils
/// disparaissent dès que la vue lit <see cref="Rings"/>. Couleurs en <c>#RRGGBB</c>.
/// </summary>
public sealed record CellModel(
    IReadOnlyList<RingModel> Rings,
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

- [ ] **Step 5 : Construire les trois anneaux dans le présentateur**

Dans `src/UsageNotch.Presentation/Pill/PillPresenter.cs`, remplacer la méthode `Cell` par :

```csharp
    public static CellModel Cell(
        UsageSnapshot snapshot,
        IReadOnlyList<IReadOnlyList<string>> ringWindowIds,
        SessionState aggregate,
        Theme theme,
        CellContent content,
        RingColoring coloring,
        DateTimeOffset now)
    {
        var activity = ActivityOf(aggregate);
        var blind = snapshot.Status == SnapshotStatus.NeedsAuth;
        string[] fixedColors = [theme.RingSession, theme.RingWeeklyAll, theme.RingWeeklyScoped];

        var rings = new RingModel[ringWindowIds.Count];
        for (var i = 0; i < ringWindowIds.Count; i++)
        {
            var window = blind ? null : snapshot.Window(ringWindowIds[i]);
            double? fraction = window is null ? null : Math.Clamp(window.UsedFraction, 0.0, 1.0);
            rings[i] = new RingModel(fraction, ColorFor(fraction, fixedColors[i], theme, coloring), theme.RingTrack);
        }

        var session = rings[0].Fraction;
        var percent = session is { } p
            ? FrenchText.Percent(p)
            : IsWaitingForFirstReading(snapshot) ? "…" : "—";

        var dimmed = snapshot.Status == SnapshotStatus.Stale
            || (snapshot.FetchedAt != DateTimeOffset.MinValue && now - snapshot.FetchedAt > StaleAfter);

        return new CellModel(
            Rings: rings,
            RingFraction: session,
            RingColor: rings[0].Color,
            TrackColor: theme.RingTrack,
            PercentText: percent,
            TextColor: theme.Text,
            ShowRing: content != CellContent.PercentOnly,
            ShowPercent: content != CellContent.RingOnly,
            Dimmed: dimmed,
            Exhausted: session >= 1.0,
            Activity: activity,
            ActivityColor: ActivityColor(activity, theme),
            BandColor: activity == ActivityKind.Attention ? theme.Attention : rings[0].Color);
    }

    /// <summary>Sans lecture, l'anneau prend la couleur de sa piste : il disparaît dedans.</summary>
    private static string ColorFor(double? fraction, string fixedColor, Theme theme, RingColoring coloring) =>
        fraction is not { } f ? theme.RingTrack
        : coloring == RingColoring.ByLevel ? theme.LevelColor(f)
        : fixedColor;
```

`fixedColors` ne porte que trois entrées : le trio est fixe, et `ringWindowIds` en compte trois par contrat. Si un fournisseur futur en déclarait davantage, l'indexation lèverait — c'est voulu, ce serait un contrat rompu à traiter, pas à masquer.

- [ ] **Step 6 : Mettre à jour les deux appelants**

Dans `src/UsageNotch.Presentation/ViewModels/NotchViewModel.cs`, méthode `Recompute` :

```csharp
        Cell = PillPresenter.Cell(snapshot, _provider.RingWindowIds, _sessions.Aggregate, theme, settings.CellContent, settings.Coloring, now);
```

Dans `src/UsageNotch.Presentation/Preferences/SettingsPreview.cs`, l'aperçu doit montrer les trois anneaux. `Build` transmet le mode :

```csharp
        PreviewSample[] samples =
        [
            Sample("Modéré · en cours", theme.ThresholdWatch / 2, SessionState.Running, theme, content, settings.Coloring),
            Sample("Vigilance · en attente", (theme.ThresholdWatch + theme.ThresholdCritical) / 2, SessionState.Attention, theme, content, settings.Coloring),
            Sample("Critique · terminé", (theme.ThresholdCritical + 1.0) / 2, SessionState.Done, theme, content, settings.Coloring),
        ];
```

et `Sample` fabrique trois fenêtres, les deux hebdomadaires décalées pour que les anneaux se distinguent dans l'aperçu :

```csharp
    private static PreviewSample Sample(
        string caption, double fraction, SessionState state, Theme theme, CellContent content, RingColoring coloring)
    {
        var snapshot = new UsageSnapshot(
            SnapshotStatus.Ok,
            [
                new LimitWindow("session", "Session en cours", fraction, SampleTime.AddHours(3)),
                new LimitWindow("weekly_all", "Hebdomadaire (tous modèles)", fraction * 0.6, SampleTime.AddDays(3)),
                new LimitWindow("weekly_scoped", "Hebdomadaire (par modèle)", fraction * 0.3, SampleTime.AddDays(3)),
            ],
            SampleTime,
            "",
            null);
        return new PreviewSample(caption, PillPresenter.Cell(snapshot, RingWindows.Claude, state, theme, content, coloring, SampleTime));
    }
```

- [ ] **Step 7 : Reprendre les tests de l'aperçu**

Trois tests de `tests/UsageNotch.Presentation.Tests/Preferences/SettingsPreviewTests.cs` portaient sur l'anneau unique et deviennent faux, le défaut `PerRing` donnant désormais `RingSession` aux trois échantillons. Les corriger **sans affaiblir l'assertion** :

Dans `Three_samples_show_each_usage_level_and_session_state`, l'échantillon doit continuer à prouver la graduation par niveau — c'est tout son objet. Construire le modèle en mode `ByLevel` et lire l'anneau extérieur :

```csharp
        var model = SettingsPreview.Build(new Settings { Coloring = RingColoring.ByLevel }, accentHex: null);
```

```csharp
        model.Samples.Select(s => s.Cell.Rings[0].Color).Should().Equal(theme.LevelAmple, theme.LevelWatch, theme.LevelCritical);
```

Dans `Samples_follow_custom_thresholds`, ajouter `Coloring = RingColoring.ByLevel` aux réglages construits, puis remplacer les quatre assertions par :

```csharp
        model.Samples[0].Cell.Rings[0].Fraction.Should().BeApproximately(0.15, 1e-9);
        model.Samples[1].Cell.Rings[0].Fraction.Should().BeApproximately(0.45, 1e-9);
        model.Samples[2].Cell.Rings[0].Fraction.Should().BeApproximately(0.8, 1e-9);
        model.Samples.Select(s => s.Cell.Rings[0].Color).Should().Equal(
            Theme.Codenotch.LevelAmple, Theme.Codenotch.LevelWatch, Theme.Codenotch.LevelCritical);
```

Dans `The_theme_follows_the_preset_and_the_system_accent`, l'assertion reste vraie en `PerRing` puisque l'accent alimente `RingSession` ; seul le chemin change :

```csharp
        model.Samples[0].Cell.Rings[0].Color.Should().Be("#0078D4");
```

Ajouter enfin le test qui manquait, sur le mode par défaut :

```csharp
    [Fact]
    public void Per_ring_colouring_shows_the_three_ring_colours_in_the_preview()
    {
        var model = SettingsPreview.Build(new Settings(), accentHex: null);
        var theme = Theme.Codenotch;

        model.Samples.Should().OnlyContain(s =>
            s.Cell.Rings[0].Color == theme.RingSession
            && s.Cell.Rings[1].Color == theme.RingWeeklyAll
            && s.Cell.Rings[2].Color == theme.RingWeeklyScoped);
    }
```

`Cell_content_is_honoured` emploie `CellContent.PercentOnly` et `ShowRing` ; il est repris en tâche 6, quand ces deux-là disparaissent. Le laisser tel quel ici.

- [ ] **Step 8 : Lancer les tests**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : les 8 nouveaux tests passent, **510 tests verts**, zéro avertissement.

- [ ] **Step 9 : Commit**

```powershell
git add src/UsageNotch.Presentation tests/UsageNotch.Presentation.Tests
git commit -m "feat(presentation): build three usage rings per cell"
```

---

## Task 5 : Dessiner la pile dans la pilule et dans l'aperçu

**Files :**
- Modify : `src/UsageNotch.Presentation/Pill/PillMetrics.cs`, `src/UsageNotch.App/Views/PillWindow.xaml`, `src/UsageNotch.App/Controls/PillPreview.cs`
- Test : `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs`

**Interfaces :**
- Consomme : `CellModel.Rings` (tâche 4).
- Produit : `PillMetrics.RingHostSize` = 56, `RingOuter` = 44, `RingMiddle` = 32, `RingInner` = 20, `RingBandThickness` = 4, `ActivitySize` = 52. `PillMetrics.RingSize` disparaît.

**Les cotes, et pourquoi elles tombent juste.** Un `ProgressRing` de côté `D` et d'épaisseur `T` trace sa bande entre les rayons `(D − T) / 2` et `(D + T) / 2`. Donc 44/4 → 18 à 22, 32/4 → 12 à 16, 20/4 → 6 à 10 : **2 DIP d'écart** entre deux anneaux voisins, et **12 DIP** de trou central libre. Les voyants d'activité, à Ø 52 et 2,5 d'épaisseur, occupent 24,75 à 27,25 : 2,75 DIP au-dessus de l'anneau extérieur, et 4,75 DIP jusqu'au bord du corps, qui fait 64 d'épaisseur. La zone de contenu mesure 104 le long du bord (`BodyLength`, congés exclus) : la pile seule en occupe 56, et avec le pourcentage 56 + 4 + 18 = 78. **La pilule ne s'allonge dans aucun des deux modes.**

- [ ] **Step 1 : Écrire le test qui échoue**

Dans `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs`, sous le test `Window_length_includes_both_fillets` :

```csharp
    [Fact]
    public void The_ring_stack_leaves_two_dip_between_neighbours_and_a_twelve_dip_core()
    {
        static (double Inner, double Outer) Band(double diameter) =>
            ((diameter - PillMetrics.RingBandThickness) / 2, (diameter + PillMetrics.RingBandThickness) / 2);

        var outer = Band(PillMetrics.RingOuter);
        var middle = Band(PillMetrics.RingMiddle);
        var inner = Band(PillMetrics.RingInner);

        (outer.Inner - middle.Outer).Should().Be(2);
        (middle.Inner - inner.Outer).Should().Be(2);
        (inner.Inner * 2).Should().Be(12);
    }

    [Fact]
    public void The_activity_marks_clear_the_outer_ring_and_stay_inside_the_host()
    {
        var outerEdge = (PillMetrics.RingOuter + PillMetrics.RingBandThickness) / 2;
        var activityInnerEdge = (PillMetrics.ActivitySize - 2.5) / 2;
        var activityOuterEdge = (PillMetrics.ActivitySize + 2.5) / 2;

        activityInnerEdge.Should().BeGreaterThan(outerEdge);
        (activityOuterEdge * 2).Should().BeLessThan(PillMetrics.RingHostSize);
    }

    [Fact]
    public void The_stack_and_the_percent_fit_the_body_without_lengthening_the_pill()
    {
        const double percentTextHeight = 18;
        const double gap = 4;

        (PillMetrics.RingHostSize + gap + percentTextHeight).Should().BeLessThan(PillMetrics.BodyLength);
        PillMetrics.RingHostSize.Should().BeLessThan(PillMetrics.Thickness);
    }
```

- [ ] **Step 2 : Lancer les tests pour vérifier qu'ils échouent**

Run : `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~PillPresenterTests"`
Expected : échec de compilation, `'PillMetrics' does not contain a definition for 'RingHostSize'`.

- [ ] **Step 3 : Poser les cotes**

Dans `src/UsageNotch.Presentation/Pill/PillMetrics.cs`, remplacer `public const double RingSize = 44;` par :

```csharp
    /// <summary>Hôte de la pile : contient les trois anneaux et les voyants d'activité qui les entourent.</summary>
    public const double RingHostSize = 56;
    public const double RingOuter = 44;
    public const double RingMiddle = 32;
    public const double RingInner = 20;
    public const double RingBandThickness = 4;
    /// <summary>Diamètre de l'arc de rotation et de l'anneau d'attente, tracés autour de la pile.</summary>
    public const double ActivitySize = 52;
```

- [ ] **Step 4 : Lancer les tests**

Run : `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~PillPresenterTests"`
Expected : PASS. `dotnet build` échouera encore : `PillPreview` référence `PillMetrics.RingSize`. C'est l'objet des deux étapes suivantes.

- [ ] **Step 5 : Redessiner la pilule**

Dans `src/UsageNotch.App/Views/PillWindow.xaml`, remplacer tout le bloc `<Grid x:Name="RingHost" …>` par :

```xml
          <Grid x:Name="RingHost" Width="56" Height="56" Margin="0,0,0,4"
                Visibility="{Binding Cell.ShowRing, Converter={StaticResource BoolVis}}">
            <controls:ProgressRing Width="44" Height="44" RingThickness="4"
                                   TargetFraction="{Binding Cell.Rings[0].Fraction}"
                                   RingBrush="{Binding Cell.Rings[0].Color, Converter={StaticResource Hex}}"
                                   TrackBrush="{Binding Cell.Rings[0].TrackColor, Converter={StaticResource Hex}}" />
            <controls:ProgressRing Width="32" Height="32" RingThickness="4"
                                   TargetFraction="{Binding Cell.Rings[1].Fraction}"
                                   RingBrush="{Binding Cell.Rings[1].Color, Converter={StaticResource Hex}}"
                                   TrackBrush="{Binding Cell.Rings[1].TrackColor, Converter={StaticResource Hex}}" />
            <controls:ProgressRing Width="20" Height="20" RingThickness="4"
                                   TargetFraction="{Binding Cell.Rings[2].Fraction}"
                                   RingBrush="{Binding Cell.Rings[2].Color, Converter={StaticResource Hex}}"
                                   TrackBrush="{Binding Cell.Rings[2].TrackColor, Converter={StaticResource Hex}}" />
            <Path x:Name="RunningArc" Width="56" Height="56" Stretch="None"
                  Data="M 28,2 A 26,26 0 0 1 54,28"
                  StrokeThickness="2.5" StrokeStartLineCap="Round" StrokeEndLineCap="Round"
                  RenderTransformOrigin="0.5,0.5"
                  Stroke="{Binding Cell.ActivityColor, Converter={StaticResource Hex}}"
                  Visibility="{Binding Cell.Activity, Converter={StaticResource Activity}, ConverterParameter=Running}">
              <Path.RenderTransform>
                <RotateTransform x:Name="SpinRotate" />
              </Path.RenderTransform>
            </Path>
            <Ellipse x:Name="AttentionRing" Width="52" Height="52" StrokeThickness="2.5"
                     Stroke="{Binding Cell.ActivityColor, Converter={StaticResource Hex}}"
                     Visibility="{Binding Cell.Activity, Converter={StaticResource Activity}, ConverterParameter=Attention}" />
            <Ellipse Width="8" Height="8"
                     Fill="{Binding Cell.ActivityColor, Converter={StaticResource Hex}}"
                     Visibility="{Binding Cell.Activity, Converter={StaticResource Activity}, ConverterParameter=Done}" />
          </Grid>
```

L'arc de rotation part de midi `(28, 2)` et va au quart droit `(54, 28)` : centre `(28, 28)`, rayon 26, soit le même quart de tour qu'avant, à l'échelle de la pile.

Dans le même fichier, la marge horizontale des bords haut et bas est posée par le code-behind ; rien à changer ici.

Dans `src/UsageNotch.App/Views/PillWindow.xaml.cs`, méthode `ApplySettings`, la marge de l'hôte reste telle quelle :

```csharp
        RingHost.Margin = vertical ? new Thickness(0, 0, 0, 4) : new Thickness(0, 0, 6, 0);
```

- [ ] **Step 6 : Redessiner l'aperçu des réglages**

Dans `src/UsageNotch.App/Controls/PillPreview.cs`, remplacer `RunningArc` et `BuildRing` par :

```csharp
    private static readonly Geometry RunningArc = FrozenGeometry("M 28,2 A 26,26 0 0 1 54,28");
```

```csharp
    private static Grid BuildRing(CellModel cell, bool vertical)
    {
        var host = new Grid
        {
            Width = PillMetrics.RingHostSize,
            Height = PillMetrics.RingHostSize,
            Margin = vertical ? new Thickness(0, 0, 0, 4) : new Thickness(0, 0, 6, 0),
        };

        double[] diameters = [PillMetrics.RingOuter, PillMetrics.RingMiddle, PillMetrics.RingInner];
        for (var i = 0; i < diameters.Length; i++)
        {
            var ring = cell.Rings[i];
            host.Children.Add(new ProgressRing
            {
                Width = diameters[i],
                Height = diameters[i],
                RingThickness = PillMetrics.RingBandThickness,
                RingBrush = HexBrushConverter.ToBrush(ring.Color),
                TrackBrush = HexBrushConverter.ToBrush(ring.TrackColor),
                // Fraction d'abord : l'animation lancée par TargetFraction part alors de la valeur finale, sans balayage à chaque retouche.
                Fraction = ring.Fraction ?? 0,
                TargetFraction = ring.Fraction,
            });
        }

        var activity = HexBrushConverter.ToBrush(cell.ActivityColor);
        switch (cell.Activity)
        {
            case ActivityKind.Running:
                host.Children.Add(new Path
                {
                    Width = PillMetrics.RingHostSize,
                    Height = PillMetrics.RingHostSize,
                    Stretch = Stretch.None,
                    Data = RunningArc,
                    StrokeThickness = 2.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Stroke = activity,
                });
                break;
            case ActivityKind.Attention:
                host.Children.Add(new Ellipse
                {
                    Width = PillMetrics.ActivitySize,
                    Height = PillMetrics.ActivitySize,
                    StrokeThickness = 2.5,
                    Stroke = activity,
                });
                break;
            case ActivityKind.Done:
                host.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = activity });
                break;
        }
        return host;
    }
```

- [ ] **Step 7 : Compiler et lancer les tests**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : **513 tests verts**, zéro avertissement.

- [ ] **Step 8 : Vérifier à l'écran**

```powershell
src\UsageNotch.App\bin\Debug\net10.0-windows\UsageNotch.App.exe --demo
```

Attendu : trois anneaux emboîtés, session 73 % à l'extérieur, hebdomadaire 21 % au milieu, par modèle 52 % à l'intérieur, chacun de sa couleur. Envoyer un événement pour voir un voyant d'activité tourner **autour** de la pile, sans la mordre :

```powershell
Invoke-WebRequest -Method Post -Uri "http://127.0.0.1:48667/event?e=attention&ppid=1" -UseBasicParsing `
  -Body '{"session_id":"test-0001","cwd":"C:\\src\\projet","message":"Autoriser Bash ?"}'
```

Quitter la démo avant de continuer. Ne pas toucher à l'instance réelle.

- [ ] **Step 9 : Commit**

```powershell
git add src/UsageNotch.Presentation/Pill/PillMetrics.cs src/UsageNotch.App/Views/PillWindow.xaml src/UsageNotch.App/Controls/PillPreview.cs tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs
git commit -m "feat(app): draw the three usage rings as a concentric stack"
```

---

## Task 6 : Retirer le contrat à un seul anneau

**Files :**
- Modify : `src/UsageNotch.Presentation/Pill/CellModel.cs`, `src/UsageNotch.Presentation/Pill/PillPresenter.cs`, `src/UsageNotch.Core/Usage/IUsageProvider.cs`, `src/UsageNotch.Core/Usage/ClaudeUsageProvider.cs`, `src/UsageNotch.App/Hosting/DemoMode.cs`, `src/UsageNotch.App/Views/PillWindow.xaml`, `src/UsageNotch.App/Views/PillWindow.xaml.cs`
- Test : `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs`

**Interfaces :**
- Consomme : tout ce que les tâches 3 à 5 ont produit.
- Produit : `CellModel` sans `RingFraction`, `RingColor` ni `ShowRing` ; `IUsageProvider` sans `HeadlineWindowId`.

**Pourquoi `ShowRing` part.** Les deux modes de contenu survivants — `RingAndPercent` et `RingOnly` — dessinent tous deux la pile, et `PercentOnly` est remappé par `Settings.Clamp()` dès la lecture du fichier. Le champ serait constamment vrai. `PillWindow.UpdateAnimations()` s'en servait pour ne pas faire tourner une animation sur une cible invisible ; la pile étant toujours dessinée, seules la visibilité de la fenêtre et celle du calque comptent encore.

- [ ] **Step 1 : Retirer les assertions sur les champs disparus**

Dans `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs` :

- supprimer les assertions `c.RingFraction`, `c.RingColor` et `c.ShowRing` — chacune a son équivalent sur `c.Rings[0]`, déjà couvert par les tests de la tâche 4 ;
- remplacer le théorème `Cell_content_setting_controls_what_is_shown` par :

```csharp
    [Theory]
    [InlineData(CellContent.RingAndPercent, true)]
    [InlineData(CellContent.RingOnly, false)]
    public void Cell_content_setting_controls_whether_the_percent_is_written(CellContent content, bool percent)
    {
        Cell(Snap(SnapshotStatus.Ok, 0.2), content: content).ShowPercent.Should().Be(percent);
    }
```

- dans `A_missing_headline_window_is_a_dash_not_another_window`, renommé `A_missing_session_window_is_a_dash_not_another_window`, remplacer `c.RingFraction.Should().BeNull()` par `c.Rings[0].Fraction.Should().BeNull()` et `c.RingColor` par `c.Rings[0].Color` ;
- dans `The_empty_startup_snapshot_waits_with_an_ellipsis`, `Needs_auth_shows_a_dash_even_with_old_windows` et `A_full_window_is_exhausted`, faire de même.

Dans `tests/UsageNotch.Presentation.Tests/Preferences/SettingsPreviewTests.cs`, le test `Cell_content_is_honoured` emploie les deux disparus. Le réécrire sur le mode survivant :

```csharp
    [Fact]
    public void Cell_content_is_honoured()
    {
        var model = SettingsPreview.Build(new Settings { CellContent = CellContent.RingOnly }, accentHex: null);

        model.Samples.Should().OnlyContain(s => !s.Cell.ShowPercent && s.Cell.Rings.Count == 3);
    }
```

- [ ] **Step 2 : Lancer les tests pour vérifier qu'ils passent encore**

Run : `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~PillPresenterTests"`
Expected : PASS — on n'a retiré que des assertions redondantes, rien n'est encore supprimé du code de production.

- [ ] **Step 3 : Retirer les champs du modèle**

Dans `src/UsageNotch.Presentation/Pill/CellModel.cs`, supprimer les paramètres `RingFraction`, `RingColor` et `ShowRing`. Dans `PillPresenter.Cell`, supprimer les trois lignes correspondantes de la construction, et garder la variable locale `session` pour `PercentText`, `Exhausted` et `BandColor`.

- [ ] **Step 4 : Retirer `HeadlineWindowId`**

Supprimer la propriété de `src/UsageNotch.Core/Usage/IUsageProvider.cs`, de `ClaudeUsageProvider`, de `DemoUsageProvider` et de tout double de test que le compilateur signalera.

- [ ] **Step 5 : Ajuster la vue**

Dans `src/UsageNotch.App/Views/PillWindow.xaml`, retirer l'attribut `Visibility` de `RingHost` — la pile est toujours dessinée :

```xml
          <Grid x:Name="RingHost" Width="56" Height="56" Margin="0,0,0,4">
```

Dans `src/UsageNotch.App/Views/PillWindow.xaml.cs`, `UpdateAnimations` :

```csharp
    private void UpdateAnimations()
    {
        var activity = _vm.Cell.Activity;
        // La pile est toujours dessinée : seule compte la visibilité réelle de la fenêtre et du calque.
        var ringShown = IsVisible && PillLayer.Visibility == Visibility.Visible;
        SetStoryboard("Spin", ref _spinRunning, ringShown && activity == ActivityKind.Running);
        SetStoryboard("Pulse", ref _pulseRunning, ringShown && activity == ActivityKind.Attention);
        SetStoryboard("BandPulse", ref _bandPulseRunning,
            IsVisible && BandPath.Visibility == Visibility.Visible && activity == ActivityKind.Attention);
    }
```

- [ ] **Step 6 : Compiler et lancer les tests**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : **512 tests verts** — le théorème du contenu perd un cas —, zéro avertissement. Le compilateur signale tout appelant oublié ; il ne doit en rester aucun.

- [ ] **Step 7 : Commit**

```powershell
git add src tests
git commit -m "refactor: drop the single-ring cell contract"
```

---

## Task 7 : Les réglages

**Files :**
- Modify : `src/UsageNotch.Presentation/Preferences/Choices.cs`, `src/UsageNotch.Presentation/Preferences/AppearancePageViewModel.cs`, `src/UsageNotch.App/Views/Preferences/AppearancePage.xaml`
- Test : `tests/UsageNotch.Presentation.Tests/Preferences/ChoicesTests.cs`, `tests/UsageNotch.Presentation.Tests/Preferences/AppearancePageViewModelTests.cs`

**Interfaces :**
- Consomme : `RingColoring` (tâche 2), `Theme.RingSession` / `RingWeeklyAll` / `RingWeeklyScoped` (tâche 1).
- Produit : `Choices.RingColorings`, `AppearancePageViewModel.Coloring`, et trois `ColorSlot` de clés `RingSession`, `RingWeeklyAll`, `RingWeeklyScoped`.

- [ ] **Step 1 : Écrire les tests qui échouent**

Dans `tests/UsageNotch.Presentation.Tests/Preferences/ChoicesTests.cs`, remplacer la ligne des contenus de cellule et ajouter celle des colorations, dans `Enumerations_are_listed_in_spec_order_with_french_labels` :

```csharp
        Choices.CellContents.Select(c => c.Value).Should().Equal(CellContent.RingAndPercent, CellContent.RingOnly);
        Choices.CellContents.Select(c => c.Label).Should().Equal("Anneaux et pourcentage", "Anneaux seuls");
        Choices.RingColorings.Select(c => c.Value).Should().Equal(RingColoring.PerRing, RingColoring.ByLevel);
        Choices.RingColorings.Select(c => c.Label).Should().Equal("Une couleur par anneau", "Selon le niveau");
```

Ajouter dans le même fichier :

```csharp
    [Fact]
    public void The_retired_percent_only_mode_is_no_longer_offered()
    {
        Choices.CellContents.Should().NotContain(c => c.Value == CellContent.PercentOnly);
    }
```

Dans `tests/UsageNotch.Presentation.Tests/Preferences/AppearancePageViewModelTests.cs`, employer les deux fabriques déjà présentes dans le fichier — `Create()`, qui rend le quadruplet `(f, vm, picker, accent)`, et `Slot(vm, key)` — sans en créer d'autres.

Le test `Lists_presets_contents_and_the_ten_theme_colours_in_order` énumère les dix couleurs et devient faux. Le renommer `Lists_presets_contents_and_the_thirteen_theme_colours_in_order` et étendre ses deux listes, les trois anneaux s'insérant après `LevelCritical` :

```csharp
        vm.Colors.Select(c => c.Key).Should().Equal(
            "PillBackground", "PillBorder", "RingTrack", "LevelAmple", "LevelWatch", "LevelCritical",
            "RingSession", "RingWeeklyAll", "RingWeeklyScoped", "Running", "Attention", "Done", "Text");
        vm.Colors.Select(c => c.Label).Should().Equal(
            "Fond de la pilule", "Contour de la pilule", "Piste de l'anneau", "Niveau modéré", "Niveau vigilance",
            "Niveau critique", "Anneau session", "Anneau hebdomadaire", "Anneau hebdo. par modèle",
            "Session en cours", "Session en attente", "Session terminée", "Texte");
```

Puis ajouter :

```csharp
    [Fact]
    public void Editing_a_ring_colour_switches_to_the_custom_theme()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        Slot(vm, "RingWeeklyScoped").Hex = "#123456";

        vm.Preset.Should().Be(ThemePreset.Custom);
        vm.Theme.RingWeeklyScoped.Should().Be("#123456");
    }

    [Fact]
    public void The_colouring_mode_is_offered_and_saved()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.RingColorings.Should().BeSameAs(Choices.RingColorings);

        vm.Coloring = RingColoring.ByLevel;

        vm.Coloring.Should().Be(RingColoring.ByLevel);
        f.Draft.Value.Coloring.Should().Be(RingColoring.ByLevel);
    }

    [Fact]
    public void Changing_the_colouring_mode_does_not_switch_to_the_custom_theme()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.Coloring = RingColoring.ByLevel;

        vm.Preset.Should().Be(ThemePreset.Codenotch);
    }
```

Le dernier test est le plus important des trois : il garde la frontière posée par la spec entre un **réglage** et une **couleur de thème**. Si `Coloring` passait par `EditTheme`, changer de mode ferait basculer l'utilisateur en thème Personnalisé sans qu'il ait touché à une seule couleur.

- [ ] **Step 2 : Lancer les tests pour vérifier qu'ils échouent**

Run : `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~Preferences"`
Expected : échec de compilation, `'Choices' does not contain a definition for 'RingColorings'`.

- [ ] **Step 3 : Ajuster les listes de choix**

Dans `src/UsageNotch.Presentation/Preferences/Choices.cs` :

```csharp
    public static IReadOnlyList<Choice<CellContent>> CellContents { get; } =
    [
        new(CellContent.RingAndPercent, "Anneaux et pourcentage"),
        new(CellContent.RingOnly, "Anneaux seuls"),
    ];

    public static IReadOnlyList<Choice<RingColoring>> RingColorings { get; } =
    [
        new(RingColoring.PerRing, "Une couleur par anneau"),
        new(RingColoring.ByLevel, "Selon le niveau"),
    ];
```

- [ ] **Step 4 : Étendre le modèle de vue**

Dans `src/UsageNotch.Presentation/Preferences/AppearancePageViewModel.cs`, ajouter trois entrées à la liste `Colors`, après `LevelCritical` :

```csharp
            Slot("RingSession", "Anneau session", t => t.RingSession, (t, h) => t with { RingSession = h }),
            Slot("RingWeeklyAll", "Anneau hebdomadaire", t => t.RingWeeklyAll, (t, h) => t with { RingWeeklyAll = h }),
            Slot("RingWeeklyScoped", "Anneau hebdo. par modèle", t => t.RingWeeklyScoped, (t, h) => t with { RingWeeklyScoped = h }),
```

Exposer la liste et la propriété, à côté de `CellContents` et `CellContent` :

```csharp
    public IReadOnlyList<Choice<RingColoring>> RingColorings => Choices.RingColorings;
```

```csharp
    public RingColoring Coloring
    {
        get => _draft.Value.Coloring;
        set
        {
            if (value == Coloring) return;
            _draft.Edit(s => s with { Coloring = value });
        }
    }
```

`Coloring` passe par `_draft.Edit` et **non** par `EditTheme` : c'est un réglage, pas une couleur, et il ne doit pas basculer le thème en Personnalisé.

- [ ] **Step 5 : Ajouter la liste déroulante à la page**

Dans `src/UsageNotch.App/Views/Preferences/AppearancePage.xaml`, juste sous `<TextBlock Text="Couleurs" Style="{StaticResource SectionTitle}" />` et avant l'`ItemsControl` :

```xml
    <StackPanel Orientation="Horizontal" Margin="0,0,0,6">
      <TextBlock Text="Coloration des anneaux" Width="180" Style="{StaticResource FieldLabel}" />
      <ComboBox Width="220" VerticalAlignment="Center"
                AutomationProperties.AutomationId="RingColoring" AutomationProperties.Name="Coloration des anneaux"
                ItemsSource="{Binding RingColorings}" DisplayMemberPath="Label" SelectedValuePath="Value"
                SelectedValue="{Binding Coloring}" />
    </StackPanel>
    <TextBlock Style="{StaticResource NoteText}"
               Text="Selon le niveau : chaque anneau prend la couleur de son propre niveau, et les trois couleurs d'anneaux ci-dessous restent sans effet." />
```

Et, dans la grille « Taille et contenu », renommer le libellé du contenu, qui ne porte plus sur une cellule à un anneau :

```xml
      <TextBlock Grid.Row="1" Text="Contenu de la pilule" Style="{StaticResource FieldLabel}" />
```

en mettant à jour les deux `AutomationProperties.Name` de la même ligne à « Contenu de la pilule ». Laisser l'`AutomationId` à `CellContent` : les procédures de vérification du Plan 3 s'en servent.

- [ ] **Step 6 : Compiler et lancer les tests**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : **516 tests verts**, zéro avertissement.

- [ ] **Step 7 : Vérifier la page à l'écran**

Lancer `--demo`, ouvrir Réglages → Apparence. Attendu : la liste « Coloration des anneaux » et sa note, les trois nouvelles couleurs dans la liste, et l'aperçu qui montre trois anneaux. Basculer sur « Selon le niveau » : les trois anneaux de l'aperçu passent aux couleurs de niveau, chacun selon sa propre fraction. Revenir à « Une couleur par anneau ». Vérifier que la bascule n'a **pas** fait passer le thème en Personnalisé.

- [ ] **Step 8 : Commit**

```powershell
git add src/UsageNotch.Presentation/Preferences src/UsageNotch.App/Views/Preferences/AppearancePage.xaml tests/UsageNotch.Presentation.Tests/Preferences
git commit -m "feat(settings): ring colours and colouring mode in the appearance page"
```

---

## Task 8 : Vérification complète et documentation

**Files :**
- Modify : `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md`, `docs/REPRISE.md`

- [ ] **Step 1 : Vérifier la matrice de rendu**

En `--demo`, et pour chaque combinaison, contrôler à l'écran :

| À faire varier | Valeurs | Ce qu'on regarde |
|---|---|---|
| Bord | Droite, Gauche, Haut, Bas | La pile reste centrée, l'arc part de midi, la pilule ne change pas de taille |
| Échelle | 40 %, 100 %, 150 % | L'anneau intérieur reste distinguable de la piste à 40 % |
| Contenu | Anneaux et pourcentage, Anneaux seuls | Le pourcentage de la **session** s'affiche ou non ; la pilule ne s'allonge pas |
| Coloration | Une couleur par anneau, Selon le niveau | Trois couleurs distinctes, ou trois niveaux |
| Activité | en cours, en attente, terminé | Arc et anneau **autour** de la pile, point au centre ; aucun chevauchement |
| Visibilité | Déplié, Replié, Masqué | Le repli et la bande fonctionnent comme avant |

Les événements de session s'envoient à la démo ainsi :

```powershell
Invoke-WebRequest -Method Post -Uri "http://127.0.0.1:48667/event?e=running&ppid=1" -UseBasicParsing `
  -Body '{"session_id":"test-0001","cwd":"C:\\src\\projet"}'
```

Événements acceptés : `session_start`, `running`, `attention`, `done`, `session_end`.

- [ ] **Step 2 : Vérifier la migration sur un vrai fichier**

Copier le `settings.json` de la démo (`%TEMP%\UsageNotch-demo`), y mettre `"version": 1` et `"cellContent": "PercentOnly"`, relancer la démo. Attendu : l'application démarre, la pilule montre ses trois anneaux **et** le pourcentage, aucun fichier `.corrupt-…` n'apparaît, et le fichier réécrit porte `"version": 2` et `"cellContent": "RingAndPercent"`.

Ne **jamais** faire cette manipulation sur `%APPDATA%\UsageNotch\settings.json`, qui appartient à l'instance réelle.

- [ ] **Step 3 : Écrire le journal de bord**

Ajouter une section au journal, `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md`, dans la forme des chantiers précédents. Y consigner au minimum :

- le contrat `CellModel.Rings`, toujours trois éléments, ordre extérieur → intérieur, et le fait qu'un anneau sans lecture prend la couleur de sa piste plutôt que d'être masqué ;
- les cotes de la pile et d'où elles sortent : bande d'un `ProgressRing` entre `(D − T) / 2` et `(D + T) / 2`, d'où les 2 DIP d'écart et les 12 DIP de trou central ;
- pourquoi les voyants d'activité ont dû sortir de la pile ;
- **le piège `JsonStringEnumConverter`** : une valeur d'enum inconnue condamne tout le fichier de réglages, d'où la survie de `CellContent.PercentOnly` dans l'enum et son remappage dans `Clamp()` ;
- pourquoi `RingColoring` vit dans `Settings` et non dans `Theme` : sinon changer de préréglage réinitialiserait le choix de l'utilisateur ;
- les groupes d'alias de `RingWindows` et la raison : le `kind` renvoyé par l'API varie.

- [ ] **Step 4 : Mettre le backlog à jour**

Dans `docs/REPRISE.md`, section Backlog, l'idée 1 est livrée : remplacer son entrée par une ligne qui renvoie à la spec et au plan, et retirer la mention « Plan d'implémentation à écrire ». Ajouter au tableau de la carte de la documentation la ligne du plan :

```markdown
| `docs/superpowers/plans/2026-09-20-usagenotch-pill-rings.md` | Plan 4 : pilule à trois anneaux |
```

Ajouter enfin à la liste « À vérifier à l'usage » : la lisibilité de l'anneau intérieur à l'usage quotidien, à l'échelle habituelle de l'utilisateur.

- [ ] **Step 5 : Vérification finale**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : **516 tests verts**, zéro avertissement.

- [ ] **Step 6 : Commit**

```powershell
git add docs
git commit -m "docs: record the three-ring pill work"
```

- [ ] **Step 7 : Fusionner**

```powershell
git switch main
git merge --no-ff feat/pill-rings
dotnet build UsageNotch.sln; if ($?) { dotnet test }
```

Ne pousser qu'après accord de l'utilisateur.

---

## Notes d'exécution

**Le compte de tests est un outil de détection, pas un dogme.** Chaque tâche annonce le total attendu. S'il diffère, comprendre pourquoi avant de continuer : un test disparu sans raison est une régression silencieuse.

**Les tâches 4 et 6 forment une paire.** La 4 ajoute le nouveau contrat à côté de l'ancien pour que tout reste vert ; la 6 retire l'ancien. Entre les deux, `CellModel` porte les deux contrats, et c'est normal. Ne pas fusionner ces deux tâches : la 5, entre elles, est le moment où la vue bascule, et c'est elle qui a besoin d'une vérification à l'écran isolée.

**Aucune tâche ne touche la carte, les notifications de seuil, le placement ni les hooks.** Si une modification semble l'exiger, c'est le signe d'une erreur d'analyse : s'arrêter et le signaler.
