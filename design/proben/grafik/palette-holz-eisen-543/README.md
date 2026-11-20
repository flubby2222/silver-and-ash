# Probestück zu Issue #543: neutrales Eisen und gedämpftes Holz

Vorschlag für zwei neue Reihen der Hauptpalette (je vier Stufen, dunkel nach hell). Die Palette selbst und alles unter `assets/` sind unverändert; es gibt keinen Spielcode.

**Zur Wahl: `blatt-543.png`** (Farbfelder mit Hex-Werten, die drei Türflügel heute / mit Holz A / mit Holz B, hell und im Spiel-Licht, dazu das Fallgitter).

## Werte

| Reihe | Stufe 0 | Stufe 1 | Stufe 2 | Stufe 3 |
|---|---|---|---|---|
| Eisen neutral | `#1E1E1F` | `#323234` | `#4A4A4C` | `#68686B` |
| Holz A, graubraun | `#25211E` | `#3F3933` | `#5C524A` | `#7B6D63` |
| Holz B, etwas wärmer | `#27211B` | `#43382F` | `#615044` | `#836A5B` |

- Eisen: Helligkeit 30 / 50 / 74 / 104 (Abstände 20, 24, 30; das Eisen von heute hat in den unteren Stufen 21 bis 26, oben 31 bis 32). R = G, B um 3 Prozent höher: das Grau, das der Mensch im Vergleichsbild von #540 gewählt hat. Sättigung 0,03 bis 0,04, also kein Blaustich. Heller als die Wandstein-Stufen 4 bis 6 (55 bis 65) wird die Reihe erst in Stufe 3; sie liegt deutlich unter dem mittleren Wandstein (Kachel im Mittel 110).
- Holz: gleiche Helligkeiten 34 / 58 / 84 / 112 (Abstände 24 bis 28; heute 12 bis 31). Sättigung A 0,19 bis 0,20 (Farbton 28 Grad), B 0,30 bis 0,31 (Farbton 26 Grad); das Holz von heute hat 0,54 bis 0,59.
- Maschinenlesbar: `vorschlag.json`. Alle Zahlen: `werte.txt`.

## Mittlere Abweichung gegen die heutige Tür-Textur (Betrag je Kanal R/G/B, nur Bretter, Platte, Band, Kante)

| Tür | heutige Reihen (Referenz) | Holz A + Eisen neu | Holz B + Eisen neu |
|---|---|---|---|
| normal | 13,6 / 6,3 / 15,7 | 10,7 / 10,7 / 13,6 | 10,9 / 10,4 / 11,4 |
| locked | 5,3 / 7,0 / 16,9 | 8,3 / 9,0 / 13,2 | 8,3 / 9,0 / 13,2 |
| boss | 9,2 / 7,3 / 8,9 | 9,9 / 9,7 / 14,2 | 10,2 / 9,7 / 16,0 |

Die Abweichung liegt im selben Bereich wie bei den heutigen Reihen (Referenz-Spalte). Die Eisentür `locked` ist in A und B gleich, weil sie kein Holz nutzt.

## Was umgerechnet wurde

Nur Bretter, Platte, Band und Kante der Atlanten (Feld für Feld auf die nächste Farbe der Rolle plus Stein Stufe 0 für die Fugen, `tools/palette/snap.py`). Schloss (Messing), Glas (Glühen) und das Dunkelfeld bleiben, wie sie sind; sie sind nicht Teil der Frage. Normal: Holz + Eisenbeschläge; Locked: alles Eisen; Boss: Holz.

## Empfehlung

Eisen neutral wie vorgeschlagen. Holz **A** (graubraun): es sitzt ruhiger neben dem rosigen Stein und zieht die Tür nicht aus der Wand; Holz B bleibt als Brett lesbar, wirkt aber neben dem Stein wärmer und hebt die Normaltür stärker ab. Wer die Tür als Orientierungspunkt im Nebel will, nimmt B.

## Unsicher

- "Beide Ebenen-Paletten": nur die Katakomben sind gebaut. Auf dem Blatt steht das Eisen daneben zu einem kühlen und einem warmen Stein; das sind Annahmen zum Ansehen, keine Palette.
- Eisenbeschläge auf Holz haben in A und B wenig Helligkeitsabstand; die Nieten und Buckel der Platte verschwinden, weil vier Stufen weniger Zwischentöne bieten. Alternative Eisen-Helligkeit 34 / 58 / 88 / 124 wäre kontrastreicher, wurde nicht gerendert.
- Die Boss-Planken fallen auf Holz-Stufe 0 und 1 zusammen; die Tür wirkt dort fast schwarz.
- Eisen B-Kanal +3 Prozent ist ein Hauch, kein Farbton; reines R = G = B wäre denkbar.

## Erzeugen

`palette_probe.py` lädt `fallgitter_quelle.py` (byte-gleich das Skript aus `origin/feat/fallgitter-probe-540`, ohne dessen `main()`); Aufruf im Kopf des Skripts. Zwei Läufe in leeren Ordnern ergaben bitgleiche Dateien (SHA-256 verglichen).
