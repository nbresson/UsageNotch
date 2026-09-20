# Logo du fournisseur comme voyant d'activité — Plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal :** remplacer les trois marques d'activité de la pilule — arc tournant, anneau pulsant, point — par un seul glyphe au centre de la pile d'anneaux : la marque du fournisseur, dont la couleur dit l'état et le mouvement le confirme.

**Architecture :** les marques d'activité libèrent la couronne Ø 52 qu'elles occupaient autour de la pile ; les anneaux passent donc de 44/32/20 à **56/44/32** et le trou central de 12 à **24 DIP**, où le glyphe tient à 20 DIP. Quatre états, quatre couleurs de thème. La pulsation « couleur ↔ noir et blanc » se fait par deux tracés superposés dont on anime l'opacité du dessus — pas d'effet WPF, qui coûterait cher sur une fenêtre en couches rendue en logiciel. La désaturation se calcule dans `Presentation`, donc sans WPF et testable.

**Tech Stack :** C# / .NET 10, WPF (`net10.0-windows`), CommunityToolkit.Mvvm, xUnit + FluentAssertions.

**Spec :** `docs/superpowers/specs/2026-09-20-usagenotch-activity-logo-design.md`

## Global Constraints

- Interface **en français** : tout libellé, texte de page et message visible par l'utilisateur.
- Compilation **sans aucun avertissement** (`TreatWarningsAsErrors` dans `Directory.Build.props`).
- La logique vit dans `UsageNotch.Core` et `UsageNotch.Presentation`, **sans référence à WPF**, et se développe en TDD. `UsageNotch.App` (WPF, interop Win32) se vérifie à l'exécution.
- Messages de commit **en anglais**, forme `type(scope): description`, fichier de message en UTF-8 **sans BOM**.
- Branche de travail : `feat/activity-logo`, fusionnée dans `main` à la fin.
- Cotes en DIP à l'échelle 100 %, multipliées par `Settings.Scale` à l'exécution.
- Ne **jamais** arrêter l'instance réelle de l'utilisateur (`publish\UsageNotch.App.exe`, port 48666). Les vérifications se font en `--demo` (port 48667), et l'application n'est lancée que par le contrôleur.
- Ne pas republier dans `publish\` pendant le chantier.

### Cycle de vérification, identique à chaque tâche

```powershell
dotnet build UsageNotch.sln
dotnet test
```

Référence de départ : **517 tests verts** (251 Core, 266 Presentation), zéro avertissement.

### L'arithmétique des bandes, à ne pas redécouvrir

Un `ProgressRing` de côté `D` et d'épaisseur `T` pose son rayon à `(D − T) / 2` et centre un stylo de largeur `T` dessus : sa bande va donc de **`(D − 2T) / 2` à `D / 2`**. Un `Path` ou une `Ellipse` ordinaire, eux, remplissent leur géométrie nominale et tracent à ±`T/2` autour. Le chantier précédent s'est fait piéger par cette différence ; le journal de bord la consigne.

---

## Structure des fichiers

**Créés :**

| Fichier | Responsabilité |
|---|---|
| `src/UsageNotch.App/Controls/BrandGeometry.cs` | La marque, transcrite une fois depuis le SVG, gelée et partagée |
| `tests/UsageNotch.Presentation.Tests/Formatting/HexColorDesaturateTests.cs` | La désaturation |

**Modifiés :**

| Fichier | Nature du changement |
|---|---|
| `src/UsageNotch.Core/Settings/Theme.cs` | `LogoDone`, ses deux valeurs de préréglage et son repli |
| `src/UsageNotch.Presentation/Formatting/HexColor.cs` | `Desaturate` |
| `src/UsageNotch.Presentation/Pill/CellModel.cs` | `ActivityMutedColor` |
| `src/UsageNotch.Presentation/Pill/PillPresenter.cs` | « terminé » prend `LogoDone` ; la couleur désaturée |
| `src/UsageNotch.Presentation/Card/CardPresenter.cs` | Sa propre correspondance d'états (option B de la spec) |
| `src/UsageNotch.Presentation/Pill/PillMetrics.cs` | 56/44/32, `LogoSize`, retrait de `ActivitySize` et `RingHostSize` |
| `src/UsageNotch.App/Views/PillWindow.xaml` | Anneaux agrandis, marques remplacées par le glyphe, storyboards reciblés |
| `src/UsageNotch.App/Controls/PillPreview.cs` | Même pile, même glyphe |
| `src/UsageNotch.Presentation/Preferences/AppearancePageViewModel.cs` | Un `ColorSlot` de plus |
| `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md` | Journal de bord |
| `docs/REPRISE.md` | Carte de la documentation et backlog |

**Non modifiés, et c'est délibéré :** `src/UsageNotch.App/Tray/TrayIconService.cs` — il ne dessine son point d'activité que sous `ActivityKind.Attention`, état dont la couleur ne change pas. `src/UsageNotch.App/Controls/ProgressRing.cs` — le contrôle reste intact.

---

## Task 0 : Branche et référence

- [ ] **Step 1 : Créer la branche**

```powershell
git switch -c feat/activity-logo
```

- [ ] **Step 2 : Vérifier la référence de départ**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : zéro avertissement, **517 tests verts** (251 Core, 266 Presentation).

Si ce nombre diffère, s'arrêter et le signaler avant toute modification.

---

## Task 1 : La couleur de marque dans le thème

**Files :**
- Modify : `src/UsageNotch.Core/Settings/Theme.cs`
- Test : `tests/UsageNotch.Core.Tests/Settings/ThemeTests.cs`

**Interfaces :**
- Consomme : rien.
- Produit : `Theme.LogoDone`, une `string` `#RRGGBB`, `#D97757` chez Codenotch et `#B0B0B0` chez Monochrome, repliée par `Clamp()`.

`ForPreset` n'est **pas** touchée : l'accent système ne teinte que `LevelAmple`, `Running` et `RingSession`.

- [ ] **Step 1 : Écrire les tests qui échouent**

Ajouter à `tests/UsageNotch.Core.Tests/Settings/ThemeTests.cs` :

```csharp
    [Fact]
    public void The_codenotch_preset_carries_the_provider_brand_colour()
    {
        Theme.Codenotch.LogoDone.Should().Be("#D97757");
    }

    [Fact]
    public void The_monochrome_preset_keeps_the_finished_logo_grey()
    {
        Theme.Monochrome.LogoDone.Should().Be("#B0B0B0");
    }

    [Fact]
    public void Every_preset_keeps_its_four_activity_colours_distinct()
    {
        foreach (var t in new[] { Theme.Codenotch, Theme.Monochrome })
        {
            new[] { t.RingTrack, t.Running, t.Attention, t.LogoDone }.Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void A_theme_missing_its_brand_colour_falls_back_to_codenotch()
    {
        var partial = Theme.Monochrome with { LogoDone = "" };

        partial.Clamp().LogoDone.Should().Be(Theme.Codenotch.LogoDone);
    }
```

Le troisième test est le plus important : il garde la lisibilité des quatre états, puisque c'est la couleur qui les distingue.

- [ ] **Step 2 : Lancer les tests pour vérifier qu'ils échouent**

Run : `dotnet test tests/UsageNotch.Core.Tests --filter "FullyQualifiedName~ThemeTests"`
Expected : échec de compilation, `'Theme' does not contain a definition for 'LogoDone'`.

- [ ] **Step 3 : Ajouter la couleur**

Dans `src/UsageNotch.Core/Settings/Theme.cs`, ajouter le paramètre **après** `Done` :

```csharp
    string Done,
    string LogoDone,
    string Text,
```

- [ ] **Step 4 : Renseigner les deux préréglages**

Dans `Codenotch`, la ligne des couleurs d'activité devient :

```csharp
        Running: "#28E07B", Attention: "#FFBF00", Done: "#57C7FF", LogoDone: "#D97757",
```

Dans `Monochrome` :

```csharp
        Running: "#E0E0E0", Attention: "#FFFFFF", Done: "#B0B0B0", LogoDone: "#B0B0B0",
```

`Monochrome` répète volontairement `#B0B0B0` : une teinte de marque jurerait dans un préréglage monochrome, et cette valeur reste distincte de `Running` et d'`Attention`.

- [ ] **Step 5 : Ajouter le repli dans `Clamp()`**

Après la ligne `Done = Or(Done, d.Done),` :

```csharp
            LogoDone = Or(LogoDone, d.LogoDone),
```

- [ ] **Step 6 : Lancer les tests**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : les 4 nouveaux tests passent, **521 tests verts**, zéro avertissement.

Le test `Lists_presets_contents_and_the_thirteen_theme_colours_in_order` de `AppearancePageViewModelTests` porte sur la page Apparence, pas sur `Theme` : il reste vert ici, et c'est la Task 6 qui l'étend.

- [ ] **Step 7 : Commit**

```powershell
git add src/UsageNotch.Core/Settings/Theme.cs tests/UsageNotch.Core.Tests/Settings/ThemeTests.cs
git commit -m "feat(core): a brand colour for the finished-session logo"
```

---

## Task 2 : Désaturer une couleur

**Files :**
- Modify : `src/UsageNotch.Presentation/Formatting/HexColor.cs`
- Test : `tests/UsageNotch.Presentation.Tests/Formatting/HexColorDesaturateTests.cs` (créer)

**Interfaces :**
- Consomme : rien.
- Produit : `HexColor.Desaturate(string hex)` → `string`, le gris de même luminance perçue, au format `#RRGGBB` majuscules. Lève `ArgumentException` sur une couleur illisible, comme `ToColorRef`.

- [ ] **Step 1 : Écrire le test qui échoue**

Créer `tests/UsageNotch.Presentation.Tests/Formatting/HexColorDesaturateTests.cs` :

```csharp
using FluentAssertions;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Tests.Formatting;

public class HexColorDesaturateTests
{
    [Theory]
    // Luminance Rec. 709 : 0,2126 R + 0,7152 V + 0,0722 B, arrondie.
    [InlineData("#FFBF00", "#BFBFBF")]   // ambre « en attente »
    [InlineData("#28E07B", "#B2B2B2")]   // vert « en cours »
    [InlineData("#D97757", "#8A8A8A")]   // terracotta « terminé »
    [InlineData("#000000", "#000000")]
    [InlineData("#FFFFFF", "#FFFFFF")]
    public void A_colour_becomes_the_grey_of_the_same_perceived_lightness(string hex, string grey) =>
        HexColor.Desaturate(hex).Should().Be(grey);

    [Fact]
    public void A_grey_desaturates_to_itself()
    {
        HexColor.Desaturate("#808080").Should().Be("#808080");
        HexColor.Desaturate("#3A3A3A").Should().Be("#3A3A3A");
    }

    [Fact]
    public void The_green_channel_weighs_most()
    {
        var fromGreen = HexColor.Desaturate("#00FF00");
        var fromBlue = HexColor.Desaturate("#0000FF");

        fromGreen.Should().Be("#B6B6B6");
        fromBlue.Should().Be("#121212");
    }

    [Fact]
    public void An_unreadable_colour_is_refused()
    {
        var act = () => HexColor.Desaturate("bleu");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_loose_form_is_accepted_like_everywhere_else()
    {
        HexColor.Desaturate(" fff ").Should().Be("#FFFFFF");
    }
}
```

`A_grey_desaturates_to_itself` est le test qui compte : les trois coefficients somment à 1, donc un gris doit rester exactement lui-même. S'il dérive, l'arrondi est faux.

- [ ] **Step 2 : Lancer le test pour vérifier qu'il échoue**

Run : `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~HexColorDesaturateTests"`
Expected : échec de compilation, `'HexColor' does not contain a definition for 'Desaturate'`.

- [ ] **Step 3 : Implémenter**

Dans `src/UsageNotch.Presentation/Formatting/HexColor.cs`, après `ToColorRef` :

```csharp
    /// <summary>
    /// Le gris de même luminance perçue (Rec. 709). Les trois coefficients somment à 1, donc un gris reste
    /// lui-même. Sert la phase « noir et blanc » de la pulsation du logo.
    /// </summary>
    /// <exception cref="ArgumentException">La couleur n'est pas lisible par <see cref="TryNormalize"/>.</exception>
    public static string Desaturate(string hex)
    {
        if (!TryNormalize(hex, out var normalized))
        {
            throw new ArgumentException($"Couleur invalide : {hex}", nameof(hex));
        }

        var r = int.Parse(normalized.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = int.Parse(normalized.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = int.Parse(normalized.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var y = (int)Math.Round(0.2126 * r + 0.7152 * g + 0.0722 * b, MidpointRounding.AwayFromZero);
        return $"#{y:X2}{y:X2}{y:X2}";
    }
```

Aucun `Math.Clamp` : les coefficients somment à 1 et chaque canal est borné à 255, donc `y` ne peut pas sortir de 0–255.

- [ ] **Step 4 : Lancer les tests**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : les 9 nouveaux tests passent (5 cas de théorie + 4 faits), **530 tests verts**.

- [ ] **Step 5 : Commit**

```powershell
git add src/UsageNotch.Presentation/Formatting/HexColor.cs tests/UsageNotch.Presentation.Tests/Formatting
git commit -m "feat(presentation): desaturate a colour to its perceived grey"
```

---

## Task 3 : Les quatre états dans le modèle

**Files :**
- Modify : `src/UsageNotch.Presentation/Pill/CellModel.cs`, `src/UsageNotch.Presentation/Pill/PillPresenter.cs`, `src/UsageNotch.Presentation/Card/CardPresenter.cs`
- Test : `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs`, `tests/UsageNotch.Presentation.Tests/Card/CardPresenterTests.cs`

**Interfaces :**
- Consomme : `Theme.LogoDone` (tâche 1), `HexColor.Desaturate` (tâche 2).
- Produit : `CellModel.ActivityMutedColor`, et `PillPresenter.ActivityColor` rendant `theme.LogoDone` pour `ActivityKind.Done`.

**La séparation que cette tâche installe, et pourquoi.** `CardPresenter.SessionRowOf` appelle aujourd'hui `PillPresenter.ActivityColor`. Si on le laisse faire, la carte passe au terracotta avec la pilule et le réglage « Session terminée » ne colore plus rien. La spec retient l'option B : la carte prend sa propre correspondance, quatre lignes dupliquées, et garde `Theme.Done`. C'est délibéré — ne pas « factoriser » les deux fonctions.

- [ ] **Step 1 : Écrire les tests qui échouent**

Dans `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs`, remplacer le test `Activity_colours_come_from_the_theme` par :

```csharp
    [Fact]
    public void Activity_colours_come_from_the_theme()
    {
        PillPresenter.ActivityColor(ActivityKind.Running, Theme).Should().Be(Theme.Running);
        PillPresenter.ActivityColor(ActivityKind.Attention, Theme).Should().Be(Theme.Attention);
        PillPresenter.ActivityColor(ActivityKind.Done, Theme).Should().Be(Theme.LogoDone);
        PillPresenter.ActivityColor(ActivityKind.None, Theme).Should().Be(Theme.RingTrack);
    }

    [Fact]
    public void The_finished_state_wears_the_brand_colour_not_the_card_colour()
    {
        PillPresenter.ActivityColor(ActivityKind.Done, Theme).Should().NotBe(Theme.Done);
    }

    [Theory]
    [InlineData(SessionState.Idle)]
    [InlineData(SessionState.Running)]
    [InlineData(SessionState.Attention)]
    [InlineData(SessionState.Done)]
    public void The_muted_colour_is_the_desaturated_activity_colour(SessionState state)
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.2), state);

        c.ActivityMutedColor.Should().Be(HexColor.Desaturate(c.ActivityColor));
    }
```

Ajouter `using UsageNotch.Presentation.Formatting;` s'il manque.

**Un test existant est déjà le garde-fou.** `tests/UsageNotch.Presentation.Tests/Card/CardPresenterTests.cs` contient, autour de la ligne 104 :

```csharp
            new SessionRow("d", "api · 1234", "Terminé en 42 s", ActivityKind.Done, Theme.Done));
```

Il passera au **rouge** dès l'étape 4, quand `ActivityColor` rendra `LogoDone` — parce que la carte appelle encore cette fonction. C'est le signal attendu, et c'est l'étape 5 qui le remet au vert en détachant la carte. Ne pas « réparer » ce test en changeant `Theme.Done` en `Theme.LogoDone` : ce serait faire exactement ce que l'option B refuse.

Ajouter à ce même fichier un test qui épingle la divergence des deux côtés à la fois, en réutilisant les fabriques `Build(...)` et `S(...)` déjà présentes :

```csharp
    [Fact]
    public void A_finished_row_keeps_the_card_colour_while_the_pill_wears_the_brand_one()
    {
        var card = Build(Ok(), S("d", SessionState.Done, total: TimeSpan.FromMinutes(1)));

        card.Sessions[0].DotColor.Should().Be(Theme.Done);
        card.Sessions[0].DotColor.Should().NotBe(Theme.LogoDone);
        PillPresenter.ActivityColor(ActivityKind.Done, Theme).Should().Be(Theme.LogoDone);
    }
```

Les trois assertions ensemble disent la règle entière : la carte garde sa couleur, la pilule prend l'autre, et les deux sont bien distinctes. Un futur lecteur tenté de refactoriser les deux fonctions en une seule verra ce test tomber.

- [ ] **Step 2 : Lancer les tests pour vérifier qu'ils échouent**

Run : `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~Presenter"`
Expected : échec de compilation sur `ActivityMutedColor`, puis échec d'assertion sur la couleur « terminé ».

- [ ] **Step 3 : Ajouter le champ au modèle**

Dans `src/UsageNotch.Presentation/Pill/CellModel.cs`, après `ActivityColor` :

```csharp
    string ActivityColor,
    string ActivityMutedColor,
    string BandColor);
```

Et compléter le résumé du record d'une phrase : `<see cref="ActivityMutedColor"/>` est la version grise que croise la pulsation de l'état « en attente ».

- [ ] **Step 4 : Faire porter « terminé » par la couleur de marque**

Dans `src/UsageNotch.Presentation/Pill/PillPresenter.cs` :

```csharp
    public static string ActivityColor(ActivityKind kind, Theme theme) => kind switch
    {
        ActivityKind.Attention => theme.Attention,
        ActivityKind.Running => theme.Running,
        ActivityKind.Done => theme.LogoDone,
        _ => theme.RingTrack,
    };
```

Dans `Cell`, calculer la couleur une fois et en dériver la grise :

```csharp
        var activityColor = ActivityColor(activity, theme);
```

puis, dans la construction du `CellModel` :

```csharp
            ActivityColor: activityColor,
            ActivityMutedColor: HexColor.Desaturate(activityColor),
```

- [ ] **Step 5 : Détacher la carte**

Dans `src/UsageNotch.Presentation/Card/CardPresenter.cs`, `SessionRowOf` cesse d'appeler `PillPresenter.ActivityColor` et emploie sa propre correspondance :

```csharp
    /// <summary>
    /// La carte garde ses propres couleurs d'état. La pilule fait porter « terminé » par la couleur de marque
    /// du logo ; la carte reste sur <see cref="Theme.Done"/>, que son réglage « Session terminée » désigne.
    /// Duplication assumée : deux langages visuels que rien n'oblige à rester liés.
    /// </summary>
    private static string RowColor(ActivityKind kind, Theme theme) => kind switch
    {
        ActivityKind.Attention => theme.Attention,
        ActivityKind.Running => theme.Running,
        ActivityKind.Done => theme.Done,
        _ => theme.RingTrack,
    };
```

`SessionRowOf` appelle `RowColor(activity, theme)` à la place de `PillPresenter.ActivityColor(activity, theme)`. `PillPresenter.ActivityOf` continue d'être partagée : c'est une traduction d'état, pas une couleur.

- [ ] **Step 6 : Lancer les tests**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : **536 tests verts** (+6 : 2 faits pilule, 4 cas de théorie, 1 fait carte, moins le test remplacé qui existait déjà). Si le compte diffère de ±1 à cause d'un test existant remanié, l'expliquer dans le rapport plutôt que d'ajuster le chiffre en silence.

L'App cesse peut-être de compiler si un fichier XAML lie un champ disparu : rien n'a disparu ici, seul un champ a été ajouté. Si le compilateur proteste, le signaler.

- [ ] **Step 7 : Commit**

```powershell
git add src/UsageNotch.Presentation tests/UsageNotch.Presentation.Tests
git commit -m "feat(presentation): the finished state wears the brand colour"
```

---

## Task 4 : La marque, transcrite une fois

**Files :**
- Create : `src/UsageNotch.App/Controls/BrandGeometry.cs`

**Interfaces :**
- Consomme : `src/UsageNotch.App/Assets/anthropic.svg`.
- Produit : `UsageNotch.App.Controls.BrandGeometry.Mark`, une `System.Windows.Media.Geometry` gelée.

Cette tâche n'a pas de test : elle vit dans `UsageNotch.App`, qui référence WPF et se vérifie à l'exécution. Elle se prouve à la tâche 5, à l'écran.

- [ ] **Step 1 : Créer la classe**

Créer `src/UsageNotch.App/Controls/BrandGeometry.cs` :

```csharp
using System.Windows.Media;

namespace UsageNotch.App.Controls;

/// <summary>
/// La marque du fournisseur, transcrite une fois depuis <c>Assets/anthropic.svg</c> : un seul sous-tracé et
/// aucune contre-forme, donc aucune règle de remplissage à déclarer. Gelée et partagée par la pilule et par
/// l'aperçu des réglages. Le SVG reste la source de référence ; analyser du XML au démarrage pour une forme
/// qui ne change jamais n'aurait pas de sens.
/// </summary>
public static class BrandGeometry
{
    /// <summary>Bornes d'origine : 235,6 × 235,6 depuis (6,2 ; 6,2). Les consommateurs emploient <c>Stretch="Uniform"</c>.</summary>
    public static Geometry Mark { get; } = Frozen("<données de tracé>");

    private static Geometry Frozen(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }
}
```

**Les données de tracé se copient verbatim** depuis l'attribut `d` de l'unique `<path>` de `src/UsageNotch.App/Assets/anthropic.svg`. Ne rien retranscrire à la main, ne rien arrondir, ne rien reformater : ouvrir le fichier, copier la valeur de `d` entre guillemets, la coller telle quelle. Elle fait environ 2,3 ko et commence par `M52.4285 162.873L`.

- [ ] **Step 2 : Vérifier que le tracé se charge**

Run : `dotnet build UsageNotch.sln`
Expected : compilation propre. `Geometry.Parse` échouerait à l'exécution, pas à la compilation — c'est la tâche 5 qui le prouve à l'écran. Si la chaîne est mal collée (guillemet avalé, retour à la ligne inséré), le compilateur le dira.

- [ ] **Step 3 : Commit**

```powershell
git add src/UsageNotch.App/Controls/BrandGeometry.cs
git commit -m "feat(app): embed the provider mark as a frozen geometry"
```

---

## Task 5 : La pile agrandie et le glyphe

**Files :**
- Modify : `src/UsageNotch.Presentation/Pill/PillMetrics.cs`, `src/UsageNotch.App/Views/PillWindow.xaml`, `src/UsageNotch.App/Controls/PillPreview.cs`
- Test : `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs`

**Interfaces :**
- Consomme : `CellModel.ActivityColor` / `ActivityMutedColor` (tâche 3), `BrandGeometry.Mark` (tâche 4).
- Produit : `PillMetrics.RingOuter` = 56, `RingMiddle` = 44, `RingInner` = 32, `RingBandThickness` = 4, `LogoSize` = 20. `ActivitySize` et `RingHostSize` disparaissent — l'hôte de la pile vaut désormais `RingOuter`.

**Les cotes, et pourquoi elles tombent juste.** Bandes : 56/4 → 24 à 28 ; 44/4 → 18 à 22 ; 32/4 → 12 à 16. Écart de 2 entre voisins, **trou central de 24**. Le glyphe occupe 20, soit 2 DIP de dégagement de chaque côté. L'anneau extérieur atteint le rayon 28 dans un corps de 64 d'épaisseur : 4 DIP de marge. Pile plus pourcentage : 56 + 4 + 18 = 78 sur les 104 de `BodyLength` — **la pilule ne s'allonge dans aucun des deux modes**.

- [ ] **Step 1 : Écrire les tests qui échouent**

Dans `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs`, remplacer les trois tests de cotes existants par :

```csharp
    [Fact]
    public void The_ring_stack_leaves_two_dip_between_neighbours_and_a_twenty_four_dip_core()
    {
        // Un ProgressRing pose son rayon à (D − T) / 2 et centre un stylo de largeur T dessus.
        static (double Inner, double Outer) Band(double diameter) =>
            ((diameter - 2 * PillMetrics.RingBandThickness) / 2, diameter / 2);

        var outer = Band(PillMetrics.RingOuter);
        var middle = Band(PillMetrics.RingMiddle);
        var inner = Band(PillMetrics.RingInner);

        (outer.Inner - middle.Outer).Should().Be(2);
        (middle.Inner - inner.Outer).Should().Be(2);
        (inner.Inner * 2).Should().Be(24);
    }

    [Fact]
    public void The_logo_fits_the_core_with_room_to_spare()
    {
        var core = PillMetrics.RingInner - 2 * PillMetrics.RingBandThickness;

        PillMetrics.LogoSize.Should().BeLessThan(core);
        (core - PillMetrics.LogoSize).Should().Be(4);   // 2 DIP de chaque côté
    }

    [Fact]
    public void The_stack_and_the_percent_fit_the_body_without_lengthening_the_pill()
    {
        const double percentTextHeight = 18;
        const double gap = 4;

        (PillMetrics.RingOuter + gap + percentTextHeight).Should().BeLessThan(PillMetrics.BodyLength);
        PillMetrics.RingOuter.Should().BeLessThan(PillMetrics.Thickness);
    }
```

Le test des voyants d'activité (`The_activity_marks_clear_the_outer_ring_and_stay_inside_the_host`) **disparaît** : plus aucune marque ne vit autour de la pile. Le supprimer, ne pas le vider.

- [ ] **Step 2 : Lancer les tests pour vérifier qu'ils échouent**

Run : `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~PillPresenterTests"`
Expected : échec de compilation, `'PillMetrics' does not contain a definition for 'LogoSize'`.

- [ ] **Step 3 : Poser les cotes**

Dans `src/UsageNotch.Presentation/Pill/PillMetrics.cs`, remplacer le bloc des cotes d'anneaux par :

```csharp
    /// <summary>Diamètres de la pile, de l'extérieur vers l'intérieur. L'hôte de la pile vaut <see cref="RingOuter"/>.</summary>
    public const double RingOuter = 56;
    public const double RingMiddle = 44;
    public const double RingInner = 32;
    public const double RingBandThickness = 4;
    /// <summary>Côté du glyphe de marque, au centre de la pile, dans un trou de 24 DIP.</summary>
    public const double LogoSize = 20;
```

`RingHostSize` et `ActivitySize` disparaissent.

- [ ] **Step 4 : Lancer les tests de cotes**

Run : `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~PillPresenterTests"`
Expected : PASS. `dotnet build` échouera encore, `PillPreview` référençant les constantes disparues — c'est l'objet des deux étapes suivantes.

- [ ] **Step 5 : Redessiner la pilule**

Dans `src/UsageNotch.App/Views/PillWindow.xaml`, ajouter au besoin l'espace de noms `controls` s'il n'y est pas déjà (il y est : `xmlns:controls="clr-namespace:UsageNotch.App.Controls"`), puis remplacer tout le bloc `<Grid x:Name="RingHost" …>` par :

```xml
          <Grid x:Name="RingHost" Width="56" Height="56" Margin="0,0,0,4">
            <controls:ProgressRing Width="56" Height="56" RingThickness="4"
                                   TargetFraction="{Binding Cell.Rings[0].Fraction}"
                                   RingBrush="{Binding Cell.Rings[0].Color, Converter={StaticResource Hex}}"
                                   TrackBrush="{Binding Cell.Rings[0].TrackColor, Converter={StaticResource Hex}}" />
            <controls:ProgressRing Width="44" Height="44" RingThickness="4"
                                   TargetFraction="{Binding Cell.Rings[1].Fraction}"
                                   RingBrush="{Binding Cell.Rings[1].Color, Converter={StaticResource Hex}}"
                                   TrackBrush="{Binding Cell.Rings[1].TrackColor, Converter={StaticResource Hex}}" />
            <controls:ProgressRing Width="32" Height="32" RingThickness="4"
                                   TargetFraction="{Binding Cell.Rings[2].Fraction}"
                                   RingBrush="{Binding Cell.Rings[2].Color, Converter={StaticResource Hex}}"
                                   TrackBrush="{Binding Cell.Rings[2].TrackColor, Converter={StaticResource Hex}}" />
            <Grid x:Name="LogoHost" Width="20" Height="20" RenderTransformOrigin="0.5,0.5">
              <Grid.RenderTransform>
                <RotateTransform x:Name="SpinRotate" />
              </Grid.RenderTransform>
              <Path Data="{x:Static controls:BrandGeometry.Mark}" Stretch="Uniform"
                    Fill="{Binding Cell.ActivityMutedColor, Converter={StaticResource Hex}}" />
              <Path x:Name="LogoTint" Data="{x:Static controls:BrandGeometry.Mark}" Stretch="Uniform"
                    Fill="{Binding Cell.ActivityColor, Converter={StaticResource Hex}}" />
            </Grid>
          </Grid>
```

Le glyphe gris est dessous, le coloré dessus : la pulsation efface le dessus pour révéler le gris.

Dans les ressources de la même fenêtre, les deux storyboards changent de cible — `SpinRotate` existe toujours mais porté par `LogoHost`, et `Pulse` ne vise plus `AttentionRing` :

```xml
    <Storyboard x:Key="Spin" RepeatBehavior="Forever" Timeline.DesiredFrameRate="30">
      <DoubleAnimation Storyboard.TargetName="SpinRotate" Storyboard.TargetProperty="Angle" From="0" To="360" Duration="0:0:1.2" />
    </Storyboard>
    <Storyboard x:Key="Pulse" RepeatBehavior="Forever" AutoReverse="True" Timeline.DesiredFrameRate="20">
      <DoubleAnimation Storyboard.TargetName="LogoTint" Storyboard.TargetProperty="Opacity" From="1" To="0" Duration="0:0:1" />
    </Storyboard>
```

`BandPulse` ne change pas. `PillWindow.xaml.cs` ne change pas non plus : `UpdateAnimations` gèle déjà `Spin` sur `Running` et `Pulse` sur `Attention`, et la pile est toujours dessinée. Le convertisseur `ActivityVisibilityConverter` n'est plus utilisé par ce bloc ; le laisser en place, il reste déclaré dans les ressources et pourrait servir ailleurs.

- [ ] **Step 6 : Redessiner l'aperçu des réglages**

Dans `src/UsageNotch.App/Controls/PillPreview.cs`, remplacer `BuildRing` par :

```csharp
    private static Grid BuildRing(CellModel cell, bool vertical)
    {
        var host = new Grid
        {
            Width = PillMetrics.RingOuter,
            Height = PillMetrics.RingOuter,
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

        // L'aperçu est statique : la pulsation ne tourne pas, on montre donc le glyphe dans sa couleur d'état.
        host.Children.Add(new Path
        {
            Width = PillMetrics.LogoSize,
            Height = PillMetrics.LogoSize,
            Stretch = Stretch.Uniform,
            Data = BrandGeometry.Mark,
            Fill = HexBrushConverter.ToBrush(cell.ActivityColor),
        });

        return host;
    }
```

Le `switch` sur `cell.Activity` et le champ statique `RunningArc` disparaissent de ce fichier. Vérifier qu'aucun `using` ne devient inutilisé — `TreatWarningsAsErrors` transformerait l'avertissement en erreur.

- [ ] **Step 7 : Compiler et lancer les tests**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : **535 tests verts** — les trois tests de cotes remplacés, un test de voyants supprimé. Zéro avertissement.

- [ ] **Step 8 : Vérification à l'écran**

**Cette étape appartient au contrôleur, pas à l'implémenteur.** L'utilisateur fait tourner une instance réelle et le cycle de vie de l'application doit rester centralisé. L'implémenteur passe directement au commit et le signale dans son rapport.

- [ ] **Step 9 : Commit**

```powershell
git add src/UsageNotch.Presentation/Pill/PillMetrics.cs src/UsageNotch.App/Views/PillWindow.xaml src/UsageNotch.App/Controls/PillPreview.cs tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs
git commit -m "feat(app): the provider mark replaces the three activity marks"
```

---

## Task 6 : Le réglage

**Files :**
- Modify : `src/UsageNotch.Presentation/Preferences/AppearancePageViewModel.cs`
- Test : `tests/UsageNotch.Presentation.Tests/Preferences/AppearancePageViewModelTests.cs`

**Interfaces :**
- Consomme : `Theme.LogoDone` (tâche 1).
- Produit : un `ColorSlot` de clé `LogoDone`, libellé « Logo, session terminée ».

La clé sert d'identifiant d'automatisation via les liaisons `StringFormat='Color_{0}'` et `'Pick_{0}'` de la page : elle doit être exactement le nom de la propriété de `Theme`. Aucun changement au XAML de la page — l'`ItemsControl` liste `Colors`, il prendra la nouvelle entrée tout seul.

- [ ] **Step 1 : Écrire les tests qui échouent**

Dans `tests/UsageNotch.Presentation.Tests/Preferences/AppearancePageViewModelTests.cs`, renommer `Lists_presets_contents_and_the_thirteen_theme_colours_in_order` en `..._the_fourteen_theme_colours_in_order` et étendre ses deux listes, la nouvelle entrée venant juste après `Done` :

```csharp
        vm.Colors.Select(c => c.Key).Should().Equal(
            "PillBackground", "PillBorder", "RingTrack", "LevelAmple", "LevelWatch", "LevelCritical",
            "RingSession", "RingWeeklyAll", "RingWeeklyScoped", "Running", "Attention", "Done", "LogoDone", "Text");
        vm.Colors.Select(c => c.Label).Should().Equal(
            "Fond de la pilule", "Contour de la pilule", "Piste de l'anneau", "Niveau modéré", "Niveau vigilance",
            "Niveau critique", "Anneau session", "Anneau hebdomadaire", "Anneau hebdo. par modèle",
            "Session en cours", "Session en attente", "Session terminée", "Logo, session terminée", "Texte");
```

Puis ajouter, en réutilisant les fabriques `Create()` et `Slot(vm, key)` déjà présentes dans le fichier :

```csharp
    [Fact]
    public void Editing_the_brand_colour_switches_to_the_custom_theme()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        Slot(vm, "LogoDone").Hex = "#123456";

        vm.Preset.Should().Be(ThemePreset.Custom);
        vm.Theme.LogoDone.Should().Be("#123456");
    }
```

- [ ] **Step 2 : Lancer les tests pour vérifier qu'ils échouent**

Run : `dotnet test tests/UsageNotch.Presentation.Tests --filter "FullyQualifiedName~AppearancePageViewModelTests"`
Expected : échec d'assertion sur la liste des couleurs, puis `Sequence contains no matching element` sur la clé `LogoDone`.

- [ ] **Step 3 : Ajouter le sélecteur**

Dans `src/UsageNotch.Presentation/Preferences/AppearancePageViewModel.cs`, dans la liste `Colors`, après l'entrée `Done` :

```csharp
            Slot("LogoDone", "Logo, session terminée", t => t.LogoDone, (t, h) => t with { LogoDone = h }),
```

Mettre à jour le résumé de classe pour qu'il reste vrai.

- [ ] **Step 4 : Lancer les tests**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : **536 tests verts**, zéro avertissement.

- [ ] **Step 5 : Commit**

```powershell
git add src/UsageNotch.Presentation/Preferences/AppearancePageViewModel.cs tests/UsageNotch.Presentation.Tests/Preferences/AppearancePageViewModelTests.cs
git commit -m "feat(settings): the brand colour joins the appearance page"
```

---

## Task 7 : Vérification et documentation

**Files :**
- Modify : `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md`, `docs/REPRISE.md`

- [ ] **Step 1 : Matrice de vérification** *(contrôleur)*

En `--demo`, contrôler à l'écran :

| À faire varier | Valeurs | Ce qu'on regarde |
|---|---|---|
| État | repos, en cours, en attente, terminé | Quatre couleurs distinctes ; rotation ; pulsation vers le gris ; fixe |
| Échelle | 40 %, 100 %, 150 % | Le glyphe reste centré et ne mord sur aucun anneau ; à 40 % la couleur reste lisible même si la marque ne l'est plus |
| Bord | Droite, Gauche, Haut, Bas | Pile centrée, pilule de taille inchangée |
| Contenu | Anneaux et pourcentage, Anneaux seuls | La pilule ne s'allonge pas |
| Visibilité | Déplié, Replié | Les animations s'arrêtent quand la pilule est repliée |
| Carte | session terminée | **La carte affiche le bleu « Session terminée » pendant que la pilule affiche le terracotta** — c'est la vérification qui prouve l'option B |

Les événements de session s'envoient à la démo ainsi :

```powershell
Invoke-WebRequest -Method Post -Uri "http://127.0.0.1:48667/event?e=done&ppid=1" -UseBasicParsing `
  -Body '{"session_id":"test-0001","cwd":"C:\\src\\projet"}'
```

- [ ] **Step 2 : Journal de bord**

Ajouter une section à `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md`, dans la forme des chantiers précédents. Y consigner :

- **la séparation délibérée entre `PillPresenter.ActivityColor` et `CardPresenter.RowColor`** : quatre lignes dupliquées, pour que la pilule porte la couleur de marque sur « terminé » pendant que la carte garde `Theme.Done`, que son réglage désigne. C'est le point qu'un futur lecteur « factoriserait » sans le journal ;
- les nouvelles cotes et le rappel de l'arithmétique des bandes, `(D − 2T)/2` à `D/2` pour un `ProgressRing`, ±`T/2` pour un `Path` ou une `Ellipse` ;
- pourquoi la pulsation superpose deux tracés au lieu d'employer un effet WPF ;
- que `TrayIconService` n'a pas été touché parce qu'il ne dessine son point que sous « en attente » ;
- les deux dégradations assumées, mesurées avant conception : la marque devient illisible à l'échelle 40 % — seule sa couleur subsiste, ce qui suffit puisque c'est elle qui porte l'état — et sa symétrie radiale rend la rotation peu contrastée face au balayage d'un arc sur un cercle vide.

- [ ] **Step 3 : Backlog**

Dans `docs/REPRISE.md` : ajouter la ligne du Plan 5 et de la spec à la carte de la documentation, et **retirer de « À vérifier à l'usage » la ligne sur la lisibilité de l'anneau intérieur** — il passe de Ø 20 à Ø 32, le sujet est clos. Relire le tableau entier et corriger toute ligne devenue fausse.

- [ ] **Step 4 : Vérification finale**

Run : `dotnet build UsageNotch.sln; if ($?) { dotnet test }`
Expected : **536 tests verts**, zéro avertissement.

- [ ] **Step 5 : Commit**

```powershell
git add docs
git commit -m "docs: record the activity logo work"
```

---

## Notes d'exécution

**Les comptes de tests sont un outil de détection, pas un critère d'acceptation.** S'ils diffèrent, comprendre pourquoi avant de continuer.

**L'ordre 4 → 5 est contraint.** `BrandGeometry` doit exister avant que le XAML la référence. Les tâches 1, 2 et 3 sont indépendantes de 4 et peuvent se faire dans n'importe quel ordre entre elles, mais 3 consomme 1 et 2.

**Aucune tâche ne touche `ProgressRing`, les seuils, le placement, les hooks, ni `TrayIconService`.** Si une modification semble l'exiger, c'est le signe d'une erreur d'analyse : s'arrêter et le signaler.
