# Effekt-Probestück: Fackel (Issue #353)

Erstes Probestück des Effektdesigners (Rolle: #352). Eine brennende Fackel – Flamme, Funken, Flackerlicht – in drei Varianten. Szene, Kamera und Fackel-Platzhalter sind bei allen dreien gleich; es ändert sich nur der Effekt. Der Platzhalter (Stiel, Kopf, Halter aus Quadern) wird nicht beurteilt.

## Dateien

| Datei | Inhalt |
|---|---|
| `fackel-blatt.png` | alle drei Streifen untereinander, beschriftet A/B/C |
| `fackel-A-streifen.png`, `-B-`, `-C-` | je 10 Einzelbilder einer Sekunde (alle 0,1 s), Ausschnitt 300 × 330 um die Fackel; darunter ein Balken für die Helligkeit des Fackellichts |
| `fackel-A.gif`, `-B.gif`, `-C.gif` | volles Bild 854 × 480, 20 Bilder je Sekunde, nahtlose Endlosschleife |
| `fackel.py` | erzeugt alles oben |

## Die drei Varianten

- **A – Daumenkino:** Die Flamme ist eine kleine gemalte Textur (9 × 15 Texel, 5 Bilder, 10 je Sekunde) auf einer Fläche, die zur Kamera zeigt, wie Animationstexturen auf der PS1. Das Licht atmet ruhig (±12 %), dazu 3 Funken.
- **B – Teilchenflamme:** Die Flamme besteht aus 12 groben Quadraten, die aufsteigen, schrumpfen und mit dem Alter von hellem zu dunklem Messing wechseln. Dazu 6 Funken. Das Licht flackert unruhig (±32 %), und seine Mitte wandert um einige Zentimeter, sodass sich das Licht an der Wand bewegt.
- **C – Polygonflamme mit Stufenlicht:** Die Flamme besteht aus drei flachen Polygon-Schichten, deren Ecken alle 0,1 s springen. Das Licht fällt nicht weich ab, sondern in vier harten Ringen und springt zwischen drei Helligkeiten. Dazu 8 langsame Glutfunken.

## Szene und Stil

- Wand, Boden und Decke mit den Katakomben-Texturen aus `assets/textures/` (nur gelesen), Flächenhelligkeit, Laterne und Nebel wie in `level.fs` und `LookSettings.cs`. Am Ende werden 15 Bit mit dem 4 × 4-Punktmuster gerastert (ADR 0010), Flamme und Funken eingeschlossen.
- Kamera auf 1,6 m Augenhöhe mit 70° senkrechtem Blickwinkel, 20° zur Seite gedreht und 4° nach unten geneigt. Die Wand liegt 2,4 m vor der Kamera.
- Flamme, Funken und Lichtfarbe stammen nur aus der Reihe *Messing/Gold* der Hauptpalette (Stufen 2 bis 7, Licht = Stufe 5), kein Rot. Teilchen sind harte Quadrate ohne Kantenglättung und ohne Leuchtschein.
- Das Fackellicht ist ein Punktlicht, das zur Laterne hinzukommt: Abfall `(1 − d / 3,6 m)²`, leicht nach der Flächenrichtung gewichtet, gefärbt mit Messing 5.

## Erzeugen

Werkzeug ist das Python, das Blender mitbringt, mit NumPy. Blender selbst rendert nichts. PNG und GIF schreibt das Skript selbst (zlib bzw. eigener LZW-Kodierer); das System-Python hat weder NumPy noch eine Bildbibliothek. Aufruf aus dem Repository-Wurzelverzeichnis:

```
"<Blender-Ordner>\5.3\python\bin\python.exe" design/proben/effekte/fackel/fackel.py
```

Laufzeit etwa 15 Sekunden. Das Ergebnis ist bei jedem Lauf gleich, weil alle Zufallswerte einen festen Seed haben.

## Grenzen

- Ein GIF-Bild kann höchstens 256 Farben haben. Hat ein Bild nach der Rasterung mehr, behält der Kodierer die 255 häufigsten und legt die übrigen auf die nächstliegende davon (das betrifft vor allem das erste, volle Bild). Die PNG-Streifen sind davon nicht betroffen.
- Die GIFs sind 1 bis 4 MB groß, weil sich mit dem Flackerlicht das Punktmuster auf großen Teilen der Wand ändert.
- Das ist eine Annäherung an das Spiel: kein Schatten des Stiels, Boden und Decke als unendliche Flächen.
