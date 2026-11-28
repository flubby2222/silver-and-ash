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

Urteil des Menschen (2026-11-28): a gefällt, aber ohne Unterlage, frei schwebend. Die Empfehlung c wurde nicht gewählt.

## Runde 2: a „Dreistich“ frei schwebend, ohne Leuchtplatte

- Blatt: `runde-2/beute-819-runde-2.png`, Erzeuger `runde-2/beute2.py`, Messwerte `runde-2/beute2-zahlen.txt`, Einzelbilder `runde-2/ansichten/`.

### Wie Beute im Spiel heute schwebt (nur gelesen)

`src/SilverAndAsh.Game/Rendering/PickupView.cs`, `DrawEquipment` (Zeilen 90 bis 109):

- Item-Würfel: Kante 0,16 m, Mitte auf `0.2 + sin(t * 2.5 + Id) * 0.04` m, Drehung `RotationY(t * 1.2 + Id)`, Farbe = Stufenfarbe.
- Auch der Würfel hat heute eine Leuchtplatte (0,45 × 0,45 m, `Blend(Dark, tier, glow)`, `glow = 0.35 + 0.25 * sin(t * 3 + Id)`). Die Stufe zeigen also Platte und Würfelfarbe.
- Stufenfarben: `ChestView.Colours` (Zeilen 85 bis 88) über `TierColours.Of`.
- Nebel und Laterne: `assets/shaders/level.fs` (Zeilen 84 bis 90) mit `LookSettings.FogStart` 3,25, `FogEnd` 18,2, `LanternMinimum` 0,35. `psx.compose("spiel")` rechnet dieselben Formeln und Werte samt 15 Bit mit 4 × 4-Muster; das galt schon für Runde 1.

Daran hält sich die Probe: Unterkante des Stücks 0,12 m (Unterkante des Würfels), Auf und Ab ±0,04 m, Drehung um die Hochachse. Die drei Drehwinkel zeigen je eine andere Höhe.

### Anordnung in der Luft

- **a1:** wie Runde 1 (Spitzen im Kreis mit 34 mm Radius im Skriptmaß, in der Welt 85 mm), nur angehoben. Die Spitzen hängen einzeln in der Luft.
- **a2:** Spitzen unten in einem Punkt zusammen, gleiche Neigung (24° nach außen). Radius 0 bis 34 mm in 2-mm-Schritten geprüft: überall 0 Dreieckspaare Messer gegen Messer.

| Stufe Vampiric, drei Winkel | 3 m: px je Messer mind. | 3 m: geteilt höchstens | 3 m: Umriss Höhe / Breite | 8 m: px je Messer mind. | 8 m: geteilt höchstens | 8 m: Umriss Höhe / Breite |
|---|---|---|---|---|---|---|
| Quader mit Platte (heute) | 962 (ganz) | – | 17 bis 43 / 50 bis 70 | 86 (ganz) | – | 5 bis 9 / 16 bis 25 |
| Item-Würfel mit Platte (heute) | 561 (ganz) | – | 27 bis 30 / 22 bis 26 | 56 (ganz) | – | 8 / 7 bis 9 |
| a1 | 107 | 0 | 68 bis 69 / 48 bis 58 | 15 | 10 | 19 bis 22 / 14 bis 19 |
| a2 | 123 | 57 | 61 bis 63 / 47 bis 52 | 15 | 11 | 21 bis 22 / 15 bis 17 |

### Stufenfarbe ohne Platte

Ohne Platte trägt das Stück die Stufe nicht mehr: Das Band ist in der Datei blutrot, gleich für alle Stufen. Gemessen an a2, Winkel 74°, als Bildpunkte, die sich vom Stück „ohne“ unterscheiden (für alle vier Stufen gleich, ±1):

| | 3 m | 8 m | Aufwand |
|---|---|---|---|
| ohne | 0 | 0 | nichts |
| Band in der Stufenfarbe | 27 | 2 | je Stufe eine Textur 128 × 128 oder das Band als eigenes, gefärbtes Material |
| Knoten: Würfel 0,09 m in der Stufenfarbe an den Spitzen | 139 | 17 | 12 Dreiecke, eine Farbe |

Zum Vergleich: Der heutige Item-Würfel zeigt 561 bis 627 px in 3 m und 56 bis 70 px in 8 m, dazu die Platte.

### Empfehlung Runde 2

**a2 mit Knoten.** Der Knoten zeigt die Stufe in 8 m mit 17 px, das Band mit 2 px. a2 statt a1, weil die Messer in einem Punkt zusammenlaufen und der Knoten dort sitzt; in den Messwerten liegen a1 und a2 gleichauf (je Messer mindestens 15 px in 8 m, Umriss 19 bis 22 px hoch), das ist ein Urteil über die Form, kein Messwert.

### Nicht geprüft (Runde 2)

- Bewegung: Drehung und Auf und Ab sind drei feste Stellen, kein Film.
- Block 2 hat nur einen Drehwinkel.
- Die Stufenfarben des Bandes liegen nicht in der Hauptpalette (Regelprüfung der Art Bible steht aus); die Textur hat mit umgefärbtem Band 12 bis 13 Farben.
- Ein Raum des Spiels mit seinen Wandzonen und Lichtquellen; gerechnet sind Nebel und Laterne des Shaders im Proberaum.
- In Runde 1 teilten sich die Messerkopien ein Netz und damit das Licht des zuletzt gestellten Messers. Die Zählungen dort betrifft das nicht, die Helligkeit der Klingen im Blatt der Runde 1 schon. Runde 2 gibt jedem Messer ein eigenes Netz.

## Nicht geprüft (Runde 1)

- Nebel und Raumlicht eines echten Raums; gemessen ist nur der Proberaum der Runde 5.
- Andere Stufenfarben als Vampiric.
- Die Bewegung des Pulses; geprüft sind drei feste Stellen.
- Ob die Leuchtplatte (hier 0,62 × 0,62 m bei a und b, 0,70 × 0,55 m bei c) zur Formel im Spiel passt, die sie aus der Größe des Quaders ableitet.
- Vergleich mit einem zweiten Beute-Modell aus dem Spiel: Auf dem Blatt steht nur der Quader.
