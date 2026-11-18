# Eigene Pixelschrift für HUD und Menüs (Issue #355)

Drei Richtungen für eine Schrift, die nicht Cinzel ist. Alles per Skript erzeugt: Jedes Zeichen ist ein Bitmuster aus `.` und `#`
in [glyphen.py](glyphen.py), keine Schriftdatei geladen, nichts nachgezeichnet, kein Systemschrift-Text. **Noch keine Wahl in die
Art-Bible eingetragen**, nichts unter `assets/`.

**Blatt:** [blatt-schrift.png](blatt-schrift.png) (alle drei untereinander, 1744 × 2652). Je Variante auch einzeln:
[A](schrift-a-kapitell.png), [B](schrift-b-textura.png), [C](schrift-c-schmal.png) sowie der reine HUD-Ausschnitt
`hud-<variante>-854.png` (854 × 480, 1×).

## Was das Blatt zeigt (je Variante gleich, nur die Schrift ändert sich)

- Der HUD aus Runde 6, Variante V3 „Reich“ (aus [hud-vorlagen](../hud-vorlagen/)): Zähler, Bossname „Gorm“, Hinweis „[F] to open“, Meldung „Picked up Hunter's Eye“. Aufbau, Farben und Lage sind unverändert; nur Cinzel ist ersetzt.
- Titelmenü (Start, Settings, Quit) und Pausenmenü (Resume, Settings, Back to title, Quit); Überschrift 3-fach, Einträge 2-fach.
- Der volle Zeichensatz in 2-fach: A–Z, a–z, 0–9, `.,:;!?'"-+=/\()[]<>%&*#_|@~^`, ein Pangramm und HUD-Zeilen wie „[F] Buy“.
- Ausschnitte in 4-fach: Meldung, Zähler, Hinweis, Boss, Stufe, Munition.

## Die Varianten

| | Richtung | Versalhöhe | Kleinbuchstaben | Zeichenbreite |
|---|---|---|---|---|
| **A Kapitell** | schlichte Kapitälchen mit kleinen Serifen (vom Code an Stammenden ergänzt) | 7 px | 5 px, als kleine Versalien | 3–5 px |
| **B Textura** | gebrochen: dicke Senkrechte, dünne Waagerechte, Kopf- und Fußspitzen | 7 px | 5 px x-Höhe, Ober- und Unterlängen | 5–8 px |
| **C Schmal** | schmal und hoch, Striche 1 px | 8 px | 6 px x-Höhe, Unterlängen | 3–5 px |

Abstand zwischen Zeichen 1 px, Leerzeichen 3 px. Es gibt je Schrift **eine** Größe (1×). Die HUD-Zahlen werden dadurch kleiner
als die Cinzel-Zahlen (Versalhöhe 7–8 statt 9–11 px).

## Dateien

- `glyphen.py`: die Bitmuster (Skelette; Serifen und Spitzen kommen aus dem Code).
- `schrift.py`: Engine in reinem Python (prüfbar ohne Blender: `python schrift.py` druckt eine Textprobe).
- `schrift_blatt.py`: setzt das Blatt zusammen. Importiert die HUD-Skripte aus `../hud-vorlagen` und biegt dort `Layer.text` und `textw` auf die Pixelschrift um; die Lage richtet sich nach der Mitte der Cinzel-Versalhöhe.

## Neu erzeugen

Blender dient nur als Python mit numpy und PNG-Ausgabe (kein Rendern); Pfad zu Blender anpassen:

```powershell
& "D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/schrift/schrift_blatt.py
```

## Grenzen (nicht geprüft oder offen)

- Nur im HUD-Ausschnitt und in zwei Menü-Skizzen gesehen, nicht im laufenden Spiel; das Menü ist hier nachgebaut, nicht das echte.
- Eine Größe je Schrift: Der HUD hat 10–16 px Cinzel; 2-fach (14–16 px Versalhöhe) ist nur im Menü geprüft. Eine größere Zahlengröße für Stufe und Leben fehlt.
- Cinzel setzt Kleinbuchstaben als Versalien („TO OPEN“); die Muster zeigen den Text so, wie er im Code steht. Nur A kommt dem Cinzel-Eindruck nahe.
- B: Versalien und Ziffern bleiben dünn (dick würde „GORM“ und die Zahlen verkleben); die Eigenart steckt in den Kleinbuchstaben.
- Keine Umlaute (Spieltexte sind Englisch), keine Ligaturen, kein Feinabgleich der Zeichenabstände (kein Kerning).
- Hörproben o. Ä. entfallen; die Entscheidung trifft der Mensch nach Blick aufs Blatt.
