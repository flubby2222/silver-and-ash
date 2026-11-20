# Skelett mit Armbrust – Probestück (Issue #334)

Drei Fassungen, gleiche Pose (aufrecht, Armbrust angelegt, Lauf zum Spieler), gleiches Licht. Alles aus `armbrust.py`
(Blender-Python, nichts heruntergeladen). Blatt: `blatt.png`. Spielbilder: `spiel-10m.png`, `spiel-ankuendigung-4m.png`.
Ansichten je Fassung: `ansichten-A|B|C.png`. Modelle: `modelle/armbrust_skelett_<A|B|C>.glb`. Maße: `masse.txt`.

| Fassung | Dreiecke | Höhe | Breite | Idee |
|---|---|---|---|---|
| A Wächter | 1134 | 1,86 m | 0,97 m | schlank, offener Brustkorb, breite Armbrust vor der Brust |
| B Kapuzenschütze | 850 | 1,97 m | 1,00 m | Kutte als breite Dreiecksmasse, Kapuzenspitze nach hinten |
| C Schildträger | 1224 | 1,98 m | 1,15 m | Eisenkrempe, hoher Holzschild links, Armbrust tief angelegt |

Textur: 128x128, 20 Farben (Ruhe) bzw. 22 (Angriff), nur Hauptpalette, ein Atlas je Figur, keine Filterung.

## Belege
- Geschlossen: `python tools/models/check_closed.py` meldet für A, B und C "geschlossen, 0 offene Kanten".
- Bitgleich: zwei Läufe, SHA-256 von blatt.png, allen glb, Spielbildern, Atlanten und Ansichten identisch.

## Regeln der Art Bible
- Rundum geschlossen (21). Keine Hände: Fäustlinge als Klötze. Rot nur in den Pupillen und nur in der Angriff-Textur
  (`texturen/atlas_angriff.png`, Blutrot-Stufen, kein Schein, keine Umrisse). Kopf klein, je mindestens zwei Asymmetrien
  (Arme, Hut/Schild, Kapuze, Armbrust seitlich versetzt). Gesicht mit dunklen Höhlen, unregelmäßiger Mund.
- Skelett mit wenig Dreiecken: Knochen als Röhren mit 6 Seiten und 3 Ringen (Rippen 4 Seiten), Lücken zwischen den Rippen
  statt Füllung; die Lücken sind in der Nebel-Lesbarkeit der wichtigste Silhouetten-Gewinn gegenüber dem Ghul.

## Lesbarkeit aus 10 m
Gliedmaßen von etwa 7 bis 9 cm sind noch 2 bis 3 Pixel breit. Die Armbrust ist an der waagrechten Querlinie des Bogens
auch im Nebel erkennbar (A und C am deutlichsten). Der Ghul ist niedrig und gebückt, alle drei Fassungen sind aufrecht
und höher; damit ist die Unterscheidung gegeben.

## Unsicherheiten
- Die rote Ankündigung liegt nur in den Pupillen; aus 4 m ist sie im Spielbild kaum zu sehen. Sie trägt vor allem aus
  der Nähe. Die Ansage sollte deshalb zusätzlich über die Pose (Anlegen und Spannen, etwa 1 s) kommen. Das Issue
  verlangt kein rotes Aufleuchten; Regel 22 erlaubt es nur in den Augen.
- A im Nebel dünn und dunkel: leicht zu übersehen. B ist als Masse am besten lesbar. C hat die breiteste Fläche
  und den auffälligsten Hut, aber auch die meisten Dreiecke.
- `check_closed.py` kann bei sich durchdringenden Teilen "mehr als zwei Flächen" melden; hier trat das nicht auf.
- Vorschlag Maße im Spiel: Trefferzone Rumpf bei A und C etwa 0,35 m breit, bei B die ganze Kutte; Kopf klein
  (Kopftreffer nur bei A und C sinnvoll, bei B verdeckt die Kapuze).

## Empfehlung
B (Kapuzenschütze): einzige Masse, die aus 10 m im Dunkel ohne Mühe vom Ghul zu trennen ist, mit 850 Dreiecken die
günstigste, und die Kutte passt zur Ersatzidee aus dem Issue. Armbrust bleibt dank Querlinie lesbar. Wer ein "echtes"
Skelett will: A, dann mit breiterem Rumpf nacharbeiten.
