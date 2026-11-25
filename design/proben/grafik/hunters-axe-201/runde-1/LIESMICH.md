# Hunter's Axe (Issue #201), Runde 1: drei Entwürfe

Probestück außerhalb des Spiels. Alles entsteht aus `axt.py` (Blender-Python), nichts heruntergeladen.
Aufruf: `blender --background --python design/proben/grafik/hunters-axe-201/runde-1/axt.py`.
Das Blatt `blatt.png` zeigt je Entwurf Seite (auf die Klingenfläche), Dreiviertel (von der Schneidenseite), Ich-Sicht
(Katakomben-Gang mit Nebel und Körnung, Hand-Ärmel des Bestands, Haltung wie die Keule: 35 Grad hoch, 10 Grad auswärts),
den Größenvergleich neben der Keule, dazu Keule und Klinge des Bestands und die Silhouetten aller fünf Waffen.

## Die Entwürfe

| | Dreiecke | Farben | Idee |
|---|---|---|---|
| A Bartaxt | 522 | 15 | Einschneidig, langer Stiel (Kopf ganz vorn), hängender Bart. Rücken stumpf, goldene Spitze. |
| B Doppelaxt | 602 | 15 | Doppelschneidig mit Mondsichel-Hörnern, kurzer dicker Stiel, Silberspitze oben, goldener Knauf. Wirkt am wuchtigsten. |
| C Pfahlaxt | 556 | 16 | Kompakte Klinge, Silberhaken auf der Rückseite, langer Silber-Pfahl oben am Stiel (Jäger-Werkzeug). |

Gemeinsam: Stiel mit Lederwicklung, goldene Kragen, Schaftfedern und Nieten (Stufe Royal, nur kleine Akzente), Silberkante
(helle Eisen-Stufen der Hauptpalette), Spitzbogen-Gravur auf der Platte. Ein Knoten "axe", Drehpunkt im Griff, Achse +z,
Schneide zeigt nach -x (zur Bildmitte), Maßstab 1,8 eingebacken, flach schattiert, Textur 128x128 ohne Filterung,
nur Palettenfarben (Reihen Holz, Eisen, Messing/Gold), höchstens 16 Farben. Keine Hand modelliert.

Maße (Dateimaß): Keule etwa 0,45 lang, Axt A etwa 0,75, B etwa 0,65, C etwa 0,72 (mit Pfahl).

## Geschlossen

Zwei Belege, alle drei Modelle: im Skript je Netz Kanten mit nur einer Fläche (offen) und mit mehr als zwei Flächen gezählt,
und `python tools/models/check_closed.py <glb>` auf die ausgegebene Datei.
Ergebnis A, B, C: 0 offene Kanten, 0 Kanten mit mehr als zwei Flächen, 0 lose Kanten; `check_closed.py` meldet je "geschlossen".
Die Teile (Stiel, Platte, Kragen, Spitzen) sind einzeln geschlossene Körper, die einander durchdringen (Regel 21).

## Reproduzierbar

Zwei Läufe des Skripts liefern byte-gleiche Dateien (SHA-256 der drei glb und von blatt.png identisch, Lauf 1 und 2 verglichen).

## Empfehlung

B (Doppelaxt) für "langsam, breiter Hieb, viel Schaden": Die Silhouette ist in der Ich-Sicht mit Abstand die breiteste
(T-förmig, ein Hammer-Umriss) und unterscheidet sich klar von der schlanken Keule und der kleinen Klinge; die breite Fläche
passt zum "breiten Hieb" und zum Aufladen. Zweite Wahl A, weil sie am klassischsten nach Axt aussieht und der Bart
die Schneide in der Ich-Sicht gut zeigt. C hat den eigenständigsten Zusatz (Pfahl), aber der Haken geht in der Ich-Sicht verloren.

## Bekannte Schwächen

- Die Ich-Sicht zeigt die Waffe unten LINKS, wie beim Bestand (Hand bei x=+0,11, +x ist links im Bild). Der Auftrag sprach von
  "unten rechts"; dort liegt im Bestand die Pistole. Die Kante der Axt zeigt zur Bildmitte.
- Die Äxte sind länger als die Keule und ragen höher ins Bild; für den Einbau eventuell Stiel kürzen oder tiefer halten.
- B: Die Platte hat sichtbare Körnung (Pocken) in der unteren Fase, weil die Textur dort stark gedehnt ist. Kein Dithering, aber unruhig.
- C: Der Haken ist dünn und in der Ich-Sicht kaum zu erkennen. Der Pfahl ragt in der Ich-Sicht nur als kleine Spitze über den Kopf.
- `check_closed.py` meldet bei sich durchdringenden Teilen manchmal "mehr als zwei Flächen"; das ist kein Loch. Hier trat es nicht auf.
- Keine Animation, keine Kollision, kein Einbau; die Waffe ist noch nicht in `assets/`.
