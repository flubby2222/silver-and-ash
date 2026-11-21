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
| `fuellungen-detail.png` | die fuenf Fuellungsmodelle (Flaschen gruen und blau, Kerzen brennend und aus, Schaedel) in der Regal-Nische, Naheinstellung |
| `rahmen-vergleich.png` | Rahmen buendig gegen 1/16 m vorspringend (Tiefe 0,3 m) |
| `einzel-t30.png`, `einzel-t15.png`, `einzel-regal-t30.png` | vorn, Seite, hinten, schraeg oben je Fassung |
| `spielaufloesung-t30.png` | 854 x 480, 70 Grad, Augenhoehe 1,6 m |

## Modelle (`modelle/`)

Je Wandvariante a, b, c (Wandkacheln aus `assets/textures`): `wandnische_t15_*.glb`, `wandnische_t30_*.glb`, `wandnische_regal_t30_*.glb`;
Fuellungen: `fuellung_flaschen_gruen`, `fuellung_flaschen_blau`, `fuellung_kerzen_brennend`, `fuellung_kerzen_aus`, `fuellung_schaedel`
(.glb mit .png-Atlas). Alle 28 Dateien mit `python tools/models/check_closed.py <glb>` geprueft: rundum geschlossen, 0 offene Kanten.

| Modell | Dreiecke | Teile |
| --- | --- | --- |
| Nische 0,15 m und 0,3 m (je a/b/c) | 322 | Gewaende 176, Platte 126, Sohlbank 20 |
| Nische Regal 0,3 m (je a/b/c) | 342 | wie oben plus Bord 20 |
| Flaschen (gruen oder blau) | 384 | 13 liegende Sechskant-Flaschen im Dreieckstapel (4-3-2... je Reihe), je Flasche Koerper, Hals, Boden |
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
- **Flaschen**: liegend, im Stapel, gehoeren unter das Bord auf die Sohle (Regal) oder auf die Sohle der Nische ohne Bord. Der Stapel ist schwer und breit und liest sich schon ohne Bord als Weinregal.
- **Kerzen**: gehoeren auf das Bord. Auf der Sohle ohne Bord sind sie bei 0,5 m Hoehe klein und gehen im Dunkel der Rueckwand unter; auf dem Bord stehen sie im Blickfeld und heben sich vor der dunklen Rueckwand ab.
- **Schaedel**: ebenfalls auf das Bord (Augenhoehe, drei Gesichter in Blickrichtung); ohne Bord wirken sie am Boden verloren.
- Kurz: Regal-Fassung = Flaschen unten, Kerzen oder Schaedel oben; Nische ohne Bord = Flaschen. Kerzen und Schaedel nur mit Bord.
- **Kerzen unbeleuchtet**: `fuellung_kerzen_aus` hat Docht statt Flamme und gar kein Licht. In der Beleuchtung des Spiels (keine Lichtquellen) ist die Fassung "aus" die Grundfassung; "brennend" ist dieselbe Figur plus Flamme als Koerper in Messing/Gold und wuerde im Spiel erst durch einen Leuchteffekt aus `Core`-Ereignissen lebendig. Ich liefere keine Leuchtquelle.
- **Flaschenfarben**: Es gibt kein Gruen und kein Blau in der Hauptpalette als eigene Reihe; ich habe aus vorhandenen Stufen gebaut.
  - Vorschlag A (im Bild als Standard): dunkles Flaschengruen aus der Reihe Haut/Knochen, Stufen 0 bis 3 (16/25/20, 40/58/43, 72/89/67, 109/118/96).
  - Vorschlag B: dunkles Blau aus der Reihe Stoff, Stufen 0, 2, 4, 5 (18/17/34, 30/34/62, 60/75/116, 86/107/154).
  - Nach meinem Eindruck liest sich A in der Nische besser (dunkler Stapel gegen die Rueckwand, dazu gehoert der Olivton zu den Knochenfarben der Schaedel). Der Mensch kann zwischen A und B wahlen; beide Modelle liegen bei, kein Rot.

## Fuellung in der Nische platzieren

Die Fuellungen sind eigene Props, Ursprung: Mitte unten (x 0, y 0), z von 0 bis 0,25 nach vorn. Platzierung bei Tiefe 0,3 m: Weltposition der Nischenmitte, y = 0,5 m (Sohle) oder 1,0 m (Bord), z um -0,3 m verschoben (an die Rueckwand). Bei Tiefe 0,15 m passen sie nicht (0,25 m tief). Die Fuellungen lassen sich einzeln tauschen oder weglassen; die leere Nische funktioniert allein.

## Bekannte Schwaechen

- Das Wandstueck ist 2 x 3 m gross und dupliziert die Wandkachel; die Nische kann nur in 2-m-Raster gesetzt werden (nicht beliebig an einer Wand).
- Im Streiflicht ist die Tiefe 0,15 gegen 0,3 kaum zu unterscheiden.
- "Kerzen brennend" liegt mit 424 Dreiecken knapp ueber der Art-Bible-Spanne.
- Die Flaschen sind sechseckig (grobe Rundung), bei harten Pixeln lesen sie sich als Fassboden mit Mulde.
- Die Figur (rot, 1,8 m) ist ein Quader, nur als Massstab.
- Der Hintergrund der Tuer im Raumbild (Schwarz) ist nur der Bildhintergrund, kein Modell.
