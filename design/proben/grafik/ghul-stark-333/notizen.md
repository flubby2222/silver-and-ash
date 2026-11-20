# Probestück: stärkerer Ghul (Issue #333)

Probe außerhalb des Spiels. Nichts unter `assets/` geändert, das Skript des heutigen Ghuls bleibt unberührt (`ghul_stark.py` lädt dessen Bauteile und verformt sie).

Erzeugen: `blender --background --python design/proben/grafik/ghul-stark-333/ghul_stark.py` (optional `-- <Zielordner>`). Blatt: `blatt.png`.

## Die Varianten

| | Dreiecke | Faktor | Breite x Höhe (m) | Merkmal |
|---|---|---|---|---|
| heute | 1024 | 1,0 | 0,72 x 0,95 | Vergleich |
| A | 1024 | 1,3 | 0,93 x 1,24 | nur größer, bleiche Haut |
| B | 1024 | 1,3 | 1,06 x 1,22 | zusätzlich breitere Schultern, längere Arme, Buckel, kleinerer Kopf |
| C | 1096 (+7 %) | 1,3 | 1,06 x 1,30 | wie B, dazu Kamm aus neun Knochenstacheln entlang der Wirbelsäule |

Bleiche Haut: gleiche Palette (Haut/Knochen- und Stoffreihe der Hauptpalette), nur um Stufen aufgehellt. Rot nur in den Augen, und die zeichnet wie heute der Spielcode während des Ausholens (im Blatt als reine Bildmarke). Keine Hände modelliert, die Klauen sind die des heutigen Ghuls.

## Regeln

- Geschlossen: `tools/models/check_closed.py` meldet für alle vier Modelle 0 offene Kanten.
- Bitgleich: zwei Läufe (Standardordner und fremder, leerer Zielordner) ergeben für alle 37 erzeugten Dateien (Bilder, glb, Texturen, `masse.txt`) dieselben SHA-256-Summen.
- Dreiecksbudget: A und B genau wie heute, C +72.
- Nur Hauptpalette, keine Leuchteffekte, keine roten Konturen.

## Empfehlung: C

Aus 10 m im Dunkel und Nebel trägt vor allem die Größe. Die bleiche Haut allein (A) geht im Nebel unter und wirkt wie ein bloß vergrößerter Ghul. B ändert die Haltung (breit, Buckel), bleibt aber ohne eindeutiges Merkmal. C bringt über dem Kopf ein Zeichen, das von vorn sichtbar ist, und liest sich trotzdem als Ghul. Rückfall: B, falls der Kamm als zu viel gilt.

## Unsicherheiten

- Die Treffer-Körper in `Core` (`BodySizes.Ghoul`, 0,72 breit, 0,94 hoch) müssten für ein 1,3-faches Modell größer werden. Das ist Core-Arbeit für eine Code-Sitzung, nicht Teil dieser Probe.
- Die Bleichung ist bei 10 m im Nebel schwach; die Erkennung beruht dort auf Umriss und Größe.
- Die Greifweite wächst (B/C reichen bis z 0,99 statt 0,65); das Ausholen nach vorn sieht dadurch anders aus als die Trefferzone.
