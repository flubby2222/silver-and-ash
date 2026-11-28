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

**Empfehlung: die neue Variante (8 mm, Drehpunkt 70 mm, kurze Bänder) ins Spiel.** Sie löst, was man im Bild sieht, und ist gegen kleine Abweichungen in Teil 2 unempfindlicher. Preis: Die drei Wicklungen stehen am Rand der Manschette dichter; die Klingen bleiben klar getrennt. Nachgemessen (`runde-4/wicklung_abstand.py`, je Messer eine Maske in der Ich-Sicht): In Runde 4 lagen zwischen den Griffen direkt über dem Manschettenrand 2 bis 6 Bildpunkte Luft. In 4b überdecken sich benachbarte Griffe dort um 10 bis 13 Bildpunkte, und zwar über 42 Bildzeilen (y 345 bis 386), also fast die ganze sichtbare Wicklung: Die drei Wicklungen lesen sich als ein Bündel, nicht als drei Griffe. Die Messer liegen in einer Ebene und stecken dort ineinander. **Der Auftraggeber hat 4b so abgenommen („4b nehmen“, 2026-11-28).** Wer das später lösen will, staffelt die Messer um ihre Dicke (je etwa 5 mm längs der Dickenachse, wie Karten in der Hand) und lässt `versatz_b.py` neu laufen; das ist nicht Teil dieser Probe.

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

## Übergabe an Teil 2

Abgenommen vom Auftraggeber am 2026-11-28: Entwurf e „Schwarzrücken“, Tiefe 40 mm, Variante 4b.

| Was | Wo |
|---|---|
| Modell (Hand und Geschoss) | `runde-4/messer-e-kurzband.glb`: ein Knoten `knife`, 196 Dreiecke, Textur 128 × 128 eingebettet (13 Farben der Hauptpalette), Maßstab 1,8 eingebacken, Ursprung am Klingenansatz, in Blender Spitze −Y, Klingenbreite Z, Dicke X |
| Erzeuger | `runde-4/messer3b.py` (ruft `runde-2/messer3.py` und kürzt nur die Bandenden; braucht `messer_gemeinsam.py` und `psx.py` aus diesem Ordner). Für `tools/` am besten beide zu einem Skript zusammenziehen |
| Textur einzeln | `runde-4/textur3b.png` (gleicher Inhalt wie `runde-2/textur3.png`) |
| Ärmel | `assets/models/pistol_hand.glb`, unverändert am Haltepunkt (−0,100; −0,095; 0,205) m relativ zum Auge (Spielachsen: x links im Bild, y oben, z vorn) |
| Haltung | Tabelle „Endwerte der Haltung, Variante 4b“ oben: Tiefe 40 mm (72 mm), Versatz 8 mm (14,4 mm) nach +x, Neigung 44°, Kippen 6°, Fächer −17°, 0°, +17° um einen Drehpunkt 70 mm (126 mm) hinter dem Klingenansatz. Werte in Klammern sind mit 1,8 multipliziert und gelten für die Datei, wie sie ist |
| Matrixfolge je Messer (Blender-Achsen) | `T(14,4 mm nach +x) · T(Auge + Haltepunkt) · R_y(6°) · Neigung(44°) · T(0, −72 mm, 0) · T(0, 126 mm, 0) · R_z(Fächerwinkel) · T(0, −126 mm, 0) · R_y(90°)`; Vorlage im Code: `blatt.py` (`held`) und `runde-4/versatz.py` (`put`) |
| Geschoss | dasselbe Modell im Spielmaß (Faktor 1/1,8), Spitze voraus, dreht um die Querachse; Vorlage `blatt.py` (`thrown`) |
| Prüfskripte | `runde-4/versatz_b.py` (Dreiecksprüfung Messer gegen Ärmel, Deckel der Manschette ausgenommen, plus Blatt; frei von 0 bis 13,5 mm Versatz), `runde-4/wicklung_abstand.py` (Abstand der Griffe im Bild), `runde-4/flug_ziel.py` (Flug und Ziel) |
| Sollbilder | `runde-4/ansichten/vb-ich-sicht-854x480.png`, `kurz-flug-854x480.png`, `kurz-steckt-854x480.png` |

Offen und bekannt: Die drei Wicklungen überdecken sich am Manschettenrand (siehe Runde 4b). Ändert Teil 2 Sichtfeld, Haltepunkt oder Ärmel, gelten die Prüfzahlen nicht mehr; dann `versatz_b.py` mit den neuen Werten laufen lassen.
