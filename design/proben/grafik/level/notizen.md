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

---

# Fassung 2: leise Fugen (2026-11-16)

**Urteil der Leitung:** Die Fugen waren fast schwarz und gleichmäßig. Das Bild wurde zu einem harten Gitter, und Figuren gingen davor unter.

**Neue Regel:** Die Umgebung ist ruhiger als die Figuren; der stärkste Kontrast gehört Gegnern, Beute und Ankündigungen.

Skript: [`level_v2.py`](level_v2.py). Fassung 1 (`level.py`, `catacomb_*.png` ohne Zusatz und ihre Bilder) bleibt unverändert.

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/level/level_v2.py
```

## Was sich geändert hat

| | Fassung 1 | Fassung 2 |
|---|---|---|
| Fugen | 62 bis 68 % dunkler als der Stein, gleichmäßig | im Mittel 22 % dunkler (ein Drittel des alten Abstands), Tiefe und Breite schwanken mit Rauschen; zugesetzte Stellen ohne Fuge (etwa ein Viertel), ausgebrochene Stellen breiter und etwas dunkler; weiche Kante statt harter Linie |
| Wandsteine | 2 bis 3 je Lage und Meter, Ton ±10 % | 3 bis 5 je Lage, Ton ±12 %, leichter Farbversatz je Stein |
| Risse auf der Wand | wenige | keine |
| Boden | Fugen 62 % dunkler | Fugen 22 % dunkler, unregelmäßig; Schmutz und Risse schwächer |
| Decke | mittlere Helligkeit = heutige Deckenfarbe, Fugen 55 % dunkler | **72 % der heutigen Deckenfarbe**, Fugen 14 % dunkler, Steine fast gleich, weicher Ruß |

**Warum die Wandsteine kleiner wurden:** Mit nur zwei bis drei Steinen je Lage zog sich ein einzelner dunkler, bläulicher Stein beim Kacheln als durchgehendes Band durch die ganze Wand. Das steckte schon in Fassung 1 und war dort hinter den harten Fugen versteckt.

**Warum die Risse auf der Wand weg sind:** Auf den kleineren Steinen wiederholten sie sich jeden Meter als auffälliges Zeichen.

**Unverändert:** 64 × 64 Pixel, 24 Farben, nahtlos kachelbar (`kacheln_catacomb_*_v2.png`). Die mittlere Helligkeit von Wand und Boden entspricht weiter den Flächenfarben des Spiels.

**Abweichung, über die die Leitung entscheiden muss:** Die Decke ist bewusst dunkler als die heutige Flächenfarbe der Decke. Wenn das Spiel die Texturen mit der Flächenfarbe multipliziert statt sie zu ersetzen, muss das beim Einbau bedacht werden.

## Bilder

Alle in der entschiedenen Fassung: Nebel mit Körnung.

| Bild | Was |
|---|---|
| `catacomb_wall_v2.png`, `catacomb_floor_v2.png`, `catacomb_ceiling_v2.png` | die Texturen |
| `kacheln_catacomb_*_v2.png` | Naht-Probe, 3 × 3 gekachelt |
| `gang_lang_v2.png`, `kampfraum_v2.png` | wie in Fassung 1, mit den neuen Texturen. Der Kampfraum ist jetzt 12 × 12 m statt 9 × 9 m, damit der Ghul darin 10 m entfernt stehen kann; Säule weiter in der Mitte |
| `vergleich_gang_4m.png`, `vergleich_gang_10m.png` | im Gang: Ghul in Ruhehaltung, rechts daneben die King-Kiste; links Fassung 1, rechts Fassung 2 |
| `vergleich_kampfraum_4m.png`, `vergleich_kampfraum_10m.png` | dasselbe im Kampfraum, Blick aus der Ecke an der Säule vorbei |

Ghul und Kiste kommen direkt aus der Übergabe (`design/uebergabe/assets/models/ghoul.glb`, `chest_king.glb`), per glTF-Import in Blender, mit dem Maul in Ruhehaltung (−12°). So zeigen die Bilder auch, dass die Übergabe-Dateien sich laden und richtig zusammensetzen lassen.

## Stehen die Figuren jetzt vorn?

- **Aus 4 m:** Ja, deutlich. In Fassung 1 konkurrierte das schwarze Fugengitter mit Ghul und Kiste, das Auge sprang zur Wand. In Fassung 2 sind Wand und Boden eine ruhige, gegliederte Fläche. Der grünliche Ghul und die gelbe Kiste sind die stärksten Kontraste im Bild.
- **Aus 10 m:** Die Figuren sind klein, aber lesbar. In Fassung 1 lagen sie in einem Netz aus dunklen Linien, in Fassung 2 vor einer gleichmäßig dunkler werdenden Fläche. Im Kampfraum stehen sie vor der hinteren Wand jetzt klar abgesetzt.
- **Die Decke** verschwindet nach oben, wie gewünscht. Sie liest sich als dunkles Gewölbe, nicht mehr als zweites Gitter.

## Wo ich unsicher bin

- **Nahe Wand weich:** Die Wand ist aus der Nähe (unter 2 m) jetzt eher weich-fleckig als gemauert. Das ist der Preis der leisen Fugen. Wenn sie zu verwaschen wirkt, wäre der nächste Schritt eine etwas dunklere Fuge nur auf der Unterkante jedes Steins (Schatten von oben), nicht eine dunklere Fuge rundherum.
- **Kleine dunkle Flecken:** Die ausgebrochenen Stellen wiederholen sich als kleine dunkle Flecken jeden Meter. Aus der Nähe fällt das auf, ab 4 m nicht mehr.
- **Ablage wie in Fassung 1 vorgeschlagen:** `assets/textures/catacomb_*.png`, Erzeuger `tools/textures/catacomb.py`. Für die Übergabe trenne ich das Skript erst, wenn das Stück angenommen ist und Leitung oder Code-Sitzung darum bitten.
