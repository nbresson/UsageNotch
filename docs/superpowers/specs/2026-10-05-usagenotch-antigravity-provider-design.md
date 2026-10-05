# UsageNotch — Suivi de l'usage Google Antigravity

Date : 2026-10-05  
Statut : Validé en conception, prêt pour le plan d'implémentation.

---

## 1. En une phrase

UsageNotch s'ouvre au multi-fournisseur en intégrant **Google Antigravity** aux côtés de Claude : interrogation de l'API ConnectRPC locale du hub `agy.exe`, report direct des quotas Gemini sur les trois anneaux de la pilule, sélection du fournisseur dans les réglages et logo Gemini dynamique au centre de la pile.

---

## 2. Origine et contexte

Le projet UsageNotch a été architecturé dès le Plan 1 autour de l'interface `IUsageProvider` (`UsageNotch.Core.Usage`), prévoyant l'arrivée future d'autres outils d'assistance (Codex, Cursor, etc.). Cependant, la version actuelle 0.4.0 est encore strictement couplée à Claude :
- lecture du jeton OAuth dans `%USERPROFILE%\.claude\.credentials.json` ;
- interrogation de l'endpoint distant non documenté `https://api.anthropic.com/api/oauth/usage` ;
- alias d'anneaux figés sur `RingWindows.Claude` ;
- logo Anthropic gelé dans `BrandGeometry` au centre de la pilule ;
- page de réglages dédiée « Claude Code ».

Google Antigravity étant un environnement de développement agentique majeur (CLI `agy` et application de bureau), ses utilisateurs sont soumis à des quotas précis (fenêtre glissante de 5 heures, limite hebdomadaire globale et limite dédiée aux modèles tiers 3P). Disposer de ces métriques directement sur l'encoche Windows, sans avoir à taper `/usage` ou `/quota` dans le terminal, répond exactement à la promesse d'UsageNotch.

Les tests d'investigation menés sur le système hôte confirment que l'environnement Antigravity expose en boucle locale (`127.0.0.1`) sur son hub `agy.exe` une API ConnectRPC renvoyant des métriques d'usage structurées et complètes.

---

## 3. Décisions arrêtées

| # | Question | Décision | Raison |
|---|---|---|---|
| 1 | **Source des quotas** | Interroger le hub local `agy.exe` via son endpoint ConnectRPC HTTP local. | Aucun appel distant, aucun secret ni jeton externe à gérer. Antigravity gère lui-même son authentification et rafraîchit ses quotas ; les données sont locales, ultra-rapides et identiques à celles affichées par la commande `/usage`. |
| 2 | **Découverte du port et du jeton CSRF** | Inspecter la ligne de commande du processus `agy.exe` actif (`--hub-port` et `--csrf_token`), avec mise en cache du couple (port, token). | Le port et le jeton changent à chaque lancement d'Antigravity. Cette détection est transparente pour l'utilisateur, robuste et ne requiert aucune configuration manuelle. |
| 3 | **Correspondance des anneaux** | • Extérieur : `gemini-5h` (limite 5h Gemini)<br>• Milieu : `gemini-weekly` (limite hebdomadaire Gemini)<br>• Intérieur : `3p-weekly` ou `3p-5h` (limite modèles tiers Claude/GPT) | Correspondance exacte avec les trois anneaux de la pilule (session/court terme, hebdomadaire global, spécifique par modèle). |
| 4 | **Calcul du taux d'usage** | $\text{UsedPercent} = (1.0 - \text{remainingFraction}) \times 100.0$ | Antigravity fournit la fraction restante (de `0.0` à `1.0`) ; UsageNotch affiche le pourcentage consommé (de `0 %` à `100 %`). |
| 5 | **Sélection du fournisseur** | Nouveau paramètre `Provider` (`"claude"` ou `"antigravity"`) dans `Settings`, avec sélecteur dans l'onglet Apparence des Réglages. | Préserve la taille fixe et épurée de la pilule (64 × 136 DIP). L'utilisateur surveille le fournisseur avec lequel il travaille. Une pilule multiple ou combinée reste une extension ultérieure distincte. |
| 6 | **Logo central dynamique** | Charger dynamiquement le glyphe dans la pilule et l'aperçu :<br>• Claude : marque Anthropic (`Assets/anthropic.svg`)<br>• Antigravity : étincelle Gemini à 4 branches (`Assets/gemini.svg`) | Le glyphe central indique immédiatement à quel outil se rapportent les anneaux. |
| 7 | **Couleur de marque (`LogoDone`)** | • Claude : Terracotta `#D97757`<br>• Antigravity : Bleu Gemini `#1A73E8` (Thème Codenotch) / Gris clair `#B0B0B0` (Monochrome) | Respect de la charte visuelle établie au Plan 5 : l'état terminé arbore la couleur distinctive du fournisseur. |
| 8 | **Activité des sessions** | Découplée du suivi de quota dans un premier temps. Si Antigravity tourne sans session active détectée, l'état reste au repos (`ActivityKind.None`, glyphe en gris de piste). Le suivi de session fera l'objet d'un sous-chantier dédié. | Permet de livrer immédiatement une surveillance de quota fiable sans introduire de couplage risqué avec le système de plugins Antigravity. |

---

## 4. Protocole d'interrogation du hub Antigravity

### 4.1 Découverte du processus hôte
Lorsque Antigravity est ouvert, un processus `agy.exe` tourne avec la ligne de commande suivante :
```text
"...\agy.exe" --app_data_dir=antigravity --hub --hub-port=37588 --csrf_token=c47a09fb50f044359d9b8c7ba430adcd ...
```

Le service de découverte (`AntigravityProcessDiscovery`) :
1. Tente d'abord de réutiliser le dernier port et jeton CSRF connus en mémoire.
2. En cas d'échec ou au premier appel, énumère les processus nommés `agy` (ou `agy.exe`).
3. Extrait les paramètres `--hub-port=(\d+)` et `--csrf_token=([a-f0-9]+)` via regex sur la ligne de commande obtenue via WMI (`Win32_Process`) ou via `NtQueryInformationProcess`.
4. Si le processus est introuvable, renvoie un statut explicite : *« Antigravity non détecté — lancez Antigravity pour lire l'usage. »*

### 4.2 Requête ConnectRPC HTTP
Une fois le port et le jeton obtenus, le fournisseur effectue un appel HTTP POST :
- **URL** : `http://127.0.0.1:<hub-port>/exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary`
- **Méthode** : `POST`
- **En-têtes** :
  - `Content-Type: application/json`
  - `x-codeium-csrf-token: <csrf_token>`
- **Corps** : `{"forceRefresh": true}`

### 4.3 Structure de la réponse et conversion
La réponse JSON contient les groupes et les buckets :
```json
{
  "response": {
    "groups": [
      {
        "displayName": "Gemini Models",
        "buckets": [
          {
            "bucketId": "gemini-5h",
            "displayName": "Five Hour Limit Remaining",
            "window": "5h",
            "remainingFraction": 0.3529,
            "resetTime": "2026-10-05T17:50:04Z"
          },
          {
            "bucketId": "gemini-weekly",
            "displayName": "Weekly Limit Remaining",
            "window": "weekly",
            "remainingFraction": 0.7128,
            "resetTime": "2026-10-10T10:44:30Z"
          }
        ]
      },
      {
        "displayName": "Claude and GPT models",
        "buckets": [
          {
            "bucketId": "3p-weekly",
            "displayName": "Weekly Limit Remaining",
            "window": "weekly",
            "remainingFraction": 1.0,
            "resetTime": "2026-10-12T17:29:46Z"
          }
        ]
      }
    ]
  }
}
```

Pour chaque bucket, `AntigravityUsageParser` produit un `LimitWindow` :
- `kind` : le `bucketId` (ex. `"gemini-5h"`, `"gemini-weekly"`, `"3p-weekly"`) ;
- `label` : le `displayName` nettoyé en français (ex. *« Modèles Gemini (5 h) »*, *« Modèles Gemini (hebdomadaire) »*, *« Modèles tiers (hebdomadaire) »*) ;
- `usedPercent` : `Math.Clamp((1.0 - remainingFraction) * 100.0, 0.0, 100.0)` ;
- `resetsAt` : converti depuis la chaîne ISO 8601 UTC `resetTime`.

---

## 5. Intégration dans l'architecture

### 5.1 Couche Core (`UsageNotch.Core`)
1. **`RingWindows.cs`** :
   Ajout de la définition des alias pour Antigravity :
   ```csharp
   public static readonly IReadOnlyList<IReadOnlyList<string>> Antigravity =
   [
       ["gemini-5h", "5h"],
       ["gemini-weekly", "weekly"],
       ["3p-weekly", "3p-5h", "models_3p"]
   ];
   ```

2. **`AntigravityUsageProvider.cs`** (implémente `IUsageProvider`) :
   - `Id` : `"antigravity"`
   - `DisplayName` : `"Google Antigravity"`
   - `RingWindowIds` : `RingWindows.Antigravity`
   - `FetchAsync(CancellationToken ct)` : utilise `AntigravityProcessDiscovery` puis `HttpClient` vers le endpoint local.

3. **`UsageProviderFactory` ou Registre** :
   Permet d'injecter ou de sélectionner le fournisseur actif (`IUsageProvider`) selon `Settings.Provider`.

4. **`Settings.cs`** :
   - Ajout d'une propriété `public string Provider { get; init; } = "claude";`
   - Dans `Settings.Clamp()` : valide que `Provider` vaut `"claude"` ou `"antigravity"`, repli sur `"claude"`.

### 5.2 Couche Présentation (`UsageNotch.Presentation`)
1. **`AppearancePageViewModel.cs`** :
   - Exposition du choix du fournisseur (`ProviderChoices`) avec libellés conviviaux (*« Claude (Anthropic) »*, *« Google Antigravity »*).
   - Liaison bidirectionnelle avec sauvegarde automatique.
2. **`FrenchText.cs`** :
   - Textes et libellés français pour les fenêtres et statuts spécifiques à Antigravity.
3. **`PillPresenter` & `CardPresenter`** :
   - Reçoivent l'identifiant du fournisseur actif pour adapter le titre de la carte (*« Usage Claude »* vs *« Usage Google Antigravity »*) et la géométrie de marque.

### 5.3 Couche Application WPF (`UsageNotch.App`)
1. **`Assets/gemini.svg` & `BrandGeometry.cs`** :
   - Transcription du symbole Gemini (étoile à 4 pointes régulière avec courbure concave) en `Geometry` gelée.
   - `BrandGeometry.ForProvider(string providerId)` renvoie la `Geometry` correspondante (Anthropic ou Gemini).
2. **`PillWindow.xaml` & `PillPreview.cs`** :
   - Remplacement de la liaison statique `BrandGeometry.Mark` par une propriété dynamique liée au fournisseur courant.
3. **Interop Windows (`AntigravityProcessDiscovery`)** :
   - Utilisation de WMI (`System.Management` ou `Get-CimInstance`) ou P/Invoke `NtQueryInformationProcess` pour récupérer la ligne de commande du processus `agy.exe`.

---

## 6. Comportements aux limites et erreurs

- **Antigravity non lancé** :  
  L'application ne crash pas. La méthode `FetchAsync` renvoie `FetchResult.NeedsAuth("Antigravity n'est pas ouvert.")` ou `FetchResult.Failed("Antigravity non détecté")`. Les anneaux s'affichent en couleur de piste neutre (`RingTrack`), et la carte indique clairement la situation.
- **Redémarrage d'Antigravity (changement de port ou de token)** :  
  Si une requête échoue avec un code 401 (jeton CSRF périmé) ou une erreur de connexion réseau (port changé), le cache de découverte est invalidé immédiatement et une nouvelle inspection des processus est tentée dès la passe suivante.
- **Absence de quota pour un groupe** :  
  Si un compte n'a pas accès aux modèles tiers 3P (`buckets` vide pour ce groupe), l'anneau intérieur reste en couleur de piste neutre, conformément à la règle arrêtée au Plan 4 (pas de décalage des autres anneaux).

---

## 7. Plan de tests (TDD)

### 7.1 Tests Core (`UsageNotch.Core.Tests`)
- `AntigravityUsageParserTests` :
  - Décodage correct des quotas 5h, hebdomadaire et 3P.
  - Calcul du pourcentage consommé à partir de `remainingFraction`.
  - Gestion d'un JSON sans buckets ou avec des champs manquants.
  - Conversion des dates UTC ISO 8601 en `DateTimeOffset`.
- `AntigravityUsageProviderTests` :
  - Simulation HTTP 200 avec payload valide.
  - Gestion des codes HTTP 401 (CSRF expiré), 500, et des erreurs de connexion.
- `SettingsTests` :
  - Sérialisation et désérialisation de `provider`.
  - Repli par défaut sur `"claude"` pour les anciens fichiers `settings.json`.

### 7.2 Tests Presentation (`UsageNotch.Presentation.Tests`)
- `CardPresenterTests` :
  - Titre de la carte et libellés adaptés au fournisseur Antigravity.
- `AppearancePageViewModelTests` :
  - Sélection du fournisseur et notification de modification.

---

## 8. Vérification à l'exécution

1. **Mode démo (`--demo`)** :
   - Ajout de données de test Antigravity simulées pour valider l'affichage de la pilule et de la carte sans impacter l'instance de production.
2. **Exécution réelle** :
   - Lancement avec Antigravity actif sur la machine locale.
   - Vérification de la bonne lecture des anneaux (5h et hebdomadaire).
   - Bascule fluide entre Claude et Antigravity depuis la fenêtre des réglages sans redémarrage.
   - Vérification visuelle du logo Gemini centré dans la pilule.

---

## 9. Hors périmètre

- **Surveillance simultanée de Claude et Antigravity dans deux pilules distinctes** : ferait l'objet d'une conception dédiée sur le placement multi-instances.
- **Hooks d'activité interactifs pour Antigravity** : suivi avancé des sessions (en cours / en attente / terminé) via un plugin Antigravity complet, réservé pour un sous-chantier ultérieur. La présente spec couvre en priorité le **suivi de consommation et de quotas**.

