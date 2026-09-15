# UsageNotch Plan 1 — Notes d'exécution

Exécuté le 2026-09-14 sur la branche `feat/core-and-hook`, par sous-agents avec relecture par tâche et relecture finale
de toute la branche. Résultat : 216 tests, compilation sans avertissement.

## Contrats dont le Plan 2 (application WPF) dépend

- L'exécutable de l'application s'appelle `UsageNotch.App.exe` et se trouve dans le même dossier que `UsageNotch.Hook.exe`.
- Le hook lance l'application avec l'argument `--from-hook`. Une seconde instance lancée avec cet argument doit quitter
  sans rien afficher, au lieu de demander l'ouverture des réglages à la première.
- `Settings.AutoLaunch` (clé `autoLaunch`) : le hook ne relance pas l'application quand elle vaut `false`.
  « Quitter » doit la passer à `false`, et un démarrage manuel la remettre à `true`.
- `HookEvent.ParentPid` est le premier ancêtre du hook qui n'est pas un shell (cmd, bash, sh, powershell, pwsh, conhost).
  Il peut désigner un PID réutilisé : le retour au terminal doit valider la fenêtre trouvée.
- `UsageStore.Changed`, `SessionStore.Changed` et `HookListener.OpenSettingsRequested` sont levés sur un thread
  d'arrière-plan et peuvent arriver dans le désordre. Les abonnés relisent `Current` ou `Snapshot()` sur le thread UI.
- `UsagePoller` appelle lui-même `UsageStore.Load()` au démarrage. L'application ne doit pas l'appeler une seconde fois.
- `ClaudeUsageProvider.Timeout` (15 s) doit être affecté à `HttpClient.Timeout` à l'enregistrement.
- `HookListener.IsListening` indique si le port est bien lié, pour la page Claude Code et `doctor`.
- `HookInstaller.Install` lève `InvalidDataException` avec un message en français quand le `settings.json` de
  Claude Code est illisible. La page de réglages doit afficher ce message.
- La publication Native AOT du hook fonctionne avec `dotnet publish src\UsageNotch.Hook -c Release -r win-x64`
  (vérifiée le 2026-09-14 sur .NET 10 avec Visual Studio Community 2026 et sa charge de travail C++).
  .NET 9 ne reconnaissait pas Visual Studio 2026, d'où le passage à .NET 10.
  Si la variable d'environnement `NoDefaultCurrentDirectoryInExePath` est définie dans le terminal, la détection de
  Visual Studio échoue avec « 'vswhere.exe' n'est pas reconnu » : la retirer le temps de la publication.
- Depuis .NET 10, `BackgroundService.StartAsync` exécute `ExecuteAsync` entièrement en arrière-plan.
  `HookListener` lie donc son port dans une surcharge de `StartAsync`, et `UsagePoller` charge `usage.json`
  juste après le démarrage plutôt que pendant : les abonnés doivent s'abonner avant `StartAsync` et relire `Current`.

## Arbitrages pris pendant l'exécution

Chaque ligne : décision, raison, coût si elle est fausse.

1. Le balayage des sessions remet l'horloge à zéro lors du passage Running → Idle. Sinon la même passe retirerait la session. Coût : une session obsolète reste 10 minutes de plus.
2. `Theme.Clamp` arrondit le seuil critique minimal, car 0,9 + 0,05 ne vaut pas exactement 0,95 en double. Coût : aucun visible.
3. Les lignes d'attribution des commits ont été normalisées en une passe sur toute la branche, contenu inchangé. Coût : cosmétique.
4. La cible est passée de .NET 9 à .NET 10 à la demande de l'utilisateur (LTS, Native AOT compatible avec Visual Studio 2026). Coût : aucun identifié ; la suite passe à l'identique.
5. Travail sur une branche plutôt qu'un worktree séparé. Coût : aucun.
6. Les avertissements d'analyseurs se corrigent par le plus petit changement, jamais en désactivant TreatWarningsAsErrors. Coût : légère dérive par rapport au texte du plan.
7. Le libellé d'une fenêtre de limite inconnue reste l'identifiant mis en forme, conforme à la spec. Coût : un libellé d'apparence anglaise pour un futur type.
8. Les messages d'exception .NET, en anglais, ne deviennent jamais des notes affichées : notes françaises dédiées, exception journalisée. Coût : moins de détail dans la carte, conservé dans les journaux.
9. `RequestRefresh` du planificateur a été rendu sûr entre threads (verrou, `CancelAsync`), contrairement au code du plan. Coût : dérive par rapport au plan.
10. Les corps de hook de plus de 256 Ko sont lus par un lecteur JSON progressif qui garde les champs lus avant la coupure, et le flux est vidé avant la réponse. Coût : une charge qui placerait `session_id` après un énorme champ irait sur la session « unknown ».
11. L'installeur reconnaît ses hooks au nom exact de l'exécutable, pas à une sous-chaîne, et ne remplace jamais une sauvegarde existante. Coût : dérive par rapport au plan.
12. Un littéral de chaîne brute invalide dans un test du plan a été reformaté, JSON et assertions inchangés. Coût : aucun.
13. Les réglages tolèrent les valeurs nulles de référence (`customTheme`, sons). Coût : aucun.
14. Le hook est borné par une échéance de 2 s, car une connexion refusée sur la boucle locale n'échoue pas vite sous Windows (8,7 s mesurés avec le plan d'origine). Coût : une application qui démarre en plus de ~1,7 s rate le premier événement d'une session.
15. L'entrée standard du hook est attendue au plus 1 s, et les délais socket sont bornés par le temps restant. Coût : un événement dont l'entrée arrive après 1 s part avec un corps partiel.
16. Le hook lance l'application via le shell pour ne pas lui transmettre les canaux de Claude Code (mesuré : fermeture à 20,5 s avant, 2,1 s après). Coût : aucun.
17. Un `settings.json` UsageNotch illisible est copié en `settings.json.corrupt-<horodatage>` avant l'écriture des valeurs par défaut. Coût : un fichier de plus sur disque.
18. L'installeur refuse un `settings.json` de Claude Code illisible au lieu de le remplacer. Coût : l'utilisateur doit réparer le fichier avant d'installer les hooks.
19. Une réponse 200 sans aucune fenêtre de limite devient un échec visible au lieu d'un « Ok » vide. Coût : aucun.
20. Le récepteur refuse les méthodes autres que POST (405) et les types d'événement inconnus (400). Coût : aucun.
21. Les sessions en Attention sont retirées après 24 h comme les Done. Coût : une session réellement en attente disparaît au bout d'un jour.
22. `session_start` sur une session existante la remet en Idle, comportement conservé et documenté par un test. Coût : un badge Terminé disparaît à la reprise.

## Points mineurs laissés en l'état

- Un thème personnalisé partiel sans seuils prend 0 avant bornage, au lieu des valeurs par défaut.
- `Retry-After` au format date HTTP est ignoré ; le backoff exponentiel s'applique quand même.
- Le chemin du hook dans la commande utilise des barres obliques inverses.
- Deux fenêtres de course théoriques dans les tests du planificateur (attendre `Calls >= 2`, et lire le statut après `Apply`).
- Après un échec de la copie « corrupt », un `Save` ultérieur écrase le fichier illisible sans copie.
- Le compteur de backoff pourrait déborder après 2³¹ échecs consécutifs.
- `StopAsync` du récepteur n'attend pas les requêtes en cours de traitement.

## Plan 2 — application

### Utilisation

- Publier : `powershell -ExecutionPolicy Bypass -File scripts\publish.ps1` (sortie dans `publish\`).
- Lancer : `publish\UsageNotch.App.exe`. Démonstration sans toucher à la vraie configuration : `--demo`. Diagnostic : `doctor`.
- Installer les hooks Claude Code : menu de l'icône de notification › « Hooks Claude Code installés ». Une sauvegarde horodatée de `~/.claude/settings.json` est écrite avant la modification.
- Réglages : voir « Plan 3 — fenêtre de réglages ».

### Écarts par rapport à la spec

- Passage des clics par la transparence par pixel des fenêtres en couches, pas par `WM_NCHITTEST` : `HTTRANSPARENT` ne transmet le clic qu'aux fenêtres du même thread.
- Déclarations Win32 écrites à la main (`NativeMethods`) au lieu de CsWin32.
- Une seule cellule : le clic gauche sur la pilule verrouille la carte ; « Rafraîchir maintenant » est dans le menu contextuel.
- Clic gauche sur l'icône de notification : aperçu de la carte 5 s (fenêtre de réglages au Plan 3). Une seconde instance lancée à la main fait de même.
- Mode Replié par glissement du contenu dans une fenêtre de taille fixe.
- Retour au terminal : la fenêtre retenue est l'ancêtre le plus proche, pas le plus lointain.
- Mode démo isolé de la vraie application : mutex `Local\UsageNotch-demo` et port 48667.
- Une modification manuelle illisible de `settings.json` est ignorée et journalisée plutôt que de réinitialiser les réglages.
- L'icône de la zone de notification est posée via `TaskbarIcon.Icon` plutôt que `IconSource`, qui n'accepte pas un bitmap en mémoire dans H.NotifyIcon 2.4.1.

### Reste à faire (Plan 3)

- Fenêtre de réglages en cinq pages avec aperçu en direct (spec §7).
- Branchement prévu par la revue finale : enregistrer depuis le thread UI via `SettingsStore.Save` (anti-rebond pour les curseurs), aperçu construit avec `PillPresenter`, `Theme.ForPreset`, `PillShapeBuilder` et `ProgressRing` sur un brouillon de réglages, fenêtre unique appartenant à `NotchShell` ouverte par le menu, `HookListener.OpenSettingsRequested` et le clic gauche sur l'icône. Un changement de port demande un redémarrage ou un `HookListener.Rebind`. Exposer la dernière erreur de relecture du watcher.

### Arbitrages pris pendant l'exécution du Plan 2

- Mode démo isolé (mutex et port 48667) ; l'élément « Démarrer avec Windows » y est désactivé. Origine : lors d'une vérification, un sous-agent a activé puis retiré la vraie valeur Run ; son absence a été vérifiée.
- Échec entre la construction de l'hôte et son démarrage : journalisé, nettoyage, `Shutdown(1)` pour libérer le mutex.
- Relecture de `settings.json` via `SettingsStore.TryParse` : une modification illisible est ignorée et journalisée.
- Glisser Alt : fin sur `LostMouseCapture`, réapplication des réglages regroupée, garde après fermeture.
- `UpdateLayout()` avant le placement : Haut et Bas affichaient des tailles périmées après l'échange largeur/hauteur.
- `SkipGetTargetFrameworkProperties` sur la référence au Hook (NETSDK1151 à la publication).
- Animations Spin, Pulse et BandPulse lancées seulement quand elles sont visibles et que l'activité correspond (environ 7,5 % d'un cœur au repos avant correction).
- Trois exceptions du thread UI en 10 s : message en français puis fermeture ; une exception isolée reste journalisée et ignorée.
- Minuteries de survol et d'aperçu protégées par un compteur de génération.

### Points laissés en l'état après le Plan 2

- Environ 5 % d'un cœur tant qu'une pulsation est visible (rendu logiciel à 60 i/s d'une fenêtre en couches). Piste : `Timeline.DesiredFrameRate` à 20 sur Pulse et BandPulse, 30 sur Spin, à mesurer.
- Spin et Pulse tournent encore quand l'anneau est masqué (contenu « pourcentage seul ») ; environ 1 % d'un cœur en mode Masqué, source inconnue.
- Non vérifié : écrans à mise à l'échelle différente (DPI mixte), clics focus et ✕ sur les lignes de session, son d'ouverture automatique.
- Mineurs : lecture du fichier de réglages sur le thread UI sans gestionnaire `Error`, purge du journal seulement au démarrage, saut du glissement à la réouverture de la carte, allocation du stylo de `ProgressRing`, longueur du jeton affichée par `doctor`, chemin de copie du Hook codé en dur, `AttachConsole` du diagnostic, minuteries de survol non remises à null, pid recyclé possible pour le retour au terminal.

## Plan 3 — fenêtre de réglages

### Utilisation

- Ouvrir les réglages de trois façons :
  - clic gauche sur l'icône de notification ;
  - « Réglages… » dans le menu de la pilule ou de l'icône ;
  - relancer `UsageNotch.App.exe` pendant qu'il tourne.
- Il n'y a pas de bouton « Appliquer ». La pilule suit en 250 ms au plus, et fermer la fenêtre ou la quitter enregistre tout.
- Le port se change dans la page Claude Code et s'applique au prochain démarrage.
- `settings.json` reste modifiable à la main : la fenêtre ouverte suit ces modifications et ne les écrase pas.

### Écarts par rapport à la spec

- Aperçu commun aux cinq pages, en haut de la fenêtre : trois pilules d'exemple (modéré, vigilance, critique) et leur bande en mode Replié. Il est statique, sans animation en boucle.
- `PillOpacity` s'applique à la forme de la pilule seulement ; la carte reste opaque.
- Modifier une couleur, l'opacité ou un seuil passe au thème Personnalisé, initialisé depuis le thème affiché.
- Sélecteur de couleur : champ `#RRGGBB` et boîte « Couleurs » de Windows.
- Changement de port appliqué au redémarrage.
- Clic gauche sur l'icône : réglages (spec §6) au lieu de l'aperçu de carte 5 s du Plan 2.
- Diagnostic depuis l'icône et la page À propos : `logs\doctor.txt` ouvert dans le Bloc-notes, sans console.

### Reste à faire

- Vérifier la pilule, la carte et la miniature des écrans sur un écran à une autre mise à l'échelle (DPI mixte).
- Tests d'interface automatisés (FlaUI, spec §9) : la vérification reste manuelle, par UI Automation en démo.

### Arbitrages pris pendant l'exécution du Plan 3

- La fenêtre de réglages s'ajuste à la zone de travail de l'écran où elle s'ouvre (marge de 24 DIP) : 940 × 760 débordait d'un écran 1280 × 800.
- La miniature des écrans déclare un pair d'automatisation : un `Canvas` n'en a pas, son identifiant restait invisible pour UI Automation.
- La zone de page revient en haut à chaque changement de page ; les légendes de l'aperçu ont leur propre fond, lisible sur les deux moitiés.
- À chaque activation de la fenêtre, l'état des hooks, de « Démarrer avec Windows » et la liste des écrans sont relus (changement possible depuis l'icône de notification).
- Vérifications faites alors que l'instance réelle de l'utilisateur tournait : fenêtres de la démo retrouvées par identifiant de processus, pilule de démo au quart du bord droit, publication de contrôle hors de `publish\`.

### Points laissés en l'état après le Plan 3

- Pas d'icône propre à la fenêtre de réglages (icône WPF générique).
- Miniature des écrans : focus clavier perdu après le choix d'une tuile, repère de la pilule minuscule avec beaucoup d'écrans, infobulle avec le nom technique `\\.\DISPLAYn`.
- Diagnostic, état des hooks et installation lus ou écrits de façon synchrone sur le thread UI (fraction de seconde).
- Mineurs : miniature recalculée à chaque lecture, repli de `KeyFor` sur l'identifiant brut, repli d'« Ouvrir le dossier » sur le chemin du fichier, pas de plancher sous 48 DIP dans l'ajustement de la fenêtre, « Quitter » pendant la boîte « Couleurs » non testé.
