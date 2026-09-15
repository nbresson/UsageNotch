# UsageNotch

Une petite « encoche » collée au bord de l'écran Windows qui affiche l'usage de vos quotas Claude et l'état de vos sessions Claude Code, sans ouvrir de fenêtre.

Inspiré de [codenotch](https://github.com/vinzdg/codenotch) (MIT), réécrit en C# / .NET 10 / WPF pour corriger ce qui gênait sous Windows : animations lentes, zone invisible qui bloquait les clics, peu de personnalisation, pas de mode discret, pas de choix de l'écran.

## Fonctionnalités

- **Pilule au bord de l'écran** : anneau d'usage de la session en cours, couleur selon le niveau (modéré, vigilance, critique), pourcentage, activité des sessions (en cours, en attente, terminée).
- **Carte de détail au survol** : chaque fenêtre de limite (session, hebdomadaire tous modèles, hebdomadaire Opus) avec l'heure de réinitialisation, et la liste des sessions ; un clic ramène au terminal de la session.
- **Aucune zone fantôme** : ce qui n'est pas dessiné laisse passer les clics vers l'application du dessous.
- **Trois modes** : Déplié, Replié (une fine bande de 2 à 12 px qui se déplie au survol), Masqué (icône de notification seule).
- **Placement libre** : bord droit, gauche, haut ou bas, position le long du bord (Alt + glisser), n'importe quel écran, mise à l'échelle par écran.
- **Personnalisation** : préréglages Codenotch, Monochrome, Accent système ou Personnalisé (dix couleurs, opacité, seuils), échelle de 40 à 150 %, contenu de la cellule.
- **Fenêtre de réglages** avec aperçu en direct ; toutes les modifications s'appliquent sans redémarrer (sauf le port).
- **Alertes de seuil** : notification Windows quand une fenêtre de limite franchit un seuil réglable (80 % par défaut) puis 100 %.
- **Plein écran** : la pilule se masque pendant un jeu, une vidéo ou un diaporama sur son écran.
- **Ouverture automatique** de la carte et son quand une session se termine ou attend une réponse.
- **Démarrer avec Windows**, diagnostic intégré, journaux quotidiens conservés 7 jours.

Interface en français.

## Prérequis

- Windows 10 ou 11.
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) pour exécuter l'application publiée.
- [Claude Code](https://docs.anthropic.com/claude-code) installé et connecté au moins une fois (`claude`), pour que ses identifiants existent.

Pour compiler :

- SDK .NET 10 (version épinglée par `global.json`).
- Pour publier le hook en Native AOT : Visual Studio 2022 ou plus récent avec la charge de travail « Développement Desktop en C++ ».

## Installation

```powershell
git clone https://github.com/nbresson/UsageNotch.git
cd UsageNotch
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
publish\UsageNotch.App.exe
```

Le script publie l'application et le hook dans `publish\`. Il vide ce dossier avant de publier : quittez UsageNotch avant de le relancer.

Ensuite, dans les réglages (clic gauche sur l'icône de notification) :

1. **Claude Code › Installer les hooks** : ajoute les hooks d'UsageNotch à `%USERPROFILE%\.claude\settings.json`. Une copie horodatée du fichier est enregistrée avant la modification ; « Désinstaller » ne retire que ces hooks.
2. **Comportement › Démarrer avec Windows**, si vous le souhaitez.

Les sessions déjà ouvertes dans Claude Code prennent en compte les hooks à leur prochain démarrage.

## Utilisation

| Action | Effet |
|---|---|
| Survol de la pilule | Ouvre la carte de détail |
| Clic gauche sur la pilule | Garde la carte ouverte (ou la libère) |
| Alt + glisser la pilule | Déplace la pilule le long du bord |
| Clic droit sur la pilule | Rafraîchir, garder la carte ouverte, réglages, quitter |
| Clic gauche sur l'icône de notification | Ouvre les réglages |
| Relancer `UsageNotch.App.exe` | Ouvre les réglages de l'instance déjà lancée |

Options de ligne de commande :

- `--demo` : données fixes, réglages et journaux dans `%TEMP%\UsageNotch-demo`, sans toucher à la vraie configuration ni appeler l'API.
- `doctor` : diagnostic en console (identifiants, hooks, port, écrans, dernière lecture), aussi écrit dans `logs\doctor.txt`.

## Données et confidentialité

- Réglages, dernière lecture, alertes envoyées et journaux : `%APPDATA%\UsageNotch\`. `settings.json` reste modifiable à la main et est relu à chaud.
- Lecture de l'usage : UsageNotch lit le jeton OAuth de Claude Code dans `%USERPROFILE%\.claude\.credentials.json` et interroge `https://api.anthropic.com/api/oauth/usage`. Cet endpoint n'est pas documenté publiquement ; s'il change, la pilule affiche un statut d'erreur plutôt qu'un chiffre inventé.
- Le jeton et le contenu des prompts ne sont jamais écrits dans les journaux ni dans le diagnostic.
- Les hooks envoient les événements de session à l'application sur `127.0.0.1` uniquement (port 48666 par défaut).

## Développement

```powershell
dotnet build UsageNotch.sln
dotnet test
src\UsageNotch.App\bin\Debug\net10.0-windows\UsageNotch.App.exe --demo
```

Organisation :

| Projet | Rôle |
|---|---|
| `src/UsageNotch.Core` | Lecture de l'usage, sessions, hooks, réglages, placement ; sans interface |
| `src/UsageNotch.Hook` | Exécutable Native AOT appelé par Claude Code, relaie l'événement à l'application |
| `src/UsageNotch.Presentation` | Textes, modèles d'affichage, ViewModels ; sans WPF, testé |
| `src/UsageNotch.App` | Application WPF : fenêtres, interop Win32, icône de notification |
| `tests/` | Tests xUnit de Core et Presentation |

La conception et les plans d'implémentation sont dans [`docs/superpowers`](docs/superpowers) (en français).

## Licence

[MIT](LICENSE). Projet inspiré de [codenotch](https://github.com/vinzdg/codenotch) de vinzdg, également sous licence MIT ; le code d'UsageNotch est une réécriture indépendante.
