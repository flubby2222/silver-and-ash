# Freistehende Säule (Probestück zu #539)

Erzeugt mit `design/proben/grafik/saeule-539/saeule.py` (Blender 5.3, Aufruf im Kopf des Skripts, etwa 40 s, zwei Läufe liefern byte-gleiche Dateien). Nur Stein-Reihe der Hauptpalette, Flat Shading, keine Texturkörnung. Sockel und Kapitell sind das Profil des Wandpfeilers (#536), ins Runde gedreht. Nichts im Spiel integriert; Wandpfeiler und Tür dienen in den Bildern nur als gelesene Nachbarn.

## Maße (Raum 6 m, Ursprung Mitte, Boden y = 0)

| Teil | von bis | Eckenhalbmesser | Flächenmitte 12-Eck | Flächenmitte 8-Eck |
|---|---|---|---|---|
| Sockel | 0 bis 0,4375, dann Fase 1/16 bis 0,5 | 0,5000 (oben nach der Fase 0,4375) | 0,4830 | 0,4619 |
| Schaft | 0,5 bis 5,5 | 0,4375 | 0,4226 | 0,4042 |
| Kapitell | 5,5 bis 6,0: Fase, Platte, Kehle, Wulst (Raute), Fase, Platte | 0,5000 an Platten und Wulst, 0,4375 in der Kehle | 0,4830 / 0,4226 | 0,4619 / 0,4042 |

Größter Halbmesser der Säule: 0,5000 (Sockel unten, Kapitellplatten). Kleinster Halbmesser: 0,4375 im Schaft (Ecken), 0,4226 an den Flächenmitten des 12-Ecks. Alles liegt im Kreis R = 0,5 m (Beweis: `draufsicht-kreis.png`, roter Kreis). Der Schaft füllt den Kreis zu 77 % (0,4375 / 0,5 im Quadrat), mit Sockel und Platte zu 97 % (12-Eck) beziehungsweise 90 % (8-Eck, Flächenmitten).

Dreiecke: **12-Eck 284**, 8-Eck 188 (Requisit-Spanne 100 bis 400). Je Seitenzahl drei Modelle a, b, c in `modelle/` (`saeule_12_a.glb` bis `saeule_8_c.glb`). `check_closed.py`: alle sechs geschlossen, 0 offene Kanten, Deck- und Standfläche je ein Vieleck.

## Gewählte Lösungen

- **Radius gegen Formenlehre:** Dort stehen Sockel und Kapitell 1,0 breit über den Flächen. Bei einem 8-Eck wären das 1,08 über den Ecken, also außerhalb des Kreises R 0,5. Ich lege die Ecken auf R 0,5 und den Schaft auf R 0,4375 (Vorsprung 1/16 wie am Wandpfeiler). Das ist eine Abweichung von der Formenlehre und bleibt zu bestätigen.
- **Seitenzahl: 12-Eck empfohlen.** Beide gebaut (`vergleich-8eck-12eck-*.png`, `einzel-8eck-a.png`, `draufsicht-kreis.png`). Im Spielbild in 8 m lesen beide als Säule; das 8-Eck zeigt schon aus 3 m harte Kanten und eine Silhouette mit Knick, das 12-Eck wirkt rund und füllt den Kreis besser (Flächenmitte 0,483 statt 0,462). Mehrpreis 96 Dreiecke.
- **Textur ohne Naht:** Atlas 192 x 384, Schaftstreifen 168 px breit (2,625 m, 12 x 14 px; Umfang 2,718 m, 3,5 % gestaucht), rundum periodisch, u läuft genau 0 bis 1 des Streifens. Schaft 10 Trommeln zu 32 px (0,5 m) mit waagerechter Fuge (2 px dunkel, 1 px Schatten) auf dem 64-px-Raster, keine senkrechten Fugen, jede Trommel ein Stück. Stufen je Zelle 14 x 8 px (0, +1, -1 Stein-Stufen), keine Körnung. Beweis: `naht.png` (Blick auf die Nahtstelle von +x), `textur-streifen.png` (Streifen zweimal nebeneinander).
- **Eigener Abschluss:** Sockel mit dunkler Bodenfuge unten und heller Fase oben; Kapitell mit Kehle im Schatten, Wulst oben hell, unten dunkel, Platte mit heller Oberkante. Deck- und Standfläche eigene Farben (Stufe 12 und 3).
- **Farben:** nur Stein-Reihe, 13 verschiedene Stufen je Textur, geprüft im Skript (jedes Pixel ist eine Farbe der Reihe). Die Wandvarianten a, b, c unterscheiden sich in den Samen (Zellen, Ausbrüche, Risse); die Grundstufe 11 ergibt sich aus der Wandkachel plus 4 und wird bei 11 gedeckelt, sodass alle drei gleich hell sind.

## Bilder

Alle in `design/proben/grafik/saeule-539/`.

- Einzelstück: `einzel-a.png`, `einzel-b.png`, `einzel-c.png` (vorn, Seite, hinten, schräg oben); `einzel-8eck-a.png`.
- Augenhöhe 1,6 m, Spielauflösung 854 x 480, 70 Grad, Nebel und 15-Bit-Körnung: `auge-3m-spiel.png`, `auge-8m-spiel.png`; hell: `auge-3m-hell.png`, `auge-8m-hell.png`, dazu `auge-3m-hell-b.png`, `auge-3m-hell-c.png`.
- Blick nach oben aufs Kapitell: `blick-hoch-hell.png`, `blick-hoch-spiel.png` (mit Decke bei 6 m).
- Sockel und Kapitell: `detail-sockel-kapitell.png`.
- Reihe von vier Säulen (4 m Abstand) mit Wand und Wandpfeilern: `reihe-spiel.png`, `reihe-hell.png`, `reihe-schraeg-hell.png`.
- Größe mit Figur (1,8 m, Quader) und Türrahmen: `groesse-tuer-figur.png`.
- Draufsicht mit Kreis R 0,5: `draufsicht-kreis.png`.
- Naht und Textur: `naht.png`, `textur-streifen.png`, `atlas-a-b-c.png`.
- Vergleich 8-Eck gegen 12-Eck: `vergleich-8eck-12eck-spiel.png`, `vergleich-8eck-12eck-hell.png`.

## Offene Fragen und Schwächen

- Die Zellen mit Stufenwechsel (14 px breit) sind aus der Nähe als Rechtecke zu sehen und können wie senkrechte Quaderfugen wirken. Gewollt sind nur Trommelfugen; bei Bedarf Zellen breiter oder die Stufen auf zwei Zeilen beschränken.
- Boden und Decke in den Szenen sind die alten Vorschau-Texturen von `psx.py`, nicht die Hauptpalette. Sie dienen nur als Kulisse.
- In 8 m im Nebel sind Wandpfeiler hinter den Säulen kaum zu erkennen; die Säulen selbst bleiben lesbar.
- Die Formenlehre sagt 1,0 breit über den Flächen für Sockel und Kapitell; ich habe auf 1,0 über den Ecken gekürzt. Falls die Leitung 1,0 über den Flächen will, muss der Kreisradius oder der Schaft neu entschieden werden.
- Die Platte des Kapitells liegt bei 6,0 m auf der Decke; der Anschluss (Bogenansatz, Rippen) ist nicht modelliert.
