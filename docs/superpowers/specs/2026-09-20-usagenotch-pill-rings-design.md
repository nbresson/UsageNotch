# UsageNotch — Pilule à trois anneaux

Date : 2026-09-20
Statut : validé en brainstorming, en attente de relecture avant plan d'implémentation.

## 1. En une phrase

La pilule cesse de n'afficher que la session en cours : elle montre trois consommations d'un coup
d'œil — session, hebdomadaire tous modèles, hebdomadaire du modèle coûteux — sous la forme de trois
anneaux concentriques, chacun de sa couleur.

## 2. Origine

La carte de détail affiche déjà toutes les fenêtres de limite renvoyées par l'API ; la pilule n'en
montre qu'une, désignée par `IUsageProvider.HeadlineWindowId`, figée à `"session"`. Les données des
trois indicateurs sont donc **déjà lues et déjà modélisées** : `ClaudeUsageParser` produit une
`LimitWindow` par entrée de `limits[]`. Ce chantier est un travail de présentation, pas
d'acquisition.

## 3. Décisions arrêtées

| # | Question | Décision | Raison |
|---|---|---|---|
| 1 | Disposition | Trois anneaux **concentriques** Ø 44 / 32 / 20 | Aucune croissance de la pilule ; l'alternative alignée aurait coûté +36 DIP le long du bord |
| 2 | Ordre extérieur → intérieur | Session, hebdo tous modèles, `weekly_scoped` | Ordre de fréquence de consultation ; conserve la continuité avec la pilule actuelle, où le grand anneau est déjà la session |
| 3 | Pourcentage écrit | Retiré du mode par défaut, conservé en option | L'information est sur la carte ; l'option garde un repli chiffré aux petites échelles |
| 4 | Couleurs | Une couleur fixe par anneau, **ou** coloration par niveau, au choix de l'utilisateur | L'utilisateur tranche entre lisibilité des trois indicateurs et alerte de niveau d'un coup d'œil |
| 5 | Voyants d'activité | Sortis de la pile, tracés **autour** d'elle à Ø 52 | Le trou central ne fait plus que 12 DIP : l'arc de rotation et l'anneau d'attente n'y tiennent pas |
| 6 | Fenêtre absente | Piste seule, sans masquage | C'est déjà la convention de l'anneau actuel quand il n'y a pas de lecture exploitable |

## 4. Géométrie

Toutes les cotes sont en DIP à l'échelle 100 %, multipliées par `Settings.Scale` à l'exécution.

La fenêtre de la pilule mesure `Thickness` × `WindowLength` = **64 × 136**, dont une zone de contenu
(`PillShapeBuilder.Body`) de **64 × 104**, les deux congés de 16 DIP étant exclus. Ces cotes ne
changent pas.

### Pile d'anneaux

| Anneau | Fenêtre de limite | Diamètre | Bande tracée (rayons) |
|---|---|---|---|
| Extérieur | session | 44 | 18 → 22 |
| Milieu | hebdomadaire tous modèles | 32 | 12 → 16 |
| Intérieur | hebdomadaire par modèle | 20 | 6 → 10 |

Épaisseur 4, écart bord à bord 2, **trou central libre de 12 DIP**. Tous partent de midi et tournent
dans le sens horaire, comme aujourd'hui.

### Voyants d'activité

L'arc de rotation (« en cours ») et l'anneau pulsant (« en attente ») passent à **Ø 52**, épaisseur
2,5 : ils occupent les rayons 24,75 à 27,25, soit 2,75 DIP de dégagement au-dessus de l'anneau
extérieur et 4,75 DIP de marge jusqu'au bord du corps. Le point « terminé » reste au centre à Ø 8,
dans les 12 DIP libres.

### Hôte et modes

L'hôte de la pile passe de 44 × 44 à **56 × 56** pour contenir les voyants d'activité. Le mode
« anneaux et pourcentage » empile 56 + 4 de marge + 18 de texte = **78 DIP sur les 104**
disponibles : **la pilule ne s'allonge dans aucun des deux modes**. Sur les bords haut et bas, le
`StackPanel` bascule en horizontal comme aujourd'hui et le calcul reste valable.

Ces cotes rejoignent `PillMetrics`, qui centralise déjà les dimensions logiques et sert à la fois la
pilule et son aperçu dans les réglages : `RingHostSize`, `RingOuter`, `RingMiddle`, `RingInner`,
`RingBandThickness`, `ActivitySize`. `RingSize` disparaît.

## 5. Rendu

Trois instances du `ProgressRing` **existant**, empilées dans le `Grid` de l'hôte avec
`Width`/`Height` explicites. Le contrôle déduit déjà son rayon de `min(ActualWidth, ActualHeight)`
et anime sa propre fraction sur 300 ms : **il n'est pas modifié**.

L'alternative — un contrôle unique dessinant N arcs — économiserait deux éléments visuels minuscules
au prix d'un `OnRender` et d'une logique d'animation à réécrire. Écartée.

Les voyants d'activité restent des enfants de l'hôte, dont la taille explicite les laisse déborder
sans influencer la mise en page. Leurs animations en boucle, leurs cadences plafonnées et la règle
« ne tourner que si la cible est visible » sont inchangées.

## 6. Contrat de présentation

`CellModel.RingFraction` et `RingColor` cèdent la place à :

```csharp
IReadOnlyList<RingModel> Rings        // ordre documenté : extérieur → intérieur
record RingModel(double? Fraction, string Color, string TrackColor)
```

Le XAML lie `Cell.Rings[0]`, `[1]`, `[2]`. Le reste de `CellModel` — `PercentText`, `ShowPercent`,
`Dimmed`, `Exhausted`, `Activity`, `ActivityColor`, `BandColor` — ne bouge pas. `PercentText` reste
celui de la session.

`ShowRing` **disparaît** : les deux modes de contenu survivants dessinent tous deux la pile, le
champ serait constamment vrai. `PillWindow.UpdateAnimations()`, qui s'en sert aujourd'hui pour ne
pas animer une cible invisible, cesse de le consulter — la pile étant toujours dessinée, seules la
visibilité de la fenêtre et celle du calque de la pilule comptent encore.

`IUsageProvider.HeadlineWindowId` devient :

```csharp
IReadOnlyList<IReadOnlyList<string>> RingWindowIds   // trois groupes, extérieur → intérieur
```

trois groupes d'alias ordonnés, parce que le `kind` renvoyé par l'API varie d'un compte et d'une
version à l'autre. `UsageSnapshot` gagne la surcharge qui va avec : `Window(IEnumerable<string>
aliases)`, qui rend la première fenêtre dont l'`Id` figure dans le groupe, ou `null`.

| Anneau | Alias acceptés, dans l'ordre d'essai |
|---|---|
| Session | `session`, `five_hour` |
| Hebdo tous modèles | `weekly_all`, `seven_day`, `weekly` |
| Hebdo par modèle | `weekly_scoped`, `weekly_opus`, `seven_day_opus` |

C'est le raisonnement d'alias que `ClaudeUsageParser` applique déjà à ses replis. Le fournisseur de
démonstration passe de `weekly_opus` à `weekly_scoped` pour que `--demo` montre le trio réel.

`Exhausted` reste calculé sur la **session**, afin de ne pas changer le comportement du repli et des
transitions qui s'en servent.

## 7. Couleurs

`Theme` gagne trois couleurs : `RingSession`, `RingWeeklyAll`, `RingWeeklyScoped`. Elles sont
définies dans les préréglages Codenotch et Monochrome, ajoutées aux replis de `Theme.Clamp()`, et
exposées comme trois `ColorSlot` de plus dans la page Apparence — l'infrastructure existe, c'est une
entrée de liste chacune. En préréglage Accent système, l'accent alimente `RingSession`, comme il
alimente déjà `LevelAmple` et `Running`.

Le **mode de coloration** va dans `Settings`, pas dans `Theme` : logé dans `Theme`, changer de
préréglage réinitialiserait le choix de l'utilisateur.

```csharp
enum RingColoring { PerRing, ByLevel }   // défaut : PerRing
```

En `PerRing`, chaque anneau prend sa couleur fixe. En `ByLevel`, chaque anneau prend
`Theme.LevelColor` de **sa propre** fraction ; les trois couleurs fixes restent réglables mais sans
effet, ce que le libellé de la page doit dire.

Les couleurs de niveau et leurs deux seuils continuent de servir la carte — `CardPresenter.Row` les
emploie déjà pour chaque ligne de fenêtre — et les notifications de seuil, dans les deux modes.
Elles ne deviennent donc jamais des réglages morts.

## 8. Réglages et migration

`Settings.CurrentVersion` passe de 1 à 2.

`Settings` gagne `RingColoring Coloring`. Le réglage « Coloration des anneaux » rejoint la page
Apparence, à côté du contenu de la cellule, alimenté par une liste `Choices.RingColorings` —
« Une couleur par anneau » et « Selon le niveau ».

**Piège à respecter impérativement.** `SettingsStore` sérialise les enums avec
`JsonStringEnumConverter`, qui lève `JsonException` sur une valeur inconnue — et `Load()` traite
alors le fichier entier comme corrompu, le copie en `settings.json.corrupt-<secondes unix>` et
repart sur les défauts. Retirer `PercentOnly` de l'enum `CellContent` effacerait donc tous les
réglages de quiconque avait choisi ce mode. En conséquence :

- `CellContent.PercentOnly` **reste** dans l'enum ;
- il quitte `Choices.CellContents`, qui se réduit à « Anneaux et pourcentage » et « Anneaux seuls » ;
- `Settings.Clamp()` le remappe silencieusement sur `RingAndPercent`.

Un `settings.json` en version 1 se relit sans perte. Un thème personnalisé dépourvu des trois
nouvelles couleurs reprend celles de Codenotch par le repli `Or()` déjà en place.

## 9. Comportements aux limites

- **Fenêtre absente de la lecture**, lecture en attente, ou `NeedsAuth` : fraction nulle, piste
  seule. La géométrie ne bouge pas ; un anneau n'est jamais masqué, pour que la position de chacun
  reste apprenable.
- **Quatrième fenêtre renvoyée par l'API** : visible sur la carte, sans anneau dédié. Le trio est
  fixe.
- **Lecture périmée** : le grisé porte sur la pile entière, comme aujourd'hui sur l'anneau unique.
- **Échelle 40 %** : l'anneau intérieur tombe à 8 DIP de diamètre. C'est assumé — c'est le prix de la
  disposition concentrique, et le mode « anneaux et pourcentage » offre le repli chiffré.

## 10. Tests

TDD dans `UsageNotch.Core` et `UsageNotch.Presentation`, selon le cycle du projet.

**Core** — `Theme.Clamp()` sur un thème partiel privé des trois nouvelles couleurs ; aller-retour
JSON d'un `settings.json` en version 1 ; relecture d'un fichier portant `"PercentOnly"` (ne doit ni
lever, ni déclencher la copie `.corrupt`, et doit donner `RingAndPercent`).

**Presentation** — `PillPresenter` produit trois anneaux dans l'ordre documenté ; résolution des
alias, y compris un `weekly_opus` hérité ; fenêtre manquante donnant une fraction nulle ;
`NeedsAuth` donnant trois pistes ; les deux modes de coloration ; `Exhausted` toujours calculé sur la
session ; grisé de péremption.

## 11. Vérification à l'exécution

Le rendu se vérifie en `--demo`, par captures et UI Automation, selon les procédures du Plan 3 :

- pile concentrique sur les quatre bords, aux échelles 40 %, 100 % et 150 % ;
- voyants d'activité qui ne mordent ni sur les anneaux ni sur le contour de la pilule, dans les trois
  états (en cours, en attente, terminé) ;
- les deux modes de contenu, les deux modes de coloration ;
- `PillPreview` de la page Apparence fidèle à la vraie pilule ;
- l'anneau intérieur reste distinguable à l'échelle 40 % sur un écran à 100 % et à 150 % de mise à
  l'échelle Windows.

Branche `feat/pill-rings`.

## 12. Hors périmètre

- **Logo de fournisseur au centre des anneaux.** Inscrit au backlog, rattaché à l'extension
  multi-fournisseurs, qui tranchera d'abord la question dont il dépend : une pilule par fournisseur,
  ou une seule à bascule.
- Toute modification de la carte, des notifications de seuil, du placement ou des hooks.
- Rendre le trio configurable, ou le déduire dynamiquement des fenêtres renvoyées.
