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

## Runde 3: pulsierender Schimmer in der Stufenfarbe

Urteil zu Runde 2: Form a2 ist gewählt, Knoten und Band entfallen. Gewünscht ist ein pulsierender Schimmer in der Stufenfarbe, als System für alle Beute.

Skript: `runde-3/schimmer.py` (Aufruf `blender --background --python schimmer.py`, braucht `ffmpeg` im Pfad). Es rendert Gang und Platten einmal, je Bild nur die bewegten Stücke, und setzt Laterne, Nebel und 15-Bit-Raster mit den Formeln aus `assets/shaders/level.fs` zusammen. Ergebnis in `runde-3/`:

- `schimmer-819-runde-3.png`: Standbild-Blatt mit Messwerten.
- `schimmer-<bauart>-a2.gif` und `schimmer-<bauart>-wuerfel.gif` für a, b, c, dazu `schimmer-heute.gif` (Würfel mit Platte) und `schimmer-ohne.gif` (a2 ohne Schimmer). Spielbild 854 × 480, 20 Bilder/s, 105 Bilder = 5,25 s, nahtlose Schleife (eine Drehung, zwei Auf und Ab). Vorn 3 m, hinten 8 m; von links Worn, Forged, Royal, Vampiric.
- `schimmer-zahlen.txt`: alle Zählungen.

Farben aus `ChestView.Colours` (Vampiric auf der Mitte seines eigenen Pulses gehalten). Bewegung wie `PickupView.DrawEquipment`: Auf und Ab ±0,04 m, Drehung um die Hochachse.

### Die drei Bauarten

| | Was pulsiert | Pulsdauer | Stärke | Einbau |
|---|---|---|---|---|
| A Aufleuchten | das Stück selbst, in vier harten Stufen | 1,75 s | a2: Fläche zu 20 bis 70 % in die Stufenfarbe gemischt; Würfel: Helligkeit 60 bis 100 % | `LevelShader.SetFlash` und `SetGlow` je Stück in `PickupView`; beides gibt es schon, keine neue Geometrie |
| B Bodenschein | gerasterte Scheibe am Boden unter dem Stück (drei Ringe, Radius 0,16 / 0,27 / 0,36 m, 15 mm über dem Boden), Stufenfarbe unbeleuchtet, im Nebel | 2,625 s | Anteil gesetzter Rasterpunkte 4 bis 12 von 16 in der Mitte, außen 60 % und 30 % davon | Scheibe als Netz mit `SetPattern`; neu im Shader: Bildpunkte außerhalb des Musters verwerfen statt sie mit der Fläche zu füllen (etwa zwei Zeilen in `level.fs`, ein Uniform) |
| C Funken | sechs unbeleuchtete Würfel (0,05 m, oben 0,03 m), die auf einem Kreis mit 0,25 m Radius von 0,06 auf 0,78 m steigen | 1,75 s je Schub, Lebensdauer 0,8 s | 4 bis 6 Funken gleichzeitig | `ShadedBox.DrawGlowing` wie die Motes in `StatusView`; nichts Neues im Shader |

Ein weicher Lichthof um das Stück ist nicht gebaut: Die Art Bible verbietet weiches Glühen und Leuchtschein („helle Dinge hellen die Umgebung auf“). B ist die Fassung, die dieser Regel folgt; C ist der Lichthof als harte Einzelstücke.

### Messung: Bildpunkte, die die Stufe tragen, in 8 m

Gezählt sind Bildpunkte, die sich vom selben Bild ohne Stufenfarbe um mehr als 16 unterscheiden (Summe der drei Kanäle). Angabe dunkelstes / hellstes Bild des Pulses, darunter der kleinste Wert im ganzen Puls.

| | Worn | Forged | Royal | Vampiric | kleinster Wert im Puls | kleinster Farbabstand zweier Stufen, dunkel / hell |
|---|---|---|---|---|---|---|
| heute: Würfel mit Platte | 156 / 168 | 158 / 151 | 152 / 162 | 155 / 170 | 138 bis 150 | 23 / 18 |
| A, a2 | 9 / 81 | 1 / 72 | 23 / 74 | 14 / 68 | 0 bis 22 | 14 / 36 |
| A, Würfel | 62 / 76 | 40 / 69 | 56 / 69 | 63 / 76 | 28 bis 56 | 21 / 50 |
| B, a2 | 16 / 64 | 15 / 61 | 16 / 62 | 15 / 61 | 15 bis 16 | 58 / 60 |
| B, Würfel | 79 / 115 | 73 / 115 | 72 / 132 | 71 / 133 | 63 bis 72 | 39 / 47 |
| C, a2 | 12 / 30 | 11 / 26 | 6 / 27 | 14 / 25 | 6 bis 11 | 63 / 60 |
| C, Würfel | 74 / 87 | 72 / 92 | 63 / 96 | 63 / 101 | 60 bis 63 | 37 / 40 |

Das engste Stufenpaar ist überall Worn gegen Vampiric. In 3 m (Werte in `schimmer-zahlen.txt`): B an a2 349 bis 353 / 1174 bis 1191 px, A an a2 170 bis 513 / 447 bis 552, C an a2 98 bis 176 / 170 bis 234, heute 1803 bis 2207.

Lesart:

- Keine Bauart erreicht die Menge von Würfel plus Platte (138 px und mehr). B am Würfel kommt mit 63 bis 133 px am nächsten und trennt die Stufen dabei besser (Abstand 39 bis 47 gegen 18 bis 23).
- A verliert die Stufe im dunklen Bild: an a2 in Forged 1 px, im Puls bis 0. Worn auf a2 hebt sich vom braunen Boden kaum ab.
- B trägt die Stufe unabhängig von der Eigenfarbe des Stücks: an a2 in allen vier Stufen 15 bis 16 px im dunkelsten Bild.
- C bleibt an a2 bei 6 bis 14 px im dunkelsten Bild und zerfällt in Einzelpunkte.

### Verwechslung mit Gegner- und Treffer-Effekten

Farbabstand der Stufenfarben zu belegten Effektfarben (ohne Nebel, Summe der Kanalabstände geteilt durch drei): Forged gegen Reif dunkel 16, gegen Kälte 61; Vampiric gegen Warnung Gorm 26, gegen kritischen Treffer 59; Royal gegen Glut Gorm 65, gegen Schlüssel 82; Worn mindestens 102.

- A benutzt denselben Weg wie das Trefferblitzen der Gegner (`SetFlash`) und das heilige Glühen der Waffe; Vampiric blitzt damit rot auf einem Stück am Boden.
- B liegt wie die Frostscheibe am Boden und ist gerastert wie sie. Die Frostscheibe ist größer (Radius 0,62 m), hat einen hellen Rand und liegt unter einem Gegner; Forged liegt farblich nur 16 neben ihrem dunklen Ton. Das ist die engste Stelle der Empfehlung.
- C sind dieselben 0,05-m-Würfel wie die Motes, die im Statuseffekt um Gegner kreisen; hier steigen sie, dort kreisen sie.

### Empfehlung Runde 3

**B Bodenschein.** Grund: im dunkelsten Bild des Pulses trägt er an a2 in 8 m in jeder Stufe 15 bis 16 px (A 1 bis 23, C 6 bis 14), und der kleinste Farbabstand zweier Stufen ist 58 bis 60 (A 14 bis 36, heute 18 bis 23). Er ist die einzige Bauart, die an Würfel und a2 gleich aussieht, also als System taugt. Preis: eine kleine Shader-Erweiterung, weniger Bildpunkte als die heutige Platte, und es liegt wieder etwas am Boden, wenn auch durchsichtig und rund.

### Nicht geprüft (Runde 3)

- Die GIFs haben eine Palette von 256 Farben je Film; A an a2 hat bis 415 Farben in einem Bild, C 267, B 253. In den GIFs von A und C sind einzelne Farbtöne daher auf die nächste Palettenfarbe gelegt. Gemessen ist am unverfälschten Bild, nicht am GIF.
- Die Filme laufen mit 20 Bildern/s, das Spiel mit 60.
- Ein Raum des Spiels, mehrere Beutestücke dicht beieinander, Beute neben einem Gegner mit Frost.
- Der eigene Puls von Vampiric (hier auf der Mitte gehalten) zusammen mit dem Schimmer-Puls.
- Versetzte Phasen je Stück (im Spiel über die Id); hier pulsieren alle acht Stücke gleichzeitig.
- Münzen und Schlüssel, die keine Stufe haben.
- Der Shader ist in Python nachgerechnet, nicht im Spiel gelaufen.

## Nicht geprüft (Runde 1)

- Nebel und Raumlicht eines echten Raums; gemessen ist nur der Proberaum der Runde 5.
- Andere Stufenfarben als Vampiric.
- Die Bewegung des Pulses; geprüft sind drei feste Stellen.
- Ob die Leuchtplatte (hier 0,62 × 0,62 m bei a und b, 0,70 × 0,55 m bei c) zur Formel im Spiel passt, die sie aus der Größe des Quaders ableitet.
- Vergleich mit einem zweiten Beute-Modell aus dem Spiel: Auf dem Blatt steht nur der Quader.
