# Menü-Design, Runde 2 (Issue #351)

Urteil des Menschen zu Runde 1: „Menü design C aber noch was eleganter.“ Er hat **nicht gesagt, was „eleganter“ heißen soll**. Die drei Fassungen sind deshalb Auslegungen der Leitung, keine Vorgaben von ihm.

Blatt: [blatt-menue-r2.png](blatt-menue-r2.png) (je Fassung ein Streifen: fünf Bildschirme in 854×480 plus vier 4-fach-Ausschnitte). Einzelstreifen `menue-c1.png` bis `menue-c3.png`, Bildschirme einzeln in `einzeln/`.

Gleich in allen drei: Texte, Hintergrund, Schrift A „Kapitell“, Farben, Lage der Überschriften, Form von C (Giebel, Ecksteine, spitzer Cursor, Rautenknopf am Regler, Giebel-Reiter).

| Fassung | Auslegung von „eleganter“ | Was sich ändert |
|---|---|---|
| **C1 Fein** | feiner | dünnere Fassung (1,8 px), kleine Ecksteine, mehr Luft um den Text, kein Doppelrahmen, kleiner Giebel |
| **C2 Schlank** | schlanker | schmalere Tafeln (240 px statt 300), hoher steiler Giebel, steile Reiter, keine Zierrauten am Cursor, keine Doppelungen |
| **C3 Geordnet** | reicher, aber geordnet | Zierrat nur an Giebel (zweistufig) und Ecken (Eckleisten), dazwischen ruhige Flächen; Rauten an den Reglerenden |

Gegenüber Runde 1 behoben: Der Giebel über der Tafel ist jetzt bei 1× sichtbar; die Reiter „Map | Equipment“ sind höher und breiter.

## Neu erzeugen

```
"<Blender>\blender.exe" --background --python menue_r2.py
```

`menue_r2.py` nutzt `../menue.py` (Text, Hintergrund) und die HUD-Skripte aus `../../hud-vorlagen`. Nichts liegt unter `assets/`, nichts ist heruntergeladen.

## Nicht geprüft

Bewegung/Auswahlwechsel, Zusammenspiel mit anderen Seitenverhältnissen als 854×480, Hörbares. Die Wahl (C2 Schlank) steht in der Art Bible.
