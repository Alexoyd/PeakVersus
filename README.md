# PEAK Versus

> **Deux duos, une seule montagne, et chaque main tendue peut vous sauver… ou vous lâcher.**

PEAK Versus est un mod pour [PEAK](https://store.steampowered.com/app/3527290/PEAK/) qui ajoute un mode **2 contre 2**. Deux équipes de deux grimpent la même montagne. Vous pouvez vous ignorer, vous entraider, ou vous savonner la planche : couper une corde, piéger un feu de camp, tirer l'autre par la cheville au mauvais moment.

Le mode vise le **chaos rigolo**, pas la rage : les pièges ne tuent jamais, la mort renvoie au feu de camp, et la montagne reste l'ennemi principal.

*English: a 2v2 rivalry mode for PEAK. Two duos, one mountain. Sabotage ropes, trap campfires, pull ankles, owe favours, betray, or escape together. Everything in game is in French for now.*

---

## Comment on gagne

On gagne **en s'échappant au sommet**.

- La première équipe complète au sommet lance la fusée, et un compte à rebours de 75 s démarre.
- **Les 4 joueurs dans la zone à la fin : c'est le Pacte des grimpeurs.** Tout le monde a gagné.
- **Une seule équipe dans la zone :** elle gagne seule. Pendant le compte à rebours, la mêlée est permise : poussez l'autre équipe hors du sommet.
- **Extraction mixte** (1 joueur de chaque équipe) : victoire aux points.
- **Personne :** tout le monde a perdu.

## Ce que le mode ajoute

### Interactions
| Action | Touche | Détail |
|---|---|---|
| Pousser | **F** | Coûte de l'endurance. Fait lâcher prise seulement si la cible a moins de 30 % d'endurance. |
| Attraper la cheville | **G** | Sur quelqu'un qui grimpe juste au-dessus de toi. Il peut se dégager en sautant. Au bord du vide… c'est voulu. |
| Fouiller un sac | (jeu de base) | Par derrière. La victime entend une fermeture éclair. |

Les feux de camp sont des **zones de trêve** : aucune attaque à moins de 15 m.

### Objets
| Objet | Effet |
|---|---|
| **Ciseaux** | Clic gauche maintenu : **entailler** une corde (elle lâchera sous un adversaire). Clic droit maintenu : la couper. |
| **Piton défectueux** | Se plante comme un vrai. Craque puis casse sous le poids d'un adversaire. |
| **Loupe** | Révèle cordes sabotées, pitons défectueux et feux piégés (avec leur type). Dans l'inventaire, elle désamorce les pièges de feu. |
| **Piège de feu** | Piège un feu pour l'équipe adverse : hoquet, poil à gratter, bûche-ressort, fringale ou fumée révélatrice, jusqu'au feu suivant. |
| **Encens du retour** | Très rare. 10 s d'incantation pour ranimer un joueur à l'endroit de sa mort, même un adversaire. |
| **Reliques** | Cachées hors du chemin dans chaque biome. Rapportez-les à un feu. |
| **Bing Bong** | La mascotte. Le porter jusqu'à un feu rapporte des points. Volez-le. |

### Économie et social
- **Points** gagnés aux feux de camp, avec les reliques, les missions secrètes et le fair-play.
- **Boutique** à chaque feu (**B**) : objets, soins, votes. Une cagnotte individuelle, plus une petite cagnotte d'équipe.
- **Dette d'honneur** : aider un adversaire en danger rapporte un point, et il te doit une faveur. S'il la rend, vous gagnez tous les deux.
- **Trahison** : faire tomber quelqu'un juste après l'avoir aidé te colle la marque **PARJURE** au-dessus de la tête et te coûte tes points de fair-play.
- **Missions secrètes**, une par biome.
- **Vote** à chaque feu : le danger montant (brume ou lave) monte plus vite, ou se fige.
- **Modificateurs de biome** tirés au sort : tempête, silence, brouillard, gravité molle, fièvre de l'or, pièges en promo.
- **Chronique de fin de partie** (**F7**) : trahisons, sauvetages, plus grosse chute, dettes non rendues.

### Voix
- **Coéquipiers :** vous vous entendez toujours, à toute distance. Option pour couper si vous êtes sur Discord.
- **Adversaires :** seulement en proximité. Attention à ce que vous dites près d'eux.

### Repris de [PvPEAK](https://github.com/HuFlungDu/PvPEAK)
Équipes par couleur de scout, réapparition au feu de camp, objets protégés près des feux, sacs et marshmallows par équipe, brouillard qui suit l'équipe en tête.

---

## Installation

**Tous les joueurs doivent avoir le mod, dans la même version.** L'hôte est prévenu si quelqu'un ne l'a pas.

### Le plus simple : l'installeur
1. Télécharge `PeakVersus-Setup.exe` depuis les [Releases](../../releases).
2. Lance-le. Windows affichera « Windows a protégé votre ordinateur » : clique sur **Informations complémentaires**, puis **Exécuter quand même**. L'exe n'est pas signé, et son code est dans [`src/PeakVersusInstaller`](src/PeakVersusInstaller).
3. Choisis **1. Installer**. Il trouve PEAK tout seul, installe BepInEx puis le mod.

Le même installeur sert à mettre à jour, à jouer sans mods ou à désinstaller.

### Avec un gestionnaire de mods
Avec [Gale](https://github.com/Kesomannen/gale) ou r2modman : installe **BepInExPack PEAK**, puis place `com.github.Alexoyd.PeakVersus.dll` dans `BepInEx/plugins/`.

## Jouer
1. Choisissez vos équipes : **même couleur de scout = même équipe**. Deux couleurs bien distinctes, par exemple rouge contre bleu. Le lobby affiche les équipes détectées.
2. L'hôte clique sur **Host PEAK Versus**, puis invite les autres.
3. L'hôte règle la partie avec **F6** : spawns séparés ou même zone avec trêve de départ, nombre de biomes, cumul des scores.

| Touche | Action |
|---|---|
| F | Pousser |
| G | Attraper une cheville |
| B | Boutique et vote (près d'un feu) |
| F7 | Scores et chronique |
| F6 | Options de partie |
| F8 | Menu de test (donne des objets, mode solo) |

Les touches se changent dans `BepInEx/config/com.github.Alexoyd.PeakVersus.cfg`.

---

## Compiler
Prérequis : .NET SDK 8 ou plus récent, et PEAK installé.

```
dotnet build -c Release
```

- Le mod sort dans `artifacts/bin/PvPEAK/release/`, l'installeur dans `artifacts/bin/PeakVersusInstaller/release/`.
- Copie `Config.Build.user.props.template` en `Config.Build.user.props` pour déposer automatiquement la DLL dans ton dossier de plugins.

## Crédits et licence
- Basé sur [PvPEAK](https://github.com/HuFlungDu/PvPEAK) de HuFlungDu (GPL-3.0).
- Conçu par Alexoyd. Ce mod n'est pas affilié à Aggro Crab ni à Landfall.
- Licence **GPL-3.0** : voir [LICENSE](LICENSE).
