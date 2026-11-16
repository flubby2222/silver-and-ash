# Pistole im Bild – Probestück 5 (2026-11-16)

Die Pistole des Jägers aus der Ich-Sicht: eine alte Steinschlosspistole mit Silberbeschlägen, PS1-nah mit Textur. Alles erzeugt [`pistole.py`](pistole.py) mit den gemeinsamen Bausteinen aus [`../gemeinsam/psx.py`](../gemeinsam/psx.py):

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/pistole/pistole.py
```

## Bilder

| Bild | Was |
|---|---|
| `ich_sicht_ruhig_nebel.png`, `ich_sicht_ruhig_dither.png` | Ich-Sicht, 854 × 480, 70° vertikal, Augen auf 1,6 m im Steingang, Blick geradeaus |
| `ich_sicht_rueckstoss_nebel.png`, `ich_sicht_rueckstoss_dither.png` | dasselbe im Rückstoß, mit Mündungsfeuer und gefallenem Hahn |
| `ansicht_links.png`, `ansicht_rechts.png`, `ansicht_schraeg_oben.png` | drei freie Ansichten, 640 × 640, ohne Nebel |
| `pistole_textur.png` | die Textur, 128 × 128 |

## Lage und Bewegung

Übernommen aus `WeaponViews.cs`:

- **Ruhelage:** Griff bei x 0,095, y −0,08, z 0,18 m im Kameraraum (x rechts, y oben, z vorn).
- **Rückstoß:** voll ausgeschlagen 14° mit der Mündung nach oben, dazu 1,2 cm hoch und 3,5 cm zurück.
- **Größe:** etwa wie beim Platzhalter. Der Lauf endet bei 0,159 m vor dem Griff (Platzhalter 0,125 m), der Kolben reicht 7,6 cm nach unten. Alles bleibt innerhalb von etwa 30 cm vor den Augen.
- **Nachladen:** Im Platzhalter gibt es keine Nachlade-Bewegung, deshalb habe ich sie nicht gezeigt. Für später: Ladestock ziehen und Hahn spannen. Beides sind schon eigene Teile oder Stücke.

## Aufbau

| Teil | Inhalt | Kanten |
|---|---|---|
| `stock` | Griff im Vogelkopf-Schwung, der Kolben verdickt; Schaft unter dem Lauf bis kurz vor die Mündung | Gouraud |
| `barrel` | achtkantiger Lauf, Silberring an der Mündung, dunkle Bohrung | hart |
| `fittings` | Kolbenkappe aus Silber mit kurzem Sporn, Seitenplatte links, Schlossplatte rechts, Abzugsbügel aus Silber, Abzug, Ladestock unter dem Schaft mit Silberspitze, Pfanne und Batterie (Feuerstahl) | hart |
| `cock` | der Hahn mit Feuerstein; Drehpunkt hinten über dem Schloss. Im Rückstoßbild ist er 38° nach vorn gefallen | hart |
| `flash` | Mündungsfeuer als Strahlenstern, unbeleuchtet, nur im Rückstoßbild; nicht in der glb | – |

**Dreiecke:** 482 für die Pistole (Schaft 144, Lauf 96, Beschläge 202, Hahn 40), dazu 42 fürs Mündungsfeuer. Das liegt deutlich unter meinem Richtwert für Waffen im Bild (bis 1 500); mehr Rundung am Griff wäre möglich.

## Textur

128 × 128, 32 Farben, keine Filterung:

| Bereich | Inhalt |
|---|---|
| Holz 64 × 64 | Nussbaum (84, 56, 38 wie der Platzhalter). Maserung entlang des Schafts; oben abgegriffen und heller; helle Kratzer; Silberdraht-Ranken als Einlage auf beiden Flanken |
| Eisen 64 × 32 | gebräuntes Eisen (58, 56, 64), Narben und Rostflecken, helle Kanten der acht Flächen, zwei Silberlinien oben entlang, gerippter Silberring am Pulversack |
| Silber 64 × 32 | hell, kühl, fast weiß (206, 210, 222), angelaufen in Flecken, Glanzlinie |
| Stahl 32 × 16 | Hahn, Batterie, Abzug |
| Bohrung, Feuer | je 16 × 16 |

Das Silber folgt der Art Bible: Silber gehört dem Jäger. Warmes Licht gibt es nur im Mündungsfeuer, mit derselben Farbe wie im Platzhalter.

## Wichtig für den Einbau: Spiegelung

`WeaponViews.CameraToWorld` baut eine Matrix mit rechts = vorn × oben. Ihre Determinante ist −1, sie **spiegelt** also.

- **Bei den Platzhalter-Quadern** fällt das nicht auf, weil sie symmetrisch sind und ShadedBox die Flächen selbst schattiert.
- **Bei einem echten Modell:**
  - Links und rechts würden vertauscht: Das Schloss säße auf der falschen Seite.
  - Die Dreiecke würden falsch herum laufen, und Raylib würde mit Rückseiten-Culling die sichtbaren Flächen wegwerfen.
- **Ich habe die Pistole ohne Spiegelung gebaut:** Rechts im Kameraraum ist −x im Modell; siehe `C()` im Skript.
- **Vorschlag:** Beim Einbau entweder eine echte Drehung als Kamera-Matrix verwenden (rechts = oben × vorn, dann ist x im Kameraraum gespiegelt definiert) oder das Modell beim Laden einmal spiegeln und die Dreiecke umdrehen. Das muss die Code-Sitzung entscheiden.

## Wo ich unsicher bin

- **Schloss- und Seitenplatte zu groß:** In der Ich-Sicht sitzen sie dicht vor der Kamera und wirken wie zwei helle Flügel am Griff. Kleiner oder dunkler angelaufen wäre ruhiger.
- **Hahn und Batterie zu wenig verbunden:** In den freien Ansichten wirken sie etwas lose über dem Schloss. Bei den Größen (wenige Millimeter) sieht man das in der Ich-Sicht kaum, aber ein Verbindungsstück wäre sauberer.
- **Keine Hand:** Der Platzhalter hat auch keine. Ohne Hand schwebt der Griff im Bild. Ein Ärmel mit Handschuh wäre ein eigenes Probestück.
- **Gouraud am Griff:** Mit nur 8 Seiten sieht man die Kanten des Griffs in der Nahsicht trotz weicher Schattierung. Hier würden 10 bis 12 Seiten helfen; das Budget ist da.
