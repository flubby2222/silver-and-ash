# Probestück 11: Ärmel statt Hände (#256) und Hahn der Pistole (#257)

**Dateien** (`modelle/`, gleiche Namen wie die Übergabe, Austausch genügt): `pistol.glb`, `pistol_hand.glb`, `pistol.png`, `blade.glb`, `blade_hand.glb`, `blade.png`. Knoten und Drehpunkte unverändert (`stock`, `barrel`, `fittings`, `cock` mit Drehpunkt (−0,0126, 0,018, 0,0072), `flash`, `arm`; Klinge `sword`, `arm`). Neu sind nur die Form von `arm` und `cock` und die Texturfelder.
**Skripte:** `pistol.py`, `blade.py`, `arm.py` (neu: `build_sleeve`, `paint_sleeve3`, `paint_cuff3`), Fotos `fotos.sh neu` (legt die Dateien vorübergehend nach `assets/models/`, rechnet die Prüfsummen neu, fotografiert, stellt aus Git wieder her), Vergleich `vergleich.py`.

## Ursache der „seltsamen Linien“ (#257)
Der Feuerstein war ein winziger Quader mit dem Texturfeld **iron** (Lauf). Dieses Feld trägt Silberlinien oben und einen gestrichelten Silberring; auf 6 mm gestreckt wurden daraus helle Striche auf einem dunklen Rechteck (im Foto `alt/pistole_nah.png` zu sehen). Es lag also an der Textur, nicht an der Geometrie oder der Körnung. Der Hahn war außerdem eine flache, helle Platte (Feld **steel**) und las sich als Flügel, nicht als Hahn.
**Neu:** Hals als Rohr mit Volumen, zwei Backen, Oberbacke, Sporn nach hinten; Schloss, Pfanne und Batterie in dunkel gebräuntem Eisen (Feld **lock**, nur Rauschen); der Stein bekommt ein **eigenes** Feld **flint** (hell graubeige, ohne Linien). Aus der Sicht der Spielfigur: dunkler Haken mit hellem Stein davor.

## Ärmel (#256)
Keine Finger, kein Handschuh mehr. Stulpe aus dickem Nussleder (umgeschlagener Rand vorn mit Naht und drei Silbernieten) umschließt bei der Pistole den Griff von unter dem Schloss bis unter die Kolbenkappe, bei der Klinge Griff, Knauf und Heft bis an die Parierstange; daran schließt der Ärmel aus schwerem nachtblauem Mantelstoff (Köperrippen, Falten, Abnutzung), der aus dem Bild nach unten läuft. Der Ärmel ist kürzer als der alte Arm; Klinge: Ärmelende hinter dem Knauf statt 8 cm weiter hinten.
Texturen: 128 × 128, 32 Farben, Felder wie vorher (`glove` entfällt, dafür `lock` und `flint` bei der Pistole). Dreiecke: Pistole 144 + 96 + 214 + 114 + 42, Klinge Schwert 152; Ärmel je etwa 180 (Klinge 180).

## Was mir nicht gefällt / unsicher
- **Der Ärmel selbst ist im Bild kaum zu sehen**: Er läuft nach unten aus dem Bild; sichtbar sind fast nur die Stulpen. Bei der Pistole wirkt die Stulpe wie ein Becher, aus dem der Griff ragt; bei der Klinge wie ein Lederhandschuh ohne Finger.
- **Klinge „aufgeladen“**: Das Heft liegt dann etwa 1 cm vor dem Auge. Die Nahebene schneidet die Stulpe an; man sieht durch die Schnittstelle in die Stulpe, Griff mit Wicklung unter dem Rand. Das war beim alten Arm genauso; ein Eingriff in die Haltung (`WeaponPose.Blade`, Code) würde es lösen, nicht das Modell.
- Der Sporn am Hahn ist ein dunkler Dorn, der im Foto etwas groß wirkt.
- Ausgelassen (nicht gefordert): Nachladen und Schlag der Klinge als eigene Fotos; Szenen gibt es dafür nicht, der Ärmel ist starr an der Waffe wie vorher.

---

# Zweite Runde (nach dem Urteil der Leitung)

**Hahn:** um ein Drittel kleiner (Faktor 0,66 um den Drehpunkt), Sporn kurz, Schlosseisen in der Farbe des Laufs (58, 56, 64) statt fast schwarz; Backen lagen vorher versehentlich auf der falschen Seite des Halses und sitzen jetzt mittig. Der helle Stein bleibt (eigenes Feld).
**Ärmel:** ein weiter Mantelärmel aus Stoff, keine Stulpe mehr. Die Öffnung ist mit einem kleinen Trichter um den Griff geschlossen (Waffe ragt heraus), fällt leicht aus (Radius 3,1 cm gegenüber 2,4 cm am Trichter), dann läuft der Ärmel zum Ellbogen außerhalb des Bildes; Pistole von rechts unten, Klinge von links unten. Nachtblau (50, 58, 84) mit hellen und dunklen Faltenbahnen längs, Saum mit hellem Faden und Borte, Abnutzung am Saum. Ein Teil `arm` mit einem Mesh wie bisher; `cuff` kommt nicht mehr vor. Fassung 3 war falsch herum gebaut (Flächen nach innen, durch Culling nur zu erahnen); die Reihenfolge der Ringe ist jetzt umgekehrt.

**Was mir nicht gefällt:**
- **Pistole:** Der Hahn ist jetzt sehr unauffällig; im Foto sieht man den Stein nur als kleinen hellen Fleck, weil der Lauf ihn aus dieser Sicht teilweise verdeckt. Wenn er stärker lesen soll, ist ein größerer Faktor (0,8) der nächste Schritt. Der Ärmel ist von hinten oben eine blaue Fläche mit Strahlenmuster (Kappe der Öffnung, einzelne Dreiecke je Ecke schattiert); sichtbar sind etwa 10 cm statt 15 bis 20, weil die Pistole im Bild weit unten sitzt und der Rest unter dem Bildrand liegt.
- **Klinge im Gang:** Der Ärmel ist ein großer blauer Klumpen links unten und bedeckt mehr Bild als der alte Arm; zu groß, Radius ruhig kleiner.
- **Klinge „aufgeladen“:** Der Fehler bleibt. Das Heft liegt dort rund 1 cm vor dem Auge; was von der Nahebene geschnitten wird, kann das Modell nicht schließen (Culling zeigt die Rückseite nicht). Der Griff mit der Wicklung schaut unter dem Ärmel hervor. **Das lässt sich nur über die Haltung im Code lösen** (`WeaponPose.Blade`: aufgeladen etwa 3 cm weiter nach vorn oder die Nahebene näher), nicht im Modell.
- Die Falten im Foto sind nur als zwei, drei Bahnen zu erkennen; 128 × 128 Pixel für Hand und Ärmel zusammen sind knapp.
