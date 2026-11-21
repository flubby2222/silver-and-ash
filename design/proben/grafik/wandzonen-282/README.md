# Probestück Wandzonen und Bänder (Issue #282)

Probe außerhalb des Spiels, nichts unter `assets/` geändert. Übersicht: `blatt-282.png`, Texturen: `texturen-blatt.png` und `texturen/`, Ansichten: `ansichten/`.
Erzeugt mit `wandzonen.py` (Blender 5.3, `blender --background --python wandzonen.py`), zweimal ausgeführt, alle 46 PNG bit-gleich.
Die Wandpfeiler-Modelle in `eingang/` sind unverändert aus `origin/feat/wandpfeiler-536` (Commit 47b91b6) kopiert, weil der Pfeiler noch nicht auf `main` ist.

## Zonenhöhen

| Zone | Gang 3,5 m | Raum 6 m | Vorsprung |
|---|---|---|---|
| Sockel | 0 bis 0,5 m | 0 bis 0,5 m | 1/16 m |
| Fläche | 0,5 bis 3,0 m | 0,5 bis 5,5 m | Wandebene |
| Abschluss | 3,0 bis 3,5 m | 5,5 bis 6,0 m | 1/8 m, untere Schräge 1/8 m |

Begründung: Das ist die Formenlehre. Der Sockel (0,5 m) ist die Höhe von Wandpfeiler-Sockel und Nischenbank (0,5 m); das Kapitell des Pfeilers (3,0 bis 3,5 bzw. 5,5 bis 6,0) deckt den Abschluss. Die Vorderebene des Abschlusses (1/8 m) ist die Tiefe des Gang-Pfeilers; im Raum (0,3125 m) ragt der Pfeiler darüber hinaus. Alle Maße liegen auf dem 64er-Raster (1/16 m = 4 px). Der Türbogen (Scheitel 3,458 m) schneidet im Gang den Abschluss, deshalb endet dort das Band.

## Textur-Aufbau

Ein Band ist ein Muster mit Periode 128 px (2 m = 1 Zelle). Ein Lauf aus n Zellen ist Anfang (64 px) + (n−1) × Mitte (128 px) + Ende (64 px) = 128 n px. Das Ende ist die zweite Hälfte der Mitte, der Anfang die erste; dazu kommt nur der Abschluss an der Stirnkante (Lichtkante bzw. dunkle Kante, 1 px) und beim Ende ein weggelassener Schmalstein. Ein 1-Zellen-Stück ist Anfang + Ende. Die Mitte wiederholt sich nahtlos (im Skript geprüft).

- Sockel: 32 px hoch, untere Schicht 16 px (Stoßfugen 0/56/96), obere 12 px Fläche + 4 px Fase (versetzt 28/76/108). Lagerfuge auf Zeile 16 (0,25 m).
- a: große Quader mit Fase. b: wie a, aber obere Schicht mit Kehle und Wulst.
- Abschluss: 32 px, oben 24 px Front, unten 8 px dunkle Schräge. a: glattes Gesims, Lagerfuge Zeile 16. b: Spitzbogenfries (4 Bögen je Mitte, 4-px-Pfeiler, Öffnung 28 px).
- Alle Farben aus der Reihe Stein der Hauptpalette (Indizes 6 bis 14, im Skript geprüft); Steinfläche ist die heutige `catacomb_wall.png`.

## Regeln für Anfang, Mitte, Ende

- Jeder Lauf beginnt mit Anfang und endet mit Ende, auch an Ecken. Keine kahle Kante.
- Tür: Das Band endet an der Türzelle (Ende) und beginnt hinter ihr neu (Anfang). Die Türzelle (2 m, Rahmen mit 0,125-m-Pfostenband) gehört dem Türmodell. Im Raum (6 m) läuft der Abschluss durchgehend über der Tür (Bogen reicht nur bis 3,46 m).
- Innenecke: Das Ende der einen Wand trifft den Anfang der anderen. Beim Bogenfries bilden beide Halbpfeiler (je 2 px) einen ganzen Pfeiler in der Ecke.
- Außenecke: ebenso, Ende der durchlaufenden Wand gegen Anfang der Rückwand. Die Stirnflächen von Sockel und Abschluss sind kleine Flächen (1/16 bzw. 1/8 m tief).
- 1-Zellen-Stück: Anfang + Ende, ohne Mitte. Beide Hälften sind ganze Steine (kleinster Stein 20 px).
- Wandpfeiler: steht auf einer Zellgrenze und ersetzt dort den Pfeiler des Bogenfrieses (Bogenfries-Pfeiler liegen auf Vielfachen von 0,5 m).

## Offene Fragen

1. Die heutige Wandtextur hat Schichten von 13/19/12/20 px, nicht 16 px. Die Formenlehre verlangt Lagerfugen alle 0,25 m durch alle Zonen. Sockelfuge 0,5 m und Abschlussfuge passen, die Fläche dazwischen nicht. Soll die Wandtextur auf 16 px umgestellt werden?
2. Soll der Abschluss im Raum dicker sein (1 m)? Hier gleich 0,5 m, im Raum wirkt das Band sehr schmal.
3. Wo liegt die Grenze zwischen Türmodell und Band? Hier hat die Türzelle keinen Sockel; ein Türschwellenstein könnte ihn ersetzen.
4. Der Wandpfeiler (Modell von #536) ist heller als die Wandtextur; sein Atlas sollte beim Einbau auf dieselbe Stein-Spanne.
5. Das Gelb-Rosa der Reihe Stein wirkt in den hellen Bildern rosa. Das ist die Palette, nicht die Zonen.

## Empfehlung

Abschluss b (Spitzbogenfries) verwenden: Er liest sich in Spielbeleuchtung auf 3 m und auf 8 m und ist eindeutig gotisch. Sockel a (große Quader mit Fase) verwenden: Das Profil von b ist im Spiel kaum zu sehen und kostet Fugen. Die Texturen sind unabhängig, die Mischung a-Sockel mit b-Abschluss geht ohne Änderung der Geometrie. Je Ebene eigene Textur, gleiche Formen.

Hinweis zu den Ansichten: Im Raum (6 m) blickt die Nahansicht um 22° nach oben, damit der Abschluss sichtbar ist; der Sockel steht in `weit` und `ecke`.
