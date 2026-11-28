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
