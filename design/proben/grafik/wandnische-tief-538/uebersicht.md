# Wandnische mit Tiefe (Issue #538), Regal-Fassung und drei Fuellungen

Probestueck ausserhalb des Spiels. Alles aus einem Skript: `wandnische_tief.py` (Blender 5.3, kein Zufall, zwei Laeufe liefern bitgleiche
.glb und .png, geprueft mit sha256 ueber alle 28 Dateien in `modelle/`).

## Bilder

| Bild | Inhalt |
| --- | --- |
| `blatt-nische-tief-538.png` | flach (bisher) / Tiefe 0,15 m / Tiefe 0,3 m, je frontal, schraeg 45 Grad, Streiflicht; Gang, Spielansicht (Nebel, harte Pixel), 1,8-m-Figur |
| `raum-empfehlung-538.png` | Empfehlung (0,3 m) im 6-m-Raum neben Tuerrahmen und Wandpfeiler, frontal und schraeg |
| `gang-empfehlung-538.png` | Empfehlung in der 3,5-m-Gangwand, frontal |
| `blatt-nische-regal-538.png` | Nische leer / Flaschen / Kerzen / Schaedel; Zeile 1 Regal frontal, Zeile 2 Regal schraeg, Zeile 3 ohne Bord frontal, Zeile 4 Regal im Gang mit 1,8-m-Figur |
| `blatt-nische-flaschen-r3-538.png` | Nacharbeit Flaschen Runde 3 (#553): Pyramide und Stapel, je Regal und ohne Bord, frontal, schraeg, im Gang mit 1,8-m-Figur, unten Spielaufloesung 854x480 aus 3 m (Blau-Fassung: `blatt-nische-flaschen-r3-blau-538.png`) |
| `staub-vergleich-538.png` | Nahaufnahme ohne und mit Staub, Pyramide und Stapel |
| `einzel-flaschen-r3.png`, `spielaufloesung-flaschen-pyramide.png`, `spielaufloesung-flaschen-stapel.png` | Flaschen einzeln (vorn, Seite, hinten, schraeg oben); 854x480 aus 3 m |
| `fuellungen-detail.png` | Fuellungsmodelle (Flaschen Pyramide/Stapel gruen und blau, Kerzen brennend und aus, Schaedel) in der Regal-Nische, Naheinstellung |
| `rahmen-vergleich.png` | Rahmen buendig gegen 1/16 m vorspringend (Tiefe 0,3 m) |
| `einzel-t30.png`, `einzel-t15.png`, `einzel-regal-t30.png` | vorn, Seite, hinten, schraeg oben je Fassung |
| `spielaufloesung-t30.png` | 854 x 480, 70 Grad, Augenhoehe 1,6 m |

## Modelle (`modelle/`)

Je Wandvariante a, b, c (Wandkacheln aus `assets/textures`): `wandnische_t15_*.glb`, `wandnische_t30_*.glb`, `wandnische_regal_t30_*.glb`;
Fuellungen: `fuellung_flaschen_{pyramide,stapel}_{gruen,blau,glatt}` (glatt = ohne Staub), `fuellung_kerzen_brennend`, `fuellung_kerzen_aus`, `fuellung_schaedel`
(.glb mit .png-Atlas). Alle Dateien mit `python tools/models/check_closed.py <glb>` geprueft: rundum geschlossen, 0 offene Kanten.

| Modell | Dreiecke | Teile |
| --- | --- | --- |
| Nische 0,15 m und 0,3 m (je a/b/c) | 322 | Gewaende 176, Platte 126, Sohlbank 20 |
| Nische Regal 0,3 m (je a/b/c) | 342 | wie oben plus Bord 20 |
| Flaschen Anordnung a (gruen oder blau) | 392 | 6 quer liegende und 1 stehende Flasche, je 56 Dreiecke |
| Flaschen Anordnung b (gruen oder blau) | 336 | 4 mit dem Hals nach oben geneigte und 2 stehende Flaschen |
| Kerzen brennend | 424 | fuenf Kerzen unterschiedlicher Hoehe, schief heruntergebrannt, Wachsreste, Flamme (40 Dreiecke) |
| Kerzen aus | 384 | dieselben Kerzen mit Docht, ohne Flamme |
| Schaedel | 264 | drei Schaedel (Knochen-Reihe) mit Augenhoehlen, Nase, Zaehnen |

Die Requisiten-Spanne der Art Bible (100 bis 400) wird von "Kerzen brennend" mit 424 knapp ueberschritten (Flamme allein 40). Faellt die
Flamme weg (Variante "aus"), liegt sie darunter.

Palette: die Nische nutzt nur die Reihe Stein (14 Farben im Atlas), die Fuellungen nur die Hauptpalette (Flaschen 5 Farben, Kerzen 9,
Schaedel 8). `assert` im Erzeuger: jede Atlasfarbe gehoert zur Palette; Fugenraster der Platte (jede Vorderflaechen-UV entspricht der
Weltlage, Kachel 1 m) wird mit `assert_raster` geprueft (102 Eckpunkte je Platte).

## Aufbau der Nische

- Eine echte Tasche in der Wand: Oeffnung in der Vorderflaeche, Laibung (Seitenwaende der Tasche, stufenweise dunkler nach hinten), Rueckwand, Sohlbank.
- Bogen: gedrueckter Spitzbogen, Kaempfer 1,25 m, Scheitel 2,0 m, Oeffnung 1 m breit; Gewaendeband 1/8 m um die Oeffnung; Sohlbank y 0,25 bis 0,5 m (Oberseite = Sohle der Nische).
- Innen: Rueckwand 8 Stufen dunkler als die Wandkachel (Reihe Stein), Laibung 3, 2, 1 Stufen dunkler nach hinten. Keine Schatten im Spiel, der dunklere Stein ersetzt sie.
- Rahmen (Gewaende) springt 1/16 m vor die Wandflaeche vor; die Sohlbank 1/8 m.
- Keine nackte Kante: Fase und Band hell, Aussenkante eine Stufe dunkler, Fuge am Bandende. Die Platte laeuft im Raster der Wand weiter (Naht auf den Meterlinien).

## Auszusparende Wandflaeche (Anschluss im Spiel)

Der Baustein ist ein Wandstueck und bringt seine eigene Wandflaeche mit:

- **Breite 2,0 m** (genau eine Zelle, Raender auf den geraden Metern des Rasters, damit die Fugen durchlaufen), **Hoehe 3,0 m ab Boden** (y 0 bis 3).
- Das Spiel laesst die gewoehnliche Wandflaeche dieser Zelle von y = 0 bis 3 m weg und setzt das Modell dorthin (Wandebene z = 0, Nische zeigt in den Raum, geht um 0,3625 m in die Wand).
- **Gang (3,5 m)**: darueber bleibt ein Streifen von 0,5 m gewoehnliche Wand; die Naht liegt auf der Meterlinie y = 3 und passt zur Kachelphase.
- **Raum (6 m)**: darueber bleiben 3 m gewoehnliche Wand, gleiche Naht bei y = 3.
- Die Wandzelle ist ein 2 m dicker Block: die Tasche (hoechstens 0,3625 m) bleibt weit davon entfernt, durchzustossen. Bewegung, Kollision und Schuesse bleiben unveraendert, die Nische ist reine Zierde.
- Der Spieler im Gang wird durch die vorspringende Sohlbank (1/8 m) nicht eingeschraenkt (nur Zierde).
- Einfachste Loesung: Zelle komplett weglassen und das Modell einsetzen; Streifen darueber ueber die gleiche Wandroutine wie bisher.

## Empfehlung

**Tiefe 0,3 m, Rahmen 1/16 m vorspringend.**

- 0,3 m: Die Laibung und die Sohlbank werden im Streiflicht und schraeg als echte Tiefe gelesen; bei 0,15 m ist die Laibung nur ein duenner Streifen, die Nische wirkt wie ein dunkles Feld mit Rahmen und liest sich aus wenigen Metern schlechter.
- Das Bild "Streiflicht" zeigt beide Tiefen kaum als Silhouette (nur Rahmen); die Tiefe zeigt sich frontal und schraeg.
- Vorspringender Rahmen: wirft im Spiel keinen Schatten, aber die Kante gegen die Wand liest sich besser als buendig (siehe `rahmen-vergleich.png`), und er passt zu Tuerrahmen und Wandpfeiler, die ebenfalls vortreten. Buendig geht ueber die Umgebungsvariable `W538T_RAHMEN=buendig`.

## Regal-Fassung und Fuellungen

- **Regal-Tiefe: 0,3 m**, ein Bord (y 0,875 bis 1,0, 1/8 m = 8 Pixel dick, laeuft ueber die ganze Tiefe, Vorderkante in der Wandebene; Fugen im Raster). Darunter 0,375 m freie Hoehe (Flaschen), darueber bis zum Kaempfer etwa 0,25 m (Kerzen, Schaedel).
- **Flaschen**: neu gebaut (siehe Abschnitt Nacharbeit Flaschen); gehoeren unter das Bord auf die Sohle (Regal) oder ohne Bord auf die Sohle.
- **Kerzen**: gehoeren auf das Bord. Auf der Sohle ohne Bord sind sie bei 0,5 m Hoehe klein und gehen im Dunkel der Rueckwand unter; auf dem Bord stehen sie im Blickfeld und heben sich vor der dunklen Rueckwand ab.
- **Schaedel**: ebenfalls auf das Bord (Augenhoehe, drei Gesichter in Blickrichtung); ohne Bord wirken sie am Boden verloren.
- Kurz: Regal-Fassung = Flaschen unten, Kerzen oder Schaedel oben; Nische ohne Bord = Flaschen. Kerzen und Schaedel nur mit Bord.
- **Kerzen unbeleuchtet**: `fuellung_kerzen_aus` hat Docht statt Flamme und gar kein Licht. In der Beleuchtung des Spiels (keine Lichtquellen) ist die Fassung "aus" die Grundfassung; "brennend" ist dieselbe Figur plus Flamme als Koerper in Messing/Gold und wuerde im Spiel erst durch einen Leuchteffekt aus `Core`-Ereignissen lebendig. Ich liefere keine Leuchtquelle.
- **Flaschenfarben**: Gruen ist Standard (Reihe Haut/Knochen, Stufen 0 bis 3), Blau die Alternative (Reihe Stoff, Stufen 0, 2, 4, 5), Korken aus der Reihe Holz. Kein Rot.

## Fuellung in der Nische platzieren

Die Fuellungen sind eigene Props, Ursprung: Mitte unten (x 0, y 0), z von 0 bis 0,25 nach vorn. Platzierung bei Tiefe 0,3 m: Weltposition der Nischenmitte, y = 0,5 m (Sohle) oder 1,0 m (Bord), z um -0,3 m verschoben (an die Rueckwand). Bei Tiefe 0,15 m passen sie nicht (0,25 m tief). Die Fuellungen lassen sich einzeln tauschen oder weglassen; die leere Nische funktioniert allein.

## Bekannte Schwaechen

- Das Wandstueck ist 2 x 3 m gross und dupliziert die Wandkachel; die Nische kann nur in 2-m-Raster gesetzt werden (nicht beliebig an einer Wand).
- Im Streiflicht ist die Tiefe 0,15 gegen 0,3 kaum zu unterscheiden.
- "Kerzen brennend" liegt mit 424 Dreiecken knapp ueber der Art-Bible-Spanne.
- Die Flaschen sind aus 3 m klein (etwa 35 Pixel breit bei 854x480); die Form liest sich, aber nicht auf den ersten Blick.
- Die Figur (rot, 1,8 m) ist ein Quader, nur als Massstab.
- Der Hintergrund der Tuer im Raumbild (Schwarz) ist nur der Bildhintergrund, kein Modell.

## Nacharbeit Flaschen (#553)

Urteil des Menschen: "Sehen aus wie Batterien. Und vllt auch auf eine seite stapeln." Daraufhin nur die Flaschen neu gebaut:

- **Form am Umriss**: Bauch (Sechseck-Querschnitt), Schulter, schmaler Hals, Muendung mit aufgeweitetem Korken (Holz-Reihe). Der Boden ist ein Sechseck mit dunklerem Kreis in der Mitte (Einwoelbung). 4 Baender, 56 Dreiecke je Flasche, Laenge 0,36 m, Mass 1,1 mal einer 0,33-m-Flasche, damit sie aus 3 m lesbar bleibt. Aufrecht passt sie unter das Bord (0,357 m gegen 0,375 m).
- **Stapel an der linken Laibung** (x -0,5), die rechte Haelfte der Sohle bleibt frei (a bis x -0,017, b bis x 0,10).
- **Anordnung a (quer)**: drei Lagen quer liegend, je zwei tief und versetzt, Haelse abwechselnd nach links und rechts, dazu eine stehende Flasche neben dem Stapel. Hals und Bauch liegen im Profil.
- **Anordnung b (schraeg)**: vier Flaschen mit dem Hals 18 Grad nach oben geneigt, Boeden an der Laibung, dazu zwei stehende. Die Hals-Linien und die stehenden Umrisse lesen sich am klarsten.
- Alles nur Hauptpalette (assert), rundum geschlossen (`check_closed.py`, 0 offene Kanten), zwei Laeufe bitgleich (sha256 ueber alle 32 Dateien in `modelle/`).
- Die alten Modelle `fuellung_flaschen_gruen/blau` sind entfernt.

### Runde 3: Weinkeller-Stapel in der Ecke, Staub

Urteil des Menschen: Anordnung wie im Weinkeller, uebereinander, aber in einer Ecke; die Flaschenform ist angenommen und bleibt unveraendert (56 Dreiecke je Flasche); dazu etwas Staub als Textur. Die Anordnungen a und b sind verworfen und entfernt.

- **Ecke**: Stapel in der Ecke aus linker Laibung (x -0,5) und Rueckwand. Alle Flaschen liegen in Richtung der Nischentiefe, keine geneigt, keine stehend. Fuge zwischen den Sechsecken 4 mm, damit jede Flasche einzeln sichtbar bleibt. Reihenfolge der Helligkeitsstufen je Flasche leicht verschoben (drei Toene im Wechsel).
- **Pyramide** (6 Flaschen, 336 Dreiecke): Lagen 3, 2, 1, versetzt um eine halbe Flasche; Boeden mit dem dunklen Kreis nach vorn, Hals mit Korken zur Rueckwand. Liest sich als Honigwabe aus dunklen Kreisen.
- **Stapel** (7 Flaschen, 392 Dreiecke): Lagen 3, 2, 2; Haelse mit Korken nach vorn. Die Korken sind kleine Farbtupfer und zeigen die Flaschenform deutlich.
- **Staub**: Pixelkacheln (24x28 Pixel, 1 Pixel etwa 1,6 cm) auf allen nach oben zeigenden Glasflaechen, je Helligkeitsstufe eine eigene Kachel mit zwei helleren, entsaettigten Stufen derselben Palettenreihe (Haut/Knochen) als Sprenkel; die Boeden haben leicht gesprenkelte Ringe. Nur Hauptpalette (assert). Die Flasche bleibt gruen. Ohne Staub: `fuellung_flaschen_*_glatt`.
- Ragen die Flaschenenden aus der Nische? Ja: Die Flasche ist 0,36 m lang, die Nische 0,3 m tief; das Ende liegt buendig mit der Vorderkante des Rahmens (z 0,3605, 0,0625 m vor der Wand). Das Bord steht an der Wand, die Flaschen darunter ragen ueber diese Ebene hinaus.
- Alles rundum geschlossen (`check_closed.py`), zwei Laeufe bitgleich (sha256 ueber alle 36 Dateien in `modelle/`).
- **Offen**: Aus 3 m ist der Staub kaum zu sehen; sichtbar wirkt er in der Naheinstellung (`staub-vergleich-538.png`). Wirkt der Stapel aus 3 m klein, kann man die Ecke mit einer vierten Lage fuellen (die Hoehe ginge bis 0,42 m, ueber das Bord).

