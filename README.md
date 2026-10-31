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
3. Cliquer sur **⚙** pour régler le radar ou le déplacer en le faisant glisser.
4. Utiliser la molette pour changer la portée : 60, 120 ou 250 mètres.
5. Quitter depuis l’icône près de l’horloge Windows.

**[Guide français et légende des symboles](docs/GUIDE_FR.md)**

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
