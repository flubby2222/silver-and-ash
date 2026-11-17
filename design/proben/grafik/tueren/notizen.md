# Türen (Issue #245), Probestück

Blätter (Spielfotos, Debug-Build `--photo`, mit Körnung und Nebel; Waffen im Bild, weil das Spiel sie immer zeichnet):
- `blatt_alle.png`: alle vier Arten nebeneinander (Spalten Normal, Boss, Locked, Cracked); Zeilen: 4 m geschlossen, 10 m geschlossen, 4 m von der Rückseite.
- `blatt_normal.png`, `blatt_boss.png`, `blatt_locked.png`: je Art 3 × 2: oben geschlossen 4 m, halb offen, offen; unten geschlossen 10 m, Rückseite geschlossen, Rückseite halb offen.
- `blatt_cracked.png`: 4 m, 10 m, Rückseite (eine Wand, nur geschlossen).
- Einzelbilder in `fotos/`. Skripte: `door.py` (Modelle + Texturen), `patch_lokal.py` + `fotos.py` (lokale Foto-Hilfe, stellt `src` und `assets` wieder her), `montage.py` (Blätter).

## Maße und Aufbau
Durchgang = Zelle des Spiels: 2 m breit, 3,5 m hoch. Ursprung Mitte des Durchgangs am Boden, Fläche in x/y, Dicke in z. Beide Seiten der Tür sind gleich beschlagen (keine Rückseite ohne Textur). Der Renderer zeichnet die Türzelle als offenen Raum, die Tür steht also in der Zellenmitte (1 m Gang davor und dahinter); die Kollision bleibt unverändert und hält den Spieler an der Zellkante, etwa 1 m vor dem Holz.

| Art | Knoten (Drehpunkt) | Form und Beschlag | Dreiecke | Textur |
|---|---|---|---|---|
| Normal | `leaf_a` (+0,995; 0; 0), `leaf_b` (−0,995; 0; 0) | zwei Flügel, 14 cm dick, graues Holz, drei Bretter je Flügel, drei spitz zulaufende Bandscharniere, Zugring an Platte | 648 | 128 × 128, 30 Farben |
| Boss | `glow` (0; 0; 0), `leaf_a`, `leaf_b` (wie oben) | zwei Flügel, 26 cm dick, fast schwarzes Holz, Eisenrahmen mit zwei Querbalken, vier Schädel mit Kiefer, zwei Dornen je Seite, roter Streifen auf der Fuge | 756 | 128 × 128, 30 Farben |
| Locked | `leaf` (+0,995; 0; 0), `lock` (−0,55; 1,1; 0) | ein einziger glatter Eisenflügel (keine Mittelfuge), sechs genietete Platten, drei Querbalken, zwei Senkrechte; Messingschloss mit Schlüsselloch in Handhöhe, durch die Tür auf beiden Seiten | 360 | 128 × 128, 29 Farben |
| Cracked | `cracks` (0; 0; 0) | keine Tür: dünne dunkle Risse 1,2 cm vor beiden Wandflächen der Zelle (z = ±1) | 264 | 128 × 128, 19 Farben |

Unterscheidung an der Form: Normal = hell, Doppelflügel mit Scharnierbändern; Boss = schwarz, schwer, Schädel, rot; Locked = eine Eisenplatte ohne Holz, Schloss; Cracked = Wand. Drei Dateien je Art: `modelle/door_<art>.glb` und `.png`.

## Wie es im Spiel gedacht ist (für die Code-Sitzung)
- Normal/Boss: beide Flügel drehen um ihren Drehpunkt (Drehung um y), `leaf_a` und `leaf_b` gegengleich bis etwa 100°. Im Foto „halb offen“ sind es 50°, „offen“ 100° (lokal mit `SA_DOOR_OPEN`, Richtung beider Flügel aus der Blickrichtung; der Code muss die Richtung wählen, die vom Spieler wegzeigt).
- Locked: `leaf` dreht um seine Kante; `lock` wird nicht mehr gezeichnet, sobald aufgeschlossen (wie die Kiste).
- Boss: `glow` pulsiert über `tint` und entfällt beim Öffnen.
- Heute verschwindet eine geöffnete Tür sofort; die Drehpunkte erlauben später eine Öffnungsbewegung, nötig ist sie nicht.

## Was noch nicht stimmt / wo ich unsicher bin
- Normal aus 10 m: das dunkle Holz verschwindet fast im Nebel und liegt im Ton nahe an der Wand; ein hellerer Ton oder hellere Bänder würden es lösen. Locked und Boss lesen sich aus 10 m noch als dunkle Fläche, die Schädel und das Rot nicht mehr.
- Cracked: die Risse sind scharf schwarz und deutlicher als „leicht zu übersehen“; sie sind nur Linien, ohne Staub oder Abplatzungen.
- Die Türen haben keinen Rahmen (eigenes Issue); die Fuge zur Wand zeigt in der Mitte der Zelle einen schmalen dunklen Spalt zum Mauerwerk, ohne Rahmen sichtbar.
- Texel-Dichte etwa 40 px/m horizontal, 37 px/m vertikal, gröber als die Wand (64 px/m); wirkt im Bild passend.
- Schädel und Rahmen des Bosses sind aus Quadern gebaut, nicht gerundet. Die Schädel sehen aus 4 m gut aus, aus der Nähe flach.
- Nicht geprüft: Aussehen im Spiel mit echten Seed-Levels (nur Gangszene mit einer Tür); Treffer-Blitz, Tint; Tür an der Ost/West-Seite (Drehung 90° wie beim Platzhalter, in der Gangszene nicht fotografiert).
- Lokal im Spiel ist die Öffnung nur aus Umgebungsvariablen gesteuert, nicht als Spielcode; kein Test dafür.
