# Probestück Throwing Knives (Issue #204)

Wurfmesser als Fernwaffe, Stufe **Vampiric**, außerhalb des Spiels. Alles per Blender-Python erzeugt, nichts heruntergeladen. Unter `assets/` und `src/` ist nichts geändert; Ärmel, Klinge, Ghul und die Bestandswaffen werden aus `assets/models/` nur gelesen.

## Stand

- **Runde 1 (a „Aderlass“, b „Kreuzblatt“): abgelehnt.** Urteil des Auftraggebers: „3 wurfmesser sind ok. Das Design stimmt überhaupt nicht. Der agent soll sich einmal die gesamte bibliothek anschauen und sich an den Modellen, die es ins Spiel geschaft haben orientieren.“ Fest steht seitdem nur: drei Messer als Fächer in der Hand. Die Dateien von a und b liegen weiter in diesem Ordner, auf dem neuen Blatt kommen sie nicht vor.
- **Runde 2 (`runde-2/`):** Bestandsaufnahme aller Spielmodelle, danach drei Messer: **e „Schwarzrücken“** (das eigene Stück, mein Vorschlag), **d „Jagdeisen“** (Gegenentwurf, näher an Axt und Armbrust) und **c „Silberblatt“** (Studie, zu nah an der Silberklinge, nur noch auf dem Vergleichsstreifen).
- Zwischen Bestandsaufnahme und Bau kam die Korrektur des Auftraggebers: Der Bestand ist Orientierung, kein Maßstab („eigene Stile entwickeln und fortlaufend besser werden“). c und d waren noch aus der Liste abgeleitet; e ist danach entstanden.

Das Blatt: `runde-2/blatt.png`. Einzelbilder: `runde-2/ansichten/`. Bestandsblätter: `runde-2/bibliothek/`.

## Was die Spielmodelle gemeinsam haben

Gemessen mit `runde-2/bibliothek.py` (alle 79 Modelle, eine Kamera, ein Licht; Zahlen in `runde-2/bibliothek/bestand-zahlen.txt`). Die Liste beschreibt die Welt, in die das Messer kommt; sie ist kein Bauplan.

| Merkmal | Bestand |
|---|---|
| Dreiecke | Klinge 152, Keule 368, Armbrust 428, Axt 580, Pistole 584, Peitsche 1200 |
| Textur | immer 128 × 128, ungefiltert; Klinge, Pistole, Keule weich gemalt und auf 31–32 Farben gerastert; Axt, Peitsche, Armbrust harte Palettenstufen, 14–20 Farben |
| Texeldichte | 250–880 Texel je Meter (Klinge 622, Pistole 881, Armbrust 808, Axt 395) |
| Metall | Silber des Jägers hell und fast weiß (206,210,222; dunkler 132,136,150); Eisen an Axt und Armbrust Eisen 1–4 der Hauptpalette mit Patina und Kratzern |
| Schneiden | Silberklinge: die ganze Klinge hell, Schneide nur eine Stufe heller, Hohlkehle dunkel. Axt: Schneide Eisen 6, nur 6,5 mm breit auf 100 mm Blatt und von Rauschen unterbrochen. Kein Stück hat einen dunklen Körper mit durchlaufendem hellem Umriss |
| Griffe | immer warmes Braun: Leder (70,48,34) mit dünnem Silberdraht, Nussbaum (84,56,38), Holz 1–5; warmer Anteil der Seitenansicht 37–97 % |
| Akzent | Gold nur an kleinen Beschlägen (Zwingen der Axt, zwei Scheiben der Armbrust). **Blutrot ab Stufe 3: 0,0 % an jeder Bestandswaffe**, auch an der Armbrust (Vampiric). Helles Rot gibt es im Bestand nur als schmale Bänder der Kiste `chest_vampire` |
| Stufe | Worn: Holz als Körper (Keule). Royal: Gold an den Zwingen (Axt). Vampiric: an der Armbrust gar nicht gezeigt. Kisten tragen die Stufe als Bänder |
| Maße | Klinge der Silberklinge: halbe Breite 10,5 mm, halbe Dicke 2,8 mm, Länge 150 mm (Skriptmaß, in der Datei 1,8-fach); Griffradius etwa 10 mm |
| Schattierung | Waffen flach, Ärmel und Pistole weich |

## Worin a und b davon abwichen

- **Dunkler Körper mit hellem Saum:** ein Viertel der Textur war Eisen 2 (55,57,84), darum lief über die ganze schmale Klinge ein Rand aus Eisen 5–7. Mittlere Helligkeit 59 bei 24–32 % hellen Bildpunkten: hart zweigeteilt, wie eine Umrisslinie. Die Vermutung der Leitung stimmt, mit einer Einschränkung: Eine helle Schneide gibt es auch an der Axt, dort aber schmal, unterbrochen und auf einem mittleren Ton.
- **Rot als Fläche:** Blutrot ab Stufe 3 auf 12,2 % (a) und 20,5 % (b) der Seitenansicht, etwa ein Drittel der Textur. Im Bestand 0 %.
- **Kein brauner Griff:** warmer Anteil 3–7 % statt 37–97 %. Damit fehlte der Ton, der alle Waffen des Jägers verbindet.
- **Gezeichnet statt gemacht:** Wicklung, Email und Saum waren gleichmäßige Muster ohne Gebrauchsspuren; die Bestandswaffen zeigen Flecken, Kratzer, Patina.

## Runde 2: die Messer

| | e „Schwarzrücken“ | d „Jagdeisen“ | c „Silberblatt“ (Studie) |
|---|---|---|---|
| Idee | aus einem Stück geschmiedetes Wurfmesser: der Rücken behält die schwarze Schmiedehaut, nur die Schneidenhälfte ist blank geschliffen | Blattklinge aus dem Eisen der Jagdaxt, Holzgriff mit Zwinge und Kappe | verkleinerte Silberklinge |
| Umriss | einschneidig, gerader Rücken, bauchige Schneide, Ringknauf (ein Loch im Umriss) | Blatt, am breitesten im unteren Drittel | gerade Raute mit kurzer Parierstange |
| Griff | flache Angel, eng mit dunkler Lederschnur umwickelt; keine Parierstange, kein Holz | dunkles Holz, Eisenzwinge, Eisenkappe | braunes Leder mit Silberdraht, Silberknauf |
| Stufe Vampiric | kurzes blutrotes Band am Ring (Blutrot 3, Licht Blutrot 5) | schmaler Ring Blutrot 3 hinter der Zwinge | nicht gezeigt |
| Dreiecke je Messer | 196 (drei: 588) | 152 (456) | 112 (336) |
| Farben, Texeldichte | 13, 1097 Texel/m | 13, 742 | 13, 794 |
| Seitenansicht: Helligkeit 10/50/90 % | 39/83/173 | 59/106/173 | 52/173/203 |
| Blutrot ab Stufe 3 | 5,8 % | 1,6 % | 0,0 % |
| Länge (Spielmaß) | 0,20 m ohne Band, 0,227 m mit | 0,190 m | 0,188 m |
| Datei | `messer-e-schwarzruecken.glb`, `textur3.png` | `messer-d-jagdeisen.glb`, `textur2.png` | `messer-c-silberblatt.glb`, `textur2.png` |

Alle Farben stammen aus `assets/palette/hauptpalette.json` (im Skript geprüft), harte Stufen ohne Dithering, flach schattiert, geschlossen (`tools/models/check_closed.py`: 0 offene Kanten bei allen dreien).

**Warum e so aussieht.** Die Form folgt dem, was das Messer im Spiel tun muss, nicht einer Liste:
- *Zweigeteilt statt umrandet.* Runde 1 hatte einen hellen Saum um einen dunklen Körper. Bei e liegen ein dunkler und ein heller Streifen nebeneinander (Schmiedehaut am Rücken, Schliff zur Schneide). Das bleibt auch bei wenigen Pixeln ein Messer und unterscheidet es von der hellen Silberklinge links.
- *Ein Wurfmesser, kein kleiner Dolch.* Keine Parierstange, kein Holzgriff, nichts, was sich beim Wurf verfängt; der Ring ist der Ort für den Finger und für das Band.
- *Das Rot hat eine Aufgabe.* Es ist die Stufe und das Einzige, was man vom fliegenden Messer von hinten sieht. 5,8 % der Seitenansicht statt 12–20 % in Runde 1.
- *Gebrauchsspuren wie im Bestand:* Hammerdellen, Schleifriefen quer zur Klinge, Scharten in der Schneide, gescheuerte Stellen am Ring.

## Bilder (`runde-2/`)

| Bild | Inhalt |
|---|---|
| `blatt.png` | oben e, c, d neben Silberklinge, Pistole, Jagdaxt und Pflockarmbrust in einem Bild und Maßstab (von oben und von der Seite); darunter je Entwurf e und d: vier Ansichten, Ich-Sicht 854 × 480 mit der Klinge links, steckt im Ziel, Flug |
| `werkstatt-oben.png`, `werkstatt-seite.png` | die beiden Vergleichsstreifen einzeln |
| `ansichten/<c\|d\|e>-…` | Einzelbilder wie in Runde 1 (`seite`, `vorn`, `hinten`, `schraeg`, `ich-sicht-854x480`, `steckt-854x480`, `steckt-nah`, `flug-854x480`, `klein-854x480`) |
| `bibliothek/bibliothek-waffen.png` | alle Waffen: Seite, oben, schräg, Ich-Sicht mit Ärmel, Textur |
| `bibliothek/bibliothek-kisten-tueren.png`, `-gegner.png`, `-requisiten.png` | der übrige Bestand |
| `bibliothek/bestand-zahlen.txt` | die Messwerte |

Die Ich-Sicht in den Bestandsblättern stellt die Ruhehaltung nach; Armbrust und Axt wirken dort größer und tiefer als im Spiel. Die Einzelbilder von c stammen aus einem früheren Lauf (Griff 2 cm tiefer in der Manschette als bei e und d).

## Erzeugen

Aus `runde-2/`, `TEMP` auf einen Arbeitsordner setzen:

```
blender --background --python messer2.py      (c und d)
blender --background --python messer3.py      (e)
blender --background --python blatt2.py       (Blatt; mit "-- --studie" nur die Einzelbilder von c)
blender --background --python bibliothek.py   (Bestandsblätter und Zahlen)
```

`blatt2.py` benutzt die Schleife von `../blatt.py` (Runde 1); dort sind nur Spitzenlänge und Titel einstellbar geworden.

## Eigene Einschätzung

**Vorschlag: e.** Es ist das Stück mit eigener Form, und es löst zwei Schwächen aus Runde 1 (Umrisslinie, Sichtbarkeit im Flug).

**Wo e noch nicht stimmt:**
- **In der Hand sieht man Ring und Band kaum.** Der Ärmel hat keine Hand; die Ringe liegen am Rand der Manschette übereinander. Das Erkennungszeichen des Messers zeigt sich erst im Flug und im Ziel. Das löst eine Haltung (Finger im Ring, Messer weiter vorn), keine Farbe.
- **Die Wicklung** wirkt in der Ich-Sicht rötlich und gestreift (Holz 1 und 2 der Palette sind rotbraun). Ruhiger wäre eine Wicklung mit weniger Windungen.
- **Im Ziel aus 3,5 m** bleibt jedes Messer bei etwa 6 Pixeln; das Band hilft, der Ring ist dort nicht zu lesen.
- **Das Band ist starr.** Im Flug und im Ziel müsste es hängen oder wehen; das ist eine Frage an die Animation.
- **e ist dunkler als die Silberklinge** und zeigt mehr Rot als jede Bestandswaffe. Das ist Absicht (Vampiric, Unterscheidung von der Klinge), aber ein Urteil, das der Auftraggeber fällen sollte.

**Zu d:** ruhiger und näher am Bestand; liest sich in der Hand gut, hat aber wenig Eigenes und trägt Rot, obwohl die Armbrust derselben Stufe keines trägt. **Zu c:** eine verkleinerte Silberklinge; neben der Klinge links stehen dann vier gleiche Dolche im Bild. Als Entwurf ziehe ich c zurück.

## Runde 3: Tiefe in der Manschette (`runde-3/`)

Urteil des Auftraggebers zu Runde 2 (2026-11-28): „E ist gut, das Modell soll aber etwas weiter in den Ärmel Ragen. Sieht grade aus, als würde er alle drei messer am untersten Ende halten.“ e ist angenommen; c und d sind erledigt. Am Modell ändert sich nichts, nur die Lage in der Hand.

`runde-3/tiefe.py` rendert dieselbe Ich-Sicht mit vier Abständen vom Klingenansatz zum Haltepunkt des Ärmels (Skriptmaß; die Wicklung ist 42 mm lang, dahinter folgt der Ring). Blatt: `runde-3/tiefen.png`, Einzelbilder `runde-3/ansichten/<r2|t50|t40|t32>-ich-sicht-854x480.png`.

| Abstand | Was man sieht |
|---|---|
| 70 mm (Runde 2) | die ganze Wicklung frei, Ring am Rand der Manschette: am letzten Ende gehalten |
| 50 mm | etwa drei Viertel der Wicklung frei; kaum anders als Runde 2 |
| **40 mm (Empfehlung)** | die vordere Hälfte der Wicklung frei: die Faust sitzt um die hintere Hälfte und den Ring; Fächer und Klingen bleiben ganz lesbar |
| 32 mm | nur ein schmaler Streifen Wicklung; die Klingen wachsen fast aus dem Ärmel, die Wicklung als Erkennungszeichen geht verloren |

Noch offen: Ring und Band sind bei jeder dieser Tiefen im Ärmel verborgen; der Fächer dreht weiter um einen Punkt nahe am Knauf, die Griffe laufen dort ineinander.

## Runde 4: Versatz zur Seite (`runde-4/`)

Urteil des Auftraggebers zu Runde 3 (2026-11-28): „40mm und etwar 25 mm nach links, damit das Modell nicht in den Ärmel clipt“. Die Tiefe 40 mm ist damit gewählt. Am Modell ändert sich nichts.

`runde-4/versatz.py` prüft an der Geometrie statt am Bild: jedes Dreieck der drei Messer gegen jedes Dreieck des Ärmels (`assets/models/pistol_hand.glb`, nur gelesen). Der Deckel der Manschette zählt nicht, dort tritt der Griff ein; jedes andere Dreieck ist ein Durchstich. Abgetastet wird der Versatz nach links von 0 bis 30 mm (`runde-4/versatz-zahlen.txt`). Blatt: `runde-4/versatz.png` (je Zeile Ich-Sicht 854 × 480, Manschette dreifach, zwei Prüfkameras), Einzelbilder unter `runde-4/ansichten/`.

| Versatz nach links | Befund |
|---|---|
| 0 mm (Runde 3) | Die Wicklung des im Bild rechten Messers sticht durch die obere rechte Wand des Kragens (4 Dreieckspaare). Das ist die Stelle, die der Auftraggeber gesehen hat |
| 1,5 bis 5,5 mm | frei: kein Dreieck des Ärmels außer dem Deckel wird berührt |
| **3,5 mm (Empfehlung)** | Mitte des freien Fensters. Fächer ganz im Bild (x 394 bis 634, y 152 bis 347), 69 mm Abstand zur Silver Blade, im Bild keine Überdeckung |
| ab 5,75 mm | das Band des rechten Messers sticht hinten durch die linke Ärmelwand; ab 9 mm die Wicklung des linken Messers, ab 12 mm auch Band und Ring des mittleren und linken |
| 25 mm (wie bestellt) | 156 Dreieckspaare: Griffe, Ringe und Bänder aller drei Messer stehen aus der linken Ärmelwand, in der Ich-Sicht steht der linke Griff neben der Manschette, und der Fächer überdeckt die Silver Blade (318 Pixel) |

Grund für das schmale Fenster: Der Ärmel knickt hinter der Manschette nach rechts unten ab, die Messer laufen gerade nach hinten. Mehr Platz nach links gäbe es erst mit kürzeren Bändern oder einem Fächer, der im Ärmel enger steht; das wäre eine Änderung an Modell oder Fächer und gehört nicht in diese Runde.

### Endwerte der Haltung (für Teil 2, die Code-Sitzung)

Alle Längen im Skriptmaß; Datei und Ich-Sicht sind 1,8-fach, die Werte in Klammern sind schon multipliziert (Spielachsen: x nach links im Bild, y oben, z vorn; Blender-Datei: Spitze −Y, Klingenbreite Z).

| Größe | Wert |
|---|---|
| Modell | `runde-2/messer-e-schwarzruecken.glb`, dreimal, unverändert |
| Haltepunkt des Ärmels relativ zum Auge | (−0,100; −0,095; 0,205) m, Ärmel `pistol_hand.glb` unverändert dort |
| Tiefe: Klingenansatz bis Haltepunkt, längs der Messerachse | 40 mm (72 mm) |
| Versatz der drei Messer gegen den Ärmel nach links (+x) | 3,5 mm (6,3 mm) |
| Neigung der Spitze nach vorn oben | 44° |
| Kippen im Bild (um die Blickachse) | 6° |
| Fächer | −17°, 0°, +17° um die Dickenachse des Messers, Drehpunkt 85 mm (153 mm) hinter dem Klingenansatz |
| Sichtfeld der Prüfung | 70° senkrecht, 854 × 480 |

Als Matrix, wie in `blatt.py` (`held`) und `runde-4/versatz.py` (`put`): `T(Versatz) · T(Auge + Haltepunkt) · R_y(6°) · Neigung(44°) · T(0, −72 mm, 0) · T(0, 153 mm, 0) · R_z(Fächer) · T(0, −153 mm, 0) · R_y(90°)`, alles in Blender-Achsen. Wer Tiefe, Fächer oder Ärmel ändert, lässt `versatz.py` neu laufen: Das freie Fenster ist nur 4 mm breit.

## Runde 4b: mittig aus der Öffnung (`runde-4/`)

Urteil der Leitung zu Runde 4 (vorläufig): 3,5 mm sind frei von Durchstichen, aber in der Ich-Sicht sitzt das rechte Messer weiter auf der rechten Ecke der Manschette. Gemeint war: Der Fächer soll aus der Öffnung kommen, nicht aus der Kante. Das stimmt auch in Zahlen: Der sichtbare obere Rand der Manschette reicht im Bild von x 511 bis 631, die Achse des rechten Messers tritt bei 3,5 mm an x 626 ein, 5 Pixel neben der Ecke.

Zwei Änderungen, beide im erlaubten Rahmen (Tiefe 40 mm, Neigung, Kippen, Fächerwinkel ±17°, Klingen und Ärmel bleiben):

- **Drehpunkt des Fächers 70 statt 85 mm hinter dem Klingenansatz.** Die Griffe laufen im Ärmel enger zusammen; vor der Manschette stehen die Klingenansätze 20 statt 25 mm auseinander, die Spitzen fast wie zuvor.
- **Kürzere Bandenden** (`runde-4/messer3b.py` schreibt `runde-4/messer-e-kurzband.glb`): Das lange Ende reicht bis 97 statt 110 mm hinter den Klingenansatz, das kurze bis 92 statt 98 mm. Blatt, Angel, Wicklung, Ring, Knoten und Textur sind unverändert (196 Dreiecke, 13 Farben, geschlossen). In der Hand sieht man die Bänder nicht, sie stecken im Ärmel; im Flug bleibt das rote Band, nur kürzer.

Abtastung mit kurzen Bändern in 0,5-mm-Schritten (dieselbe Dreiecksprüfung wie in Runde 4):

| Drehpunkt | Bänder | frei von Durchstichen bei Versatz |
|---|---|---|
| 85 mm | lang (Runde 4) | 1,5 bis 5,5 mm |
| 85 mm | kurz | bis 8,5 mm (geprüft ab 6 mm) |
| 75 mm | kurz | 0 bis 12 mm |
| **70 mm** | **kurz** | **0 bis 13,5 mm** |
| 65 mm | kurz | bis 12 mm (geprüft ab 6 mm); die Griffe laufen am Rand der Manschette schon fast ineinander |

**Neue Variante: 8 mm nach links, Drehpunkt 70 mm, kurze Bänder.** Die Achsen der drei Messer treten bei x 598, 572 und 547 durch die Öffnung: 33 Pixel bis zur rechten Ecke, 36 bis zur linken. Null Durchstiche, mit mindestens 5,5 mm Luft nach beiden Seiten (Runde 4: 2 mm). Fächer im Bild x 394 bis 609, y 153 bis 345; 69 mm Abstand zur Silver Blade, keine Überdeckung. Blatt: `runde-4/versatz-b.png` (oben 3,5 mm aus Runde 4, unten die neue Variante), erzeugt von `runde-4/versatz_b.py`; Einzelbilder `runde-4/ansichten/vb-*.png`.

**Empfehlung: die neue Variante (8 mm, Drehpunkt 70 mm, kurze Bänder) ins Spiel.** Sie löst, was man im Bild sieht, und ist gegen kleine Abweichungen in Teil 2 unempfindlicher. Preis: Die drei Wicklungen stehen am Rand der Manschette dichter; die Klingen bleiben klar getrennt. Nachgemessen (`runde-4/wicklung_abstand.py`, je Messer eine Maske in der Ich-Sicht): In Runde 4 lagen zwischen den Griffen direkt über dem Manschettenrand 2 bis 6 Bildpunkte Luft. In 4b überdecken sich benachbarte Griffe dort um 10 bis 13 Bildpunkte, und zwar über 42 Bildzeilen (y 345 bis 386), also fast die ganze sichtbare Wicklung: Die drei Wicklungen lesen sich als ein Bündel, nicht als drei Griffe. Die Messer liegen in einer Ebene und stecken dort ineinander. Der Auftraggeber hatte 4b zunächst angenommen („4b nehmen“, 2026-11-28) und nach dieser Messung Nacharbeit bestellt: siehe Runde 5, dort sind die Messer gestaffelt.

### Endwerte der Haltung, Variante 4b (ersetzen die Tabelle oben, wenn der Auftraggeber 4b wählt)

| Größe | Wert |
|---|---|
| Modell | `runde-4/messer-e-kurzband.glb`, dreimal (auch als Wurfgeschoss, damit Hand und Flug dasselbe Messer zeigen) |
| Haltepunkt des Ärmels relativ zum Auge | (−0,100; −0,095; 0,205) m, unverändert |
| Tiefe | 40 mm (72 mm), unverändert |
| Versatz der drei Messer gegen den Ärmel nach links (+x) | 8 mm (14,4 mm) |
| Neigung, Kippen | 44°, 6°, unverändert |
| Fächer | −17°, 0°, +17°, Drehpunkt 70 mm (126 mm) hinter dem Klingenansatz |

Matrix wie oben, mit 126 mm statt 153 mm an beiden Stellen und dem neuen Versatz.

## Flug und Ziel mit kurzem Band (`runde-4/flug-ziel-kurzband.png`)

`runde-4/flug_ziel.py` rendert Flug und Ziel wie in Runde 2 (gleiche Kamera, gleicher Gang, gleicher Ghul, gleiche Lagen), links mit langem, rechts mit kurzem Band. Einzelbilder: `runde-4/ansichten/lang-*.png` und `kurz-*.png`. Rote Bildpunkte im Spielbild 854 × 480 (r ≥ 70 und r > 1,8 × g und b; sonst ist im Gang nichts rot):

| Bild | langes Band | kurzes Band |
|---|---|---|
| Flug aus Sicht des Werfers, drei Messer in 1,5, 3 und 5 m | 5 | 5 |
| quer in 2, 4 und 8 m | 9 | 5 |
| im Ghul aus 3,5 m | 1 | 1 |

Befund: Von hinten im Flug verliert das kurze Band nichts; man sieht dort den Knoten und den Ansatz der Enden, nicht ihre Länge. Im Ziel war das Band aus Wurfweite schon mit langen Enden nur ein Bildpunkt; dort trägt der Ring die Lesbarkeit, nicht das Band. Nur quer zur Blickrichtung fehlen 4 von 9 roten Bildpunkten, und so sieht der Werfer sein eigenes Messer nicht. **Empfehlung: ein Modell für Hand und Geschoss, `messer-e-kurzband.glb` (196 Dreiecke je Messer, drei in der Hand 588).** Zwei Modelle (Hand kurz, Geschoss lang, je 196 Dreiecke) brächten 4 Bildpunkte in einer Ansicht, die im Spiel kaum vorkommt, und dafür ein Messer, das beim Loslassen seine Form wechselt.

## Runde 5: gestaffelt, tiefer im Ärmel, Beute am Boden (`runde-5/`)

Nach der Korrektur zur Überdeckung hat der Auftraggeber 4b nicht angenommen, sondern Nacharbeit bestellt: „Messer Staffeln, ist wichtig für das Drop modell. außerdem könnten sie noch 10cm weiter in den Ärmel ragen. Muss dann natürlich schauen, dass kein sichtbares clipping vorhanden ist.“

Blatt: `runde-5/runde-5.png`, erzeugt von `runde-5/runde5.py` (Bibliothek `runde-5/staffel.py`). Einzelbilder: `runde-5/ansichten/`. Zahlen: `runde-5/staffel-zahlen.txt`, `tiefe-zahlen.txt`, `beute-zahlen.txt`. Alle Maße im Skriptmaß (die Datei und die Ich-Sicht zeigen 1,8-fach).

**Staffeln.** Die Messer lagen in 4b in einer Ebene und steckten ineinander (1392 Dreieckspaare Messer gegen Messer). Jetzt ist jedes Messer entlang seiner Dicke versetzt, wie Karten in der Hand: rechts 0, Mitte 6, links 12 mm, und der Drehpunkt des Fächers liegt 90 statt 70 mm hinter dem Klingenansatz. Die Richtung ist nicht frei: Nur nach dieser Seite hat der Deckel der Manschette Platz; mit −5 / 0 / +5 mm um die Mitte sticht das rechte Messer durch den Kragen (13 Paare, 7 Punkte sichtbar). Staffel 10 mm ist bei keinem Versatz frei, Staffel 4 mm lässt die Messer noch ineinander stecken (270 Paare). Freies Fenster des Versatzes nach links bei Drehpunkt 90, Staffel 6: 4 bis 14 mm; gewählt 8 mm.

| Messung in der Ich-Sicht 854 × 480 | 4b (vorher) | gestaffelt (s40) |
|---|---|---|
| Bildzeilen, in denen sich benachbarte Messer überdecken | 46 (y 345 bis 390), bis 14 Bildpunkte breit | 0 |
| Wicklung rechts / Mitte / links: überdeckte Bildpunkte | 266 / 586 / 320 (in 42 / 46 / 43 Zeilen) | 0 / 0 / 0 |
| Wicklung frei sichtbar | 2911 / 2968 / 3207 | 2948 / 3110 / 2952 |
| Messer durchdringen sich (Dreieckspaare) | 1392 | 0 |
| Durchstiche gegen den Ärmel (Regel 30) | 0 | 0 |

Das Ziel ist erreicht, es bleibt keine Überdeckung der Wicklungen. Eine Längsstaffel (Mitte 12 mm weiter vorn, oder 0/10/20 mm) ist ebenfalls frei und ohne Überdeckung; ich habe sie nicht genommen, weil sie nichts mehr löst.

**Tiefer in den Ärmel.** „Tiefe“ heißt in dieser Probe: Abstand vom Klingenansatz zum Haltepunkt, kleiner ist tiefer, negativ heißt Klingenansatz im Ärmel. „10 cm tiefer“ ist darum Tiefe −60 mm (wörtlich) oder −16 mm (10 cm im Bildmaß der Ich-Sicht, die 1,8-fach zeigt; 56 mm im Skriptmaß). Beide stehen auf dem Blatt, dazu eine Zwischenstufe. Der Drehpunkt bleibt jeweils 50 mm hinter dem Haltepunkt.

Befund vorweg: Das ganze Messer passt tiefer nicht in den Ärmel. Es läuft 44° schräg zur Ärmelachse und tritt hinten durch die Wand: mindestens 66 Dreieckspaare bei Tiefe 12, 134 bei −16, 126 bei −60 (`tiefe-zahlen.txt`), bei jedem Versatz. Die tiefen Varianten brauchen deshalb ein **Handmodell, das 35 mm hinter dem Haltepunkt abgeschnitten ist**; der Schnitt liegt hinter dem Deckel der Manschette und ist aus keiner Richtung zu sehen. Geschoss und Beute bleiben das ganze Messer.

| Variante | Tiefe | sichtbar entlang der Achse | Bildpunkte je Messer | Überdeckung im Bild | Durchstiche Ärmel / sichtbare Punkte hinter dem Deckel | freier Versatz | Handmodell |
|---|---|---|---|---|---|---|---|
| s40 | 40 mm | Klinge 117 von 117 mm, Griff 40 mm (Hals 12, Wicklung 28) | 8992 / 8691 / 8880 | keine | 0 / 0 | 4 bis 14 mm | ganz, 196 Dreiecke |
| s12 (28 mm tiefer) | 12 mm | Klinge 117 mm, Hals 12 mm; von der Wicklung nur der Rand (184 / 555 / 437 Bildpunkte) | 8917 / 8694 / 8924 | keine | 0 / 0 | 6 bis 12 mm | abgeschnitten, 84 |
| s-16 (56 mm tiefer) | −16 mm | Klinge 101 mm (86 %), kein Griff | 8342 / 8136 / 8562 | 31 Zeilen, bis 4 Bildpunkte (Klingen am Rand) | 0 / 0 | 2 bis 12 mm | abgeschnitten, 72 |
| s-60 (100 mm tiefer) | −60 mm | Klinge 57 mm (49 %), kein Griff | 4474 / 4441 / 4647 | 30 Zeilen, bis 11 Bildpunkte | 0 / 0 | 2 bis 6 mm | abgeschnitten, 30 |

In den abgeschnittenen Varianten stecken die Stümpfe im Ärmel noch ineinander (s12: 86 Paare, s-16: 73); das liegt hinter dem Deckel und ist nicht sichtbar.

**Empfehlung: s12.** Es ist die tiefste Stufe, in der jedes Messer noch als ganzes Messer zu erkennen ist (Klinge samt Ansatz und Stahlhals), und es hat mit 6 bis 12 mm das Fenster, das Abweichungen in Teil 2 verträgt. Bei −16 fehlt der Klingenansatz, die Klingen wachsen ohne Übergang aus dem Stoff; bei −60 bleiben drei kurze Zacken von 57 mm, die als Krallen lesen, nicht als Wurfmesser, und die Staffel-Arbeit ist dort unsichtbar. Wer die Wicklungen sehen will, nimmt s40; tiefer als s12 rate ich ab.

**Durchstich über die Bewegung.** Für Wurf und Nachladen der Messer gibt es noch keine Haltungen (weder in `src/` noch in `docs/`); geprüft ist darum nur die Ruhehaltung, je Variante mit Dreiecksprüfung plus Sichtprüfung vom Auge aus und zwei Prüfkameras (`ansichten/*-pruef-*.png`). Solange Messer und Ärmel sich als ein starrer Verbund bewegen, gilt das Ergebnis für jede Haltung. Zwei Dinge muss der Animationsdesigner wissen: Das abgeschnittene Handmodell darf nie aus der Manschette gezogen werden (beim Loslassen wird es durch das ganze Geschoss ersetzt, beim Nachladen erscheint es im Ärmel), und der sichtbare Schnitt wäre der einzige Fehler, den diese Prüfung nicht findet.

**Beute am Boden.** Heute liegt jede Waffe als Quader 0,55 × 0,09 × 0,12 m in der Stufenfarbe auf einer Leuchtplatte (`PickupView.DrawEquipment`, nur gelesen); ein eigenes Beute-Modell gibt es für keine Waffe. Das Blatt zeigt den Quader und das Bündel flach auf der Platte, Auge 1,6 m hoch, in 3 m und 8 m. Bildpunkte je Messer, in Klammern die von mehr als einem Messer belegten:

| Anordnung | 3 m | 8 m |
|---|---|---|
| 4b, 1,8-fach | 73 / 79 / 88 (25) | 5 / 6 / 6 (2) |
| gestaffelt, 1,8-fach | 75 / 79 / 89 (30) | 5 / 5 / 7 (3) |
| gestaffelt, 1:1 | 24 / 27 / 24 (11) | 1 / 2 / 3 (2) |
| gestaffelt, 2,5-fach | 161 / 171 / 181 (88) | 11 / 12 / 15 (10) |
| gestaffelt, 2,5-fach, Fächer ±32° | 161 / 178 / 195 (58) | 8 / 14 / 17 (6) |

In 3 m sind die drei Klingen ab 1,8-fach einzeln lesbar, die Griffe liegen als ein Knoten aufeinander. In 8 m ist in keiner Anordnung ein einzelnes Messer lesbar (höchstens 17 Bildpunkte); dort trägt die Leuchtplatte, und der heutige Quader ist aus der Ferne sogar besser zu sehen. Die Staffel der Hand (6 mm in der Dicke) ändert am Beute-Bild nichts, weil man die Beute von oben sieht und die Staffel dann senkrecht steht. **Hand- und Beute-Anordnung müssen sich also unterscheiden:** Die Beute braucht das ganze Messer (nie das abgeschnittene), einen weiteren Fächer (±32° trennt die Klingen bis zum Griff) und einen größeren Maßstab als die Hand. Ein fertiges Beute-Modell ist das noch nicht; für 8 m fehlt etwas, das größer liest als drei flache Klingen (aufgestellt, im Bündel mit Band, oder schräg auf einem Sockel). Das wäre ein eigenes Probestück.

## Übergabe an Teil 2

**Abgenommen vom Auftraggeber am 2026-11-28: Entwurf e „Schwarzrücken“ in der Haltung s12** (gestaffelt, Tiefe 12 mm). Die Beute am Boden ist ein eigenes Probestück (Issue #819).

**Bedingung:** Das Handmodell wird nie aus der Manschette gezogen. Beim Loslassen ersetzt das Geschoss (ganzes Messer) es, beim Nachladen erscheint es im Ärmel. Sein Schnitt liegt 35 mm hinter dem Haltepunkt und wäre sonst zu sehen.

| Datei | Inhalt | Erzeuger |
|---|---|---|
| `runde-5/messer-e-hand-s12.glb` | Handmodell: das Messer 47 mm (84,6 mm) hinter dem Klingenansatz abgeschnitten, Schnitt verschlossen, 100 Dreiecke, drei in der Hand 300 | `runde-5/handmodell.py` |
| `runde-5/messer-e-geschoss.glb` | das ganze Messer für Geschoss und Beute, 196 Dreiecke; Byte für Byte gleich `runde-4/messer-e-kurzband.glb` (das Skript prüft es) | `runde-5/handmodell.py` |

`handmodell.py` schreibt das Handmodell zweimal und bricht ab, wenn die beiden Läufe sich in einem Byte unterscheiden. `runde-5/pruefe_datei.py` lädt die geschriebene Datei dreimal und misst die Haltung s12 daran (`runde-5/datei-zahlen.txt`): 0 / 0 / 0 Dreieckspaare gegen den Ärmel, 0 sichtbare Punkte hinter dem Deckel, 0 Bildzeilen mit Überdeckung, Bildpunkte je Messer 8917 / 8694 / 8924, freier Versatz 5 bis 14 mm in 1-mm-Schritten (gewählt 8). Die drei Stümpfe stecken im Ärmel in 54 + 54 Dreieckspaaren ineinander; im Bild überdeckt sich nichts.

| Was | Wo |
|---|---|
| Modell (Hand und Geschoss) | `runde-4/messer-e-kurzband.glb`: ein Knoten `knife`, 196 Dreiecke, Textur 128 × 128 eingebettet (13 Farben der Hauptpalette), Maßstab 1,8 eingebacken, Ursprung am Klingenansatz, in Blender Spitze −Y, Klingenbreite Z, Dicke X |
| Erzeuger | `runde-4/messer3b.py` (ruft `runde-2/messer3.py` und kürzt nur die Bandenden; braucht `messer_gemeinsam.py` und `psx.py` aus diesem Ordner). Für `tools/` am besten beide zu einem Skript zusammenziehen |
| Textur einzeln | `runde-4/textur3b.png` (gleicher Inhalt wie `runde-2/textur3.png`) |
| Ärmel | `assets/models/pistol_hand.glb`, unverändert am Haltepunkt (−0,100; −0,095; 0,205) m relativ zum Auge (Spielachsen: x links im Bild, y oben, z vorn) |
| Haltung s12 (Empfehlung) | Tiefe 12 mm (21,6 mm), Versatz 8 mm (14,4 mm) nach +x, Neigung 44°, Kippen 6°, Fächer −17°, 0°, +17° um einen Drehpunkt 62 mm (111,6 mm) hinter dem Klingenansatz, Staffel entlang der Dicke 0 / 6 / 12 mm (0 / 10,8 / 21,6 mm) für rechts / Mitte / links. Werte in Klammern sind mit 1,8 multipliziert und gelten für die Datei, wie sie ist |
| Haltung s40 | wie s12, aber Tiefe 40 mm (72 mm) und Drehpunkt 90 mm (162 mm); ganzes Messer |
| Matrixfolge je Messer (Blender-Achsen), s12 | `T(14,4 mm nach +x) · T(Auge + Haltepunkt) · R_y(6°) · Neigung(44°) · T(0, −21,6 mm, 0) · T(0, 111,6 mm, 0) · R_z(Fächerwinkel) · T(0, −111,6 mm, 0) · R_y(90°) · T(Staffel, 0, 0)`; Vorlage im Code: `blatt.py` (`held`) und `runde-5/staffel.py` (`put`) |
| Handmodell | `runde-5/messer-e-hand-s12.glb`, dreimal, nur im Ärmel (Bedingung oben) |
| Geschoss und Beute | immer das ganze Messer `runde-5/messer-e-geschoss.glb`, Geschoss im Spielmaß (Faktor 1/1,8), Spitze voraus, dreht um die Querachse; Vorlage `blatt.py` (`thrown`). Beute: Issue #819 |
| Prüfskripte | `runde-5/pruefe_datei.py` (Haltung s12 an der geschriebenen Datei: Dreiecksprüfung Messer gegen Ärmel und Messer gegen Messer, Sichtprüfung hinter dem Deckel, Überdeckung im Bild), `runde-5/runde5.py` (alle Varianten, Blatt), `runde-4/flug_ziel.py` (Flug und Ziel) |
| Sollbilder | `runde-5/ansichten/datei-s12-ich-sicht-854x480.png`, `datei-s12-pruef-*.png`, `runde-4/ansichten/kurz-flug-854x480.png`, `kurz-steckt-854x480.png` |

Offen: Haltungen für Wurf und Nachladen (es gibt noch keine, geprüft ist nur die Ruhehaltung). Ändert Teil 2 Sichtfeld, Haltepunkt oder Ärmel, gelten die Prüfzahlen nicht mehr; dann `pruefe_datei.py` mit den neuen Werten laufen lassen. Die Schnittfläche des Handmodells hat keine eigene Textur (sie ist nie zu sehen).
