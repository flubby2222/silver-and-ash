# Probestück zerbrechende Säule (Issue #594)

Erzeugung: `blender --background --python saeule_bruch.py -- alles` (Modi: modelle, ansichten, blatt, anim; `quick` für kleine Bilder).
`saeule_basis_539.py` ist die Kopie des angenommenen Säulen-Skripts (#539) und dient als Bibliothek.

## Zustände (Dreiecke, alle geschlossen, geprüft mit `tools/models/check_closed.py`)
| Zustand | Datei | Dreiecke |
|---|---|---|
| Z0 heil | modelle/saeule_z0_heil.glb | 284 |
| Z1 nach Schlag 1 | modelle/saeule_z1_treffer1.glb | 380 |
| Z2 nach Schlag 2 | modelle/saeule_z2_treffer2.glb | 384 |
| Z3 zerbrochen | modelle/saeule_z3_bruch.glb | 156 |

- Textur je Zustand 280 x 384 (Stein-Reihe der Hauptpalette, nur Stein-Stufen). Frische Bruchflächen sind heller (Stufen 11 bis 13), Schutt mittel.
- Lesbarkeit aus 10 m über die Silhouette: Z1 hat einen schrägen Keil auf der Trefferseite (20 Grad von +x zur Vorderseite), Z2 einen tieferen Keil und ein um 3 Grad geneigtes Oberteil, Z3 nur Stumpf (0,74 m) mit Schutt.
- Z3: kein Deckungsvolumen über Kniehöhe, der Weg bleibt frei. Der Schutt ist flach (unter 0,25 m).
- Ursprung bei allen Zuständen: Mitte der Grundfläche am Boden.

## Animation (bruch-animation.gif, bruch-posen.png)
60 Ticks pro Sekunde, GIF mit 3-Tick-Bildern (20 B/s). Ablauf: Tick 0 Treffer (Z2), Ruck Tick 0 bis 6 (5 cm Versatz, -1,2 Grad), Lösen der Teile ab Tick 6 bis 12, Landungen Tick 21 (Unterteil), 24 (Taille), 31 und 38 (Oberteil in zwei Stücken), 46 (Kapitell), Tausch auf Z3 bei Tick 51. Gesamtdauer 51 Ticks (0,85 s). Stilisierte Schwerkraft 30 m/s2, deterministisch. Das Oberteil ist für die Animation in zwei Stücke geteilt (gleiche Form wie Z2). Staub: Vorschlag an den Effektdesigner, Wolke am Stumpf Tick 21 bis 46.

## Regeln der Art Bible
Flat Shading, Textur aus Palette, Requisiten-Budget 100 bis 400 Dreiecke, geschlossene Modelle (Regel 21), Silhouette (Regel 25), eigener Stil, nichts heruntergeladen.

## Unsicher
- Augenhöhe: Aufgabe nannte 1,8 m, Code und Art Bible 1,6 m; Blatt nutzt 1,6 m.
- Z1 und Z2 sind im Spiellicht auf 10 m unterscheidbar, aber nicht stark; der Keil von Z1 ist bewusst kleiner.
- Die Animation ist ein Entwurf (starre Teile), keine Endfassung.

## Runde 2 (Dateien mit -r2, Runde 1 bleibt stehen)
- Z1 jetzt kleiner Ausbruch (etwa ein Drittel), Säule gerade, 2 Schuttstücke; Z2 großer Keil über die halbe Schaftdicke, Oberteil 5,5 Grad geneigt, abgeplatzte Ecke am Kapitell, 6 Schuttstücke. Bruchflächen mit je 2 bis 3 Facetten (Zusatzebenen `FACETS` im Skript).
- Dreiecke: Z0 284, Z1 392, Z2 398, Z3 156 (alle geschlossen). Modelle in `modelle-r2/`.
- Animation startet aus der neuen Z2-Form; Ablauf und Ticks unverändert (Landungen 21, 22, 32, 38, 46; Tausch auf Z3 bei Tick 51).
- Entscheidung der Leitung: Stumpf gibt keine Deckung, die Deckung endet mit dem dritten Treffer sofort.
