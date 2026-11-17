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
