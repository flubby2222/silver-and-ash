# Menü-Design im HUD-Stil (Issue #351)

Drei Richtungen für Titelmenü, Pausenmenü, Einstellungen, Beenden-Frage und die Reiterleiste des Tab-Menüs. Alles per Skript erzeugt,
nichts heruntergeladen, keine Systemschrift. Schrift ist in allen Varianten A „Kapitell“ aus [../schrift](../schrift/) (importiert),
Formen und Silberfassungen kommen aus [../hud-vorlagen](../hud-vorlagen/) (`hud_runde4`: `cell`, `sframe`, `trough`, `ornament`).
**Noch keine Wahl in die Art-Bible eingetragen**, nichts unter `assets/`.

**Blatt:** [blatt-menue.png](blatt-menue.png) (alle drei, 2610 × 3886). Je Variante einzeln: [A](menue-a.png), [B](menue-b.png), [C](menue-c.png);
die reinen Bildschirme (854 × 480, 15 Bit, 4×4-Körnung) in [einzeln/](einzeln/). Unter jedem Block vier Ausschnitte in 4-fach
(Markierung Titel, Markierung Einstellungen, Regler, Reiter).

## Was gleich ist

Texte, Lage, Hintergrund (HUD-Basisbild, auf 30–34 % abgedunkelt) und Farben (Gold für den markierten Eintrag, Bein für Überschriften,
neutrales Silber). Markiert ist jeweils „Start“, „Resume“, „Mouse sensitivity“ (mit aktivem Regler), „No“; gewählter Wert bei Display ist „Window“;
aktiver Reiter ist „Map“.

## Was sich ändert

| | A Schlicht | B Gefasst | C Reich |
|---|---|---|---|
| Tafel | nur Rahmenlinie, keine Füllung | Silberfassung mit dunkler Nische wie die HUD-Felder | wie B plus Giebel, Eckrauten, Doppelrahmen, Fialen |
| Markierung | Goldtext zwischen zwei Silberrauten, Linie darunter | erhabene Silberplatte | spitze Platte (wie die HUD-Balken) mit Rauten an den Spitzen |
| Regler | dünne Rinne, Strich als Griff | HUD-Balken in Gold | HUD-Balken, Raute als Griff, Skalenstriche |
| Reiter | Goldtext, Goldlinie darunter | zwei Zellen, aktive mit hellem Ring | Giebelreiter, aktiver höher mit Raute |

Gewählter Wert (Display: Window) trägt dieselbe Markierung in klein, ohne Rauten.

## Neu erzeugen

```powershell
& "D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/menue/menue.py
```

## Grenzen

- Nur als Bild gesehen, nicht im laufenden Spiel; Inhalt der Tab-Seiten (Karte, Ausrüstung) ist leer gelassen, nur Reiter und Tafel sind beurteilbar.
- Eine Textgröße je Rolle (Überschrift 3–4-fach, Einträge 2-fach); Lagen sind für die gezeigten Texte gerechnet, längere Einträge brauchen eine breitere Tafel.
- Nicht gezeigt: gesperrte/ausgegraute Einträge, Maus-Hover, Reiter mit mehr als zwei Seiten, Tafel über hellem Hintergrund.
- Verdeckung des Spielbilds nicht gemessen (Menü liegt auf abgedunkeltem Bild, A lässt am meisten sichtbar).
