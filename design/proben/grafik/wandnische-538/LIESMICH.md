# Wandnische (Probestück zu #538)

Erzeugt mit `design/proben/grafik/wandnische-538/wandnische.py` (Blender 5.3, Aufruf im Skriptkopf). Das Skript führt `wandpfeiler.py` (#536) ohne dessen `main()` aus und überschreibt nur Atlas, UV-Felder und Szenenaufbau. Nur Palette `assets/palette/hauptpalette.json`, Reihe Stein (im Skript je Modell geprüft). Nichts im Spiel integriert. Zwei Läufe liefern byteidentische Modelle und Bilder (Hash verglichen).

## Maße (alles Setzung, soweit die Formenlehre nichts vorgibt)

| Größe | Wert | Begründung |
|---|---|---|
| Öffnung | 1,0 m breit, Sohle 0,5 m, Scheitel 2,0 m | Formenlehre-Zeile Wandnische |
| Bogen | gedrückter Spitzbogen, R = 13/16, Mittelpunkte 0,3125 neben der Achse, Kämpferlinie 1,25 m | Stich genau 0,75 (5-12-13-Dreieck), Scheitel 2,0 |
| Gewände | 2 Bänder je 1/8 m, Gesamtbreite 1,5 m, Außenscheitel 2,2655 m | Fase und Stufe 1/16 |
| Sohlbank | y 0,25 bis 0,5, so breit wie das Gewände | trägt, sperrt nichts |
| Fassung gang | Gewände vorn 0,125, Rand 0,0625, Bank 0,125 | Vorsprung 0,125 (Grenze 0,125) |
| Fassung raum | Gewände vorn 0,25, Rand 0,125, Bank 0,25 | Vorsprung 0,25 (Grenze 0,3125) |
| Zahn | Rückseite 1/8 m in der Wand | Formenlehre Kontakt |

Dreiecke je Modell: 312 (Gewände 236, Rückwandplatte 56, Sohlbank 20). Das ist ein Vielfaches der 60 der Formenlehre-Zeile.

Je Wandvariante a, b, c und Fassung gang/raum ein Modell in `modelle/` (6, alle geschlossen laut `check_closed.py`), dazu zwei Rundbogen-Vergleichsmodelle (`wandnische_rund_*`, Kämpfer 1,5 m).

## Widerspruch zur Formenlehre

Die Formenlehre-Zeile nennt einen Rundbogen (Kämpfer 1,5), das Issue und die Leitung einen Spitzbogen. Gebaut ist der Spitzbogen; der Kämpfer liegt bei 1,25 statt 1,5, weil der Scheitel bei 2,0 bleiben soll. `rundbogen-vergleich.png` zeigt beide nebeneinander.

## Tiefe, ehrlich

Die Wand im Spiel ist eine dünne Fläche und lässt sich nicht aushöhlen. Die Nische ist ein vorgesetzter, gestufter Gewändekranz mit Sohlbank. Die Öffnung zeigt eine dunkle Rückwandplatte (1/64 m vor der Wand, nur gegen Z-Kampf). Echtes Relief sind nur Laibung (Gang 0,11 m, Raum 0,23 m) und Sohlbank. Der Rest ist gemalt: Platte = Wandkachel in Weltphase (Fugen laufen durch), 5 Stufen dunkler, am Rand in harten Stufen ohne Dithering noch dunkler; Laibung 2 Stufen dunkler im Atlas. Eine Nische, in die man hineinsieht, ist das nicht: aus streifendem Winkel sieht man die Platte nicht hinter dem Rand, nur einen schmalen Vorsprung (`streiflicht*.png`).

## Textur

Fugen im Raster 64 px je Meter (Welt-UV). Gewändesteine: 7 Steine je Hälfte, Fugen 3 px lesbar, Stein jeder zweiten eine Stufe dunkler, Schlussstein ohne Scheitelfuge. Stein = gehobene Wandkachel (LIFT wie Pfeiler), Fugen der Kachel entfernt. Eigener Abschluss zur Wand: Fase und Stufe 1/16, Zahn in der Wand; keine nackte Kante.

## Bilder

Je Fassung in `gang/` und `raum/`: `auge-frontal-hell.png`, `auge-schraeg-hell.png`, `spiel-frontal.png`, `spiel-schraeg.png` (854x480, 70 Grad, Nebel, 15-Bit-Körnung), `streiflicht.png`, `streiflicht-spiel.png`, `detail.png` (Gewände, Scheitel, Fuß, Seite), `einzel.png` (vorn, Seite, hinten, schräg oben), `vergleich-tuer-pfeiler.png` und `-schraeg.png` (Türrahmen, Wandpfeiler, 1,8-m-Figur), `wandvarianten.png` und `wandvarianten/` (a, b, c), `rundbogen-vergleich.png`. Oben im Ordner: `spielaufloesung.png`, `gang-mit-spieler.png`, `textur-a.png`.

## Offene Fragen

- Spitz- oder Rundbogen (siehe oben), und ob die Dreieckszahl 312 statt 60 tragbar ist.
- Streiflicht: die Tiefe liest sich nicht, weil sie nur 0,125 bis 0,25 m echt ist. Reicht das, oder soll die Nische größer vortreten (Grenze 0,3125)?
- Dunkle Platte ist nur eine Stufenmalerei; ein Verlauf nach unten (Lichtfall) fehlt.
- Wandvariante b hat wenig Kontrast zwischen Gewände und Wand.
