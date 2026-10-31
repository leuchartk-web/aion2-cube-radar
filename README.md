# Radar de cubes · Français

Une version française, simplifiée et consacrée aux **cubes cachés d’AION 2**.
Basée sur [dr89mh/aion2-cube-radar v0.9.2](https://github.com/dr89mh/aion2-cube-radar/tree/v0.9.2),
commit `c9c8497ed03b06f0b281f431e0c1ce6204c7c866`. Ce fork indépendant ne modifie pas le dépôt de l’auteur.

## Télécharger

Ouvrir les [versions françaises](https://github.com/leuchartk-web/aion2-cube-radar/releases).
Choisir le ZIP Windows x64, puis extraire les fichiers dans un dossier permanent.
L’EXE contient .NET ; il n’installe pas l’application dans Windows et ne configure aucun démarrage automatique.
Le ZIP contient également la licence et les sources correspondantes.

**Prérequis :** Windows 10/11 64 bits, Npcap déjà installé, AION 2 Global en mode fenêtré ou plein écran sans bordure.
L’application n’installe pas Npcap. Elle ne demande pas automatiquement les droits administrateur.
Si Npcap a été installé en mode réservé aux administrateurs, son accès peut nécessiter ces droits.

## Utilisation

1. Lancer AION 2 puis double-cliquer sur **Radar de cubes FR.exe**.
2. Les cubes apparaissent avec leur distance et leur hauteur relative.
3. Appuyer sur **Ctrl + Maj + F9** pour placer le radar : afficher le curseur du jeu (Alt si nécessaire), faire glisser le radar, puis appuyer à nouveau sur ce raccourci ou sur **Terminer**. Les menus du jeu ne le masquent pas pendant le placement.
4. Utiliser la molette pour changer la portée : 60, 120 ou 250 mètres.
5. Appuyer sur **Ctrl + Maj + F8** pour une vraie pause : la capture réseau et les alertes sont coupées, le radar est masqué. Le même raccourci reprend la détection. La pause est mémorisée au prochain lancement.
6. **Ctrl + Maj + F10** masque seulement l’affichage. **Quitter**, près de l’horloge, ferme entièrement l’application.

**[Guide français et légende des symboles](docs/GUIDE_FR.md)**

## Position et diagnostic (0.9.5)

Le bouton **Rechercher ma position** est accessible sous le radar, dans les paramètres et près de l’horloge.
Il rouvre la capture et réinitialise uniquement le décodage du transport réseau, en conservant la position et les cubes déjà connus.
Il ne reprend pas un radar mis en pause et n’envoie aucune commande au jeu. Les demandes sont espacées de cinq secondes.
Le diagnostic affiche le nombre de paquets reçus, de flux reconnus et de messages décodés, puis l’état de la position.
La présence d’une position ne signifie pas qu’un cube a été découvert : le point bleu est ton personnage et « Aucun cube repéré » indique seulement l’absence de cube identifié dans les données reçues.

Les coordonnées reçues dans un message de chargement ou téléportation sont désormais affichées même si l’identifiant du personnage n’a pas encore été reçu. Aucun identifiant n’est inventé.
L’âge de la position affichée continue d’augmenter même si le flux s’arrête.
Un bouton ne peut pas contraindre le serveur à transmettre des coordonnées ; utiliser manuellement un buff sur soi-même ou changer de zone peut aider.
La cause exacte d’une absence de position dans une session réelle nécessite le diagnostic et n’est pas déduite de ces seules corrections.

## Correction Alt / curseur (0.9.5)

L’apparition du curseur, y compris avec **Alt**, ne masque plus le radar et ses boutons.
La détection de menus fondée sur le curseur et la lecture de l’état de la touche Alt ont été retirées.
L’ancien réglage `OverlayHideInMenus` est ignoré, même s’il était activé dans les préférences de la version précédente.
Le radar reste donc visible dans les menus du jeu ; **Pause**, **Afficher / masquer** et le masquage hors du jeu restent disponibles.
Aucun besoin de lancer le radar comme administrateur pour cette correction.

## Fenêtré sans bordure, placement et pause (0.9.4)

Dans les options vidéo d’AION 2, sélectionner **fenêtré sans bordure** (ou fenêtré). Le radar utilise une fenêtre native toujours au-dessus, sans injection dans le jeu. Le plein écran exclusif reste incompatible.
Les contrôles **Déplacer** et **Pause** sont accessibles directement sous le radar, dans les paramètres et près de l’horloge.
Le placement reste possible si le radar était masqué ou en pause, et sa position est conservée entre les lancements.
La pause arrête le moniteur Npcap, ferme la carte de capture, coupe l’audio et abandonne l’état live et les buffers de réassemblage.
La reprise repart d’un flux neuf : les cubes ne réapparaissent qu’après de nouveaux messages du serveur. L’apprentissage et l’historique persistent.

## Ce qui change

- Interface native française : radar, menus, fenêtres, paramètres, infobulle d’orientation, notifications et messages propres à l’application.
- Paramètres répartis en trois onglets : **Radar**, **Alertes sonores**, **Aide**, avec sauvegarde immédiate.
- Radar agrandi par défaut, contraste renforcé et résumé du cube le plus proche.
- Aucun affichage ni suivi des ennemis dans l’application : les messages de contacts ne sont pas traités par le moteur en mode cubes.
- Suppression de l’interface web et de son serveur local, du vérificateur de mises à jour et des marqueurs de diagnostic.
- Aucun enregistrement brut des paquets, journal de session ou recensement des messages dans l’application.
- Rafraîchissement visuel à 4 images/s quand le radar est visible, vérification à 1 Hz lorsqu’il est masqué.
- Carte de capture fermée lorsque le jeu ou sa connexion disparaît ; vérification toutes les 3 secondes.
- Paramètres et historique des cubes dans `%LOCALAPPDATA%\CubeRadarFR`, séparés de ceux de la version originale.

Il s’agit de réduire les fonctions et le travail inutiles. Aucune mesure chiffrée de gain de CPU/mémoire en jeu n’est annoncée.
La capture réseau et le réassemblage du flux restent nécessaires : on ne peut pas filtrer chaque message de cube directement dans Npcap sans décoder le flux.
Les routines de suivi des joueurs et de mise à jour de la bibliothèque amont restent dans les sources pour préserver ses tests,
mais elles ne sont pas activées par cette application et aucune interface ne permet de les activer.

## Fonctionnement et limites

Le radar **lit passivement le trafic réseau via Npcap**. Ce n’est pas un détecteur audio.
Il ne lit pas la mémoire du jeu, n’injecte pas de paquets et n’automatise aucune action.
Les alertes sonores sont seulement des sons produits par le radar à la découverte d’un cube.
La détection, le réassemblage TCP, le décodage des messages imbriqués et les calculs de position des cubes proviennent du projet amont.

Une lecture passive ne garantit ni l’autorisation par l’éditeur, ni l’absence de détection ou de sanction.
Ce fork n’ajoute aucun mécanisme de dissimulation ou de contournement de l’anti-cheat.
Les positions peuvent être anciennes ou estimées ; les codes réseau peuvent changer après un patch du jeu.
Une ouverture de cube peut contenir l’identifiant de son acteur : cela reste utilisé pour le cycle du cube, sans créer de suivi de joueurs.
L’historique SQLite conserve les positions des cubes, leurs états et les échantillons utiles à la calibration de position.

## Vérification et compilation

Le [workflow Windows](https://github.com/leuchartk-web/aion2-cube-radar/actions) installe le SDK .NET 10 **sur le runner GitHub**,
exécute les tests .NET hors Npcap, les tests Python et la vérification des clés de traduction,
compile l’EXE autonome et produit des aperçus de l’interface sans lancer de capture.

```powershell
dotnet test CubeRadar.slnx -c Release --filter "Needs!=Npcap"
python -m unittest discover -s tests -p "test_*.py"
./tools/package.ps1
```

Les tests ajoutés comparent le cycle des cubes et la position du joueur entre le moteur amont et le mode sans contacts,
et vérifient qu’un jeu de messages d’ennemis ne crée aucun contact dans ce mode.
Des tests de cycle de capture vérifient la pause, la reprise et le lancement en pause. Des contrôles Windows natifs vérifient le placement, la mémorisation des coordonnées et les boutons, sans lancer AION 2 ni Npcap.
Les tests qui exigent Npcap sont exclus du runner. La compilation et les tests hors ligne ne constituent pas une validation en jeu.
L’EXE n’est pas signé. Ne désactivez pas les protections Windows ; consultez les sources, le workflow et les sommes SHA-256 avant toute utilisation.

## Traduction et mises à jour amont

Les textes français sont dans [`src/CubeRadar.Core/Localization/fr.json`](src/CubeRadar.Core/Localization/fr.json),
appelés par des clés stables avec `Text.T`. Les noms des champs réseau, des options de commande et du schéma SQLite restent inchangés.
La mise à jour amont doit être revue manuellement pour ne pas réintroduire les ennemis, le serveur ou l’enregistrement.
Le [README amont archivé](docs/README.upstream.md) et [FINDINGS.md](docs/FINDINGS.md) sont des références techniques non traduites ;
leurs instructions ne décrivent pas toutes les fonctions de ce fork.
Les messages provenant directement de Windows, de Npcap ou de bibliothèques tierces peuvent suivre la langue de ces composants.

## Licence et attribution

**GPL-3.0**, voir [LICENSE](LICENSE), conservée sans modification. Crédit à dr89mh et aux auteurs amont,
notamment cyberbadger6969/aion2-dps-meter pour les composants dérivés signalés dans les sources.
Modifications de ce fork : 31 octobre 2026, traduction française, ergonomie et fonctionnement limité aux cubes.
Toute redistribution doit conserver les notices et fournir les sources correspondantes sous GPL-3.0.
