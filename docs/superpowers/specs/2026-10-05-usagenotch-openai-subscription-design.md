# Spec de conception — Mode Abonnement OpenAI (ChatGPT / Codex)

## 1. Contexte & Objectifs

UsageNotch supporte le suivi de consommation de la plateforme OpenAI en mode API (« pay-as-you-go », avec budget mensuel et suivi des coûts en dollars via `/v1/organization/costs`).

Cependant, de nombreux développeurs utilisent OpenAI via un abonnement **ChatGPT Plus, Team, Pro ou Enterprise**, ainsi qu'au travers des outils officiels tels que le **Codex CLI**. Dans ce cadre, la consommation ne se mesure pas en dollars dépensés, mais en quotas d'utilisation par fenêtres de temps glissantes :
- Une **fenêtre de session 5 heures** (`primary_window`, 18 000 secondes).
- Une **fenêtre hebdomadaire 7 jours** (`secondary_window`, 604 800 secondes).
- Des **quotas spécifiques pour modèles avancés / raisonnement** (`additional_rate_limits`, ex. o1/o3 ou Codex Spark).

### Objectifs :
1. **Support du mode Abonnement OpenAI** en complément du mode Clé d'API existant.
2. **Sélecteur de mode dans les réglages OpenAI** :
   - « Abonnement (ChatGPT / Codex) » (par défaut).
   - « Clé API (Organisation) ».
3. **Authentification transparente** :
   - Détection automatique des identifiants OAuth dans `~/.codex/auth.json` (ou `$CODEX_HOME/auth.json`).
   - Champ manuel dans les réglages pour saisir un token de session / Bearer token si désiré.
4. **Interrogation de l'endpoint d'usage d'abonnement** :
   - `GET https://chatgpt.com/backend-api/wham/usage`.
5. **Harmonisation des 3 anneaux** :
   - **Anneau 1 (Extérieur / principal)** : Fenêtre de session 5h (`primary_window`). Affiche le % dans la pilule.
   - **Anneau 2 (Intermédiaire)** : Fenêtre hebdomadaire (`secondary_window`).
   - **Anneau 3 (Intérieur)** : Modèles de raisonnement / limites spécifiques (`additional_rate_limits`).
6. **Robustesse et dégradation gracieuse** :
   - Statut clair (`NeedsAuth`) si aucun token n'est trouvé ou si la session a expiré (HTTP 401).
   - Gestion des codes HTTP 429 et erreurs réseau.
7. **Simulations fidèles en mode `--demo`**.

---

## 2. Architecture & Composants

### 2.1 Modèle de données & Configuration (`UsageNotch.Core.Settings`)

Dans [`Settings.cs`](file:///C:/Users/nbres/source/repos/codenotchbis/src/UsageNotch.Core/Settings/Settings.cs) :
- `OpenAiMode` : `string` (`"subscription"` ou `"api"`, défaut : `"subscription"`).
- `OpenAiSessionToken` : `string` (token d'accès Bearer manuel optionnel, défaut : `""`).
- `OpenAiAccountId` : `string` (identifiant de compte optionnel issu du fichier ou réglages, défaut : `""`).
- Normalisation dans `Clamp()` :
  ```csharp
  OpenAiMode = OpenAiMode is "api" ? "api" : "subscription",
  OpenAiSessionToken = OpenAiSessionToken?.Trim() ?? "",
  OpenAiAccountId = OpenAiAccountId?.Trim() ?? "",
  ```

### 2.2 Authentification & Lecture des identifiants (`UsageNotch.Core.Usage`)

Nouveau composant **`OpenAiSubscriptionCredentialReader`** :
1. Si `Settings.OpenAiSessionToken` est renseigné non vide : l'utiliser (avec `Settings.OpenAiAccountId` si présent).
2. Sinon, chercher `auth.json` :
   - `$CODEX_HOME/auth.json` si `CODEX_HOME` est défini dans l'environnement.
   - Sinon `%USERPROFILE%/.codex/auth.json`.
3. Parser le JSON d'`auth.json` :
   - Format standard Codex :
     ```json
     {
       "auth_mode": "chatgpt",
       "tokens": {
         "access_token": "...",
         "account_id": "..."
       }
     }
     ```
   - Ou format plat `{ "access_token": "...", "account_id": "..." }`.
4. Renvoyer `(string AccessToken, string? AccountId)?` ou `null`.

### 2.3 Parsing des réponses de quotas (`UsageNotch.Core.Usage`)

Nouveau composant **`OpenAiSubscriptionParser`** :
- Reçoit le corps JSON de `https://chatgpt.com/backend-api/wham/usage` et l'horodatage courant `now`.
- Extrait :
  1. `rate_limit.primary_window` :
     - `used_percent` : si > 1.0, normalisé en `used_percent / 100.0`. Borné entre 0.0 et 1.0.
     - `reset_at` ou `reset_after_seconds` : calcul du `DateTimeOffset` de réinitialisation.
     - Produit `LimitWindow("session", "Session 5h", usedFraction, resetsAt)`.
  2. `rate_limit.secondary_window` :
     - Produit `LimitWindow("weekly", "Quota hebdomadaire", usedFraction, resetsAt)`.
  3. `additional_rate_limits` :
     - Parcourt la liste des limites additionnelles (ex. `codex-spark` ou o1/o3).
     - Si présent : extrait la fraction et produit `LimitWindow("reasoning_models", "Modèles raisonnement", usedFraction, resetsAt)`.
     - Si absent : produit une fenêtre à 0.0 réinitialisée avec la session ou hebdo.

### 2.4 Fournisseur unifié `OpenAiUsageProvider`

`OpenAiUsageProvider` aiguille selon `Settings.OpenAiMode` :
- Si `OpenAiMode == "api"` :
  - Exécute le flux existant (clé d'API, `/v1/organization/costs`, `OpenAiCostParser`).
- Si `OpenAiMode == "subscription"` :
  - Lit les identifiants via `OpenAiSubscriptionCredentialReader`.
  - Si aucun token : renvoie `FetchResult.NeedsAuth("Aucun token de session OpenAI/ChatGPT trouvé — connectez-vous avec Codex CLI ou renseignez votre token dans les réglages.")`.
  - Appelle `GET https://chatgpt.com/backend-api/wham/usage` avec :
    - `Authorization: Bearer <access_token>`
    - `ChatGPT-Account-Id: <account_id>` (si présent)
    - `User-Agent: UsageNotch/1.0`
  - Si 401 : `FetchResult.NeedsAuth("Session OpenAI/ChatGPT expirée ou invalide — veuillez vous reconnecter.")`.
  - Si 429 : `FetchResult.RateLimited(...)`.
  - Si 200 : parse via `OpenAiSubscriptionParser` et renvoie `FetchResult.Success(windows)`.

### 2.5 Anneaux & Visualisation (`RingWindows.cs`)

Harmonisation de `RingWindows.OpenAi` pour matcher les deux modes :
- **Ring 1 (Extérieur)** : `["session", "five_hour", "primary_window", "monthly_cost", "monthly", "budget"]`
- **Ring 2 (Médian)** : `["weekly_all", "seven_day", "weekly", "secondary_window", "daily_cost", "daily", "day"]`
- **Ring 3 (Intérieur)** : `["reasoning_models", "o1_o3", "reasoning", "codex-spark", "additional_limits"]`

En mode Abonnement :
- Ring 1 correspond à `session` (Session 5h, affiché dans la pilule).
- Ring 2 correspond à `weekly` (Quota hebdomadaire).
- Ring 3 correspond à `reasoning_models` (Modèles de raisonnement).

### 2.6 Interface de Préférences (`AppearancePage.xaml` & ViewModel)

- Ajout de `OpenAiModes` dans `Choices.cs` :
  - `"subscription"` : « Abonnement (ChatGPT / Codex) »
  - `"api"` : « Clé API (Organisation) »
- Dans `AppearancePageViewModel.cs` :
  - `OpenAiMode` (getter/setter lié au draft).
  - `IsOpenAiSubscriptionMode => OpenAiMode == "subscription"`.
  - `IsOpenAiApiMode => OpenAiMode == "api"`.
  - `OpenAiSessionToken` (getter/setter lié au draft).
- Dans `AppearancePage.xaml` :
  - ComboBox pour le choix du mode OpenAI.
  - Panneau contextuel sous condition :
    - Si Abonnement : champ `OpenAiSessionToken` + note explicative.
    - Si Clé API : champ `OpenAiApiKey` + curseur de budget mensuel.

---

## 3. Stratégie de Test (TDD)

1. **`OpenAiSubscriptionCredentialReaderTests`** :
   - Lecture depuis `Settings.OpenAiSessionToken`.
   - Lecture depuis `~/.codex/auth.json` (avec jeton et account_id).
   - Gestion des fichiers absents, corrompus ou sans tokens.
2. **`OpenAiSubscriptionParserTests`** :
   - Payload complet (5h + hebdo + additional reasoning).
   - Payload sans `additional_rate_limits`.
   - Parsing des pourcentages (format 0-100 et format 0.0-1.0).
   - Parsing des reset timestamps (`reset_at` unix epoch vs `reset_after_seconds`).
3. **`OpenAiUsageProviderTests`** :
   - Aiguillage vers subscription vs API selon `Settings.OpenAiMode`.
   - Gestion HTTP 200, 401, 429, erreurs réseau.
4. **`AppearancePageViewModelTests`** :
   - Bascule de mode, visibilité des contrôles, persistance des propriétés.
5. **Exécution de l'intégralité des tests de la solution** :
   - 0 régression, 0 avertissement de compilation.
