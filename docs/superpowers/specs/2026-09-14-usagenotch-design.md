# UsageNotch — Spécification de conception

Date : 2026-09-14
Statut : validé en brainstorming, en attente de relecture avant plan d'implémentation.

> `UsageNotch` est un nom de travail. Il peut être changé au moment du scaffolding.

## 1. En une phrase

Une application Windows en C# / .NET 10 / WPF qui soude un petit « notch » à un bord d'écran
et répond à deux questions d'un coup d'œil : **combien de mon quota Claude Code ai-je consommé**,
et **est-ce que Claude travaille encore, a fini, ou attend une réponse de ma part**.

## 2. Origine et objectifs

Le projet s'inspire de [codenotch](https://github.com/vinzdg/codenotch) (macOS, Swift) et de
son port Windows (Rust + Tauri 2 / WebView2). Aucun code n'est copié ; le concept, le modèle de
données et les règles de comportement sont réimplémentés.

Le port Windows présente six défauts que ce projet corrige par construction :

| # | Défaut observé | Cause dans le port Windows | Réponse de ce projet |
|---|---|---|---|
| 1 | Animations lentes | Page HTML dans WebView2, fenêtre transparente | Rendu WPF composé en accélération matérielle |
| 2 | Zone invisible qui bloque les clics derrière | Une seule grande fenêtre ; click-through géré par sondage à 50 ms de rectangles « chauds » | Deux fenêtres (pilule, carte) ; hit-testing natif `WM_NCHITTEST` ; carte cachée = aucune surface |
| 3 | Peu de personnalisation des couleurs | Palette codée en dur | Thèmes prédéfinis + thème personnalisé, seuils réglables |
| 4 | Place occupée non choisie | Taille fixe, bord droit uniquement | Échelle, densité, bord d'ancrage, position le long du bord |
| 5 | Pas de mode ultra discret | — | Mode Replié : fine bande de 2 à 12 px, dépli au survol |
| 6 | Pas d'ancrage sur un autre écran | Écran principal codé en dur | Choix de l'écran par identifiant, résilient au débranchement |

### Périmètre de la première version

- Fournisseur **Claude Code seul**, derrière une interface extensible.
- État de session **par hooks Claude Code uniquement** (CLI et extension VS Code).
- **Outil personnel** : exécutable à lancer, option « démarrer avec Windows », pas d'installeur.
- Interface en **français**.

### Hors périmètre de la première version

Autres fournisseurs (Codex, Cursor, Antigravity…), surveillance des transcripts pour l'app
desktop Claude, masquage automatique en plein écran, notifications système de seuil 80 % / 100 %,
mise à jour automatique, interface multilingue, tests FlaUI de bout en bout.

## 3. Architecture générale

### Solution

```
UsageNotch.sln
├── src/
│   ├── UsageNotch.Core/        bibliothèque .NET 10, aucune dépendance UI
│   ├── UsageNotch.App/         WPF, net10.0-windows, PerMonitorV2
│   └── UsageNotch.Hook/        console, Native AOT, aucune dépendance
├── tests/
│   └── UsageNotch.Core.Tests/  xUnit
└── docs/
```

**UsageNotch.Core** contient tout ce qui décide quelque chose :

- Modèles : `LimitWindow`, `UsageSnapshot`, `SnapshotStatus`, `Session`, `SessionState`, `HookEvent`.
- `IUsageProvider` et `ClaudeUsageProvider` (lecture du jeton, appel HTTP, parseur, backoff).
- `UsageStore` : dernière bonne lecture, marquage `Stale`, persistance, événement de changement.
- `UsagePoller` : cadence 60 s / 5 min, rafraîchissement forcé.
- `SessionStore` : machine à états des sessions, balayage, événement de changement.
- `HookListener` : `HttpListener` sur 127.0.0.1, filtrage anti-CSRF, parsing tolérant.
- `HookInstaller` : fusion / retrait dans `~/.claude/settings.json`.
- `Settings` (immuable, versionné) et `SettingsStore` (JSON, écriture atomique).
- `Placement` : calcul pur de la position de la pilule et de la carte à partir d'un rectangle
  d'écran, d'un bord, d'une fraction, d'une échelle et d'un facteur DPI.

**UsageNotch.App** ne fait que dessiner et relayer des commandes :

- Fenêtres : `PillWindow`, `CardWindow`, `SettingsWindow`.
- ViewModels avec CommunityToolkit.Mvvm : `PillViewModel`, `CardViewModel`, `SettingsViewModel`.
- Dossier `Interop/` : styles étendus, `WM_NCHITTEST`, énumération des moniteurs, message de
  changement d'affichage, mise au premier plan du terminal, clé `Run`, mutex d'instance unique.
- Icône de zone de notification (H.NotifyIcon.Wpf).
- Contrôles : `ProgressRing` (propriété de dépendance `Fraction` animable), `PillShape`.
- Thèmes : `ResourceDictionary` généré à partir du thème courant.

**UsageNotch.Hook** : lit le nom d'événement en argument et le JSON de Claude Code sur stdin,
POST sur `http://127.0.0.1:<port>/event?e=<événement>&ppid=<pid parent>`. Si l'app ne répond pas,
la lance détachée et réessaie pendant 2 s au plus. Sort toujours avec le code 0. Compilé en
Native AOT pour un démarrage en quelques millisecondes.

### Injection et hébergement

`Microsoft.Extensions.Hosting` : conteneur DI, `ILogger`, services de fond (`UsagePoller`,
`HookListener`, balayage des sessions) en `BackgroundService`.

### Flux de données

Deux chaînes indépendantes qui ne se rencontrent que dans les ViewModels :

1. `UsagePoller` → `IUsageProvider.FetchAsync` → `UsageStore` (conserve, marque, persiste) →
   événement `Changed` → `PillViewModel` / `CardViewModel`.
2. `HookListener` → `SessionStore.Apply` → événement `Changed` **uniquement si l'état visible a
   changé** → `PillViewModel` / `CardViewModel`.

Les ViewModels reçoivent les événements sur le thread UI via le `Dispatcher`.

## 4. Fournisseur Claude et modèle d'usage

### Modèle

```csharp
enum SnapshotStatus { Ok, Stale, NeedsAuth, Backoff, Error, Absent }

record LimitWindow(
    string Id,           // "session" | "weekly_all" | "weekly_opus" | autre, stable
    string Label,        // libellé affiché
    double UsedFraction, // 0.0 à 1.0
    DateTimeOffset ResetsAt);

record UsageSnapshot(
    SnapshotStatus Status,
    IReadOnlyList<LimitWindow> Windows,
    DateTimeOffset FetchedAt,
    string Note,
    DateTimeOffset? BackoffUntil);

interface IUsageProvider
{
    string Id { get; }               // "claude"
    string DisplayName { get; }      // "Claude"
    string HeadlineWindowId { get; } // "session" : la fenêtre que l'anneau dessine
    Task<UsageSnapshot> FetchAsync(CancellationToken ct);
}
```

L'anneau dessine la fenêtre `HeadlineWindowId`. Si elle est absente de la réponse, la cellule
montre un tiret : une fenêtre manquante n'est pas remplacée par une autre.

### Lecture du jeton

- Fichier : `%USERPROFILE%\.claude\.credentials.json` (puis `credentials.json` en secours).
- Clé : `claudeAiOauth.accessToken` ; `claudeAiOauth.expiresAt` sert d'indice pour la note.
- Lecture seule. Jamais de rafraîchissement : c'est le rôle de Claude Code.
- Absent → `NeedsAuth`, note : « Aucun identifiant Claude Code trouvé — connectez-vous une fois
  avec la CLI claude ».
- Le jeton n'apparaît jamais dans les journaux ni dans la sortie de `doctor`.

### Appel

- `GET https://api.anthropic.com/api/oauth/usage`
- En-têtes : `Authorization: Bearer <jeton>`, `anthropic-beta: oauth-2025-04-20`
- Délai : 15 s.

### Parseur

1. Lire le tableau `limits[]` : `{ kind, percent, resets_at }`. Une entrée sans `resets_at`
   est ignorée.
2. Fusionner en secours `five_hour` et `seven_day` : `{ utilization, resets_at }`. Dédoublonnage
   par trois règles : alias d'identifiant (`five_hour` ≡ `session` ; `seven_day` ≡ `weekly_all` ≡
   `weekly`), même libellé, ou même `resets_at` à la seconde près avec écart d'usage < 0,5 point.
3. Libellés : `session` → « Session en cours », `seven_day` / `weekly_all` → « Hebdomadaire (tous
   modèles) », `seven_day_opus` / `weekly_opus` → « Hebdomadaire (Opus) », inconnu → identifiant
   mis en forme.
4. `session` toujours triée en premier.

### Erreurs et backoff

| Cas | Comportement |
|---|---|
| 401 / 403 | Relire le fichier ; si le jeton a changé, un unique nouvel essai ; sinon `NeedsAuth` |
| 429 | Backoff 60 s × 2^n, n = 429 consécutifs, plafond 15 min, `Retry-After` comme plancher. Échéance persistée. Dernière lecture conservée en `Stale` |
| Autre erreur | Dernière lecture conservée en `Stale` avec la note ; `Error` s'il n'y a jamais eu de lecture |
| Succès | `Ok`, compteur de 429 remis à zéro, backoff effacé |

### Cadence

- 60 s quand au moins une session est en état `Running` ou `Attention`.
- 5 min sinon.
- « Rafraîchir maintenant » force un appel immédiat et efface le backoff.
- Un appel n'est jamais lancé pendant une fenêtre de backoff.

### Persistance

`%APPDATA%\UsageNotch\usage.json` : dernier snapshot `Ok`. Rechargé au démarrage et affiché en
`Stale` avec son âge.

## 5. État de session par hooks

### Installation des hooks

`HookInstaller` écrit dans `%USERPROFILE%\.claude\settings.json` :

| Événement Claude Code | Matcher | Argument passé au hook |
|---|---|---|
| SessionStart | — | `session_start` |
| UserPromptSubmit | — | `running` |
| PreToolUse | `*` | `running` |
| PostToolUse | `*` | `running` |
| Notification | — | `attention` |
| Stop | — | `done` |
| SessionEnd | — | `session_end` |

Chaque entrée : `{ "hooks": [{ "type": "command", "command": "\"<chemin>\\UsageNotch.Hook.exe\" <argument>", "timeout": 5 }] }`.

Règles :
- Les entrées existantes de l'utilisateur sont préservées.
- Les entrées du projet sont reconnues par `UsageNotch.Hook` dans la commande, et remplacées.
- Une sauvegarde `settings.json.usagenotch-bak-<timestamp>` est écrite avant toute modification.
- La désinstallation retire uniquement les entrées du projet.
- Un interrupteur dans Réglages › Claude Code installe / désinstalle et affiche l'état.

### Transport

- Hook → app : `POST http://127.0.0.1:<port>/event?e=<événement>&ppid=<pid parent du hook>`,
  corps = JSON de Claude Code (`session_id`, `cwd`, `prompt`, `message`, `tool_name`,
  `tool_input.command`, `model`…), lu jusqu'à 256 Ko.
- Port par défaut 48666, lu dans `settings.json` de l'app (le hook fait une lecture textuelle
  minimale de la clé `"port"`).
- Délais du hook : connexion 300 ms, lecture/écriture 700 ms, budget total 2 s. Toute erreur →
  sortie code 0 silencieuse.
- Récepteur : `HttpListener` lié à `127.0.0.1` uniquement. Une requête est refusée (403) si
  elle porte plus d'un en-tête `Origin`, un `Origin` non local (`127.0.0.1`, `localhost`,
  `[::1]`, http/https, port optionnel), ou `Sec-Fetch-Site: cross-site`. Les appels natifs du
  hook ne portent aucun de ces en-têtes.
- Parsing tolérant : aucun champ manquant n'est une erreur ; `session_id` absent → `"unknown"`.

### Machine à états

```csharp
enum SessionState { Idle, Done, Running, Attention } // ordre croissant de coût d'attention

record Session(
    string Id, string Title, SessionState State,
    DateTimeOffset Started, TimeSpan Total,
    string LastAction, string AttentionMessage, string Prompt, string Model,
    int ParentPid, string Cwd, DateTimeOffset LastEvent);
```

`Title` = dernier segment de `cwd` + « · » + 4 premiers caractères de l'identifiant.

| Événement | Effet |
|---|---|
| `session_start` | Crée la session en `Idle` si elle n'existe pas |
| `running` | → `Running` ; si l'état précédent n'était pas `Running`, `Started = maintenant` ; efface `AttentionMessage` ; mémorise `Prompt` (120 car.) ou `LastAction` (« 🔧 Outil : commande », 60 car.) |
| `attention` | → `Attention` ; mémorise `AttentionMessage` (200 car.) |
| `done` | → `Done` ; si l'état précédent n'était pas `Done`, `Total = maintenant − Started` ; efface `AttentionMessage` |
| `session_end` | Retire la session |

Balayage toutes les 30 s :
- `Running` sans événement depuis 30 min → `Idle`.
- `Idle` depuis plus de 10 min → retirée.
- `Done` depuis plus de 24 h → retirée.

`Done` persiste jusqu'au prochain `running` de la même session, à un rejet manuel (✕ sur la
carte), ou au balayage.

`Changed` n'est levé que si le tuple (état, dernière action, message d'attention, prompt,
modèle) d'une session a changé, ou si une session est ajoutée ou retirée.

### Agrégation et affichage

- État agrégé = le plus coûteux parmi les sessions : `Attention` > `Running` > `Done` > `Idle`.
- Cellule : arc fin en rotation (`Running`), anneau ambre pulsant (`Attention`), point bleu
  (`Done`).
- Carte : liste des sessions non `Idle`, triées par état puis par date de début décroissante,
  cinq au plus, chacune avec sa pastille, son titre, et pour `Attention` son message.
- Transition vers `Done` ou `Attention` : la carte s'ouvre d'elle-même 5 s, et un son système
  optionnel est joué. Ces deux comportements se désactivent séparément. Rien n'est annoncé pour
  les sessions déjà présentes au premier événement après le démarrage.

### Retour au terminal

Clic sur une session dans la carte : depuis `ParentPid`, remonter la chaîne des processus
(8 niveaux au plus, via `CreateToolhelp32Snapshot`), énumérer les fenêtres visibles de premier
niveau, choisir celle dont le PID est le plus haut dans la chaîne (ou dont le parent est dans la
chaîne, cas conhost), la restaurer si réduite, `SetForegroundWindow` puis `FlashWindowEx`.
Aucune fenêtre trouvée → aucun effet, journalisé en Debug.

## 6. Fenêtres, hit-testing et animations

### Deux fenêtres

| Fenêtre | Rôle | Visible |
|---|---|---|
| `PillWindow` | La pilule (ou la bande repliée) | Toujours, sauf mode Masqué |
| `CardWindow` | La carte de détail | Uniquement pendant le survol ou l'auto-ouverture |
| `SettingsWindow` | Réglages, fenêtre classique activable | À la demande |

`PillWindow` et `CardWindow` : `WindowStyle=None`, `AllowsTransparency=True`,
`Background=Transparent`, `Topmost=True`, `ShowActivated=False`, `ShowInTaskbar=False`. À la
création (`SourceInitialized`), styles étendus `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`.

Une fenêtre cachée n'a aucune surface à l'écran : quand la carte est fermée, elle est `Hide()`,
et rien ne peut intercepter un clic destiné à l'application derrière.

### Hit-testing

Chaque fenêtre intercepte `WM_NCHITTEST` dans son `HwndSourceHook` :
1. Convertir le point écran en coordonnées locales (en tenant compte du DPI).
2. Tester `Geometry.FillContains(point)` sur la géométrie dessinée (pilule avec ses congés ;
   carte avec sa flèche).
3. Dedans → `HTCLIENT`. Dehors → `HTTRANSPARENT`.

Aucun sondage périodique de la position du curseur pour le click-through.

### Forme de la pilule

Rectangle aux coins arrondis côté intérieur de l'écran, plat côté bord, avec deux congés
concaves (rayon = rayon des coins) qui prolongent la pilule jusqu'au bord de l'écran. Contour
d'un pixel dans une couleur du thème, pour rester visible sur un fond sombre. La forme tourne
selon le bord d'ancrage.

### Survol

- `MouseEnter` sur une cellule → `CardWindow` se positionne face à cette cellule et s'affiche.
- La carte se ferme 250 ms après que le curseur a quitté à la fois la pilule et la carte.
- Filet de sécurité : pendant que la carte est ouverte, un `DispatcherTimer` à 200 ms compare
  `GetCursorPos` aux rectangles des deux fenêtres ; si le curseur est hors des deux depuis plus
  de 250 ms, la carte se ferme.
- Clic gauche sur le corps de la pilule → verrouille la carte ouverte ; second clic → libère.
  « Garder ouvert » dans le menu contextuel fait la même chose et est coché quand actif.
- Clic gauche sur une cellule → rafraîchit ce fournisseur.
- Clic droit → menu : Rafraîchir maintenant, Garder ouvert, Réglages…, Quitter.

### Carte de détail

- En-tête : glyphe + « Claude ».
- Sous-titre « Mis à jour il y a N min » si `Stale`.
- Un bloc par `LimitWindow` : libellé à gauche, texte de reset à droite (« Réinitialisation dans
  51 min » sous une heure, « Réinitialisation jeu. 00:00 » au-delà), barre de 4 px, « N % utilisé ».
- Note du snapshot si présente.
- Liste des sessions (§5).
- Positionnée face à la cellule survolée, vers l'intérieur de l'écran, maintenue dans l'écran ;
  la flèche pointe sur l'anneau survolé.

### Animations

| Élément | Animation |
|---|---|
| `ProgressRing.Fraction` | Interpolation 300 ms, `CubicEase EaseOut` |
| Arc « en travail » | `RotateTransform` continu, 1 tour / 1,2 s |
| Anneau « attention » | Opacité 0,4 ↔ 1,0 sur 1 s, en boucle |
| Carte | Fondu + glissement 8 px depuis le bord ; 180 ms ouverture, 150 ms fermeture |
| Repli / dépli de la pilule | 200 ms, `CubicEase EaseInOut` |
| Cellule qui apparaît / disparaît | Fondu 150 ms |

Toutes les animations sont des `Storyboard` WPF composés en accélération matérielle.

### DPI

- `app.manifest` : `PerMonitorV2`.
- WPF redimensionne le contenu ; les positions calculées par l'interop en pixels physiques sont
  converties avec le facteur d'échelle **du moniteur cible**.

### Icône de zone de notification

Menu identique au menu contextuel de la pilule. Clic gauche → Réglages. Info-bulle : « UsageNotch
— Claude 73 % ». Forcée visible en mode Masqué.

## 7. Personnalisation et mode replié

Tout s'applique immédiatement, sans redémarrage.

### Thème

```csharp
record Theme(
    Color PillBackground, Color PillBorder, Color RingTrack,
    Color LevelAmple, Color LevelWatch, Color LevelCritical,
    Color Running, Color Attention, Color Done,
    Color Text,
    double PillOpacity,          // 0,2 à 1,0
    double ThresholdWatch,       // défaut 0,50
    double ThresholdCritical);   // défaut 0,80
```

Préréglages : **Codenotch** (noir, vert `#28E07B`, jaune `#F5E400`, orange `#FF4500`),
**Monochrome** (gris et blanc), **Accent système** (suit la couleur d'accentuation Windows).
Quatrième choix : **Personnalisé**, un sélecteur par couleur, initialisé depuis le préréglage
courant.

Couleur de l'anneau et de la barre : `LevelAmple` sous `ThresholdWatch`, `LevelWatch` jusqu'à
`ThresholdCritical`, `LevelCritical` au-delà ; à 100 %, anneau plein et glyphe assombri.

### Taille et densité

- Échelle globale : 40 % à 150 %, une seule valeur qui pilote toutes les dimensions.
- Contenu de cellule : anneau et pourcentage / anneau seul / pourcentage seul.

### Modes de visibilité

| Mode | Au repos | Au survol |
|---|---|---|
| **Déplié** | Pilule complète | Carte au survol d'une cellule |
| **Replié** | Bande de 2 à 12 px (réglable) collée au bord, longueur = longueur de la pilule, colorée dans la teinte du niveau d'usage, pulsant en `Attention`, sans texte ni anneau | Dépli en 200 ms ; repli 400 ms après le départ du curseur, sauf verrouillage |
| **Masqué** | Rien | — ; icône de notification forcée visible |

En mode Replié, une transition vers `Done` ou `Attention` déplie la pilule 5 s (même règle que
l'auto-ouverture de la carte).

### Placement

- Bord d'ancrage : droite, gauche, haut, bas. Droite / gauche : cellules empilées verticalement.
  Haut / bas : cellules alignées horizontalement. La carte s'ouvre vers le centre de l'écran.
- Position le long du bord : fraction 0 à 1, mémorisée **par bord**. Réglable par Alt + glisser
  sur la pilule, ou par curseur dans les réglages. Bouton « Recentrer ».
- Écran d'ancrage : §8.

### Fenêtre de réglages

Navigation latérale, cinq pages, chacune avec un aperçu en direct de la pilule :

| Page | Contenu |
|---|---|
| Apparence | Préréglage, couleurs personnalisées, opacité, seuils, échelle, densité |
| Position | Miniature des écrans, écran d'ancrage, bord, curseur de position, Recentrer, mode de visibilité, largeur de la bande repliée |
| Comportement | Auto-ouverture de la carte, son (avec choix et aperçu), démarrer avec Windows, icône de notification |
| Claude Code | État des hooks, installer / désinstaller, port, chemin de settings.json, chemin de l'exécutable hook |
| À propos | Version, dossier de données, journaux, niveau de journalisation, `doctor` |

## 8. Multi-écran et persistance des réglages

### Écrans

- Énumération via `EnumDisplayMonitors` + `GetMonitorInfo` + `GetDpiForMonitor` : rectangle
  physique, rectangle de travail, échelle, identifiant de périphérique (`szDevice`).
- L'écran d'ancrage est mémorisé par **identifiant**, ou par l'option « Écran principal ».
- Écran absent → repli sur l'écran principal sans perdre le choix ; retour dès qu'il réapparaît.
- `WM_DISPLAYCHANGE` et `WM_DPICHANGED` → recalcul de la position.
- La pilule est positionnée sur le **bord physique** de l'écran, pas sur la zone de travail :
  masquer la barre des tâches ne la déplace pas.

### Fichier de réglages

`%APPDATA%\UsageNotch\settings.json`, classe `Settings` immuable avec `Version`.

- Clé absente → valeur par défaut. Clé inconnue → ignorée.
- Valeur hors bornes → ramenée dans les bornes (une pilule ne peut pas devenir invisible par
  édition manuelle).
- Écriture atomique : fichier temporaire puis `File.Move(overwrite: true)`.
- Le hook lit la clé `"port"` dans ce fichier.

### Démarrage et instance unique

- « Démarrer avec Windows » : valeur dans `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Mutex nommé `Local\UsageNotch`. Un second lancement envoie « ouvrir les réglages » à l'instance
  existante (via le `HookListener`, route `/open-settings`, même filtrage) et quitte.

## 9. Erreurs, journalisation et tests

### Principe

Aucune défaillance n'est silencieuse, aucune n'invente un chiffre. Chaque statut a une
représentation visible (§4, §6). Une exception dans un service de fond est journalisée et la
boucle redémarre. Une exception non gérée dans l'UI est journalisée ; l'app continue si la
fenêtre peut être reconstruite, sinon message puis sortie.

### Journalisation

- `Microsoft.Extensions.Logging` + fournisseur fichier léger.
- `%APPDATA%\UsageNotch\logs\usagenotch-<yyyyMMdd>.log`, sept jours conservés.
- Niveau Information par défaut, Debug activable dans les réglages.
- Jamais de jeton, jamais de contenu de prompt : noms d'événement, identifiants de session
  tronqués à 8 caractères, codes HTTP.

### Diagnostic

`UsageNotch.exe doctor` : présence du fichier de credentials (sans contenu), état des hooks,
port en écoute, écrans détectés, dernier snapshot. `UsageNotch.exe --demo` : données fixes pour
juger l'apparence.

### Tests unitaires (`UsageNotch.Core.Tests`, xUnit)

| Sujet | Cas |
|---|---|
| Parseur d'usage | Réponses enregistrées ; fusion `limits` / `five_hour` / `seven_day` ; fenêtre sans reset ignorée ; tri session en premier ; `kind` inconnu |
| Backoff | 60, 120, 240, 480, 900, plafond ; plancher `Retry-After` ; remise à zéro après succès ; pas d'appel pendant le backoff |
| Lecture du jeton | Fichier absent, JSON invalide, clé absente, forme avec et sans `claudeAiOauth` |
| Machine à états | Chaque transition ; `Started` / `Total` ; persistance de `Done` ; balayages 30 min / 10 min / 24 h ; `Changed` non levé sans changement visible |
| Agrégation | `Attention` > `Running` > `Done` > `Idle` ; tri des sessions |
| Fusion des hooks | settings.json absent, vide, avec hooks tiers, avec anciennes entrées ; sauvegarde écrite ; désinstallation ne retire que les nôtres |
| Récepteur HTTP | `Origin` accepté / refusé (liste du port Rust reprise) ; `Sec-Fetch-Site` ; deux `Origin` ; parsing tolérant |
| Réglages | Défauts, clés inconnues, bornes, écriture atomique, version |
| Placement | Pilule et carte pour chaque bord, fractions 0 / 0,5 / 1, écran à 150 %, carte maintenue dans l'écran |
| ViewModels | Couleur, texte et état d'anneau pour des snapshots donnés |

### Tests de l'UI

Vérification manuelle avec `--demo` pour la première version. FlaUI ultérieurement.

## 10. Dépendances

| Paquet | Projet | Rôle |
|---|---|---|
| CommunityToolkit.Mvvm | App | ViewModels, commandes |
| Microsoft.Extensions.Hosting | App | DI, logging, services de fond |
| H.NotifyIcon.Wpf | App | Icône de zone de notification |
| Microsoft.Windows.CsWin32 | App | Génération des signatures Win32 |
| xUnit, FluentAssertions | Tests | Tests |

Aucune dépendance dans `UsageNotch.Hook` (Native AOT).

## 11. Références

- Dépôt d'origine : https://github.com/vinzdg/codenotch (MIT). Spec de conception macOS :
  `docs/specs/2026-08-28-usage-notch-design.md` ; port Windows : `windows/`.
- Endpoint d'usage : `GET https://api.anthropic.com/api/oauth/usage`, en-tête
  `anthropic-beta: oauth-2025-04-20`. Non documenté publiquement ; le parseur est épinglé par
  des tests et toute évolution dégrade vers un statut visible.
