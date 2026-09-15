# UsageNotch — reprise du projet

Projet mis en pause le 2026-09-15, dans un état complet et utilisable. Ce document permet de reprendre sans relire tout
l'historique.

## État au moment de la pause

- Version `0.3.0` (`<Version>` dans `src/UsageNotch.App/UsageNotch.App.csproj`), branche `main`, poussée sur
  https://github.com/nbresson/UsageNotch.
- Compilation sans avertissement (`TreatWarningsAsErrors`), 489 tests xUnit verts : 237 Core, 252 Presentation.
- Fonctionnel au quotidien : pilule et carte, modes Déplié / Replié / Masqué, placement multi-écran, thèmes, fenêtre de
  réglages à cinq pages, état des sessions par hooks Claude Code, retour au terminal, alertes de seuil, masquage en plein
  écran, démarrer avec Windows, diagnostic, icône d'application.
- Tout ce que la spec prévoyait pour la première version est livré. Deux sujets qu'elle plaçait hors périmètre ont été
  ajoutés ensuite : les alertes de seuil et le masquage en plein écran.

## Carte de la documentation

| Document | Contenu |
|---|---|
| `README.md` | Présentation, installation, utilisation, confidentialité, organisation du code |
| `docs/superpowers/specs/2026-09-14-usagenotch-design.md` | Spec de conception d'origine (référence ; ses sections « hors périmètre » sont en partie dépassées, voir plus haut) |
| `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook.md` | Plan 1 : Core et hook |
| `docs/superpowers/plans/2026-09-15-usagenotch-app.md` | Plan 2 : application WPF |
| `docs/superpowers/plans/2026-09-15-usagenotch-settings-window.md` | Plan 3 : fenêtre de réglages |
| `docs/superpowers/plans/2026-09-14-usagenotch-core-and-hook-notes.md` | **Journal de bord** : contrats entre couches, arbitrages, écarts à la spec, points laissés en l'état, puis chaque chantier postérieur (performance, fluidité, alertes, plein écran, finitions) |

Les plans décrivent l'intention au moment de leur écriture ; le code et le journal de bord font foi quand ils divergent.

## Cycle de travail

```powershell
dotnet build UsageNotch.sln
dotnet test
src\UsageNotch.App\bin\Debug\net10.0-windows\UsageNotch.App.exe --demo
```

- **Branches** : une branche par chantier (`feat/…`, `fix/…`), fusion locale dans `main` après tests, puis `git push`.
- **Tests** : la logique vit dans `UsageNotch.Core` et `UsageNotch.Presentation` (sans WPF) et se développe en TDD.
  `UsageNotch.App` (WPF, interop Win32) se vérifie à l'exécution.
- **Mode démo** (`--demo`) : données fixes (session 73 %, hebdomadaires 21 % et 52 %, trois sessions dont une qui
  alterne toutes les 20 s), réglages et journaux dans `%TEMP%\UsageNotch-demo`, mutex `Local\UsageNotch-demo`, port
  48667. Il ne touche jamais la vraie configuration, n'appelle pas l'API et désactive « Démarrer avec Windows ».
  Pour envoyer un événement de session à la démo :
  ```powershell
  Invoke-WebRequest -Method Post -Uri "http://127.0.0.1:48667/event?e=attention&ppid=1" -UseBasicParsing `
    -Body '{"session_id":"test-0001","cwd":"C:\\src\\projet","message":"Autoriser Bash ?"}'
  ```
  Événements acceptés : `session_start`, `running`, `attention`, `done`, `session_end`.
- **Vérification à l'exécution** : UI Automation depuis PowerShell (chaque contrôle des réglages a un `AutomationId`),
  captures d'écran, lecture de `settings.json` de la démo. Les procédures complètes sont dans le Plan 3, section
  « Procédures de vérification ».
- **Publication** : `powershell -ExecutionPolicy Bypass -File scripts\publish.ps1` publie dans `publish\` (application
  dépendante du runtime, hook en Native AOT). Le script **vide** le dossier cible : quitter UsageNotch avant, ou publier
  ailleurs avec `-Output <dossier>`.
- **Icône** : `scripts/generate-icon.ps1` régénère `src/UsageNotch.App/Assets/UsageNotch.ico`.

## Pièges connus

- **Native AOT du hook** : exige Visual Studio avec la charge « Développement Desktop en C++ ». Si la variable
  `NoDefaultCurrentDirectoryInExePath` est définie, la détection échoue (« 'vswhere.exe' n'est pas reconnu ») ; le script
  de publication la retire.
- **.NET 10 `BackgroundService`** : `StartAsync` exécute `ExecuteAsync` en arrière-plan ; le récepteur lie son port dans
  `StartAsync`, et tout abonnement aux magasins se fait avant `host.StartAsync`.
- **Passage des clics** : assuré par la transparence par pixel des fenêtres en couches (`AllowsTransparency`), pas par
  `WM_NCHITTEST`/`HTTRANSPARENT`, qui ne transmet le clic qu'aux fenêtres du même thread.
- **Fenêtres non activables** (pilule, carte) : `WS_EX_NOACTIVATE`, `MA_NOACTIVATE`, placement par `SetWindowPos`
  avec `SWP_NOACTIVATE`. Sur un écran d'une autre mise à l'échelle, WPF repositionne la fenêtre sans `SWP_NOACTIVATE` et
  Windows l'active : les deux fenêtres rendent l'activation (`WindowStyles.ReturnActivation`).
- **Animations d'une fenêtre transparente** : rendues en logiciel à chaque image ; les animations en boucle ont une
  cadence plafonnée (20 ou 30 images/s) et ne tournent que si leur cible est visible.
- **Styles des pages de réglages** : chaque page fusionne `PreferencesStyles.xaml` elle-même ; une `StaticResource`
  est résolue pendant `InitializeComponent`, avant que la page soit dans la fenêtre.
- **UI Automation** : un `Canvas` n'a pas de pair d'automatisation (d'où celui de `MonitorMapView`) ; une fenêtre
  cachée n'apparaît pas dans l'arbre UIA (utiliser `EnumWindows` par identifiant de processus).
- **Instance réelle pendant les tests** : l'utilisateur fait tourner `publish\UsageNotch.App.exe` (port 48666, mutex
  `Local\UsageNotch`, pilule au même titre « UsageNotch — pilule »). Ne jamais l'arrêter ; retrouver les fenêtres de la
  démo par identifiant de processus ; ne pas republier dans `publish\` sans qu'elle soit quittée.
- **Scripts PowerShell 5.1** : les enregistrer en UTF-8 **avec** BOM, sinon le tiret cadratin des titres et les accents
  sont mal lus.
- **Commits** : messages en anglais `type(scope): description`, fichier de message en UTF-8 sans BOM.

## Backlog

**À vérifier à l'usage** (aucun code tant qu'aucun défaut n'apparaît) :
- pilule, carte et miniature sur un écran à une autre mise à l'échelle ;
- clic sur une ligne de session (retour au terminal) et bouton ✕ ;
- son d'ouverture automatique ;
- masquage avec un vrai jeu en plein écran exclusif et une vidéo YouTube en plein écran ;
- alerte de seuil à 100 % et changement de période en conditions réelles.

**Mineurs, sans effet visible** : allocation du stylo de `ProgressRing` à chaque rendu, longueur du jeton affichée par
`doctor`, chemin de copie du hook codé en dur dans le projet App, minuteries de survol non remises à null, arrêt du
récepteur sans attendre les requêtes en cours. Détail et raisons dans le journal de bord.

**Extensions possibles** (chacune demande sa propre conception, puis un plan) :
- autres fournisseurs d'usage (Codex, Cursor…) : l'interface `IUsageProvider` existe, la carte et la pilule supposent
  aujourd'hui un seul fournisseur ;
- suivi de l'application desktop Claude, qui n'utilise pas les hooks (surveillance des transcriptions) ;
- tests d'interface automatisés (FlaUI) pour remplacer les vérifications manuelles ;
- mise à jour automatique, interface multilingue (peu utiles pour un outil personnel).
