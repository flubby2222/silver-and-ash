# Statuseffekte am Gegner, Runde 3 (#616)

Urteil des Menschen nach Runde 2: Holy Fire Stufe a, aber der Burn gefällt noch nicht, Vorbild soll die Fackel-Animation sein. Leech Stufe b, aber etwas weniger Partikel. Reihe Violett passt. Fest: Stun A, Grave Chill A, Marked B (aus Runde 2, unverändert).

## Fackel-Vorbild (gefunden)

- `design/proben/effekte/fackel/fackel.py`, Funktion `flipbook_images` und Abschnitt „Variante A: Daumenkino“.
- `design/proben/effekte/fackel/runde2/fackel_runde2.py` (Variante B, gewählt) und `docs/design/art-bible.md`, Abschnitt „Fackel: Wahl und Ablehnungen“. Im Spielcode unter `src` gibt es noch keine Fackel; das Vorbild ist das gewählte Probestück.

Was übernommen wird:

| Merkmal der Fackel | In Holy Fire |
|---|---|
| 5 gemalte Bilder zu 9 x 15 Texeln, Tropfenform, unten breit, oben schmal, pendelt seitlich | dieselben Bilder, direkt aus `fackel.py` geladen (keine Kopie) |
| Bildwechsel alle 0,1 s, Schleife 1 s | gleicher Takt; die Flammen haben je einen Phasenversatz |
| abgerissene Zunge auf jedem zweiten Bild | kommt mit den Bildern mit |
| 4 Farbstufen: Kern Messing 7, Mitte 6, Rand 5, Spitze 3 | Kern 7, Mitte 6, **Rand 4** (Messing 5 wird am Gegner gemieden), Spitze 3 |
| 4 Funken, 0,6 s, um 0,25 s versetzt, aufsteigend | gleich, mindestens 2 Pixel statt 3 |
| harte Texel, kein Schein, keine Teilchenflamme | gleich |

## Fassungen

- **Holy Fire a1:** nah am Vorbild. Eine Fackelflamme auf dem Kopf (Texel 0,036 m), je eine auf den Schultern (0,028 m, die rechte gespiegelt), leichte Tönung und Innenrand der Stufe a, 4 Funken.
- **Holy Fire a2:** wie a1, dazu vier kleine Fackelflammen (0,024 m), die in der unteren Körperhälfte wachsen, abwechselnd gespiegelt, mit eigenem Takt.
- **Leech b1** (etwa zwei Drittel): 2 Perlen (vorher 3), 3 Tropfen (5), 5 Spuren (8), Faden halb gestrichelt. **Leech b2** (etwa die Hälfte): 1 Perle, 2 Tropfen, 4 Spuren, Faden zu einem Drittel. Violett-Tönung, Innenrand und gepunkteter Umriss bleiben in beiden.

## Dateien

- `probenblatt_r3.png`: Vorbild-Tabelle, Aufstellung 8 m und 12 m je Fassung (mit Marked, Grave Chill, Stun), Holy Fire mit der Fackel daneben, vier Phasen, Leech b1/b2 bei 4/8/12 m mit Runde 2 b zum Vergleich.
- `holy_fire_a1_a2.gif` (a1, a2, Fackel, 10 Bilder zu 0,1 s), `leech_b1_b2.gif`.
- `spiel_8m_fassung1/2.png`, `spiel_12m_fassung1/2.png`, `spiel_holy_fire_<a1|a2>_<4|8|12>m.png`, `spiel_leech_<b1|b2>_<4|8|12>m.png` (854 x 480), `farbtabelle_r3.md`.
- Skripte: `effekte_r3.py`, `blatt_r3.py` (benutzt `r2/gifschreiber.py`). Aufruf aus dem Wurzelordner:

      & "D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/statuseffekte-616/r3/blatt_r3.py

## Grenzen

- Die Fackel im Blatt wird bei 2,4 m gerendert und ist daher größer als die Gegner-Flammen im 2-fach-Ausschnitt (etwa 1,7-fach).
- Bei 4 m wirkt die Kopfflamme mit den Körnungsstreifen eher wie ein Kamm; Kopf- und Schulterflammen laufen bei 8 m zu einem Dreieck zusammen. Die Fackel ist reicher, weil sie vor hellem Stein steht.
- Bei 12 m sind Flammen und Leech nur noch gedämpft sichtbar; Leech b2 ist dort sehr schwach, b1 gerade noch als Violett erkennbar.

## Vorschlag für die Art Bible (nicht eingetragen)

- Brennende Gegner nehmen dieselbe Flamme wie die Fackel (Daumenkino, Takt 0,1 s, vier Funken), keine eigene Zackenform.
- Fäden, Perlen und Tropfen an Gegnern höchstens zwei Perlen und drei Tropfen gleichzeitig.
