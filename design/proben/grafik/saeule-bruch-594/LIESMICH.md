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

## Runde 3 (Dateien mit -r3, Skript saeule_bruch_r3.py, Modelle in modelle-r3/)
Urteil des Auftraggebers: "wie herausgesägt", etwas detailreicher, nicht so übertrieben; Animation langsamer, wuchtiger, mit Staub.
- **Bruch statt Sägeschnitt:** keine rechtwinkligen Schnittflächen und keine glatte Platte mehr. Der Bruch besteht aus 4 bis 5 schrägen Facetten je Seite mit gezackter Kante und einer Taille mit Grat; die Bruchflächen-Textur ist grob (Poren, Splitter, Adern). Von der Bruchkante laufen Risse aus, dazu feinere Haarrisse.
- **Mehr Detail:** abgeplatzte Kanten an den Trommelfugen (eingedrückte Ringecken), Schutt in drei Größen (Z1 4, Z2 9, Z3 12 Stücke).
- **Weniger übertrieben:** Z2-Keil etwa 40 Prozent der Schaftdicke, Oberteil 3,5 Grad geneigt (vorher 5,5).
- **Dreiecke (alle geschlossen, 0 offene Kanten):** Z0 284, Z1 464, Z2 488, Z3 148. Das Nennbudget von 400 ist bei Z1 und Z2 überschritten, genutzt ist die erlaubte Grenze 500 für diese Probe.
- **Z3:** gezackter Stumpf, höchster Punkt 0,82 m (unter 0,85 m), ein schmaler Zahn, keine Deckung.
- **Animation (bruch-animation-r3.gif, bruch-posen-r3.png), 60 Ticks/s, GIF in 3-Tick-Bildern:**
  - Tick 0 bis 10 Halten und Knirschen (Zittern 1 cm), Staubpuff Tick 2.
  - Tick 10 bis 34 Oberteil kippt um die Bruchkante, Winkel wächst mit dem Quadrat der Zeit (bis 14 Grad), Puff Tick 14.
  - Tick 28 Taille reisst ab, großer Staub. Tick 34 bis 36 Oberteil frei (Stücke U1, U2, Kapitell), Schwerkraft 30 m/s2.
  - Landungen: Taille Tick 44, Unterteil 48, Oberteil 57 und 63, Kapitell 70. Kein Springen: senkrechte Geschwindigkeit 0, Waagerechtes 60 Prozent, danach Rutschen mit Reibung, Stücke kippen flach.
  - Tausch auf Z3 bei Tick 81 (1,35 s), Staub dort am größten und verdeckt den Tausch.
- **Staub (Vorschlag an den Effektdesigner):** 5 bis 11 kantige, abgeflachte Oktaeder je Ereignis (8 Dreiecke), Farbe aus der Stein-Palette, wachsen in 7 Ticks, schrumpfen bis Tick 30 (0,5 s), driften nach außen und oben. Ereignisse: Ticks 2, 14, 28, 44, 48, 57, 63, 70, 81. Im GIF ohne Wandkulisse, damit der Staub vor dem dunklen Grund lesbar ist.
- **Regeln unverändert:** Stumpf gibt keine Deckung, die Deckung endet mit dem dritten Treffer.
- **Unsicher:** Z3-Stumpf ist ein Haufen mit Zacken, die Brocken wirken im Spiellicht auf 10 m eher als Schutt denn als Säulenrest. Der Staub ist im Spiel ohne Transparenz nur als helle Kantenform lesbar. Die Oberteile prallen in der Animation leicht ineinander (starre Körper ohne Kollision).
