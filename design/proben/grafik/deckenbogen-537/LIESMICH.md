# Deckenbogen (Gurtbogen) quer über den Gang, Probestück zu Issue #537

Nur Probestück außerhalb des Spiels. Kein Einbau, kein Eintrag in `assets/`.

## Erzeugen

```
blender --background --python design/proben/grafik/deckenbogen-537/deckenbogen.py
```

Das Skript lädt `../wandpfeiler-536/wandpfeiler.py` (unverändert) und nutzt dessen Pfeilerfassung `gang_traeger`. Zwei Läufe liefern byte-gleiche Dateien (Hashes verglichen).

## Maße (Meter)

- Gang 2,0 breit, 3,5 hoch. Kämpfer (Oberkante Kapitell) auf 2,0. Lichte Weite am Kämpfer 1,75.
- Gedrückter Spitzbogen aus zwei Kreisbögen, Radius innen 1,5, außen 1,625, Mittelpunkte bei x = +-0,625 auf y = 2,0.
- Scheitel innen 3,364, außen 3,5 (Ring 1/8 dick, 0,5 tief). Fasen 1/16 an der Laibung.
- 6 Keilsteine je Hälfte, radiale Fugen alle 11 Grad. Schlussstein durchgehend ohne Fuge am Scheitel.
- Fuß und Schlussstein weiten außen auf Radius 1,75 auf und stecken in Wand und Decke, so bleibt keine nackte Kante.

## Lichte Höhen (Laibung)

- Gangmitte: 3,364 m.
- 0,35 m neben der Wand (x = +-0,65): 2,790 m.
- Spieler (1,8 m) und Gruftwächter (Stab 2,52 m) schneiden die Öffnung nirgends. Reine Zierde. Bild `lichte-hoehe.png` (Wächter blau, Spieler rot).

## Dreiecke

Bogen 152, je Pfeiler 182, je Modell 516. Alle drei Modelle rundum geschlossen (`tools/models/check_closed.py`: 0 offene Kanten).

## Textur

Atlas 128 x 128, Welt-UV mit 64 px je Meter, flach schattiert, keine Vertex-Farben nötig. Atlas-Farben 10 (a), 9 (b), 11 (c), alle aus der Reihe Stein der Hauptpalette. Keilsteine aus Ausschnitten der Pfeilerkachel, jeder zweite eine Stufe dunkler, Fuge 3 px in `Stein[lo+LIFT-2]`.

## Bilder

- `spielaufloesung.png`, `auge-hell.png`: Blick entlang des Gangs auf 1,6 m
- `schraeg.png`, `schraeg-aussen.png`: schräg von außen
- `detail-kaempfer.png`, `detail-scheitel.png`
- `folge-drei.png`, `folge-oben.png`: drei Bögen im Abstand 4 m
- `vergleich-tuer.png`: Vergleich mit Türrahmen am Gangende
- `einzel.png`, `blatt-vorn.png`, `lichte-hoehe.png`
- `wandvarianten/`: Wand b und c
- `modelle/deckenbogen_{a,b,c}.glb` und `.png`

## Schwächen

- Fugenkontrast in Spielauflösung schwach, aber lesbar; nur eine Fugenfarbe.
- Die Bogenhälften sind Kopien, nicht gespiegelt.
- Die Zähne sind eine Regelfläche zwischen den Ringen.
- Rückseiten der Pfeiler zeigen eine Dreiecksnaht (schon in 536 dokumentiert, im Spiel unsichtbar).
- Kapitell-Oberseite und Fußkappe überdecken sich hinter dem Kapitell.

## Offene Fragen

- Der Pfeilervorsprung reicht bis x = -0,875: Figuren breiter als 0,45 m berühren ihn bei x = +-0,65. Gilt für Wächter und Pfeiler auch ohne Bogen.
- Soll die Fugenfarbe zwei Stufen dunkler als die Pfeilerfugen bleiben?
- Soll die Decke die Wandkachel nutzen?
