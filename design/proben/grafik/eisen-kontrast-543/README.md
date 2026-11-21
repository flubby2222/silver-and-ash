# Probestück #543, Runde 2: neutrales Eisen, kontrastreicher

Anlass: Mit der Eisen-Reihe der ersten Probe (Helligkeit 30/50/74/104) verschwinden Nieten und Beschläge auf dem Holz der Tür,
die Eisentür wirkt flach. Das Holz bleibt wie heute im Spiel (Pixel für Pixel unverändert, vom Skript geprüft).
Nichts unter `assets/` wurde angefasst; das ist ein Probestück außerhalb des Spiels.

## Empfehlung: K2

| Stufe | Hex | RGB | Helligkeit |
|---|---|---|---|
| 0 | `#202021` | 32, 32, 33 | 32 |
| 1 | `#3E3E3F` | 62, 62, 63 | 62 |
| 2 | `#68686A` | 104, 104, 106 | 104 |
| 3 | `#949497` | 148, 148, 151 | 148 |

Weitere Reihen, alle neutral (R = G, B zwei Prozent höher, Sättigung höchstens 0,03):

| Reihe | Stufe 0 | Stufe 1 | Stufe 2 | Stufe 3 | Niet minus Beschlag | Beschlag minus Holz (Normaltür) |
|---|---|---|---|---|---|---|
| Probe 1 | `#1E1E1F` | `#323234` | `#4A4A4C` | `#68686B` | +30 | -2 (zu schwach) |
| K1 | `#222223` | `#3A3A3B` | `#58585A` | `#7C7C7E` | +36 | +12 (zu schwach) |
| **K2** | `#202021` | `#3E3E3F` | `#68686A` | `#949497` | +44 | +28 |
| K3 | `#1E1E1F` | `#404041` | `#747476` | `#B0B0B4` | +60 | +40 |

Niet = Stufe 3, Beschlag = Stufe 2. Das Holz der Normaltür hat mittlere Helligkeit 76; der Beschlag liegt bei K2 gut 28 darüber.

Warum K2: Die Beschläge heben sich vom Holz ab (K1 noch nicht, dort liegt der Beschlag nur 12 über dem Holz), Nieten sind
sichtbar, und die Eisentür behält Tiefe, ohne dass das Eisen neben dem Mauerwerk (mittlere Helligkeit etwa 110) hervorsticht.
K3 gibt noch mehr Kontrast an der Normaltür, aber Eisentür und Fallgitter werden heller als die Wand und ziehen den Blick auf
sich; Stufe 3 mit Helligkeit 176 ist dann der hellste Ton der Umgebung. K3 wäre die Ausweichwahl, wenn Beschläge im Spielbild
noch zu leise wirken.

## Zuordnung auf den Tür-Atlanten

Die Tür-Atlanten (normal, locked, boss) liegen nicht in der Palette; die Eisenfarben werden deshalb nach Rang auf die vier Stufen
gelegt (Tabellen `STUFE` im Skript). Bretter und Kanten der Normaltür sowie das Messing des Schlosses bleiben. Das Skript bricht ab,
wenn ein Pixel eines Eisenfeldes weder zugeordnet noch als bleibend bekannt ist.

## Prüfungen im Skript (Abbruch bei Verstoß)

- jede Reihe hat vier Stufen, aufsteigend, benachbarte Stufen mindestens 20 Helligkeit auseinander
- neutral: R = G, B höchstens 3 Prozent höher, Sättigung höchstens 0,04
- Niet gegen Beschlag mindestens 30, Beschlag gegen Holz der Normaltür mindestens 20
- Gegenprobe: Probe 1 und K1 müssen an der Holz-Prüfung durchfallen, K2 und K3 bestehen
- Holz und alles außerhalb der Eisenfelder ist Pixel für Pixel gleich dem heutigen Atlas
- Fallgitter: `check_closed.py` meldet alle acht Modelle als geschlossen (Dreiecke unverändert: Gitter breit 174, schmal 118, Führungen je 100)

Reproduzierbar: zwei Läufe in leeren Ordnern ergeben bitgleiche Dateien (72 Dateien verglichen).

## Dateien

- `blatt-eisen-543.png`: Übersicht, je Reihe Normaltür, Eisentür, Fallgitter, hell und im Spiel-Licht; Nahaufnahme der Beschläge; Messwerte
- `tuer-normal-*.png`, `tuer-locked-*.png`: Einzelbilder je Reihe (links hell, rechts Spiel-Licht)
- `gitter-*-schraeg-3m.png`: Fallgitter je Reihe
- `beschlaege-nah-*.png`: Normaltür im Spielbild (854 x 480), heute und je Reihe
- `fallgitter/`: das Fallgitter-Probestück aus #540 (Form unverändert) in K2; Modelle in `fallgitter/modelle/`, Bilder darunter,
  `fallgitter/vergleich-eisen.png` stellt das bläuliche Eisen von heute neben K2
- `werte.txt`, `vorschlag.json`: Farbwerte und Messungen
- `eisen_kontrast.py`: Erzeuger. `blender --background --python design/proben/grafik/eisen-kontrast-543/eisen_kontrast.py`
  (Blatt) und mit `EK_STEP=gitter` (Fallgitter). Baut auf `palette-holz-eisen-543/palette_probe.py` und `fallgitter_quelle.py` auf.

## Offene Fragen

- K2 oder K3? Entscheidung nach Blick auf das Blatt; K2 empfohlen.
- Beim Einbau in die Hauptpalette müssten die Tür-Atlanten neu erzeugt werden (heute sind sie nicht in der Palette). Das ist ein eigener Auftrag.
- Die Boss-Tür und die Tür mit Schloss (`locked`) sind mit umgerechnet, aber nur die Tür mit Schloss ist auf dem Blatt zu sehen.
