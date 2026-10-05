# Spécification technique — Capsule double & affichage simultané Claude + Antigravity

**Date :** 2026-10-05  
**Projet :** UsageNotch  
**Statut :** Brouillon pour validation  

---

## 1. Contexte et Objectifs

UsageNotch prend en charge deux fournisseurs d'usage :
1. **Claude (Anthropic)** : lecture via l'API Anthropic à partir des identifiants locaux de Claude Code, et suivi temps réel des sessions via les hooks CLI.
2. **Google Antigravity** : lecture locale via le hub ConnectRPC d'`agy.exe`, découvrant automatiquement le port et le jeton CSRF.

Actuellement, l'utilisateur doit choisir dans les réglages entre Claude **ou** Antigravity.  
L'objectif de cette évolution est de permettre l'affichage **simultané** des deux fournisseurs au sein d'une **capsule unique unifiée (Option A)** :
- **Sur la pilule** : deux cellules compactes côte à côte (ou empilées selon le bord d'écran), chacune avec ses 3 anneaux concentriques, son logo vectoriel (Anthropic / Gemini) et son voyant d'activité.
- **Sur la carte de détail** : deux sections dédiées (« Claude » et « Google Antigravity ») présentant leurs barres de quotas respectives et les sessions en cours.
- **Dans les réglages** : choix flexible sous « Fournisseur d'usage » entre :
  - *« Les deux (capsule double) »* (nouveau mode par défaut ou disponible)
  - *« Claude (Anthropic) »* (mode simple historique)
  - *« Google Antigravity »* (mode simple Antigravity)

---

## 2. Expérience Utilisateur et Rendu Visuel

### 2.1 La pilule (capsule unifiée)

La capsule s'adapte à l'orientation du bord d'écran (`ScreenEdge`) et au mode de contenu (`CellContent`).

#### Disposition horizontale (Bord Haut / Bord Bas)
Les deux cellules sont agencées horizontalement :
```
+-------------------------------------------------------------------------------+
|  ( Congé )   [  Claude  ]      [ Séparateur ]      [ Antigravity ]   ( Congé )|
|               3 anneaux                               3 anneaux               |
|               + logo ⚹                                + étoile ✦              |
|               + 73 %                                  + 65 %                  |
+-------------------------------------------------------------------------------+
```
- **Cellule Claude** : 3 anneaux (Session 5h, Hebdo tous modèles, Hebdo Opus/modèle), logo Anthropic central, pourcentage optionnel.
- **Séparateur discret** : espace de 10 DIP ou trait fin vertical de couleur `Theme.RingTrack`.
- **Cellule Antigravity** : 3 anneaux (Gemini 5h, Gemini hebdo, Modèles tiers), étoile Gemini centrale, pourcentage optionnel.

#### Disposition verticale (Bord Gauche / Bord Droite)
Les deux cellules sont empilées verticalement le long du bord de l'écran :
- Cellule supérieure : Claude
- Séparateur horizontal discret
- Cellule inférieure : Antigravity

#### Dimensions logiques (DIP) à échelle 100 %
- **Épaisseur (`Thickness`)** : 64 DIP (inchangée, profondeur constante vers l'intérieur de l'écran).
- **Congés (`Fillet`) & Rayon (`CornerRadius`)** : 16 DIP (inchangés).
- **Longueur du corps (`BodyLength`)** :
  - En mode simple : 104 DIP (`WindowLength = 136 DIP`).
  - En mode double avec pourcentages (`RingAndPercent`) :
    - Horizontal : `56 (anneaux) + 4 + 32 (texte) + 12 (séparateur) + 56 + 4 + 32 = 196 DIP` (`WindowLength = 228 DIP`).
    - Vertical : `56 (anneaux) + 4 + 18 (texte) + 12 (séparateur) + 56 + 4 + 18 = 168 DIP` (`WindowLength = 200 DIP`).
  - En mode double sans pourcentages (`RingOnly`) :
    - `56 + 12 + 56 = 124 DIP` (`WindowLength = 156 DIP`).

#### Mode Replié (`VisibilityMode.Folded`)
- La bande repliée glisse au bord de l'écran sur toute la longueur du corps.
- **Couleur d'attention prioritaire** : si l'une des deux cellules est à l'état `Attention` (demande d'autorisation), la bande entière pulse en ambre (`Theme.Attention`).
- En dehors d'une attention, la bande affiche la couleur de l'anneau le plus sollicité ou une division subtile 50/50.

#### Animations d'activité
Chaque cellule possède son propre glyphe central animé indépendamment :
- **Claude** : rotation en cours d'exécution CLI, pulsation ambre en attente, terracotta `#D97757` une fois terminé.
- **Antigravity** : bleu Google `#1A73E8` une fois terminé, neutre au repos.

---

### 2.2 La carte de détail (`CardWindow`)

La carte s'ouvre au survol de la pilule et structure l'information en deux sections bien identifiées :
```
+-------------------------------------------------------+
|  Usage & Quotas                                       |
|                                                       |
|  CLAUDE (ANTHROPIC)             Mis à jour il y a 2 min|
|  Session en cours               Réinitialisation 14:15|
|  [=======================>            ] 73 % utilisé  |
|  Hebdomadaire (tous modèles)    Réinitialisation ven. |
|  [=======>                            ] 21 % utilisé  |
|                                                       |
|  GOOGLE ANTIGRAVITY             Mis à jour il y a 1 min|
|  Modèles Gemini (5 h)           Réinitialisation 16:30|
|  [=====================>              ] 65 % utilisé  |
|  Modèles Gemini (hebdomadaire)  Réinitialisation lun. |
|  [=========>                          ] 29 % utilisé  |
|                                                       |
|  SESSIONS ACTIVES                                     |
|  ● proj · abcd              🔧 Bash : dotnet test     |
|  ● web · fix                Attend votre réponse      |
+-------------------------------------------------------+
```
- Largeur de carte : 320 DIP (légèrement élargie de 300 à 320 pour aérer les libellés).
- Si un des deux services est inaccessible (ex. Antigravity non lancé ou session Claude expirée), sa section affiche la note explicative claire sans perturber l'autre section.

---

## 3. Architecture Technique

### 3.1 Couche Core (`UsageNotch.Core`)

1. **`Settings.Provider`** :
   - Valeurs autorisées : `"both"` (défaut ou sélectionnable), `"claude"`, `"antigravity"`.
   - `Settings.Clamp()` accepte ces 3 valeurs et replie les inconnues sur `"both"`.

2. **Magasin de données multi-fournisseurs (`UsageStore`)** :
   - Stocke les lectures par fournisseur : `IReadOnlyDictionary<string, UsageSnapshot>`.
   - Fournit `SnapshotFor(string providerId)` qui renvoie le snapshot d'un fournisseur ou `UsageSnapshot.Empty`.
   - Conserve la persistance dans `usage.json` sous forme structurée :
     ```json
     {
       "claude": { "status": "Ok", "windows": [...], ... },
       "antigravity": { "status": "Ok", "windows": [...], ... }
     }
     ```
   - Rétro-compatibilité : un ancien `usage.json` sans clés de fournisseur est automatiquement importé comme snapshot de Claude.

3. **Boucle de scrutation parallèle (`UsagePoller`)** :
   - Le poller maintient une politique de backoff et un cycle de rafraîchissement indépendants pour chaque fournisseur activé.
   - Si Antigravity n'est pas ouvert, son état `NeedsAuth` n'impose aucun backoff à Claude.
   - `RequestRefresh()` force la relecture immédiate de tous les fournisseurs actifs.

---

### 3.2 Couche Présentation (`UsageNotch.Presentation`)

1. **Modèle de cellule & pilule** :
   - `CellModel` conserve sa structure par cellule (Rings, Text, Activity, ActivityColor, ProviderId).
   - `PillModel` regroupe `IReadOnlyList<CellModel> Cells`, `double BodyLength`, `double WindowLength`, `string BandColor`.
   - `PillPresenter.BuildPill(...)` génère :
     - 1 cellule si `Provider == "claude"` ou `"antigravity"`.
     - 2 cellules si `Provider == "both"`.

2. **Modèle de carte (`CardModel`)** :
   - `CardSection(string ProviderId, string Title, string? Subtitle, IReadOnlyList<WindowRow> Windows, string? Note)`
   - `CardModel(IReadOnlyList<CardSection> Sections, IReadOnlyList<SessionRow> Sessions)`
   - Rétro-compatibilité de `CardModel` via des propriétés d'accès direct `Title => Sections[0].Title`, etc.

3. **Choix dans les préférences (`Choices.cs` & `AppearancePageViewModel.cs`)** :
   - `Choices.Providers` :
     - `new("both", "Les deux (capsule double)")`
     - `new("claude", "Claude uniquement")`
     - `new("antigravity", "Google Antigravity uniquement")`

---

### 3.3 Couche Application WPF (`UsageNotch.App`)

1. **Calcul dynamique de la taille de fenêtre (`NotchPlacer`)** :
   - `NotchPlacer.Compute` calcule `length` à partir de la longueur du modèle de pilule (`PillModel.WindowLength * scale * monitorScale`), permettant à la capsule de s'agrandir automatiquement en mode double.

2. **Contrôle WPF de la pilule (`PillWindow.xaml`)** :
   - Le `Body` accueille les deux cellules via un `ItemsControl` (ou deux slots dédiés `CellLeft` / `CellRight`) avec un séparateur central élégant.
   - Chaque cellule gère ses 3 `ProgressRing`, son glyphe central (`AnthropicMark` ou `GeminiMark`) et ses animations.

3. **Aperçu des réglages (`PillPreview.cs`)** :
   - Affiche les exemples avec la capsule double lorsque `"both"` est sélectionné.

---

## 4. Plan de tests (TDD)

1. **Core** :
   - `UsageStoreTests` : sérialisation, désérialisation multi-providers, rétro-compatibilité ancien format monobloc.
   - `UsagePollerTests` : scrutation conjointe de deux fournisseurs, backoff isolé en cas d'erreur sur l'un d'eux.
   - `SettingsTests` : validation de `Provider = "both"`, rétro-compatibilité.

2. **Presentation** :
   - `PillPresenterTests` : génération de 2 `CellModel` en mode `"both"` avec les longueurs et anneaux appropriés selon le bord (`Top` vs `Right`).
   - `CardPresenterTests` : génération des deux sections avec leurs barres respectives.
   - `AppearancePageViewModelTests` : sélection de `"both"` et notification.

3. **App & Démo** :
   - Mode démo (`--demo`) : simulation simultanée des quotas Claude (73 %) et Antigravity (65 %) dans la capsule double.
   - Tests en conditions réelles avec `agy.exe` et Claude Code.

---

## 5. Décisions et Arbitrages

1. **Ordre d'affichage des cellules** :
   - Convention : **Claude en premier** (à gauche ou en haut), **Antigravity en second** (à droite ou en bas).
2. **Priorité d'attention sur la bande repliée** :
   - Si une session demande une validation (attention), la bande entière adopte la couleur ambre d'attention pour garantir la détection par l'utilisateur.
3. **Largeur de la carte** :
   - Fixée à 320 DIP pour accueillir confortablement les deux sections sans dépasser de l'écran.
