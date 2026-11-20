# Probestück Gruftwächter (Issue #335)

Drei Fassungen des schweren Gegners, nur Probe außerhalb des Spiels. Skript: `gruftwaechter.py`, Blatt: `blatt.png`.

Aufruf (Repository-Wurzel): `"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/gruftwaechter-335/gruftwaechter.py`

## Fassungen

| | Name | Dreiecke | Maße B x T x H | Silhouette |
|---|---|---|---|---|
| A | Plattenträger | 1060 | 2,1 x 1,0 x 2,40 m | aufrechter Block, davor eine Grabplatte mit Kreuz als Schild, schwerer Maul am Boden |
| B | Glocke | 1312 | 2,3 x 1,7 x 2,65 m | Glocke: unten am breitesten, Schulterkuppeln, Spitzhelm, Schwert mit Faustblöcken |
| C | Joch | 1124 | 2,6 x 0,9 x 2,31 m | T-Form: Tragbalken über den Schultern, zwei Gewichte hängen daran |

Budget: Ghul 1024, Gorm 2268; alle drei liegen dazwischen. Teile je Fassung in `masse.txt`.

## Regeln der Art Bible

- Geschlossen (Regel 21), keine Texturen außer dem 128x128-Atlas, nur Farben der Hauptpalette (im Skript per `assert` geprüft), höchstens 32 Farben, kein Dithering.
- Keine modellierten Hände: Panzerfäuste und Stulpen als Blöcke, Waffe (Maul, Schwert) sitzt an den Block-Fäusten.
- Rot nur in den Augen (eigener Atlas `atlas_ankuendigung`, harte Stufen Blutrot 5/6, kein Schein), keine roten Umrisse. Ruhend sind die Augen dunkles Eisen.
- Werkstein nicht benutzt; keine große Fläche in Wand-/Bodenfarbe (Eisen und Stoff-Blau gegen das Violettgrau des Steins).
- Kopf höchstens 1/6 bis 1/7 der Höhe, mindestens zwei Asymmetrien je Fassung (A: Schulterschienen 3 gegen 2, abgebrochenes Horn; B: ungleiche Schulterkuppeln, Beule am Helm; C: ungleiche Gewichte, Ohrplatte nur links).
- Eigene Welt, keine Figur aus den Vorbildern.

## Beweis: geschlossen

- `python tools/models/check_closed.py design/proben/grafik/gruftwaechter-335/modelle/*.glb`: alle drei "geschlossen", 0 offene Kanten (1060 / 1312 / 1124 Dreiecke).
- Zusätzlich im Skript je Teil: keine Kante mit genau einer oder mehr als zwei Flächen, Rauminhalt über null. Ergebnis je Teil in `pruefung.txt`.
- Aufbau: Teile sind geschlossene Körper (Kiste, Prisma, Loft mit Kappen) und überlappen statt sich nur zu berühren.

## Beweis: bit-identisch

Das Skript wurde zweimal hintereinander ausgeführt; SHA-256 aller Bilder, Modelle und Textdateien sind gleich (`diff` ohne Unterschied). Die Summen stehen in `pruefsummen.txt`.

## Empfehlung: A, Rückfall B

- **A** zeigt den Spielzug sofort: Die Platte steht vorn, also "von vorn kaum verwundbar"; der Maul am Boden kündigt den breiten Schwung an. Aus 10 m dunkel bleibt der helle Kreuz-Block gut lesbar und hebt sich klar vom flachen Ghul und vom hohen Gorm ab.
- **B** hat die eigenständigste Silhouette (Glocke, aus 10 m sofort erkennbar), aber das Schwert verschwindet vor dem Körper und die Glocke wirkt eher wie ein Turm als wie ein Kämpfer.
- **C** ist breit und schwer, ähnelt im Umriss aber stärker Gorm (zwei Arme zur Seite) und die Gewichte lesen sich eher als Last denn als Waffe.

## Unsicherheiten

- Texturstreifen (Plattenfugen) wirken auf den Flächen etwas fein; aus 10 m verschwinden sie, im Nahbild könnte ein gröberes Raster besser sein.
- Gruftwächter sind mit 2,3 bis 2,65 m deutlich kleiner als Gorm (3,2 m); gewollt, damit der Boss der größte bleibt.
- Haltung ist eine Ruhepose; Gliedmaßen sind als eigene Teile mit Drehpunkt angelegt, aber nicht animiert.
- `gorm.glb` hat laut `check_closed.py` offene Kanten (Keule, Unterarme, Rumpf); das ist bestehend und nicht Teil dieser Probe.
