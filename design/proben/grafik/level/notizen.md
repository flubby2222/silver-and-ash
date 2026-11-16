# Level-Texturen der Katakomben – Probestück 8 (2026-11-16, Issue #233)

Wand, Boden und Decke für die Katakomben. Alles erzeugt [`level.py`](level.py):

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/level/level.py
```

## Texturen

| Datei | Inhalt |
|---|---|
| `catacomb_wall.png` | Katakombenmauer: vier Lagen unterschiedlicher Höhe (13, 19, 12, 20 Pixel), Steine 14 bis 40 Pixel breit, Lagen versetzt; tiefe dunkle Fugen, abgerundete Kanten, ruhige Steintöne, Körnung, feuchte senkrechte Laufspuren mit einem Hauch Salpeter-Grau, wenige feine Risse |
| `catacomb_floor.png` | große, abgetretene Platten in zwei Lagen zu 32 Pixeln (0,5 m), 22 bis 44 Pixel breit; in der Mitte heller, an den Kanten dunkler; Schmutz in Flecken, Risse, ein paar helle Splitter |
| `catacomb_ceiling.png` | Gewölbe-Mauerwerk in vier Lagen zu 16 Pixeln, Ruß in großen Flecken |

**Für alle drei gilt:**

- **Größe:** 64 × 64 Pixel, 24 Farben (erlaubt bis 32), keine Filterung.
- **Wiederholung:** **1 m je Wiederholung**, also 1,6 cm je Texel. Aus 4 m ist ein Texel etwa ein Bildpixel.
- **Helligkeit:** Die mittlere Helligkeit ist auf die heutige Flächenfarbe aus `LookSettings` gesetzt (Wand 122, 104, 112; Boden 84, 72, 78; Decke 64, 56, 66). Der Raum wird also nicht heller oder dunkler als heute, nur gegliedert.

**Nahtlos kachelbar:**

- Das Rauschen ist periodisch gebaut: Das Zufallsgitter wiederholt sich nach einer ganzen Zahl von Zellen.
- Die Fugen sind so verteilt, dass jede Lage genau 64 Pixel breit ist und am Rand weiterläuft.
- Risse laufen über den Rand und kommen gegenüber wieder herein.
- Geprüft in `kacheln_catacomb_*.png` (je 3 × 3 gekachelt, dreifach vergrößert): keine sichtbaren Nähte.

## Bilder

Alle 854 × 480, 70° vertikal, Augen auf 1,6 m, Laterne und Nebel je Pixel, dann 15-Bit-Körnung (die entschiedene Fassung).

| Bild | Was |
|---|---|
| `kacheln_catacomb_wall.png`, `kacheln_catacomb_floor.png`, `kacheln_catacomb_ceiling.png` | Naht-Probe, 3 × 3 gekachelt |
| `gang_lang.png` | Gang 5 m breit, 3,5 m hoch, 34 m lang; Blick schräg an der linken Wand entlang, gut 20 m Wand im Bild |
| `gang_mitte.png` | derselbe Gang, Blick in die Mitte |
| `kampfraum.png` | Raum 9 × 9 m, 3,5 m hoch, Säule 0,9 × 0,9 m in der Mitte, Blick aus einer Ecke |

## Ist das Kachelmuster auf 20 m sichtbar?

- **Ja, in der Nähe:** Bis etwa 6 m erkennt man, dass sich die Lagen jeden Meter wiederholen.
- **Weiter weg nicht:**
  - Die ungleich hohen Lagen und die unregelmäßigen Steinbreiten brechen das Raster.
  - Weiter hinten verschwimmt die Wand ohnehin im Nebel.
- **Wie es dazu kam:** In einem ersten Versuch sprangen einzelne helle oder dunkle Steine und deutliche Risse jeden Meter ins Auge. Ich habe die Steintöne beruhigt (±10 % statt ±16 %) und die Risse auf drei schwache reduziert.
- **Was wirklich hilft:** gegen die Wiederholung auf großen Flächen mehrere Varianten der Wand und unregelmäßige Bauteile (Nischen, Pfeiler, Knochenfächer). Das ist Level-Bau, nicht Textur.

## Wo ich unsicher bin

- **Wand zu regelmäßig:** Sie ist eher gemauerter Bruchstein als roher Fels. Für Katakomben passt das, für ausgehauene Gänge wäre eine zweite, rohere Felstextur nötig.
- **Moiré in der Ferne:** Ohne Filterung und Mipmaps flimmern Fugen in der Ferne leicht. Bei der Decke habe ich die Steine deshalb größer gemacht als zuerst geplant (16 statt 8 Pixel hoch). In Bewegung muss der Mensch das beurteilen.
- **Farbton:** Die Wand bleibt rosagrau wie die heutige Wandfarbe. Das ist die Grundlage der Regel „Figuren nicht im Farbton der Wand“; ich habe sie deshalb nicht verschoben.
- **Ablage:** Vorschlag `assets/textures/catacomb_wall.png` usw., Erzeuger `tools/textures/catacomb.py`. Das Skript hier rendert noch Probebilder; für die Übergabe trenne ich es wie bei den Modellen, sobald das Stück angenommen ist.
