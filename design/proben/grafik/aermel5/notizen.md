# Fassung f (Issue #265, nach „Ja, rechte Fassung“)

Blatt `blatt_e_f.png` (links e, rechts f; oben Gang, Mitte Schuss, unten aufgeladene Klinge). Modelle `modelle_e/` (Ordnername aus dem Skript-Standard, enthält f), Fotos `fotos_f/`, Skripte `pistol.py`, `blade.py`, `arm.py`, `fotos.sh`, `blatt.py`. Übergabe: `design/uebergabe/` (Skripte, acht Dateien, README mit allen Haltungswerten).

## Die vier Punkte
1. **Stulpe abgesetzt:** (160, 125, 90) statt (134, 102, 72), Faust dunkler (34, 25, 22). Im Gang-Foto liest sich die Stulpe als helles Band mit Nietenrand.
2. **Pistolenfaust schlank:** Radius 0,0145 bis 0,018 (etwa 1,4 Griffbreiten), deckt nur 62 Prozent des Griffs; Handrücken (glatter, hellerer Streifen) und Fingerrücken (drei Fugen) sind zwei Flächen mit dunkler Kante und hellem Saum; Daumen eigenes Stück; Zeigefinger ein Stummel. Dreiecke 226.
3. **Mantelärmel im Bild:** bei der **Pistole** läuft hinter der Stulpe ein blaugrauer Ärmel mit Faltenbahnen nach rechts aus dem Bild (im Gang-Foto etwa 14 cm sichtbar); die Stulpe ist auf 1,6 cm gekürzt und der Unterarm zeigt seitlich statt zur Kamera; dazu `PistolGrip` x von 0,09 auf 0,06. Bei der **Klinge** ist er weiter nicht richtig zu sehen (siehe unten).
4. **Klingenhand:** Stulpe liegt an, kein Flügel mehr.

Aufgeladene Klinge wie bei e (Haltung 22° / (−0,03; 0,01; 0)); gleichgeblieben.

## Haltungswerte (lokal für die Fotos, in der Übergabe-README)
`PistolGrip` (0,06; −0,04; 0,18), `BladeGrip` (−0,11; −0,07; 0,19), `WindUpShift` (−0,03; 0,01; 0,0), `WindUpYawDegrees` 22.

## Was nicht stimmt
- **Klinge:** Der Ärmel ist im Gang-Foto nicht als Stoffstück zu erkennen; er läuft links aus dem Bild und ist dunkel wie die Faust. Punkt 3 ist dort nicht erfüllt.
- **Pistole von hinten oben:** die Stulpe ist ein großer, runder, heller Block, weil man sie von der Seite des Rohrs ansieht; sie liest sich eher als Wulst als als schmales Band.
- Der Ärmel an der Pistole hat nur zwei, drei Faltenbahnen und ist am Bildrand abgeschnitten.
- Getestet habe ich nur die drei Fotos; Schwung und Rückstoß mit den neuen Haltungswerten nicht, ebenso nicht der Core-Test zu `Reach`.
