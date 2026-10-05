# Plan d'implémentation — Suivi de consommation OpenAI et Capsule Triple

> **Branche de travail** : `feat/openai-provider`  
> **Référence de départ** : Version 0.6.0-dev, 601 tests verts (295 Core, 306 Presentation), zéro avertissement (`TreatWarningsAsErrors=true`).

---

## Vue d'ensemble des tâches

| Tâche | Domaine | Fichiers principaux | Tests associés |
|---|---|---|---|
| **1** | Core : Réglages & Stockage | `Settings.cs`, `UsageStore.cs` | `SettingsStoreTests.cs` |
| **2** | Core : Fournisseur OpenAI | `OpenAiCredentialReader.cs`, `OpenAiUsageProvider.cs` | `OpenAiUsageProviderTests.cs` |
| **3** | Core : Sondeur multi-fournisseurs | `UsagePoller.cs` | `UsagePollerTests.cs` |
| **4** | Presentation : Métriques & Modèles | `PillMetrics.cs`, `PillModel.cs`, `Choices.cs`, `NotchViewModel.cs` | `PillMetricsTests.cs`, `PillModelTests.cs`, `NotchViewModelTests.cs` |
| **5** | App : Graphismes & Capsule UI | `BrandGeometry.cs`, `PillWindow.xaml/.cs`, `PillPreview.cs` | `dotnet build` (WPF UI) |
| **6** | App : DI & Mode Démo | `DemoMode.cs`, `AppHost.cs` | Test en direct `--demo` sur port 48667 |
| **7** | Validation & Finalisation | `HANDOFF.md`, `docs/REPRISE.md` | `dotnet test`, `dotnet build -c Release` |

---

## Tâche 1 : Core — Réglages et persistance multi-fournisseur

**Objectifs :**
- Ajouter les paramètres OpenAI dans `Settings.cs` :
  - `string OpenAiApiKey = ""`
  - `double OpenAiMonthlyBudget = 20.0`
- Étendre `Settings.Clamp()` pour valider les nouveaux modes de sélection :
  - `"all"` (capsule triple : Claude, Antigravity, OpenAI)
  - `"both"` (capsule double : Claude, Antigravity)
  - `"claude_openai"` (capsule double : Claude, OpenAI)
  - `"antigravity_openai"` (capsule double : Antigravity, OpenAI)
  - `"claude"`
  - `"antigravity"`
  - `"openai"`
- Mettre à jour `SettingsStoreTests.cs` pour valider le clamp et la persistance.

**Étapes :**
- [ ] Modifier `src/UsageNotch.Core/Settings/Settings.cs`.
- [ ] Mettre à jour `tests/UsageNotch.Core.Tests/Settings/SettingsStoreTests.cs`.
- [ ] Valider avec `dotnet test --filter SettingsStoreTests`.

---

## Tâche 2 : Core — Fournisseur OpenAI (`OpenAiCredentialReader`, `OpenAiUsageProvider`)

**Objectifs :**
- Créer `OpenAiCredentialReader` pour extraire la clé API (`Settings.OpenAiApiKey` ou variable `OPENAI_API_KEY`).
- Créer `OpenAiUsageProvider` implémentant `IUsageProvider` :
  - `Id` = `"openai"`, `DisplayName` = `"OpenAI"`.
  - `RingWindowIds` pour les 3 anneaux : budget mensuel, consommation journalière, modèles de raisonnement.
  - Récupération sécurisée via appel HTTP vers `https://api.openai.com/v1/organization/costs`.
  - Calcul des fractions sur la base du budget mensuel configuré.
  - Gestion des statuts : succès, non configuré, erreur d'authentification, rate limit (HTTP 429).
- Écrire la suite de tests unitaires `OpenAiUsageProviderTests.cs` avec mocks HTTP.

**Étapes :**
- [ ] Créer `src/UsageNotch.Core/Usage/OpenAiCredentialReader.cs`.
- [ ] Créer `src/UsageNotch.Core/Usage/OpenAiUsageProvider.cs`.
- [ ] Créer `tests/UsageNotch.Core.Tests/Usage/OpenAiUsageProviderTests.cs`.
- [ ] Valider avec `dotnet test --filter OpenAiUsageProviderTests`.

---

## Tâche 3 : Core — Orchestration du sondeur (`UsagePoller`)

**Objectifs :**
- Mettre à jour `UsagePoller.GetActiveProviders()` pour filtrer les fournisseurs actifs selon tous les modes :
  - Mode `"all"` : renvoie les 3 fournisseurs.
  - Modes doubles : `"both"`, `"claude_openai"`, `"antigravity_openai"`.
  - Modes simples : `"claude"`, `"antigravity"`, `"openai"`.
- Valider la gestion des backoffs isolés pour 3 fournisseurs concurrents.
- Mettre à jour `UsagePollerTests.cs`.

**Étapes :**
- [ ] Modifier `src/UsageNotch.Core/Usage/UsagePoller.cs`.
- [ ] Mettre à jour `tests/UsageNotch.Core.Tests/Usage/UsagePollerTests.cs`.
- [ ] Valider avec `dotnet test --filter UsagePollerTests`.

---

## Tâche 4 : Presentation — Métriques paramétriques et modèles de présentation

**Objectifs :**
- Généraliser `PillMetrics.BodyLengthFor(provider, edge, content)` et `WindowLengthFor` pour 1, 2 et 3 cellules :
  - 3 cellules horizontal `RingAndPercent` : 300 DIP corps / 332 DIP fenêtre.
  - 3 cellules vertical `RingAndPercent` : 258 DIP corps / 290 DIP fenêtre.
  - 3 cellules `RingOnly` : 192 DIP corps / 224 DIP fenêtre.
- Ajouter les définitions d'anneaux dans `RingWindows.OpenAi`.
- Mettre à jour `PillModel` avec `Cell3` et `CellOpenAi`.
- Mettre à jour `Choices.Providers` pour inclure toutes les options avec libellés français clairs.
- Mettre à jour `SettingsPreview` et `NotchViewModel` pour composer dynamiquement de 1 à 3 cellules et de 1 à 3 sections.
- Ajouter et mettre à jour les tests unitaires.

**Étapes :**
- [ ] Modifier `src/UsageNotch.Presentation/Pill/PillMetrics.cs` et `RingWindows.cs`.
- [ ] Modifier `src/UsageNotch.Presentation/Pill/PillModel.cs` et `PillPresenter.cs`.
- [ ] Modifier `src/UsageNotch.Presentation/Preferences/Choices.cs` et `SettingsPreview.cs`.
- [ ] Modifier `src/UsageNotch.Presentation/ViewModels/NotchViewModel.cs`.
- [ ] Mettre à jour les tests unitaires de présentation.
- [ ] Valider avec `dotnet test`.

---

## Tâche 5 : App — Graphismes vectoriels et capsule triple UI

**Objectifs :**
- Transcrire le SVG `openai-light.svg` en géométrie gelée `OpenAiMark` dans `src/UsageNotch.App/Controls/BrandGeometry.cs`.
- Mettre à jour `PillWindow.xaml` et `PillWindow.xaml.cs` :
  - Ajouter `Cell3Stack` et `CellDivider2`.
  - Ajouter les storyboards `Spin3` et `Pulse3`.
  - Gérer la visibilité dynamique des 3 cellules et des 2 séparateurs selon le nombre de cellules actives.
- Mettre à jour `PillPreview.cs` pour restituer l'aperçu statique jusqu'à 3 cellules avec leurs logos respectifs.
- Compiler avec zéro avertissement.

**Étapes :**
- [ ] Modifier `src/UsageNotch.App/Controls/BrandGeometry.cs`.
- [ ] Modifier `src/UsageNotch.App/Views/PillWindow.xaml` et `src/UsageNotch.App/Views/PillWindow.xaml.cs`.
- [ ] Modifier `src/UsageNotch.App/Controls/PillPreview.cs`.
- [ ] Compiler avec `dotnet build UsageNotch.sln`.

---

## Tâche 6 : App — Mode Démo et Câblage DI (`DemoMode`, `AppHost`)

**Objectifs :**
- Mettre à jour `DemoUsageProvider` pour simuler le fournisseur `"openai"` :
  - Budget mensuel : 42 %
  - Jour : 18 %
  - Modèles de raisonnement (o1/o3) : 65 %
- Mettre à jour `AppHost.cs` pour injecter `OpenAiUsageProvider` et le trio de fournisseurs dans `UsagePoller`.
- Tester en direct en mode démo (`--demo`) :
  - Vérifier la capsule triple avec les 3 logos (Anthropic, Gemini, OpenAI).
  - Vérifier la carte de détail avec les 3 sections.
  - Vérifier la bascule de fournisseurs dans les réglages.

**Étapes :**
- [ ] Modifier `src/UsageNotch.App/Hosting/DemoMode.cs`.
- [ ] Modifier `src/UsageNotch.App/Hosting/AppHost.cs`.
- [ ] Valider le fonctionnement visuel et le hot-reload.

---

## Tâche 7 : Validation globale et fusion

**Objectifs :**
- Exécuter la suite complète des tests unitaires (`dotnet test`).
- Génération Release stricte (`dotnet build -c Release`).
- Fusionner la branche `feat/openai-provider` dans `main`.
- Mettre à jour `HANDOFF.md` et `docs/REPRISE.md`.

**Étapes :**
- [ ] `dotnet test` (100 % passants).
- [ ] `dotnet build -c Release`.
- [ ] Fusionner `feat/openai-provider` dans `main`.
- [ ] Mettre à jour la documentation.
