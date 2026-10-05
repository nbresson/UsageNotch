# UsageNotch — Guide de transition & reprise (HANDOFF)

> **Document destiné aux développeurs et agents d'IA prenant le relais sur le projet.**  
> Il résume l'état du système, les règles d'or de sécurité, l'architecture et les commandes indispensables pour être immédiatement opérationnel.

---

## 1. Règles d'or & Sécurité de l'environnement (CRITIQUE)

1. **Ne JAMAIS arrêter l'instance de production de l'utilisateur** :
   - L'utilisateur utilise UsageNotch au quotidien (`publish\UsageNotch.App.exe`).
   - Cette instance tourne sur le port `48666`, tient le mutex système `Local\UsageNotch`, et stocke ses données dans `%APPDATA%\UsageNotch`.
   - **Interdiction formelle de tuer ou fermer ce processus.**
2. **Toujours tester en mode démo (`--demo`)** :
   - Totalement isolé : port `48667`, mutex `Local\UsageNotch-demo`, données et journaux dans `%TEMP%\UsageNotch-demo`.
   - Ne touche pas aux clés de registre (désactive le démarrage avec Windows), n'appelle pas les API externes et n'interfère pas avec l'instance réelle.
3. **Langue de l'interface & conventions** :
   - **Interface utilisateur (UI)** : Tous les textes, libellés, infobulles, fenêtres et notifications sont **exclusivement en français**.
   - **Code & documentation interne** : Identifiants, commentaires et messages de commit (`type(scope): description`) en **anglais**.
4. **Qualité stricte** :
   - `TreatWarningsAsErrors=true` : aucun avertissement de compilation toléré.
   - 100 % des tests unitaires doivent être verts avant toute fusion sur `main`.

---

## 2. État courant du projet (au 2026-10-05)

- **Version en développement** : `0.6.0-dev` (branche `main`).
- **Dernière release officielle** : [v0.5.0](https://github.com/nbresson/UsageNotch/releases/tag/v0.5.0) publiée le 2026-10-05.
- **Tests** : **642 tests xUnit verts** (313 Core, 329 Presentation), durée totale d'exécution < 2 s.
- **Dernières fonctionnalités majeures livrées** :
  1. **Fournisseur OpenAI & Capsule Triple Unifiée** :
     - Suivi officiel des coûts & quotas OpenAI (`/v1/organization/costs` ou clé d'API / `OPENAI_API_KEY`) avec trois anneaux : Budget mensuel, Consommation du jour, et Modèles de raisonnement (o1 / o3).
     - Support complet de la capsule triple unifiée (Claude + Google Antigravity + OpenAI côte à côte) et combinaisons modulaires au choix : `"all"`, `"both"`, `"claude_openai"`, `"antigravity_openai"`, `"claude"`, `"antigravity"`, `"openai"`.
     - Intégration du logo vectoriel officiel `OpenAiMark` (`openai-light.svg`) au centre des anneaux avec animations d'activité (`Spin3`, `Pulse3`).
     - Carte de détail multi-sections jusqu'à 3 sections et configuration dédiée dans les préférences (clé d'API, curseur de budget mensuel).
  2. **Fournisseur Google Antigravity** : détection automatique du processus `agy.exe`, découverte du port d'écoute et du jeton SQLite/process, interrogation des quotas Gemini 5 h, hebdomadaire et modèles tiers.
  3. **Capsule double unifiée** : affichage simultané des quotas Claude et Antigravity dans une seule pilule dynamique avec séparateur.
  4. **Robustesse zone de notification** : gestion tolérante de l'initialisation de l'icône de barre des tâches (`TrayIconService`) évitant les crashs en environnement restreint.

---

## 3. Architecture des couches

Le projet est découpé en 4 composants hautement découplés :

```
┌─────────────────────────────────────────────────────────────┐
│                      UsageNotch.App                         │
│  Hôte WPF, fenêtres transparentes (PillWindow, CardWindow), │
│  rendu DirectX / RenderTargetBitmap, interop Win32, Tray    │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼                               ▼
┌─────────────────────────────┐  ┌────────────────────────────┐
│   UsageNotch.Presentation   │  │      UsageNotch.Core       │
│  MVVM pur, aucun type WPF,  │  │  Logique métier, magasins  │
│  PillModel, PillMetrics,    │  │  JSON tolérants, poller    │
│  Theme, CardPresenter       │  │  multi-fournisseurs        │
└──────────────┬──────────────┘  └─────────────┬──────────────┘
               │                               │
               └───────────────┬───────────────┘
                               │
                               ▼
                 ┌───────────────────────────┐
                 │      UsageNotch.Hook      │
                 │  Binaire Native AOT léger │
                 │  appelé par Claude Code   │
                 └───────────────────────────┘
```

### Détail des responsabilités :
- **`UsageNotch.Core`** (.NET 10, multiplateforme, sans dépendance UI) :
  - `UsageStore` : persistance atomique de l'usage dans `usage.json`, support multi-fournisseur (`SnapshotFor(id)`), migration transparente de l'ancien format.
  - `UsagePoller` : sondeur d'usage périodique (60 s en session active, 5 min au repos), politiques de backoff isolées par fournisseur, réveil immédiat sur `RequestRefresh()`.
  - `ClaudeUsageProvider` & `AntigravityUsageProvider` : récupération des quotas réels.
  - `SessionStore` & `HookListener` : réception des événements de sessions de travail (Claude Code) sur port HTTP local.
  - `SettingsStore` : chargement et sauvegarde normalisée (`Clamp()`) de `settings.json`.
- **`UsageNotch.Presentation`** (.NET 10, MVVM pur, testé en TDD à 100 %) :
  - `PillMetrics` : calcul exact des dimensions du corps et de la fenêtre (`WindowLengthFor`, `BodyLengthFor`) selon le fournisseur, le bord et le contenu.
  - `PillModel` & `CellModel` : modèles de rendu de la capsule (support simple et double capsule, anneaux, voyants d'activité).
  - `CardModel` & `CardPresenter` : carte de détail multi-sections (`Claude`, `Google Antigravity`, `Sessions actives`).
  - `NotchViewModel` : chef d'orchestre réactif reliant magasins, minuteur et présentation.
- **`UsageNotch.App`** (.NET 10 Windows Desktop, WPF) :
  - `PillWindow` : capsule ancrée aux bords d'écran (`Top`, `Bottom`, `Left`, `Right`), clics traversants (`AllowsTransparency`), storyboards dédiés (`Spin1/2`, `Pulse1/2`, `BandPulse`).
  - `CardWindow` : volet déroulant au survol/maintien, liste des sessions avec retour au terminal.
  - `PillPreview` : rendu vectoriel statique de la pilule dans la fenêtre des préférences.
  - `TrayIconService` : icône dynamique 32×32 dans la zone de notification avec menu contextuel complet.
- **`UsageNotch.Hook`** (Native AOT) :
  - Exécutable autonome ultra-rapide (< 10 ms) notifiant `HookListener` lors des événements de session.

---

## 4. Commandes de développement essentielles

Toutes les commandes s'exécutent depuis la racine du dépôt :

```powershell
# 1. Compiler la solution (vérification des avertissements)
dotnet build UsageNotch.sln

# 2. Exécuter tous les tests unitaires
dotnet test

# 3. Lancer l'application en mode DÉMO (fenêtre et capsule visibles)
dotnet run --project src/UsageNotch.App -- --demo

# 4. Ouvrir la fenêtre de réglages de la démo
Invoke-RestMethod -Uri "http://127.0.0.1:48667/open-settings" -Method Post

# 5. Simuler un événement de session (ex. attention requise)
Invoke-RestMethod -Uri "http://127.0.0.1:48667/event?e=attention&ppid=1" -Method Post `
  -Body '{"session_id":"demo-test","cwd":"C:\\src\\api","message":"Autoriser Bash ?"}'

# 6. Lancer le diagnostic système
dotnet run --project src/UsageNotch.App -- --doctor

# 7. Compiler en configuration Release
dotnet build -c Release UsageNotch.sln
```

---

## 5. Pièges connus & Bonnes pratiques d'implémentation

1. **Scoping des storyboards WPF dans `PillWindow`** :
   - Dans WPF, les storyboards déclarés dans `Window.Resources` ne peuvent pas cibler des éléments générés dynamiquement dans un `ItemsControl`.
   - La pilule utilise donc trois stacks explicites (`Cell1Stack`, `Cell2Stack`, `Cell3Stack`), deux séparateurs (`CellDivider`, `CellDivider2`) et des storyboards triplés (`Spin1/2/3`, `Pulse1/2/3`).
2. **Dimensionnement de la pilule** :
   - Ne jamais coder en dur la longueur de la fenêtre ou du corps de la pilule. Toujours passer par `PillMetrics.WindowLengthFor(provider, edge, content)` et `PillMetrics.BodyLengthFor(provider, edge, content)` qui supportent dynamiquement 1, 2 ou 3 cellules.
   - Épaisseurs standard : 32 DIP (corps), 48 DIP (fenêtre avec marge d'ombrage et congés).
3. **Consommation CPU des fenêtres transparentes** :
   - Les fenêtres WPF en couches (`AllowsTransparency = true`) effectuent leur composition logicielle en mémoire.
   - Les storyboards en boucle ne doivent **tourner que si l'élément visuel est effectivement visible à l'écran**.
4. **Relecture des réglages à chaud (`SettingsFileWatcher`)** :
   - Toute modification de `settings.json` sur le disque est automatiquement détectée et appliquée sans redémarrer l'application.
5. **Encodage de fichiers sous Windows PowerShell** :
   - Fichiers source C# et Markdown : **UTF-8 sans BOM**.
   - Scripts PowerShell 5.1 contenant des caractères non-ASCII (accents, tirets cadratins `—`) : **UTF-8 avec BOM**, sous peine de corruption de chaîne par le runtime PowerShell classique.

---

## 6. Carte de la documentation

| Document | Rôle & Contenu |
|---|---|
| `README.md` | Présentation générale du projet pour l'utilisateur final. |
| `HANDOFF.md` *(ce fichier)* | Guide de reprise rapide et synthèse technique. |
| `docs/REPRISE.md` | Historique de pause et journal de bord de l'architecture initiale. |
| `docs/superpowers/specs/` | Spécifications fonctionnelles de conception (conception d'origine, anneaux, logos, Antigravity, capsule double, OpenAI). |
| `docs/superpowers/plans/` | Plans d'implémentation détaillés étape par étape. |

---

## 7. Pistes de travail futures (Backlog)

- **Packaging & Déploiement** : script d'installation MSIX ou installateur InnoSetup pour faciliter la mise à jour par l'utilisateur.
- **Notifications avancées** : réglage fin des seuils d'alerte par fournisseur (ex. alerte spécifique pour la limite 5h de Gemini distincte de la session Claude ou du budget OpenAI).
- **Indicateurs de consommation supplémentaires pour OpenAI** : support de métriques temps réel additionnelles (ex. requêtes TPM/RPM si l'API organisation les expose par clé).
- **Historique et graphiques de consommation** : vue chronologique dans une sous-page des réglages pour analyser l'évolution de la consommation au fil des jours.

