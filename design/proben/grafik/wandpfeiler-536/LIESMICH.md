# Wandpfeiler (Probestück zu #536)

Erzeugt mit `design/proben/grafik/wandpfeiler-536/wandpfeiler.py` (Blender 5.3, Aufruf im Kopf des Skripts, etwa 40 s). Nur Palette `assets/palette/hauptpalette.json`, Wandtextur wie `tools/models/door.py` (Welt-UV, 64 px je Meter). Nichts im Spiel integriert.

## Fassungen

| Fassung | Schaft | Sockel/Kapitell | Kapitell ab | Dreiecke |
|---|---|---|---|---|
| raum (6 m) | 0,25 m | 0,3125 m | 5,5 m | 238 |
| gang (3,5 m) | 0,0625 m | 0,125 m | 3,0 m | 196 |
| gang_traeger | 0,0625 m | 0,125 m | 1,5 m (Oberkante = Kämpfer) | 182 |

Jeweils je Wandvariante a, b, c ein eigenes Modell (`modelle/`), alle geschlossen (`check_closed.py`: 9 geschlossen), samt Rückseite bei z = -1/8.

## Gewählte Lösungen für offene Punkte

- Profil: Sockel mit oberer Fase 1/16, Schaft, Kapitell aus Platte, Kehle, Wulst, V-Kerbe, Platte (Stufen 1/16).
- Textur: Fugen setzen das Wandraster fort (gleiche u-Phase wie die Wand). Der Pfeiler nutzt die Wandkachel, um 5 Stein-Stufen aufgehellt (lineare Stauchung oben, weil die Wand schon Index 14 erreicht). Bei 3 Stufen war er kaum zu sehen, dunkel wirkte er wie Schmutz.
- Wandkontakt: eigener Abschluss durch die hellere Tönung und die Schaftkante; Beweisbilder `wandvarianten-*.png`.
- Rückseite als eine konkave Fläche mit eingefügten Randpunkten (sonst offene Kanten durch T-Stöße).

## Bilder

Je Fassung in `raum/`, `gang/`, `gang_traeger/`: `vorn.png`, `schraeg.png`, `einzel.png` (vorn, Seite, hinten, oben), `detail.png`, `spielaufloesung.png`, `auge-hell.png`. Dazu `blatt-vorn.png`, `blatt-schraeg.png`, `vergleich-tuer*.png`, `wandvarianten-*.png`.

## Offene Fragen

- Rückseite zeigt im Raum eine schwache Dreiecksnaht (flache Schattierung der konkaven Fläche); unsichtbar im Spiel, bei Bedarf Polygon anders zerlegen.
- Der Pfeiler ist farblich nur eine Stufe heller als die Wand; ob mehr Kontrast gewünscht ist, ist Geschmack (`W536_LIFT`).
- Lastende Fassung: Kapitell-Oberkante fest bei 1,5 m, Anschluss an Bogen/Kämpfer ist nicht modelliert.
