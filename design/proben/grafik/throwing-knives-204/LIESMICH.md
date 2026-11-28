# Probestück Throwing Knives (Issue #204): Entwurf a und b

Zwei Entwürfe für das Wurfmesser als Fernwaffe, Stufe **Vampiric**, außerhalb des Spiels. Alles per Blender-Python erzeugt, nichts heruntergeladen. Unter `assets/` und `src/` ist nichts geändert; Ärmel, Klinge und Ghul werden aus `assets/models/` nur gelesen.

Das Blatt: `blatt.png`. Einzelbilder: `ansichten/`.

## Die Entwürfe

| | a „Aderlass“ | b „Kreuzblatt“ |
|---|---|---|
| Idee | schlankes Wurfstilett des 18./19. Jahrhunderts, in der Hand ein **Fächer aus drei** | das ältere Stück: schwerer Dolch mit Parierstange, in der Hand **ein einzelnes** |
| Umriss | Nadel: 28 cm lang, 2,7 cm breit | Kreuz: 26 cm lang, Parierstange 10 cm breit |
| Klinge | schmale Raute, geschwärzt, geschliffene Silberschneide | breites Blatt, geschwärzt, Silberschneide, blutrotes Email in der Hohlkehle |
| Griff | blutrote Schnurwicklung, heller Bund, gedrechselter Knauf | dunkles Leder, blutroter Stein in der Parierstange, Scheibenknauf mit blutrotem Email |
| Wo sitzt das Rot | am Griff (in der Hand vorn, im Ziel außen) | auf der Klinge (in der Hand groß, im Ziel teils verdeckt) und am Knauf |
| Dreiecke | 168 je Messer, 504 in der Hand | 172 |
| Datei | `messer-a-aderlass.glb` | `messer-b-kreuzblatt.glb` |

Beide teilen sich eine Textur (`textur.png`, 128 × 128, 15 Farben, nur Hauptpalette, harte Stufen ohne Dithering). Die Stufe steht in Material und Farbe: geschwärztes Eisen (Eisen 1–3), Silberschneide (Eisen 5–7), Blutrot 2–5 mit den Ankern der Stufe Vampiric (Blutrot 3 = 150,20,30 und Blutrot 5 = 200,30,40). Die Anker der anderen Stufen (Holz 3, Eisen 4, Messing 5) kommen nicht vor.

## Bilder

Je Entwurf (`a-…`, `b-…`) in `ansichten/`:

| Bild | Inhalt |
|---|---|
| `seite`, `vorn`, `hinten`, `schraeg` | einzelnes Messer, vier Blickwinkel |
| `ich-sicht-854x480` | Spielauflösung, 70 Grad, Augenhöhe 1,6 m, Nebel und Körnung; rechts die Messer im Ärmel der Pistole, links die Klinge |
| `steckt-854x480` | Ghul 3,5 m vor dem Auge, Messer in der Brust, Sicht des Werfers |
| `steckt-nah` | dasselbe von der Seite aus etwa 1 m |
| `flug-854x480` | drei Augenblicke eines Wurfs (1,5 m, 3 m, 5 m), das Messer dreht sich um die Querachse |
| `klein-854x480` | dasselbe Messer quer zur Blickrichtung in 2 m, 4 m und 8 m |

Fairer Vergleich: `blatt.py` führt beide Entwürfe durch dieselbe Schleife. Ärmel, Klinge, Licht, Kamera, Gang, Ziel, Trefferpunkt und Wurfbahn sind gleich; es wechselt nur die Datei des Messers und die Zahl der Messer in der Hand (a drei, b eines).

## Maße und Haltung

- Ein Knoten `knife`, Ursprung am Klingenansatz, Spitze nach +z, Klinge breit in y. Maßstab 1,8 eingebacken wie bei allen Waffen in der Hand; als Geschoss zeichnet das Blatt dieselbe Datei mit 1/1,8.
- Haltung: Hand (-0,100 / -0,095 / 0,205) relativ zum Auge, Spitze 44 Grad nach vorn-oben, 6 Grad im Bild gekippt, breite Seite zur Kamera. Der Griff sitzt 5 cm (Skriptmaß) in der Manschette; keine Hand modelliert, der Ärmel bleibt unverändert.
- Fächer bei a: -17 / 0 / +17 Grad um einen Punkt nahe am Knauf.
- Im Ziel: Spitze 24 Grad nach unten, 20 Grad seitlich, 5,5 cm tief.

## Geschlossenheit und Reproduzierbarkeit

- Beide Modelle geschlossen: Prüfung im Skript je Teil (0 offene Kanten, 0 Kanten mit mehr als zwei Flächen) und `python tools/models/check_closed.py` (168 und 172 Dreiecke, 0 offene Kanten).
- Zwei Läufe hintereinander ergeben dieselben Dateien (ein gemeinsamer SHA-256 über alle glb und png stimmt überein).

Erzeugen (aus diesem Ordner, `TEMP` auf einen Arbeitsordner setzen):

```
blender --background --python messer_a.py
blender --background --python messer_b.py
blender --background --python blatt.py
```

## Eigene Einschätzung

**Was trifft:**
- Beide sind dunkel und eisern, das Silber sitzt nur an der Schneide (Silber gehört zur Waffe des Jägers), das Rot ist die Stufe Vampiric und kommt sonst nirgends her.
- In der Ich-Sicht bleibt die Bildmitte frei; beide Waffen sind im Bild.
- Weit unter der Grenze von 1500 Dreiecken, eine Textur, 15 Farben.

**Wo sie sich im Spiel unterscheiden:**
- **In der Hand** liest sich a sofort als „mehrere Wurfmesser“ und ist damit nicht mit der Klinge links zu verwechseln. b ist ein einzelner Dolch und steht der Klinge links näher: zwei Dolche im Bild.
- **Im Ziel aus Sicht des Werfers** ist das Messer bei 3,5 m nur etwa 6 Pixel groß. Bei b bleibt das Kreuz aus Parierstange und Griff erkennbar, bei a ein rot-heller Strich. b liest sich hier besser.
- **Im Flug von hinten** sind beide ein dünner Strich; ab etwa 5 m verschwindet a fast, b hält sich durch die Parierstange etwas länger. Quer gesehen (Messer eines Gegners, oder im Vorbeigehen) lesen sich beide bis 4 m, bei 8 m bleibt nur ein Fleck.

**Schwächen und Unsicherheiten:**
- Ein fliegendes Messer ist von hinten bei beiden Entwürfen kaum zu sehen. Das löst kein Modell, sondern ein Effekt (Schweif oder Aufblitzen der Schneide); das wäre eine Frage an Animation und Effekte.
- a zeigt in der Hand viel Blutrot nahe an der Manschette. Ob das zu laut ist, sollte am Spielbild mit HUD geprüft werden.
- b ist ein Kreuzdolch in einem Vampirjäger-Spiel. Die Form ist allgemein (mittelalterlicher Dolch), kein erkennbares Stück aus den Vorbildern; wer den Abstand größer will, nimmt a.
- Das Nachladen (ein Messer nachziehen) ist bei a im Bild angelegt: der Fächer kann um je ein Messer schrumpfen. Bei b verschwindet die Waffe ganz aus der Hand, bis nachgezogen ist. Bewegung ist hier nicht geprüft.
- Die Stufen Worn, Forged und Blessed sind nicht gebaut. Bei a wechselt die Wicklung die Farbe, bei b Email und Stein; ob das allein reicht, zeigt erst eine Reihe aller Stufen.

**Empfehlung (vorläufig):** a für die Hand und das Gesamtbild (schlank, eisern, klar als Wurfwaffe zu lesen, kein zweiter Dolch neben der Klinge); von b würde ich für das steckende Messer den breiteren Umriss am Griffende übernehmen, falls a im Ziel zu unscheinbar bleibt.
