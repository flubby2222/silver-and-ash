# Throwing Knives als Beute am Boden (Issue #819)

Probestück: drei Entwürfe für die Throwing Knives als Beute, neben dem heutigen Quader. Nichts davon ist im Spiel.

- Blatt: `beute-819.png`
- Erzeuger: `beute.py` (Blender-Python, Aufruf steht im Kopf des Skripts)
- Messwerte: `beute-zahlen.txt`
- Einzelbilder: `ansichten/` (je Entwurf vorn, Seite, hinten, schräg oben; Spielbild 854 × 480 je Drehwinkel)

## Modell

Es gibt keine neue Modelldatei. Die Beute ist dreimal das ganze Messer `../throwing-knives-204/runde-5/messer-e-geschoss.glb` (196 Dreiecke, Textur 128 × 128, 13 Farben), also dasselbe Messer wie das Geschoss. Maßstab 2,5-fach gegenüber dem Skriptmaß (Messer 0,53 m lang; der heutige Quader ist 0,55 m lang). Frei ist nur die Anordnung; sie steht als Formel in `beute.py` (`dreistich`, `dreibein`, `staffel`). Kein Entwurf hat einen Träger.

| Entwurf | Anordnung |
|---|---|
| a „Dreistich“ | Spitzen im Boden (20 mm tief im Skriptmaß), im Kreis je 120°, 24° nach außen geneigt |
| b „Dreibein“ | Griffenden am Boden, Spitzen oben aneinander, 27° nach innen geneigt |
| c „Staffel“ | Spitzen im Boden, alle 28° gleich geneigt, schräg hintereinander, 15/30/45 mm tief, je 40° um die eigene Achse gedreht |

## Messung

Kamera und Licht wie das Beute-Bild der Runde 5 von #204: Auge 1,6 m, Spielbild 854 × 480, Leuchtplatte in der Stufenfarbe Vampiric. Drei Drehwinkel wie im Spiel (`drop.Id * 1,3` rad für Id 0, 1, 2: 0°, 74°, 149°), je Winkel eine andere Stelle des Pulses (0,10 / 0,35 / 0,60).

Bildpunkte je Messer, kleinster Wert über die drei Winkel; „geteilt“ sind Bildpunkte, die mehr als ein Messer belegt, größter Wert über die drei Winkel; Höhe ist die Höhe des Umrisses.

| | Dreiecke | 3 m: px je Messer mind. | 3 m: geteilt höchstens | 3 m: Höhe | 8 m: px je Messer mind. | 8 m: geteilt höchstens | 8 m: Höhe |
|---|---|---|---|---|---|---|---|
| Quader (heute) | 12 | 962 (ganzes Stück) | – | 17 bis 43 | 86 (ganzes Stück) | – | 5 bis 9 |
| a „Dreistich“ | 588 | 109 | 64 | 63 bis 65 | 15 | 18 | 19 bis 21 |
| b „Dreibein“ | 588 | 100 | 82 | 63 bis 69 | 11 | 9 | 20 bis 21 |
| c „Staffel“ | 588 | 128 | 0 | 45 bis 61 | 17 | 0 | 17 bis 20 |

Die Messer durchdringen sich in keinem Entwurf (0 Dreieckspaare Messer gegen Messer). Zum Vergleich aus Runde 5: flach liegend, 2,5-fach, hatte ein Messer in 8 m 11 bis 15 px (ein Winkel gemessen).

## Empfehlung

**c „Staffel“.** Es ist der einzige Entwurf, bei dem in allen drei Winkeln und beiden Entfernungen kein Bildpunkt von zwei Messern belegt ist (0 px gegenüber bis zu 64 px bei a und 82 px bei b in 3 m), und er hat je Messer den größten kleinsten Wert (128 px in 3 m, 17 px in 8 m). Die Staffel nimmt zudem die Staffelung der Hand (s12) auf.

Schwäche von c: Bei 74° stehen die Messer fast hintereinander, der Umriss ist in 8 m dann 20 × 7 px (bei 0° 20 × 19, bei 149° 17 × 23).

## Nicht geprüft

- Nebel und Raumlicht eines echten Raums; gemessen ist nur der Proberaum der Runde 5.
- Andere Stufenfarben als Vampiric.
- Die Bewegung des Pulses; geprüft sind drei feste Stellen.
- Ob die Leuchtplatte (hier 0,62 × 0,62 m bei a und b, 0,70 × 0,55 m bei c) zur Formel im Spiel passt, die sie aus der Größe des Quaders ableitet.
- Vergleich mit einem zweiten Beute-Modell aus dem Spiel: Auf dem Blatt steht nur der Quader.
