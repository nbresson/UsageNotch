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

## 2. État courant du projet (au 2026-10-06)

- **Version en développement** : `0.7.0-dev` (branche `main`).
- **Dernière release officielle** : [v0.6.0](https://github.com/nbresson/UsageNotch/releases/tag/v0.6.0) publiée le 2026-10-06.
- **Tests** : **711 tests xUnit verts** (373 Core, 338 Presentation), durée totale d'exécution < 2 s, 0 avertissement.
- **Dernières améliorations techniques majeures (Chantier Qualité & Architecture - Axes 1 à 5)** :
  1. **Axe I — Architecture, Modularité & Extensibilité** :
     - `ProviderSelection` : remplacement des cascades de `switch` combinatoires par un système de masques d'identifiants et de résolutions atomiques (`ActiveProviders`).
     - `IUsageProviderRegistry` : registre ouvert de fournisseurs de quotas supprimant le routage rigide en dur.
     - Contrôle unitaire `PillCellView` : composant WPF mutualisé encapsulant les 3 anneaux, le logo et les animations pour 1, 2 ou 3 cellules sans duplication XAML.
     - `CurrencyFormatter` / `FrenchText.Currency` : centralisation du formatage monétaire bilingue ($ / €) avec arrondi intelligent.
     - Configurabilité système : prise en charge des variables d'environnement `CLAUDE_CONFIG_DIR` et `OPENAI_CONFIG_DIR`.
  2. **Axe II — Concurrence, Réseau & Sondeur d'Usage** :
     - Parallélisation du sondage multi-fournisseurs : interrogation simultanée (`Task.WhenAll`) empêchant les requêtes distantes lentes de ralentir les fournisseurs locaux.
     - `SocketsHttpHandler` configuré avec rotation DNS (`PooledConnectionLifetime = 15 min`, idle timeout 2 min).
     - Réactivité des réglages : rafraîchissement immédiat (`RequestRefresh()`) sur modification des clés API, jetons ou modes OpenAI.
     - Résilience E/S fichiers Windows : boucle de réessais exponentiels sur verrous transitoires (indexeurs/AV) dans `UsageStore` et `SettingsStore`.
     - Prise en charge des en-têtes serveur `Retry-After` sur les réponses HTTP 5xx (`ClaudeUsageProvider`, `OpenAiUsageProvider`).
  3. **Axe III — Rendu WPF, Performance & Ressources Win32** :
     - Désenregistrement systématique des hooks Win32 `WndProc` dans `PillWindow.OnClosed` et `CardWindow.OnClosed` (`RemoveHook`).
     - Migration vers `VisualStateManager` : animation déclarative des états (`Idle`, `Running`, `Attention`, `BandPulse`) éliminant le code impératif et les drapeaux booléens de contrôle.
     - Fenêtre de détail adaptative (`CardWindow`) : largeur fluide (`MinWidth="320"`, `MaxWidth="420"` avec `SizeToContent="WidthAndHeight"`).
     - Flèche de carte adaptative : `ArrowGeometryConverter` avec géométries pré-figées (`Freeze()`) et calcul dynamique de positionnement alignant la flèche sur le centre physique de la pilule, quel que soit le bord de l'écran.
     - Optimisation Native AOT du Hook : lecture de l'entrée standard via `Task.Run` sur le pool de threads sans création d'un thread OS dédié par invocation.
  4. **Axe IV — Sécurité & Protection des Données** :
     - `SecretProtector` & `ProtectedStringConverter` : chiffrement Windows DPAPI (`DataProtectionScope.CurrentUser`) transparent avec préfixe `dpapi:` pour les clés API et tokens OpenAI dans `settings.json` (migration automatique des clés existantes en clair).
     - Sécurisation du `HookListener` : contrôle strict de l'origine locale (`IsLocal` et boucle locale `IPAddress.IsLoopback`), en-tête `X-UsageNotch-Hook: 1`, rejet des `User-Agent` de navigateurs pour bloquer toute attaque CSRF/SSRF.
     - Plafond de requête et protection anti-DoS (`HookListener`) : contrôle précoce `ContentLength64` rejetant immédiatement les charges excessives (`413 Payload Too Large`), cap dur `MaxRequestBytes = 2 Mo` et mémoire tampon limitée à 256 Ko.
     - Assainissement des erreurs d'API (`ExceptionSanitizer`) : masquage des clés d'API (`sk-***`), jetons d'autorisation (`Bearer ***`), paramètres de requêtes d'URL et normalisation conviviale des exceptions réseau.
     - Création et intégrité des répertoires applicatifs (`AppPaths.EnsureDirectoriesCreated`).
  5. **Axe V — Ergonomie, UI & Accessibilité** :
     - Réduction des animations : respect automatique des réglages Windows (`SystemParameters.ClientAreaAnimation`) et option dédiée `Settings.ReduceAnimations` dans les préférences d'apparence.
     - Contraste renforcé sur fond clair : effet d'ombre portée subtil (`DropShadowEffect`) sur le contour de la pilule dans la fenêtre principale et dans l'aperçu.
     - Indicateur visuel d'authentification (`AuthBadge`) : badge d'avertissement ambre immédiat sur la cellule en cas de statut `NeedsAuth` ou `Error`.
     - Épinglage direct de la carte : bouton d'épinglage 📌 dans l'en-tête de `CardWindow` pour figer l'affichage de la carte sans devoir recourir au clic droit sur la pilule.
     - Retour visuel immédiat d'actualisation : état `IsRefreshing` affichant « Actualisation en cours… » sur le bouton et dans le menu contextuel.

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

1. **Scoping et gestion des animations WPF dans `PillCellView`** :
   - Les animations d'activité de chaque cellule (`Spin`, `Pulse`) sont encapsulées dans le composant `PillCellView` et pilotées de manière déclarative par `VisualStateManager` (`Idle`, `Running`, `Attention`), évitant tout code impératif de storyboard dans la fenêtre parente.
   - Les storyboards s'arrêtent automatiquement lors du déchargement (`Unloaded`) ou lorsque la fenêtre est masquée/repliée.
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

