# Spec de conception — Suivi de consommation OpenAI et Capsule Triple

## 1. Contexte & Objectifs

Après l'intégration réussie de Google Antigravity et de la capsule double unifiée (Option A), UsageNotch accueille un troisième fournisseur majeur : **OpenAI**.

Les développeurs utilisent fréquemment plusieurs assistants et modèles d'IA en parallèle (Claude Code pour l'orchestration de code, Google Antigravity / Gemini pour les modèles à longue fenêtre de contexte, et les modèles OpenAI comme GPT-4o, o1 et o3-mini pour le raisonnement approfondi ou les outils compatibles OpenAI).

### Objectifs :
1. **Fournisseur `OpenAiUsageProvider`** : Récupérer les métriques d'usage et de coût de la plateforme OpenAI via clé d'API (variable d'environnement `OPENAI_API_KEY` ou réglages).
2. **Indicateurs des 3 anneaux** :
   - **Anneau extérieur** : Budget mensuel ($ consommés vs budget mensuel configuré).
   - **Anneau intermédiaire** : Consommation journalière ($ consommés sur la journée UTC en cours).
   - **Anneau intérieur** : Quota / consommation des modèles de raisonnement (série o1 / o3).
3. **Capsule triple complète** :
   - Affichage côte à côte de 1, 2 ou 3 cellules dans une capsule unifiée dynamique.
   - Longueurs adaptées :
     - 1 cellule : corps 100 DIP / fenêtre 132 DIP (horizontal, Anneau + pourcentage).
     - 2 cellules : corps 196 DIP / fenêtre 228 DIP (horizontal, Anneau + pourcentage).
     - 3 cellules : corps 300 DIP / fenêtre 332 DIP (horizontal, Anneau + pourcentage).
     - Déclinaisons correspondantes en `RingOnly` et en orientation verticale (`Left`, `Right`).
4. **Sélecteur de fournisseurs flexible** :
   - `"all"` / `"triple"` : Les trois (capsule triple — Claude + Antigravity + OpenAI).
   - `"both"` : Claude et Google Antigravity (capsule double).
   - `"claude_openai"` : Claude et OpenAI (capsule double).
   - `"antigravity_openai"` : Google Antigravity et OpenAI (capsule double).
   - `"claude"` : Claude uniquement.
   - `"antigravity"` : Google Antigravity uniquement.
   - `"openai"` : OpenAI uniquement.
5. **Carte de détail** :
   - Section OpenAI dédiée avec ses 3 jauges colorées, montants en dollars / pourcentages et informations de réinitialisation.
6. **Mode démo (`--demo`)** :
   - Simulation fidèle des quotas OpenAI (ex. 42 % budget mensuel, 18 % jour, 65 % modèles de raisonnement).

---

## 2. Architecture & Composants

### 2.1 Couche Core (`UsageNotch.Core`)

1. **`OpenAiCredentialReader`** :
   - Lit la clé d'API depuis `Settings.OpenAiApiKey` si renseignée, sinon depuis la variable d'environnement `OPENAI_API_KEY`.
2. **`OpenAiUsageProvider : IUsageProvider`** :
   - `Id` : `"openai"`
   - `DisplayName` : `"OpenAI"`
   - `RingWindowIds` :
     - `["monthly_cost"]` : « Budget mensuel »
     - `["daily_cost"]` : « Consommation journalière »
     - `["reasoning_models"]` : « Modèles raisonnement (o1/o3) »
   - Méthode `FetchAsync` :
     - Appel HTTP vers l'API OpenAI Administration / Costs (`https://api.openai.com/v1/organization/costs`).
     - Si aucune clé n'est configurée : renvoie un statut explicatif (`SnapshotStatus.Ok` avec message d'information ou avertissement sans crash).
     - Calcule la fraction du budget mensuel à partir de `Settings.OpenAiMonthlyBudget` (défaut : 20.0 $).
     - Calcule la consommation du jour UTC.
     - Extrait le coût / volume imputé aux modèles de raisonnement (`o1`, `o1-mini`, `o1-preview`, `o3-mini`).
3. **`Settings` & `SettingsStore`** :
   - Propriétés ajoutées :
     - `OpenAiApiKey` (string, défaut `""`)
     - `OpenAiMonthlyBudget` (double, défaut `20.0`)
   - Normalisation `Clamp()` autorisant les nouveaux modes de `Provider` :
     - `"all"`, `"both"`, `"claude_openai"`, `"antigravity_openai"`, `"claude"`, `"antigravity"`, `"openai"`.
4. **`UsagePoller`** :
   - Interroge tous les fournisseurs actifs déterminés par le mode de `Provider` (1, 2 ou 3 fournisseurs en parallèle avec backoffs isolés).

### 2.2 Couche Presentation (`UsageNotch.Presentation`)

1. **`RingWindows.OpenAi`** :
   - Définition des identifiants des fenêtres d'anneaux pour OpenAI.
2. **`PillMetrics`** :
   - Mise à jour de `WindowLengthFor` et `BodyLengthFor` pour supporter 1, 2 et 3 cellules :
     - Mode triple horizontal `RingAndPercent` : corps 300 DIP, fenêtre 332 DIP.
     - Mode triple vertical `RingAndPercent` : corps 258 DIP, fenêtre 290 DIP.
     - Mode triple `RingOnly` : corps 192 DIP, fenêtre 224 DIP.
3. **`PillModel`** :
   - Supporte $N$ cellules (`Cells`).
   - Accesseurs de confort : `Cell1`, `Cell2`, `Cell3`, `CellClaude`, `CellAntigravity`, `CellOpenAi`.
4. **`Choices.Providers`** :
   - Liste ordonnée et libellés français pour le ComboBox des réglages.
5. **`NotchViewModel`** :
   - Recombinaison dynamique de 1 à 3 cellules et de 1 à 3 sections selon le mode actif.
   - `TrayText` adapté (ex. « UsageNotch — Claude 73 % · Antigravity 65 % · OpenAI 42 % »).

### 2.3 Couche App (`UsageNotch.App`)

1. **`BrandGeometry`** :
   - Ajout de `OpenAiMark` transcrit depuis `src/UsageNotch.App/Assets/openai-light.svg` et gelé (`Geometry.Freeze()`).
   - `BrandGeometry.ForProvider("openai") => OpenAiMark`.
2. **`PillWindow.xaml` & `PillWindow.xaml.cs`** :
   - Ajout du support de la 3ᵉ cellule (`Cell3Stack`) et du second séparateur (`CellDivider2`).
   - Storyboards associés `Spin3` et `Pulse3`.
   - Disposition horizontale ou verticale dynamique des 3 cellules et des 2 séparateurs.
3. **`CardWindow.xaml`** :
   - Affichage fluide des sections via l'`ItemsControl` existant.
4. **`PillPreview.cs`** :
   - Rendu fidèle de l'aperçu statique (1, 2 ou 3 cellules avec séparateurs et logos respectifs).
5. **`DemoMode` & `AppHost`** :
   - Enregistrement de `OpenAiUsageProvider` (production) et `DemoUsageProvider("openai", time)` (démo).
   - Simulation en démo : 42 % mensuel, 18 % jour, 65 % raisonnement.

---

## 3. Rétrocompatibilité & Sécurité

- Si `settings.json` contient `"both"`, la capsule double existante (Claude + Antigravity) continue de s'afficher sans aucune régression.
- Le magasin `usage.json` accueille simplement la clé `"openai"` à côté de `"claude"` et `"antigravity"`.
- En l'absence de clé API OpenAI, l'application reste 100 % opérationnelle pour Claude et Antigravity.
