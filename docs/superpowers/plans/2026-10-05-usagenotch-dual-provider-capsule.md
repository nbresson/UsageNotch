# Capsule double (Claude + Antigravity) — Plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal :** Afficher simultanément les quotas de **Claude** et de **Google Antigravity** dans une capsule unique unifiée (Option A) :
- **Sur la pilule** : deux cellules compactes côte à côte (ou empilées selon le bord d'écran), chacune avec ses 3 anneaux concentriques, son logo vectoriel (Anthropic / Gemini) et son voyant d'activité indépendant, séparées par un fin trait discret.
- **Sur la carte de détail** : deux sections dédiées (« Claude (Anthropic) » et « Google Antigravity ») présentant leurs barres de quotas respectives et les sessions en cours.
- **Dans les réglages** : choix flexible sous « Fournisseur d'usage » entre :
  - *« Les deux (capsule double) »* (`"both"`)
  - *« Claude uniquement »* (`"claude"`)
  - *« Google Antigravity uniquement »* (`"antigravity"`)

**Architecture :**
- Couche `Core` :
  - `Settings` : paramètre `Provider` acceptant `"both"`, `"claude"`, `"antigravity"`.
  - `UsageStore` : dictionnaire de snapshots par fournisseur (`SnapshotFor(id)`, `Snapshots`), persistance JSON sous forme d'objet clé-valeur avec rétro-compatibilité automatique pour l'ancien format monobloc (mappé sur `"claude"`).
  - `UsagePoller` : scrutation conjointe des fournisseurs actifs avec politiques de backoff indépendantes (l'indisponibilité d'un fournisseur ne bloque pas l'autre).
- Couche `Presentation` :
  - `PillMetrics` : calcul dynamique des longueurs de corps et de fenêtre (`BodyLengthFor`, `WindowLengthFor`) selon le mode simple/double, le bord d'écran et le mode de contenu (`RingAndPercent` vs `RingOnly`).
  - `PillModel` & `PillPresenter` : création du modèle composite unifié portant les cellules et la couleur de bande repliée prioritaire (ambre en cas d'attention).
  - `CardModel` & `CardPresenter` : sections multiples (`CardSection`) avec titre, sous-titre de fraîcheur, barres de quota et note par fournisseur.
  - `Choices` & `SettingsPreview` : mise à jour des options et prévisualisation de la capsule double dans les préférences.
  - `NotchViewModel` & `ThresholdNotifications` : orchestration des deux flux de données et surveillance conjointe des seuils d'alerte.
- Couche `App` :
  - `NotchPlacer` : dimensionnement physique de la fenêtre de pilule selon la longueur dynamique.
  - `PillWindow` : intégration des deux cellules, du séparateur central, et des storyboards d'animation indépendants (`Spin` / `Pulse`).
  - `CardWindow` : élargissement à 320 DIP et affichage multi-sections.
  - `DemoMode` & `AppHost` : simulation conjointe des deux fournisseurs et injection de dépendances.

**Tech Stack :** C# / .NET 10 (C# 14), WPF (`net10.0-windows`), xUnit + FluentAssertions.

**Spec :** [`docs/superpowers/specs/2026-10-05-usagenotch-dual-provider-capsule-design.md`](file:///C:/Users/nbres/source/repos/codenotchbis/docs/superpowers/specs/2026-10-05-usagenotch-dual-provider-capsule-design.md)

---

## Contraintes globales

- Interface **en français** : tout libellé, texte de carte, infobulle et message d'erreur.
- Compilation **sans aucun avertissement** (`TreatWarningsAsErrors` dans `Directory.Build.props`).
- La logique vit dans `UsageNotch.Core` et `UsageNotch.Presentation`, **sans référence à WPF**, et se développe en TDD strict. `UsageNotch.App` (WPF, interop Win32) se vérifie à l'exécution.
- Messages de commit **en anglais**, forme `type(scope): description`, fichier de message en UTF-8 **sans BOM**.
- Branche de travail : `feat/dual-provider-capsule`, fusionnée dans `main` à la fin.
- Ne **jamais** arrêter l'instance réelle de l'utilisateur (`publish\UsageNotch.App.exe`, port 48666, mutex `Local\UsageNotch`). Les vérifications se font en `--demo` (port 48667, mutex `Local\UsageNotch-demo`), ou sur les binaires de build debug.
- Ne pas republier dans `publish\` pendant le chantier.

### Cycle de vérification (identique à chaque tâche)

```powershell
dotnet build UsageNotch.sln
dotnet test
```

Référence de départ : **576 tests verts** (287 Core, 289 Presentation), zéro avertissement.

---

## Structure des fichiers

**Créés :**

| Fichier | Responsabilité |
|---|---|
| `src/UsageNotch.Presentation/Pill/PillModel.cs` | Modèle de données composite pour la pilule unifiée (Cellules, BodyLength, WindowLength, BandColor, IsDual) |
| `tests/UsageNotch.Presentation.Tests/Pill/PillModelTests.cs` | Tests unitaires pour `PillModel` |

**Modifiés :**

| Fichier | Modifications |
|---|---|
| `src/UsageNotch.Core/Settings/Settings.cs` | Ajout de `"both"` aux fournisseurs valides, repli par défaut |
| `tests/UsageNotch.Core.Tests/Settings/SettingsTests.cs` | Tests pour `"both"` et repli des valeurs inconnues |
| `src/UsageNotch.Core/Usage/UsageStore.cs` | Dictionnaire de snapshots, persistance JSON multi-fournisseurs, rétrocompatibilité ancien format |
| `tests/UsageNotch.Core.Tests/Usage/UsageStoreTests.cs` | Tests unitaires de sérialisation multi-fournisseurs et rétrocompatibilité |
| `src/UsageNotch.Core/Usage/UsagePoller.cs` | Prise en charge de plusieurs `IUsageProvider` avec backoff isolé |
| `tests/UsageNotch.Core.Tests/Usage/UsagePollerTests.cs` | Tests de scrutation conjointe et d'isolation des pannes |
| `src/UsageNotch.Presentation/Pill/PillMetrics.cs` | Calculs de longueur `BodyLengthFor` et `WindowLengthFor` |
| `tests/UsageNotch.Presentation.Tests/Pill/PillMetricsTests.cs` | Tests des dimensions logiques en mode simple et double |
| `src/UsageNotch.Presentation/Pill/PillPresenter.cs` | Méthode `Pill(...)` assemblant les cellules et calculant la couleur de bande repliée |
| `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs` | Tests de génération de pilule composite |
| `src/UsageNotch.Presentation/Card/CardModel.cs` | Types `CardSection` et `CardModel` avec support multi-sections |
| `src/UsageNotch.Presentation/Card/CardPresenter.cs` | Construction de sections multiples pour la carte |
| `tests/UsageNotch.Presentation.Tests/Card/CardPresenterTests.cs` | Tests de mise en page de la carte en mode double |
| `src/UsageNotch.Presentation/Preferences/Choices.cs` | Libellés français actualisés (`"both"`, `"claude"`, `"antigravity"`) |
| `src/UsageNotch.Presentation/Preferences/SettingsPreview.cs` | Aperçus de capsule double pour les réglages |
| `tests/UsageNotch.Presentation.Tests/Preferences/SettingsPreviewTests.cs` | Tests unitaires de l'aperçu |
| `src/UsageNotch.Presentation/ViewModels/NotchViewModel.cs` | Intégration des snapshots multiples, `PillModel`, `CardModel`, `TrayText` |
| `tests/UsageNotch.Presentation.Tests/ViewModels/NotchViewModelTests.cs` | Tests unitaires du ViewModel en mode simple et double |
| `src/UsageNotch.Presentation/Behavior/ThresholdNotifications.cs` | Surveillance conjointe des seuils en mode double |
| `tests/UsageNotch.Presentation.Tests/Behavior/ThresholdNotificationsTests.cs` | Tests de notifications avec deux fournisseurs |
| `src/UsageNotch.App/Views/NotchPlacer.cs` | Utilisation des longueurs dynamiques de fenêtre |
| `src/UsageNotch.App/Views/PillWindow.xaml` | Disposition des deux cellules, séparateur central, storyboards |
| `src/UsageNotch.App/Views/PillWindow.xaml.cs` | Câblage du redimensionnement, des orientations et des animations |
| `src/UsageNotch.App/Views/CardWindow.xaml` | Largeur 320 DIP, présentation multi-sections |
| `src/UsageNotch.App/Controls/PillPreview.cs` | Rendu visuel de la capsule double dans les aperçus |
| `src/UsageNotch.App/Hosting/DemoMode.cs` | Simulation de deux fournisseurs actifs pour `--demo` |
| `src/UsageNotch.App/Hosting/AppHost.cs` | Enregistrement DI multi-fournisseurs |

---

## Tâches d'implémentation

### Tâche 1 : Core — Support multi-fournisseurs dans `Settings` et `UsageStore`

**Objectifs :**
- Autoriser `"both"` dans `Settings.Provider` (valeur par défaut pour les nouvelles installations, repli des valeurs invalides).
- Évoluer `UsageStore` pour stocker `IReadOnlyDictionary<string, UsageSnapshot>`, exposer `SnapshotFor(string providerId)`, `Snapshots`, et `Apply(string providerId, FetchResult result, TimeSpan backoffWait = default)`.
- Conserver la persistance dans `usage.json` sous la forme `{"claude": {...}, "antigravity": {...}}` avec conversion automatique transparente de l'ancien format monobloc.

**Étapes :**
- [ ] Créer la branche `feat/dual-provider-capsule`.
- [ ] Écrire les tests TDD dans `tests/UsageNotch.Core.Tests/Settings/SettingsTests.cs` pour vérifier que `"both"` est valide et que `Clamp()` replie sur `"both"`.
- [ ] Mettre à jour `src/UsageNotch.Core/Settings/Settings.cs`.
- [ ] Écrire les tests TDD dans `tests/UsageNotch.Core.Tests/Usage/UsageStoreTests.cs` pour :
  - Sauvegarde et rechargement de plusieurs snapshots par fournisseur.
  - Rechargement d'un fichier `usage.json` existant au format monobloc (migré automatiquement vers la clé `"claude"`).
  - Gestion indépendante de `Apply` pour différents fournisseurs.
  - `ClearBackoff` effaçant les backoffs de tous les fournisseurs.
- [ ] Implémenter les modifications dans `src/UsageNotch.Core/Usage/UsageStore.cs`.
- [ ] Valider avec `dotnet test --filter UsageStoreTests` et `dotnet test --filter SettingsTests`.

---

### Tâche 2 : Core — Scrutation conjointe avec backoff isolé dans `UsagePoller`

**Objectifs :**
- Permettre à `UsagePoller` d'accepter une collection de `IUsageProvider` (`IReadOnlyList<IUsageProvider>`).
- En mode `"both"`, scruter tous les fournisseurs actifs ; en mode simple (`"claude"` ou `"antigravity"`), ne scruter que celui demandé.
- Maintenir une politique de backoff (`BackoffPolicy`) et un état d'échéance par fournisseur afin qu'une erreur ou un rate-limit sur l'un (ex. Antigravity non lancé ou Claude 429) n'affecte en rien l'autre.
- Conserver le constructeur avec un seul provider pour compatibilité.

**Étapes :**
- [ ] Écrire les tests TDD dans `tests/UsageNotch.Core.Tests/Usage/UsagePollerTests.cs` :
  - Scrutation conjointe de deux providers en mode `"both"`.
  - Scrutation filtrée en mode `"claude"` ou `"antigravity"`.
  - Backoff isolé : rate-limiting sur le provider 1 n'empêche pas le provider 2 de continuer ses cycles.
  - `RequestRefresh` relance immédiatement les deux providers.
- [ ] Mettre à jour `src/UsageNotch.Core/Usage/UsagePoller.cs`.
- [ ] Valider avec `dotnet test --filter UsagePollerTests`.

---

### Tâche 3 : Presentation — Métriques, Modèles et Présenteurs (`PillModel`, `PillPresenter`, `CardModel`, `CardPresenter`)

**Objectifs :**
- Ajouter `PillMetrics.BodyLengthFor(provider, edge, content)` et `WindowLengthFor(provider, edge, content)` :
  - Simple (`"claude"` ou `"antigravity"`) : `BodyLength = 104`, `WindowLength = 136`.
  - Double (`"both"`), `RingAndPercent` :
    - Horizontal (`Top`/`Bottom`) : `BodyLength = 196`, `WindowLength = 228`.
    - Vertical (`Left`/`Right`) : `BodyLength = 168`, `WindowLength = 200`.
  - Double (`"both"`), `RingOnly` : `BodyLength = 124`, `WindowLength = 156`.
- Créer `PillModel` et `PillPresenter.Pill(...)` combinant les cellules, déterminant la couleur de bande repliée (priorité à `Theme.Attention`).
- Mettre à jour `CardModel` avec `CardSection` et `CardPresenter.Build(...)` pour générer deux sections en mode double ou une section en mode simple.

**Étapes :**
- [ ] Créer `tests/UsageNotch.Presentation.Tests/Pill/PillMetricsTests.cs` et tester les calculs de dimension.
- [ ] Mettre à jour `src/UsageNotch.Presentation/Pill/PillMetrics.cs`.
- [ ] Créer `src/UsageNotch.Presentation/Pill/PillModel.cs` et ses tests `tests/UsageNotch.Presentation.Tests/Pill/PillModelTests.cs`.
- [ ] Écrire les tests TDD dans `tests/UsageNotch.Presentation.Tests/Pill/PillPresenterTests.cs` pour `PillPresenter.Pill(...)` (couleur de bande, dimensions).
- [ ] Mettre à jour `src/UsageNotch.Presentation/Pill/PillPresenter.cs`.
- [ ] Écrire les tests TDD dans `tests/UsageNotch.Presentation.Tests/Card/CardPresenterTests.cs` pour la génération des sections multiples.
- [ ] Mettre à jour `src/UsageNotch.Presentation/Card/CardModel.cs` et `src/UsageNotch.Presentation/Card/CardPresenter.cs`.
- [ ] Valider avec `dotnet test --filter Presentation`.

---

### Tâche 4 : Presentation — Préférences, ViewModel et Notifications de seuil

**Objectifs :**
- Mettre à jour `Choices.Providers` avec `"both"` (« Les deux (capsule double) »), `"claude"` (« Claude uniquement »), `"antigravity"` (« Google Antigravity uniquement »).
- Mettre à jour `SettingsPreview` pour générer un aperçu à deux cellules quand `"both"` est sélectionné.
- Mettre à jour `NotchViewModel` pour composer `PillModel`, `CellModel` (première cellule), `CardModel` (multi-sections) et `TrayText` combiné.
- Mettre à jour `ThresholdNotifications` pour surveiller les quotas de tous les fournisseurs actifs.

**Étapes :**
- [ ] Mettre à jour `src/UsageNotch.Presentation/Preferences/Choices.cs`.
- [ ] Écrire les tests TDD dans `tests/UsageNotch.Presentation.Tests/Preferences/SettingsPreviewTests.cs` pour l'aperçu en mode `"both"`.
- [ ] Mettre à jour `src/UsageNotch.Presentation/Preferences/SettingsPreview.cs`.
- [ ] Écrire les tests TDD dans `tests/UsageNotch.Presentation.Tests/ViewModels/NotchViewModelTests.cs` (génération dual, bascule de provider, TrayText).
- [ ] Mettre à jour `src/UsageNotch.Presentation/ViewModels/NotchViewModel.cs`.
- [ ] Écrire les tests TDD dans `tests/UsageNotch.Presentation.Tests/Behavior/ThresholdNotificationsTests.cs`.
- [ ] Mettre à jour `src/UsageNotch.Presentation/Behavior/ThresholdNotifications.cs`.
- [ ] Valider avec `dotnet test`.

---

### Tâche 5 : App — Placement physique et fenêtres WPF (`NotchPlacer`, `PillWindow`, `CardWindow`, `PillPreview`)

**Objectifs :**
- Mettre à jour `NotchPlacer.Compute` pour utiliser `PillMetrics.WindowLengthFor(s.Provider, s.Edge, s.CellContent)`.
- Refondre `PillWindow.xaml` et `PillWindow.xaml.cs` pour afficher les deux cellules en mode `"both"`, le séparateur central de 12 DIP (vertical ou horizontal selon le bord), et gérer les storyboards d'animation indépendants (`Spin` pour rotation, `Pulse` pour attention, `BandPulse` pour replié).
- Mettre à jour `CardWindow.xaml` : largeur 320 DIP, en-tête global en mode double, `ItemsControl` pour les sections et en-tête `SESSIONS ACTIVES`.
- Mettre à jour `PillPreview.cs` pour dessiner les deux cellules et le séparateur dans la fenêtre de réglages.

**Étapes :**
- [ ] Mettre à jour `src/UsageNotch.App/Views/NotchPlacer.cs`.
- [ ] Mettre à jour `src/UsageNotch.App/Views/PillWindow.xaml` et `src/UsageNotch.App/Views/PillWindow.xaml.cs`.
- [ ] Mettre à jour `src/UsageNotch.App/Views/CardWindow.xaml`.
- [ ] Mettre à jour `src/UsageNotch.App/Controls/PillPreview.cs`.
- [ ] Compiler et s'assurer de zéro avertissement (`dotnet build`).

---

### Tâche 6 : App — Mode Démo et Câblage DI (`DemoMode`, `AppHost`)

**Objectifs :**
- Mettre à jour `DemoUsageProvider` / `DemoMode` pour simuler simultanément Claude (73 % / 21 % / 52 %) et Antigravity (65 % / 29 % / 0 %).
- Mettre à jour `AppHost.cs` pour enregistrer la liste des fournisseurs et injecter le bon ensemble dans `UsagePoller`.

**Étapes :**
- [ ] Mettre à jour `src/UsageNotch.App/Hosting/DemoMode.cs`.
- [ ] Mettre à jour `src/UsageNotch.App/Hosting/AppHost.cs`.
- [ ] Tester en mode démo (`--demo`) :
  - Vérifier l'affichage de la capsule double avec les 2 logos (Anthropic et Gemini).
  - Vérifier l'ouverture de la carte avec les deux sections.
  - Vérifier la bascule de fournisseur dans les réglages (`"both"` -> `"claude"` -> `"antigravity"` -> `"both"`).
  - Vérifier le comportement en bord d'écran Haut, Bas, Gauche et Droite.
  - Vérifier le mode replié et l'attention ambre.

---

### Tâche 7 : Validation finale et fusion

**Objectifs :**
- Exécuter la suite complète de tests unitaires (100 % passants).
- Vérifier l'absence d'avertissement de compilation.
- Fusionner la branche `feat/dual-provider-capsule` dans `main`.

**Étapes :**
- [ ] `dotnet test` (tous les tests verts).
- [ ] `dotnet build -c Release`.
- [ ] Fusionner `feat/dual-provider-capsule` dans `main`.
