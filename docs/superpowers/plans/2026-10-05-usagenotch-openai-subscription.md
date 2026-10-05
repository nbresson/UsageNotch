# Plan d'implémentation — Mode Abonnement OpenAI (ChatGPT / Codex)

> **Branche de travail** : `feat/openai-subscription`  
> **Référence de départ** : Version 0.7.0-dev, 642 tests verts (315 Core, 327 Presentation), zéro avertissement (`TreatWarningsAsErrors=true`).

---

## Vue d'ensemble des tâches

| Tâche | Domaine | Fichiers principaux | Tests associés |
|---|---|---|---|
| **1** | Core : Réglages & Persistance | `Settings.cs` | `SettingsStoreTests.cs` |
| **2** | Core : Authentification Abonnement | `OpenAiSubscriptionCredentialReader.cs` | `OpenAiSubscriptionCredentialReaderTests.cs` |
| **3** | Core : Analyseur de Quotas | `OpenAiSubscriptionParser.cs` | `OpenAiSubscriptionParserTests.cs` |
| **4** | Core : Aiguillage Fournisseur | `OpenAiUsageProvider.cs` | `OpenAiUsageProviderTests.cs` |
| **5** | Presentation : Anneaux & Réglages UI | `RingWindows.cs`, `Choices.cs`, `AppearancePageViewModel.cs`, `AppearancePage.xaml` | `AppearancePageViewModelTests.cs` |
| **6** | App : Simulation Mode Démo | `DemoMode.cs` | Vérification `--demo` |
| **7** | Validation & Documentation | `HANDOFF.md` | `dotnet test`, `dotnet build -c Release` |

---

## Tâche 1 : Core — Réglages et persistance

**Objectifs :**
- Étendre [`Settings.cs`](file:///C:/Users/nbres/source/repos/codenotchbis/src/UsageNotch.Core/Settings/Settings.cs) avec :
  - `string OpenAiMode = "subscription"` (« subscription » ou « api »).
  - `string OpenAiSessionToken = ""` (token d'accès Bearer manuel optionnel).
  - `string OpenAiAccountId = ""` (identifiant de compte optionnel).
- Normaliser dans `Clamp()` :
  - `OpenAiMode = OpenAiMode is "api" ? "api" : "subscription"`
  - `OpenAiSessionToken = OpenAiSessionToken?.Trim() ?? ""`
  - `OpenAiAccountId = OpenAiAccountId?.Trim() ?? ""`
- Mettre à jour `SettingsStoreTests.cs` pour valider le clamp et la persistance.

**Étapes :**
- [ ] Modifier `src/UsageNotch.Core/Settings/Settings.cs`.
- [ ] Mettre à jour `tests/UsageNotch.Core.Tests/Settings/SettingsStoreTests.cs`.
- [ ] Valider avec `dotnet test --filter SettingsStoreTests`.

---

## Tâche 2 : Core — Authentification Abonnement (`OpenAiSubscriptionCredentialReader`)

**Objectifs :**
- Créer un lecteur d'identifiants d'abonnement `OpenAiSubscriptionCredentialReader` :
  1. Si `Settings.OpenAiSessionToken` est renseigné non vide, l'utiliser avec priorité.
  2. Sinon, chercher `auth.json` :
     - `$CODEX_HOME/auth.json` si défini, sinon `%USERPROFILE%/.codex/auth.json`.
  3. Parser `auth.json` en extrayant :
     - `tokens.access_token` et `tokens.account_id` (format standard Codex).
     - Ou à la racine `access_token` et `account_id` (format plat).
  4. Renvoyer `(string AccessToken, string? AccountId)?`.
- Écrire la suite de tests unitaires `OpenAiSubscriptionCredentialReaderTests.cs`.

**Étapes :**
- [ ] Créer `src/UsageNotch.Core/Usage/OpenAiSubscriptionCredentialReader.cs`.
- [ ] Créer `tests/UsageNotch.Core.Tests/Usage/OpenAiSubscriptionCredentialReaderTests.cs`.
- [ ] Valider avec `dotnet test --filter OpenAiSubscriptionCredentialReaderTests`.

---

## Tâche 3 : Core — Analyseur de Quotas (`OpenAiSubscriptionParser`)

**Objectifs :**
- Créer `OpenAiSubscriptionParser` pour transformer la réponse JSON de `https://chatgpt.com/backend-api/wham/usage` en `IReadOnlyList<LimitWindow>` :
  - `rate_limit.primary_window` -> `LimitWindow("session", "Session 5h", usedFraction, resetsAt)`.
  - `rate_limit.secondary_window` -> `LimitWindow("weekly", "Quota hebdomadaire", usedFraction, resetsAt)`.
  - `additional_rate_limits[]` -> `LimitWindow("reasoning_models", "Modèles raisonnement", usedFraction, resetsAt)`.
- Gérer les formats de pourcentages (0..100 ou 0.0..1.0), et les temps de reset (`reset_at` unix epoch ou `reset_after_seconds`).
- Écrire la suite de tests unitaires `OpenAiSubscriptionParserTests.cs`.

**Étapes :**
- [ ] Créer `src/UsageNotch.Core/Usage/OpenAiSubscriptionParser.cs`.
- [ ] Créer `tests/UsageNotch.Core.Tests/Usage/OpenAiSubscriptionParserTests.cs`.
- [ ] Valider avec `dotnet test --filter OpenAiSubscriptionParserTests`.

---

## Tâche 4 : Core — Aiguillage Fournisseur (`OpenAiUsageProvider`)

**Objectifs :**
- Injecter `OpenAiSubscriptionCredentialReader` dans `OpenAiUsageProvider`.
- Dans `FetchAsync` :
  - Si `settings.Current.OpenAiMode == "api"` : exécuter la requête vers `/v1/organization/costs`.
  - Si `settings.Current.OpenAiMode == "subscription"` :
    - Lire le token d'accès via `OpenAiSubscriptionCredentialReader`.
    - Si absent : renvoyer `FetchResult.NeedsAuth(...)`.
    - Envoyer la requête `GET https://chatgpt.com/backend-api/wham/usage` avec en-têtes `Authorization: Bearer <token>` et optionnellement `ChatGPT-Account-Id: <account_id>`.
    - Gérer 200 (parse avec `OpenAiSubscriptionParser`), 401 (`NeedsAuth`), 429 (`RateLimited`), et exceptions réseau.
- Mettre à jour `OpenAiUsageProviderTests.cs`.

**Étapes :**
- [ ] Mettre à jour `src/UsageNotch.Core/Usage/OpenAiUsageProvider.cs`.
- [ ] Mettre à jour `tests/UsageNotch.Core.Tests/Usage/OpenAiUsageProviderTests.cs`.
- [ ] Valider avec `dotnet test --filter OpenAiUsageProviderTests`.

---

## Tâche 5 : Presentation — Anneaux & Réglages UI

**Objectifs :**
- Mettre à jour `RingWindows.OpenAi` pour inclure les alias de fenêtres pour les deux modes (`session`, `weekly`, `reasoning_models`, `monthly_cost`, `daily_cost`).
- Ajouter `OpenAiModes` dans `Choices.cs` :
  - `"subscription"` (« Abonnement (ChatGPT / Codex) »).
  - `"api"` (« Clé API (Organisation) »).
- Dans `AppearancePageViewModel.cs` :
  - Ajouter `OpenAiMode`, `IsOpenAiSubscriptionMode`, `IsOpenAiApiMode`, `OpenAiSessionToken`.
  - Notifier les changements de visibilité quand `OpenAiMode` change.
- Dans `AppearancePage.xaml` :
  - Ajouter le sélecteur ComboBox du mode OpenAI.
  - Afficher les champs conditionnellement selon le mode sélectionné.
- Mettre à jour `AppearancePageViewModelTests.cs`.

**Étapes :**
- [ ] Mettre à jour `src/UsageNotch.Core/Usage/RingWindows.cs`.
- [ ] Mettre à jour `src/UsageNotch.Presentation/Preferences/Choices.cs`.
- [ ] Mettre à jour `src/UsageNotch.Presentation/Preferences/AppearancePageViewModel.cs`.
- [ ] Mettre à jour `src/UsageNotch.App/Views/Preferences/AppearancePage.xaml`.
- [ ] Mettre à jour `tests/UsageNotch.Presentation.Tests/Preferences/AppearancePageViewModelTests.cs`.
- [ ] Valider avec `dotnet test --filter AppearancePageViewModelTests`.

---

## Tâche 6 : App — Simulation Mode Démo

**Objectifs :**
- Dans `DemoMode.cs`, simuler les quotas subscription d'OpenAI selon le mode configuré (ou par défaut pour le mode subscription) :
  - Fenêtre 5h : 28 %
  - Quota hebdomadaire : 54 %
  - Modèles raisonnement : 15 %
- Valider la cohérence visuelle.

**Étapes :**
- [ ] Modifier `src/UsageNotch.App/Hosting/DemoMode.cs`.
- [ ] Vérifier la compilation.

---

## Tâche 7 : Validation globale & Documentation

**Objectifs :**
- Exécuter la suite complète de tests unitaires (`dotnet test`).
- Compiler le projet en Release (`dotnet build -c Release`).
- Mettre à jour `HANDOFF.md`.

**Étapes :**
- [ ] `dotnet test`.
- [ ] `dotnet build -c Release`.
- [ ] Mettre à jour `HANDOFF.md`.
