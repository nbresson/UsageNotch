# UsageNotch — Le logo du fournisseur comme voyant d'activité

Date : 2026-09-20
Statut : validé en brainstorming, en attente de relecture avant plan d'implémentation.

## 1. En une phrase

Les trois marques d'activité — arc tournant, anneau pulsant, point — cèdent la place à un seul
glyphe au centre de la pile d'anneaux : la marque du fournisseur, dont la **couleur** dit l'état et
le **mouvement** le confirme.

## 2. Origine

Deux entrées du backlog se rejoignent. La première demandait un logo de fournisseur au centre des
anneaux, pour savoir de quel outil parle la pilule le jour où il y en aura plusieurs ; elle avait
été refusée en l'état, le trou central ne faisant que 12 DIP. La seconde notait que l'anneau
intérieur, à Ø 20, devient illisible aux petites échelles.

Faire porter l'activité par le logo résout les deux d'un coup : les marques d'activité libèrent la
couronne Ø 52 qu'elles occupent depuis le chantier précédent, la pile d'anneaux peut donc grandir,
et le trou central double.

## 3. Décisions arrêtées

| # | Question | Décision | Raison |
|---|---|---|---|
| 1 | Ce qui distingue les états | La **couleur** porte l'état, le mouvement le confirme | Quatre états, quatre couleurs : on lit l'état sur une image fixe. Le mouvement seul ne distingue pas « fixe » de « en pulsation à son sommet » |
| 2 | L'état de repos | Le logo reste, en gris neutre | Seule façon que l'identité du fournisseur soit visible au moment où on se demande de quel outil il s'agit — c'est-à-dire quand rien ne se passe |
| 3 | L'état « terminé » | Le logo reprend sa **couleur de marque** | Lecture « tout est fait, la marque est rendue telle qu'elle est ». Coût accepté : une couleur de thème de plus, et la carte garde son bleu pour le même état |
| 4 | Taille du glyphe | 20 DIP dans un trou de 24 | Mesuré : la marque est nette à 20 et 30 DIP |
| 5 | Échelle 40 % | Dégradation assumée, pas de géométrie de repli | À 8 DIP la marque devient une tache, mais **sa couleur reste parfaitement lisible** : on perd l'identité, pas l'information d'état. Une seconde géométrie coûterait deux jeux de formes à maintenir pour le cas où tout est déjà minuscule |
| 6 | Contraste de mouvement | Assumé | La marque est un éclat à symétrie radiale : sa rotation se verra moins que le balayage d'un arc sur un cercle vide. La couleur compense pour la lecture ; le signal périphérique s'affaiblit |

## 4. Géométrie

Cotes en DIP à l'échelle 100 %, multipliées par `Settings.Scale` à l'exécution. La fenêtre de la
pilule reste **64 × 136**, dont une zone de contenu de **64 × 104**.

### Pile d'anneaux, agrandie

| Anneau | Fenêtre de limite | Diamètre | Bande tracée (rayons) |
|---|---|---|---|
| Extérieur | session | 56 | 24 → 28 |
| Milieu | hebdomadaire tous modèles | 44 | 18 → 22 |
| Intérieur | hebdomadaire par modèle | 32 | 12 → 16 |

Épaisseur 4, écart bord à bord 2, **trou central libre de 24 DIP**. Rappel de l'arithmétique, qui
est la source d'erreur du chantier précédent : un `ProgressRing` de côté `D` et d'épaisseur `T`
trace sa bande entre `(D − 2T) / 2` et `D / 2`, parce qu'il pose son rayon à `(D − T) / 2` et centre
un stylo de largeur `T` dessus.

L'anneau extérieur atteint le rayon 28, soit 4 DIP de marge jusqu'au bord du corps, qui fait 64
d'épaisseur. L'hôte de la pile reste **56 × 56**.

### Le glyphe

20 DIP au centre, 2 DIP de dégagement de chaque côté dans le trou de 24.

Mode « anneaux et pourcentage » : 56 + 4 de marge + 18 de texte = **78 DIP sur les 104**
disponibles. **La pilule ne s'allonge dans aucun des deux modes.** Sur les bords haut et bas, le
`StackPanel` bascule en horizontal comme aujourd'hui.

`PillMetrics` voit ses trois diamètres passer à 56 / 44 / 32, perd `ActivitySize` — plus aucune
marque ne vit autour de la pile — et gagne `LogoSize` = 20.

## 5. L'asset

Le tracé vient de `src/UsageNotch.App/Assets/anthropic.svg`, fourni par l'utilisateur : `viewBox`
de 248, un seul `<path>`, un seul sous-tracé, une seule couleur. Ses bornes mesurent 235,6 × 235,6
à partir de (6,2 ; 6,2) — parfaitement carré et centré.

Deux conséquences heureuses : **aucune règle de remplissage à déclarer**, le tracé n'ayant pas de
contre-forme, et **aucune coordonnée à retranscrire**, `Stretch="Uniform"` dans une boîte de 20 DIP
faisant la normalisation.

Le SVG reste dans `Assets/` comme source de référence. Le tracé lui-même est transcrit une fois
dans une `Geometry` statique et gelée, exposée par une petite classe de `UsageNotch.App.Controls` :
la pilule et l'aperçu des réglages en ont besoin tous les deux, et analyser du XML au démarrage pour
une forme qui ne change jamais n'aurait pas de sens.

## 6. Les quatre états

| État | Couleur | Mouvement |
|---|---|---|
| Aucune activité | `Theme.RingTrack` | fixe |
| En cours | `Theme.Running` | rotation, 1,2 s |
| En attente | `Theme.Attention` | pulsation vers sa version désaturée |
| Terminé | `Theme.LogoDone` | fixe |

`PillPresenter.ActivityColor` rend **déjà** `RingTrack` pour `ActivityKind.None` : l'état de repos
ne demande aucune règle nouvelle, seulement de dessiner le glyphe au lieu de ne rien dessiner.

La pulsation se fait par deux tracés superposés — le glyphe désaturé dessous, le glyphe coloré
dessus — dont on anime l'opacité du dessus. Pas d'effet WPF : ils coûteraient cher sur une fenêtre
en couches, dont chaque image est rendue en logiciel.

Les cadences plafonnées sont conservées telles quelles : 30 images/s pour la rotation, 20 pour la
pulsation, et aucune animation ne tourne si sa cible n'est pas visible.

## 7. Couleurs et thème

`Theme` gagne **une** couleur, `LogoDone` : `#D97757` chez Codenotch — la couleur de marque du SVG —
et `#B0B0B0` chez Monochrome, où une teinte de marque jurerait et où cette valeur reste distincte de
`Running` (`#E0E0E0`) et d'`Attention` (`#FFFFFF`). Elle rejoint les replis de `Theme.Clamp()` et la
page Apparence comme un `ColorSlot` de plus, clé `LogoDone`, libellé « Logo, session terminée ».

`Theme.Done` **subsiste et garde son libellé** : la carte de détail s'en sert pour ses lignes de
session, et les notifications de seuil ne la touchent pas. Elle ne décrit simplement plus ce que
montre la pilule pour cet état — c'est le coût accepté de la décision 3, et il doit être écrit dans
le journal de bord pour que personne ne « corrige » l'un vers l'autre plus tard.

Les trois couleurs d'anneaux, les couleurs de niveau et leurs seuils ne bougent pas.

## 8. Contrat de présentation

`CellModel` gagne `ActivityMutedColor` à côté de `ActivityColor` : la version désaturée que la
pulsation croise. `ActivityColor` intègre le nouvel état « terminé » :

```csharp
public static string ActivityColor(ActivityKind kind, Theme theme) => kind switch
{
    ActivityKind.Attention => theme.Attention,
    ActivityKind.Running => theme.Running,
    ActivityKind.Done => theme.LogoDone,
    _ => theme.RingTrack,
};
```

La désaturation vit dans `UsageNotch.Presentation.Formatting.HexColor`, à côté de `TryNormalize` :

```csharp
public static string Desaturate(string hex)   // luminance Rec. 709 : 0,2126 R + 0,7152 V + 0,0722 B
```

Elle est donc calculée hors WPF et testable comme le reste, conformément à la règle du projet.
`#FFBF00` donne un gris clair d'environ `#BFBFBF`, nettement visible sur le fond sombre de la
pilule : la phase « noir et blanc » se lit comme telle et non comme une disparition.

### Les trois consommateurs de la couleur d'activité

`ActivityColor` alimente aujourd'hui **trois** surfaces, ce qu'il faut savoir avant de la modifier :

| Surface | Appel | Ce qu'elle dessine |
|---|---|---|
| Pilule | `CellModel.ActivityColor`, lié en XAML | le futur glyphe |
| Icône de notification | `CellModel.ActivityColor`, `TrayIconService` | un point de 4 px de rayon |
| Carte de détail | `PillPresenter.ActivityColor(...)`, `CardPresenter.SessionRowOf` | la pastille de chaque ligne de session |

L'icône de notification est un miroir de la pilule et doit la suivre : elle passera au terracotta
pour « terminé », sans travail particulier, puisqu'elle lit le même champ.

La carte est le cas qui demande une décision. Elle appelle la **même fonction** que la pilule, donc
elle afficherait elle aussi le terracotta. **C'est une correction de l'analyse faite en
brainstorming**, où j'annonçais que la carte garderait le bleu sans avoir vérifié qu'elle partageait
la fonction. Deux réponses possibles, à trancher à la relecture de cette spec :

- **A.** La carte suit la pilule : un seul appel, rien à faire, `Theme.Done` devient inutilisée et
  doit alors quitter le thème et la page Apparence.
- **B.** La carte garde `Theme.Done` : `CardPresenter` cesse d'appeler `PillPresenter.ActivityColor`
  et prend sa propre correspondance, ce qui duplique quatre lignes mais sépare deux langages
  visuels qui n'ont pas à rester liés.

La spec retient **B** par défaut, parce que la décision 3 a été prise en sachant que la carte
garderait son bleu, et qu'un réglage nommé « Session terminée » qui ne colorerait plus rien serait
un piège. La duplication est de quatre lignes et se teste.

## 9. Comportements aux limites

- **Échelle 40 %** : glyphe à 8 DIP, illisible comme marque, parfaitement lisible comme couleur.
  Assumé (décision 5).
- **Rotation peu contrastée** : assumée (décision 6). Si l'usage montre que le signal périphérique
  manque, la réponse ne sera pas d'accélérer la rotation mais de rendre un élément asymétrique —
  sujet de conception à part entière, hors de ce chantier.
- **Aucun changement** pour les seuils, le placement, les hooks, le repli, le masquage en plein
  écran, ni pour les trois anneaux eux-mêmes en dehors de leur diamètre.

## 10. Tests

TDD dans `UsageNotch.Core` et `UsageNotch.Presentation`.

**Core** — `Theme.Clamp()` replie `LogoDone` sur Codenotch quand elle manque ; les deux préréglages
la définissent ; `LogoDone` reste distincte des autres couleurs d'activité dans chaque préréglage.

**Presentation** — `HexColor.Desaturate` sur des couleurs connues, dont un gris déjà neutre, qui
doit rester lui-même ; `ActivityColor` rend `LogoDone` pour « terminé » et `RingTrack` pour « aucune
activité » ; `ActivityMutedColor` est la désaturée de `ActivityColor` pour chaque état ; la nouvelle
arithmétique de cotes — bandes 24→28, 18→22, 12→16, écarts de 2, trou central de 24, glyphe de 20
tenant dans le trou, pile et pourcentage tenant dans `BodyLength` ; `CardPresenter` garde
`Theme.Done` pour ses lignes de session (option B).

## 11. Vérification à l'exécution

En `--demo`, par captures et UI Automation, selon les procédures du Plan 3 :

- les quatre états, aux échelles 40 %, 100 % et 150 %, sur les quatre bords ;
- la rotation et la pulsation tournent quand elles doivent et s'arrêtent quand la pilule est repliée
  ou masquée ;
- le glyphe ne mord sur aucun anneau ;
- `PillPreview` de la page Apparence fidèle à la vraie pilule ;
- la carte de détail affiche toujours le bleu « Session terminée » pendant que la pilule affiche le
  terracotta — c'est la vérification qui prouve l'option B.

Branche `feat/activity-logo`.

## 12. Hors périmètre

- **Plusieurs fournisseurs.** Ce chantier prépare l'identité visuelle mais ne rend rien
  multi-fournisseur : la pilule suppose toujours un seul `IUsageProvider`, et le glyphe est celui
  d'Anthropic, non choisi dynamiquement. Le jour venu, le glyphe devra venir du fournisseur.
- L'arité fixe du présentateur et les cotes du XAML à relier à `PillMetrics`, deux points de backlog
  hérités du chantier précédent, qui restent au backlog.
- Toute modification des seuils, du placement, des hooks ou de la fenêtre de réglages au-delà du
  `ColorSlot` ajouté.
