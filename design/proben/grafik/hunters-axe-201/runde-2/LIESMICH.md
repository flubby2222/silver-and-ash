# Hunter's Axe (#201), Runde 2

Urteil des Menschen zu Runde 1: C bleibt, die Textur des Axtkopfes wird ueberarbeitet, das gelbe Feld faellt weg.

Erzeugen: `blender --background --python axt2.py` (nutzt `runde-1/axt.py`). Zwei Laeufe liefern bitgleiche Dateien (SHA-256 von blatt.png und allen vier glb verglichen).

| Fassung | Datei | Dreiecke | Farben |
|---|---|---|---|
| C1 Geschmiedet (Eisen, helle Kantenfase, dunkle Hammerschlagflecken) | axt-c1-geschmiedet.glb | 556 | 16 |
| C2 Rune (helles Eisen, Silberrille und Rune) | axt-c2-rune.glb | 556 | 16 |
| C3 Schartig (Scharten, Patina, Kratzer) | axt-c3-schartig.glb | 628 | 16 |
| C1 mit Eisenzwingen (Vergleich ohne Gold am Stiel) | axt-c1-eisenzwingen.glb | 556 | 13 |

Aenderungen gegenueber Runde 1
- Kein Gelb und kein Gold auf dem Axtkopf (im Skript geprueft).
- Stiel um 0,115 kuerzer: Oberkante in der Ich-Sicht bei Zeile 56 statt 14 (Keule: 84). Kopf, Haken und Pfahl liegen im Ausschnitt.
- Haken kraeftiger und laenger. Fehler aus Runde 1: Der Haken sass wegen eines Vorzeichens auf der Klingenseite; jetzt auf der Ruecken­seite.
- Pfahl etwas kuerzer, Zwingen und Nieten angepasst.

Pruefungen: geschlossen (0 offene Kanten, `tools/models/check_closed.py`), Hauptpalette, 128x128, hoechstens 16 Farben, kein Dithering, kein Rot, unter 1500 Dreiecke, keine Hand.

Schwaechen: Der Haken wirkt in der Ich-Sicht noch gross; C3 ist bei Spielaufloesung eher unruhig; die Rune in C2 ist ausgedacht.
