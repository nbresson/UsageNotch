# Suivi de l'usage Google Antigravity — Plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal :** ajouter à UsageNotch la surveillance des quotas de **Google Antigravity** aux côtés de Claude, avec détection locale automatique du hub `agy.exe`, mapping sur les trois anneaux, choix du fournisseur dans les réglages et logo Gemini dynamique au centre de la pilule.

**Architecture :** 
- Couche `Core` : parser dédié (`AntigravityUsageParser`), découverte du processus local et de ses identifiants `--hub-port` / `--csrf_token` (`AntigravityProcessDiscovery`), et implémentation de `IUsageProvider` (`AntigravityUsageProvider`). Nouveau paramètre `Provider` dans `Settings`.
- Couche `Presentation` : option de sélection du fournisseur dans `AppearancePageViewModel`, libellés français dans `FrenchText`, titre et lignes de quotas adaptés dans `CardPresenter`, et transport du fournisseur actif dans `CellModel`.
- Couche `App` : marque Gemini vectorielle gelée dans `BrandGeometry`, liaison dynamique du glyphe central dans `PillWindow` et `PillPreview`, sélecteur dans `AppearancePage.xaml`, injection et bascule à chaud du fournisseur actif dans l'hôte `AppHost`.

**Tech Stack :** C# / .NET 10 (C# 14), WPF (`net10.0-windows`), ConnectRPC / HTTP local, xUnit + FluentAssertions.

**Spec :** [`docs/superpowers/specs/2026-10-05-usagenotch-antigravity-provider-design.md`](file:///C:/Users/nbres/source/repos/codenotchbis/docs/superpowers/specs/2026-10-05-usagenotch-antigravity-provider-design.md)

---

## Contraintes globales

- Interface **en français** : tout libellé, texte de carte, infobulle et message d'erreur.
- Compilation **sans aucun avertissement** (`TreatWarningsAsErrors` dans `Directory.Build.props`).
- La logique vit dans `UsageNotch.Core` et `UsageNotch.Presentation`, **sans référence à WPF**, et se développe en TDD strict. `UsageNotch.App` (WPF, interop Win32) se vérifie à l'exécution.
- Messages de commit **en anglais**, forme `type(scope): description`, fichier de message en UTF-8 **sans BOM**.
- Branche de travail : `feat/antigravity-provider`, fusionnée dans `main` à la fin.
- Ne **jamais** arrêter l'instance réelle de l'utilisateur (`publish\UsageNotch.App.exe`, port 48666, mutex `Local\UsageNotch`). Les vérifications se font en `--demo` (port 48667, mutex `Local\UsageNotch-demo`), ou sur les binaires de build debug.
- Ne pas republier dans `publish\` pendant le chantier.

### Cycle de vérification (identique à chaque tâche)

```powershell
dotnet build UsageNotch.sln
dotnet test
```

Référence de départ : **538 tests verts** (255 Core, 283 Presentation), zéro avertissement.

---

## Structure des fichiers

**Créés :**

| Fichier | Responsabilité |
|---|---|
| `src/UsageNotch.Core/Usage/AntigravityUsageParser.cs` | Analyse de la réponse ConnectRPC JSON de `RetrieveUserQuotaSummary` |
| `src/UsageNotch.Core/Usage/AntigravityProcessDiscovery.cs` | Découverte du processus local `agy.exe`, extraction de `--hub-port` et `--csrf_token`, cache mémoire |
| `src/UsageNotch.Core/Usage/AntigravityUsageProvider.cs` | Implémentation de `IUsageProvider` pour Antigravity |
| `tests/UsageNotch.Core.Tests/Usage/AntigravityUsageParserTests.cs` | Tests unitaires du parser |
| `tests/UsageNotch.Core.Tests/Usage/AntigravityProcessDiscoveryTests.cs` | Tests unitaires de la découverte de processus |
| `tests/UsageNotch.Core.Tests/Usage/AntigravityUsageProviderTests.cs` | Tests unitaires du provider avec simulation HTTP |
| `src/UsageNotch.App/Assets/gemini.svg` | SVG de référence pour le symbole Gemini |

**Modifiés :**

| Fichier | Nature du changement |
|---|---|
| `src/UsageNotch.Core/Usage/RingWindows.cs` | Matrice d'alias `RingWindows.Antigravity` |
| `src/UsageNotch.Core/Settings/Settings.cs` | Propriété `Provider`, repli et validation dans `Clamp()` |
| `tests/UsageNotch.Core.Tests/Settings/SettingsTests.cs` | Tests de sérialisation et bornage de `Provider` |
| `src/UsageNotch.Presentation/Formatting/FrenchText.cs` | Textes et statuts pour Antigravity |
| `src/UsageNotch.Presentation/Preferences/Choices.cs` | Liste des choix `ProviderChoices` |
| `src/UsageNotch.Presentation/Preferences/AppearancePageViewModel.cs` | Propriété `Provider` et notification de changement |
| `src/UsageNotch.Presentation/Pill/CellModel.cs` | Propriété `ProviderId` |
| `src/UsageNotch.Presentation/Pill/PillPresenter.cs` | Transmission du fournisseur et de sa couleur de marque |
| `src/UsageNotch.Presentation/Card/CardPresenter.cs` | Libellés adaptés à Antigravity |
| `tests/UsageNotch.Presentation.Tests/Preferences/AppearancePageViewModelTests.cs` | Tests du ViewModel de réglages |
| `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs` | Tests de présentation du modèle de cellule |
| `tests/UsageNotch.Presentation.Tests/Card/CardPresenterTests.cs` | Tests de présentation de la carte |
| `src/UsageNotch.App/Controls/BrandGeometry.cs` | Méthode `ForProvider(string providerId)` avec géométrie Gemini |
| `src/UsageNotch.App/Views/PillWindow.xaml` & `.cs` | Dessin du glyphe selon `CellModel.ProviderId` |
| `src/UsageNotch.App/Controls/PillPreview.cs` | Dessin de l'aperçu selon le fournisseur sélectionné |
| `src/UsageNotch.App/Views/Preferences/AppearancePage.xaml` | Sélecteur de fournisseur |
| `src/UsageNotch.App/Hosting/AppHost.cs` | Enregistrement du provider actif et bascule à chaud |
| `src/UsageNotch.App/Hosting/DemoMode.cs` | Données de simulation Antigravity pour `--demo` |
| `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md` | Journal de bord |
| `docs/REPRISE.md` | Mise à jour de la table et du backlog |

---

## Task 0 : Branche et référence

- [x] **Step 1 : Créer la branche de travail**

```powershell
git switch -c feat/antigravity-provider
```

- [x] **Step 2 : Vérifier la référence de départ**

Exécuter :
```powershell
dotnet build UsageNotch.sln
dotnet test
```
Attendu : 0 avertissement, **538 tests verts** (255 Core, 283 Presentation).

---

## Task 1 : Core — Parser de réponse Antigravity (`AntigravityUsageParser`)

**Fichiers :**
- Créer : `src/UsageNotch.Core/Usage/AntigravityUsageParser.cs`
- Créer : `tests/UsageNotch.Core.Tests/Usage/AntigravityUsageParserTests.cs`

**Contrat :**
- Entrée : payload JSON de `RetrieveUserQuotaSummary`.
- Sortie : `IReadOnlyList<LimitWindow>`.
- Formule d'usage : `usedPercent = Math.Clamp((1.0 - remainingFraction) * 100.0, 0.0, 100.0)`.
- Horodatages : `resetTime` converti en `DateTimeOffset`.

- [x] **Step 1 : Écrire les tests unitaires du parser**

Créer `tests/UsageNotch.Core.Tests/Usage/AntigravityUsageParserTests.cs` testant :
1. Décodage d'un payload complet avec les groupes `"Gemini Models"` et `"Claude and GPT models"`.
2. Calcul exact du pourcentage consommé (`remainingFraction = 0.3529` => `usedPercent = 64.71 %`).
3. Parsing de la date ISO 8601 UTC.
4. Gestion d'un payload vide ou mal formé (renvoie liste vide sans lever d'exception non gérée).
5. Gestion d'un bucket avec `remainingFraction` manquant ou hors bornes (clamp 0..100).

- [x] **Step 2 : Implémenter `AntigravityUsageParser`**

Créer `src/UsageNotch.Core/Usage/AntigravityUsageParser.cs` avec `System.Text.Json` haute performance.

- [x] **Step 3 : Valider les tests**

Exécuter `dotnet test --filter "AntigravityUsageParserTests"`.

- [x] **Step 4 : Commit**

```powershell
git add src/UsageNotch.Core/Usage/AntigravityUsageParser.cs tests/UsageNotch.Core.Tests/Usage/AntigravityUsageParserTests.cs
git commit -m "feat(core): parse Antigravity RetrieveUserQuotaSummary response"
```

---

## Task 2 : Core — Découverte du hub local (`AntigravityProcessDiscovery`)

**Fichiers :**
- Créer : `src/UsageNotch.Core/Usage/AntigravityProcessDiscovery.cs`
- Créer : `tests/UsageNotch.Core.Tests/Usage/AntigravityProcessDiscoveryTests.cs`

**Contrat :**
- Recherche les processus `agy` ou `agy.exe`.
- Extrait `--hub-port=(\d+)` et `--csrf_token=([a-f0-9]+)`.
- Conserve le dernier résultat valide en cache mémoire.
- Fournit une méthode `InvalidateCache()` pour forcer la ré-inspection en cas de 401 ou d'erreur réseau.
- Interface abstraite pour l'obtention des lignes de commande afin de permettre le test unitaire sans processus réel.

- [x] **Step 1 : Écrire les tests unitaires de découverte**

Créer `tests/UsageNotch.Core.Tests/Usage/AntigravityProcessDiscoveryTests.cs` :
1. Extraction correcte depuis une ligne de commande standard `"C:\...\agy.exe" --app_data_dir=antigravity --hub --hub-port=37588 --csrf_token=c47a09fb50f044359d9b8c7ba430adcd`.
2. Gestion de l'absence du processus (renvoie `null`).
3. Ligne de commande sans token ou sans port (renvoie `null`).
4. Utilisation du cache au second appel, et réévaluation après `InvalidateCache()`.

- [x] **Step 2 : Implémenter `AntigravityProcessDiscovery`**

Implémenter l'extracteur et le résolveur de ligne de commande dans `src/UsageNotch.Core/Usage/AntigravityProcessDiscovery.cs`.

- [x] **Step 3 : Valider les tests**

Exécuter `dotnet test --filter "AntigravityProcessDiscoveryTests"`.

- [x] **Step 4 : Commit**

```powershell
git add src/UsageNotch.Core/Usage/AntigravityProcessDiscovery.cs tests/UsageNotch.Core.Tests/Usage/AntigravityProcessDiscoveryTests.cs
git commit -m "feat(core): discover Antigravity hub port and csrf token from process"
```

---

## Task 3 : Core — Fournisseur `AntigravityUsageProvider` & `RingWindows`

**Fichiers :**
- Modifier : `src/UsageNotch.Core/Usage/RingWindows.cs`
- Créer : `src/UsageNotch.Core/Usage/AntigravityUsageProvider.cs`
- Créer : `tests/UsageNotch.Core.Tests/Usage/AntigravityUsageProviderTests.cs`

**Contrat :**
- `RingWindows.Antigravity` définit les 3 alias d'anneaux :
  - Extérieur : `["gemini-5h", "5h"]`
  - Milieu : `["gemini-weekly", "weekly"]`
  - Intérieur : `["3p-weekly", "3p-5h", "models_3p"]`
- `AntigravityUsageProvider` implémente `IUsageProvider` :
  - `Id => "antigravity"`
  - `DisplayName => "Google Antigravity"`
  - `RingWindowIds => RingWindows.Antigravity`
  - `FetchAsync(ct)` :
    - Si la découverte échoue => `FetchResult.NeedsAuth("Antigravity n'est pas lancé...")`
    - Si HTTP 200 => `FetchResult.Success(windows)`
    - Si HTTP 401 => invalide le cache et renvoie `FetchResult.Failed("Session Antigravity expirée")`
    - Si connexion refusée => invalide le cache et renvoie `FetchResult.Failed("Connexion impossible au hub Antigravity")`

- [x] **Step 1 : Écrire les tests unitaires du fournisseur**

Créer `tests/UsageNotch.Core.Tests/Usage/AntigravityUsageProviderTests.cs` avec un `HttpMessageHandler` mocké :
1. Réponse 200 valide => `FetchResult.Success` avec les 3 fenêtres.
2. Découverte retournant `null` => `FetchResult.NeedsAuth`.
3. Réponse 401 => `FetchResult.Failed` et invalidation du cache.
4. `HttpRequestException` => invalidation du cache.

- [x] **Step 2 : Implémenter `RingWindows.Antigravity` et `AntigravityUsageProvider`**

Ajouter `Antigravity` dans `RingWindows.cs` et créer `AntigravityUsageProvider.cs`.

- [x] **Step 3 : Valider les tests**

Exécuter `dotnet test --filter "AntigravityUsageProviderTests"`.

- [x] **Step 4 : Commit**

```powershell
git add src/UsageNotch.Core/Usage/RingWindows.cs src/UsageNotch.Core/Usage/AntigravityUsageProvider.cs tests/UsageNotch.Core.Tests/Usage/AntigravityUsageProviderTests.cs
git commit -m "feat(core): implement AntigravityUsageProvider and ring window definitions"
```

---

## Task 4 : Core — Paramètre `Provider` dans `Settings`

**Fichiers :**
- Modifier : `src/UsageNotch.Core/Settings/Settings.cs`
- Modifier : `tests/UsageNotch.Core.Tests/Settings/SettingsTests.cs`

**Contrat :**
- Nouvelle propriété `public string Provider { get; init; } = "claude";`
- Clé JSON `"provider"`
- `Settings.Clamp()` valide que `Provider` est `"claude"` ou `"antigravity"`, sinon repli sur `"claude"`.
- Rétro-compatibilité : un fichier sans clé `provider` donne `"claude"`.

- [x] **Step 1 : Écrire les tests unitaires de `Settings`**

Ajouter dans `tests/UsageNotch.Core.Tests/Settings/SettingsTests.cs` :
1. Valeur par défaut `"claude"`.
2. Sérialisation et désérialisation de `"antigravity"`.
3. Valeur inconnue `"unknown"` remappée vers `"claude"` par `Clamp()`.

- [x] **Step 2 : Implémenter dans `Settings.cs`**

Ajouter la propriété et mettre à jour `Clamp()`.

- [x] **Step 3 : Valider les tests**

Exécuter `dotnet test --filter "SettingsTests"`.

- [x] **Step 4 : Commit**

```powershell
git add src/UsageNotch.Core/Settings/Settings.cs tests/UsageNotch.Core.Tests/Settings/SettingsTests.cs
git commit -m "feat(core): add provider selection to settings with claude default"
```

---

## Task 5 : Presentation — Choix du fournisseur & adaptation de la carte et de la pilule

**Fichiers :**
- Modifier : `src/UsageNotch.Presentation/Preferences/Choices.cs`
- Modifier : `src/UsageNotch.Presentation/Preferences/AppearancePageViewModel.cs`
- Modifier : `src/UsageNotch.Presentation/Formatting/FrenchText.cs`
- Modifier : `src/UsageNotch.Presentation/Pill/CellModel.cs`
- Modifier : `src/UsageNotch.Presentation/Pill/PillPresenter.cs`
- Modifier : `src/UsageNotch.Presentation/Card/CardPresenter.cs`
- Tests : `tests/UsageNotch.Presentation.Tests/Preferences/AppearancePageViewModelTests.cs`, `PillPresenterTests.cs`, `CardPresenterTests.cs`

**Contrat :**
- `Choices.Providers` : liste des choix `[ ("claude", "Claude (Anthropic)"), ("antigravity", "Google Antigravity") ]`.
- `AppearancePageViewModel` expose `Provider` et `ProviderChoices`.
- `CellModel` expose `ProviderId`.
- `PillPresenter.ActivityColor` : pour `ActivityKind.Done`, renvoie la couleur de marque correspondant au provider (`#D97757` pour Claude, `#1A73E8` pour Antigravity).
- `CardPresenter.Build` affiche le nom du fournisseur courant dans l'en-tête de la carte.

- [x] **Step 1 : Écrire les tests Presentation**

1. `AppearancePageViewModelTests` : modifier `Provider` met à jour le brouillon et notifie.
2. `PillPresenterTests` : `CellModel.ProviderId` reflète le provider passé ; `ActivityColor(Done)` respecte le provider.
3. `CardPresenterTests` : en-tête et libellés adaptés au fournisseur passé.

- [x] **Step 2 : Implémenter les modifications Presentation**

Mettre à jour `Choices`, `AppearancePageViewModel`, `CellModel`, `PillPresenter` et `CardPresenter`.

- [x] **Step 3 : Valider les tests**

Exécuter `dotnet test tests/UsageNotch.Presentation.Tests`.

- [x] **Step 4 : Commit**

```powershell
git add src/UsageNotch.Presentation/ tests/UsageNotch.Presentation.Tests/
git commit -m "feat(presentation): support provider selection and Antigravity card/pill presentation"
```

---

## Task 6 : App — Symbole Gemini vectoriel et affichage dynamique du logo

**Fichiers :**
- Créer : `src/UsageNotch.App/Assets/gemini.svg`
- Modifier : `src/UsageNotch.App/Controls/BrandGeometry.cs`
- Modifier : `src/UsageNotch.App/Views/PillWindow.xaml` & `PillWindow.xaml.cs`
- Modifier : `src/UsageNotch.App/Controls/PillPreview.cs`
- Modifier : `src/UsageNotch.App/Views/Preferences/AppearancePage.xaml`

**Contrat :**
- `Assets/gemini.svg` : tracé SVG standard de l'étincelle à 4 pointes Gemini dans une `viewBox="0 0 24 24"`.
- `BrandGeometry.ForProvider(string providerId)` renvoie la `Geometry` gelée correspondante (`AnthropicMark` ou `GeminiMark`).
- `PillWindow` et `PillPreview` mettent à jour la géométrie du `Path` central en fonction du fournisseur actif.
- `AppearancePage.xaml` intègre la `ComboBox` « Fournisseur d'usage ».

- [x] **Step 1 : Créer `Assets/gemini.svg` et enrichir `BrandGeometry`**

Transcrire le path Gemini en `Geometry` statique gelée dans `BrandGeometry.cs`.

- [x] **Step 2 : Mettre à jour `PillWindow` et `PillPreview`**

Lier la géométrie affichée au `ProviderId` de la cellule.

- [x] **Step 3 : Ajouter le sélecteur dans `AppearancePage.xaml`**

Ajouter le bloc de réglage « Fournisseur d'usage » sous la section Thème/Général.

- [x] **Step 4 : Vérifier la compilation**

```powershell
dotnet build UsageNotch.sln
dotnet test
```

- [x] **Step 5 : Commit**

```powershell
git add src/UsageNotch.App/
git commit -m "feat(app): add Gemini mark geometry and provider selector in settings window"
```

---

## Task 7 : App — Câblage dans l'hôte, bascule à chaud et mode démo

**Fichiers :**
- Modifier : `src/UsageNotch.App/Hosting/AppHost.cs`
- Modifier : `src/UsageNotch.App/Hosting/DemoMode.cs`
- Modifier : `src/UsageNotch.Core/Usage/UsagePoller.cs` (si nécessaire pour le rafraîchissement au changement de fournisseur)

**Contrat :**
- `AppHost` enregistre les providers `ClaudeUsageProvider` et `AntigravityUsageProvider`.
- Au changement de `Settings.Provider`, le `UsagePoller` bascule vers le nouveau provider et déclenche un rafraîchissement immédiat.
- En mode `--demo`, simulation de données réalistes pour Antigravity (5h à 65 %, Weekly à 29 %, 3P à 0 %).

- [x] **Step 1 : Implémenter la sélection et la bascule dans `AppHost` / `UsagePoller`**

Permettre au poller d'interroger le fournisseur actif désigné par `Settings.Provider`.

- [x] **Step 2 : Mettre à jour `DemoMode.cs`**

Fournir les snapshots de démo adaptés selon le fournisseur actif.

- [x] **Step 3 : Vérifier la compilation et les tests**

```powershell
dotnet build UsageNotch.sln
dotnet test
```

- [x] **Step 4 : Commit**

```powershell
git add src/UsageNotch.App/Hosting/ src/UsageNotch.Core/Usage/
git commit -m "feat(app): wire active provider switching and demo mode support"
```

---

## Task 8 : Vérification à l'exécution et documentation

- [x] **Step 1 : Exécuter l'application en mode démo**

```powershell
src\UsageNotch.App\bin\Debug\net10.0-windows\UsageNotch.App.exe --demo
```
Vérifier :
- L'ouverture des réglages sur la page Apparence.
- La présence de la ComboBox « Fournisseur d'usage ».
- Le basculement entre Claude et Google Antigravity :
  - Le glyphe central passe de la marque Anthropic à l'étoile Gemini.
  - La couleur de marque passe au bleu Gemini.
  - La carte affiche « Usage Google Antigravity » avec les libellés de fenêtres 5h et Weekly.

- [x] **Step 2 : Exécuter l'application en conditions réelles**

Lancer l'application avec Antigravity ouvert sur la machine et vérifier que les quotas réels sont lus sans erreur depuis le hub local `agy.exe`.

- [x] **Step 3 : Mettre à jour la documentation et le journal de bord**

- Documenter l'exécution dans `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md`.
- Mettre à jour `docs/REPRISE.md` (passer la spec et le plan en « réalisés », mettre à jour le nombre de tests).

- [x] **Step 4 : Commit de clôture et fusion**

```powershell
git add docs/
git commit -m "docs: record Antigravity provider implementation"
git switch main
git merge feat/antigravity-provider --no-ff -m "feat: Google Antigravity quota and usage provider"
```

