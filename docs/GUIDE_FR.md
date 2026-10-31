# Guide du radar de cubes

## Démarrer et arrêter

Npcap doit être installé séparément depuis son site officiel, https://npcap.com.
Le radar ne l’installe pas et n’altère pas son installation.
Fermer une ancienne instance, extraire le ZIP de la version française puis lancer **Radar de cubes FR.exe**.
Le radar attend la connexion du jeu et affiche ses données lorsqu’elles sont reçues.
Pour fermer complètement l’application : icône près de l’horloge → **Quitter**.
Pour ne plus utiliser le radar en jeu, appuyer sur **Ctrl + Maj + F8** : la capture Npcap et les alertes s’arrêtent et le radar se masque. Le même raccourci reprend la capture. Cette pause est mémorisée entre les lancements.
**Ctrl + Maj + F10** masque uniquement l’affichage ; sa capture continue si elle était active.

## Fenêtré sans bordure et déplacement en jeu

Dans les options vidéo du jeu, choisir **fenêtré sans bordure** (ou fenêtré), et non plein écran exclusif.
Appuyer sur **Ctrl + Maj + F9** ou choisir **Déplacer** depuis le radar, ses paramètres ou son icône près de l’horloge.
Le radar reste alors affiché même si le curseur ou un menu du jeu est visible. Faire apparaître le curseur (Alt si nécessaire),
puis cliquer-glisser le radar jusqu’à la position voulue. Cliquer sur **Terminer** ou appuyer de nouveau sur **Ctrl + Maj + F9**.
La position est enregistrée à la fin du déplacement et ramenée sur un écran accessible si elle dépasse ses bords.
Ce mode permet aussi de placer un radar qui était masqué ou en pause, sans reprendre sa capture.
Le radar reste sans bordure et ne demande pas de droits administrateur pour le placement.

## Lire le radar

| Symbole | Signification |
| --- | --- |
| Point bleu au centre | Ta position, possiblement ancienne ou estimée |
| Carré vert plein | Cube actuellement visible dans les données reçues |
| Carré/triangle ambre vide | Dernière position connue d’un cube sorti de portée |
| Marqueur jaune | Cube en cours d’ouverture |
| Triangle vers le haut / bas | Cube plus haut / plus bas ; nombre = différence en mètres |
| ≈ | Hauteur similaire, à trois mètres près |
| Chevrons sur le bord | Cube hors de la portée affichée |
| Pos. … s | Âge de la dernière position du personnage dans le flux |
| ± … m | Position estimée avec une incertitude |

Le résumé indique le cube visible/en cours d’ouverture le plus proche. Les positions ambre ne sont pas considérées comme confirmées.
Les distances sont calculées à partir de la position disponible ; elles peuvent être approximatives.
En l’absence de position, bouger ou utiliser un buff dans le jeu peut fournir un nouveau message de position.
Le radar n’effectue aucune de ces actions à ta place.

## Régler sans se compliquer

**⚙ → Radar** : portée, taille, opacité et axe du nord. Si le nord ne correspond pas à la minicarte, essayer un autre axe.
Les cases de visibilité permettent de masquer le radar hors du jeu ou dans les menus.
Sous Windows, sans droits administrateur, Alt peut être interprété comme une ouverture de menu et masquer le radar.
Décocher « Masquer dans les menus du jeu » si ce comportement gêne.

**⚙ → Alertes sonores** : activer le bip, régler le volume, choisir un WAV non compressé et tester le son.
Le microphone et le son du jeu ne sont pas capturés. Les fichiers WAV très volumineux sont refusés pour limiter la mémoire utilisée.

**⚙ → Aide** : légende, raccourcis, limites et accès au dossier des données.
Tout est sauvegardé immédiatement ; **Fermer** referme uniquement les paramètres.

| Raccourci | Action |
| --- | --- |
| Ctrl + Maj + F8 | Mettre en pause / reprendre la capture et les alertes |
| Ctrl + Maj + F9 | Activer / terminer le placement |
| Ctrl + Maj + F10 | Afficher / masquer |
| Ctrl + Maj + F11 | Ouvrir / fermer les paramètres |
| Ctrl + Maj + F12 | Changer la portée |
| Molette au-dessus du radar | Zoomer / dézoomer |

Le radar reste visible lorsque ses paramètres sont ouverts pour faciliter son placement.
Les menus près de l’horloge restent disponibles si un raccourci est déjà utilisé par une autre application.

## Données et dépannage

Dossier : `%LOCALAPPDATA%\CubeRadarFR`.
`data/settings.json` contient les préférences ; `data/radar.db` contient l’historique des cubes et les données utiles aux estimations.
Aucun fichier de capture brute, journal de session ou historique d’ennemis n’est créé par cette application.
Pour repartir à zéro, quitter le radar puis renommer son dossier de données (cela réinitialise aussi les réglages et l’apprentissage).

Si aucun cube ne s’affiche : vérifier le jeu Global, sa connexion, Npcap et le mode fenêtré/sans bordure.
Le radar ne peut pas deviner un cube que le serveur n’a pas envoyé depuis son démarrage.
Un patch du jeu peut rendre ses codes réseau obsolètes ; consulter le dépôt avant de remplacer des fichiers.
Pour un VPN, l’option avancée `radar --device NOM` permet de sélectionner une carte Npcap.
Si Npcap est réservé aux administrateurs, le radar peut ne pas pouvoir ouvrir la carte sans droits suffisants.

À la reprise après une pause, l’état live et les anciens buffers réseau sont vidés pour ne pas présenter de vieux cubes comme actuels. Les cubes réapparaissent au fil des nouveaux messages du serveur ; quitter puis relancer le jeu peut être nécessaire si le serveur ne renvoie pas les objets déjà proches. L’apprentissage et l’historique des cubes sont conservés.

Les mises à jour se téléchargent manuellement depuis les versions de **leuchartk-web/aion2-cube-radar**.
L’application ne vérifie ni ne télécharge de mise à jour automatiquement.
