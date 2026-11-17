# Türen Runde 2 (Issue #245): Spitzbogen, neue Bosstür A/B, Öffnungsbewegung

Blätter (Spielfotos, Debug-Build `--photo`, Körnung und Nebel; Waffen im Bild, weil das Spiel sie immer zeichnet):
- `blatt_arten.png`: Spalten Normal, Verschlossen, Boss A, Boss B; Zeilen: 4 m, 10 m, 4 m von der Rückseite, 4 m an der Ost/West-Seite (Gang entlang x).
- `blatt_phasen.png`: Öffnungsphasen zu, 1/3, 2/3, offen (Zeilen Normal, Verschlossen, Boss A, Boss B).
- Einzelbilder in `fotos/` (36), darunter je Art Rückseite halb offen und Ost/West halb offen.
- Skripte: `door2.py` (Modelle und Texturen), `pruefung.py` (rechnerische Prüfung, Blender-Python), `fotos2.py` + `patch_lokal2.py` (lokale Foto-Hilfe, stellt `src`/`assets` wieder her), `montage.py`. Modelle in `modelle/` (`door_normal`, `door_locked`, `door_boss_a`, `door_boss_b`; Risswand unverändert aus Runde 1).

## Recherche (nur Nachschlagewerke, nichts übernommen)
Wikipedia „Archivolt“, „Portal (architecture)“, „Gothic architecture“, „Chartres Cathedral“; Met Museum „Gothic Doorway“; Wikipedia/Britannica „Portcullis“. Angewendet: ein gotisches Portal besteht aus gestuften, zurückspringenden Bogenläufen (Archivolten) auf Pfosten, darin die Tür und darüber ein festes Bogenfeld (Tympanon); das Fallgitter läuft in Nuten und wird in einen Schlitz über der Durchfahrt hochgezogen.

## Form
- **Der Spitzbogen** ist ein gleichseitig-ähnlicher Bogen aus zwei Kreisen (Funktion `arc` in `door2.py`). Die Zelle bleibt 2 × 3,5 m; der Bogen setzt bei 2,0 m an, Scheitel 3,45 m.
- **Entscheidung:** Die beweglichen Flügel sind unterhalb des Bogenansatzes **rechteckig**, der Bogen entsteht aus einem **festen Kopffeld** (Holz bzw. Eisenplatten) darüber, mit einem Eisenband auf der Fuge. Ein Flügel mit Spitzbogenkante kann nicht schwingen, ohne in den Bogen zu stechen (der Kopf bewegt sich auf die Scharnierseite, wo der Bogen schmaler ist); echte Portale lösen das genauso.
- **Bogenfeld:** Stein, gemalt wie die Wand (Quader, dunkle Fuge, Wandfarbe 122/104/112), voll in die Ecken über dem Bogen bis zur Decke, Tiefe 1,94 m (Vorderfläche 0,97 m von der Mitte). Es gehört zum Knoten `portal` und bleibt beim Öffnen stehen.
- Der Stein passt in der Farbe, nicht Quader für Quader, zur Wand des Spiels (die hat Varianten); bei der Integration prüfen.

| Art | Knoten (Drehpunkt in Dateikoordinaten) | Inhalt | Dreiecke | Textur |
|---|---|---|---|---|
| Normal | `portal` (0;0;0), `leaf_a` (+0,895;0;0), `leaf_b` (−0,895;0;0) | Look wie Runde 1 (graues Holz, Bandscharniere, Ringe) mit helleren Bändern; Spitzbogen aus Kopffeld und Bogenfeld | 928 | 128 × 128, 32 Farben |
| Verschlossen | `portal`, `leaf` (+0,865;0;0), `lock` (−0,4;1,0;0, hängt an `leaf`) | Look wie Runde 1 (Eisenplatten, Messingschloss), hellere Eisenbalken; Kopffeld aus Eisenplatten | 576 | 128 × 128, 32 Farben |
| Boss A (Stufenportal) | `portal` (0;0;0), `leaf_a` (+0,525;0;0), `leaf_b` (−0,525;0;0) | drei zurückspringende Bogenläufe aus hellem Werkstein, tiefschwarze Flügel mit Eisenbändern, festes Kopffeld mit leuchtender Scheibe (rotes Glas) | 1 008 | 128 × 128, 32 Farben |
| Boss B (Fallgitter) | `portal` (0;0;0), `grate` (0;0;0) | heller Sturz mit Eisengitter darunter, Spitzbogenfenster (rotes Glas) im Sturz | 488 | 128 × 128, 32 Farben |
| Risswand | unverändert | | 264 | |

Boss A und B sind zwei verschiedene Silhouetten: A = hoher, heller Stufenbogen mit dunkler Tür und rotem Auge; B = breiter heller Block mit Gitter und rotem Spitzfenster. Beide lesen sich aus 10 m an hellem Stein und einem roten Punkt (A) bzw. dem hellen Block (B).

## Öffnungsbewegung (für die Code-Sitzung)
Allgemein: Die Bewegung geht **vom Spieler weg**: Der Code wählt das Vorzeichen nach der Seite, von der der Spieler öffnet. Drehpunkte stehen in der Datei (Knoten-Verschiebung), `Rig.Pose` wie bei der Kiste. Die Foto-Phasen sind linear (1/3, 2/3); im Spiel mit Beschleunigung (ease-in-out).

| Art | Bewegung | Dauer (Vorschlag) |
|---|---|---|
| Normal | `leaf_a` dreht um y bis 90° (+ vom Spieler weg, wenn dieser bei kleinerem z steht), `leaf_b` gegengleich (−90°) | 0,9 s |
| Verschlossen | `leaf` dreht um y bis **80°** (das Schloss ragt 17 cm aus der Fläche und würde bei 90° die Wand schneiden); `lock` entfällt, sobald aufgeschlossen | 1,3 s (schwerer Eisenflügel) |
| Boss A | `leaf_a`/`leaf_b` wie Normal, bis 90°; langsam und schwer, zum Schluss kurzes Nachfedern (2–3°) möglich | 2,2 s |
| Boss B | `grate` fährt in y um **1,8 m** nach oben (Drehung gibt es nicht): Verschiebung der Teilmatrix; es verschwindet im Sturz; unten bleibt 1,8 m Durchgang (Spieler 1,8 m) | 2,6 s, Rasselgeräusch (Kette) |

Richtungsvorzeichen (System.Numerics, `CreateRotationY`): ein Spieler bei kleinerem z (Blick nach +z): `leaf_a`/`leaf` +θ, `leaf_b` −θ; bei größerem z umgekehrt. Ost/West-Türen drehen das Ganze wie der Platzhalter um 90°.
Während der Bewegung bleibt die Zelle für die Kollision geschlossen, bis die Bewegung etwa zur Hälfte läuft (Vorschlag); **das ist Entscheidung der Code-Sitzung**, die Kollision ändert sich in diesem Issue nicht.

## Prüfung der Bewegung
- **Rechnerisch** (`pruefung.py`, vier Phasen, beide Richtungen): Alle Ecken bleiben in der Zelle (|x| ≤ 1 m, y ≤ 3,5 m). Normal, Verschlossen und Boss A: **0 Durchstiche** durch das feste Portal (Scharnierpfosten ausgenommen, dort sitzt das Scharnier im Stein) und 0 zwischen den beweglichen Teilen. Boss B: Das Gitter taucht beim Hochfahren in die Unterseite des Sturzes ein (**beabsichtigt**, der Schlitz ist dort als dunkle Leiste gemalt); sein oberes Ende liegt genau auf der Decke (3,5 m), nichts steht über.
- **Wie ich darauf kam:** Eine erste Fassung mit Scharnier an der Flügelkante ließ den Beschlag bei 90° bis 14 cm in die Wand ragen; die Scharniere sitzen jetzt so weit innen, dass der Flügel samt Beschlag flach an der Wand liegt.
- **Im Spiel** (lokal, nicht committet): alle 4 Phasen je Art (Blatt), dazu Rückseite halb offen und Ost/West. Keine untexturierte Seite zu sehen; die Rückseiten sind wie die Vorderseiten beschlagen.

## Was noch nicht stimmt
- **Boss B ist der schwächere Entwurf:** Der Sturz ist ein großer glatter heller Block, das Spitzbogenfenster wirkt aufgemalt und leicht „Comic“ (flache Platte). Ein Zierband oder ein gestufter Rand würde helfen.
- Boss A: Die Scheibe ist klein; ob sie aus 10 m als roter Punkt trägt, zeigt das Blatt (sie ist dort noch erkennbar). Der Boss-Flügel ist schmal (1,05 m im Bogen), weil die Bogenläufe die Breite füllen.
- Aus 10 m sind Normal und Verschlossen trotz hellerer Bänder nur wenig besser als in Runde 1; die Spitzbogenform mit dem Kopffeld hilft; ein heller Rand um den Bogen würde mehr bringen (steht nicht im Auftrag).
- Der Stein des Bogenfelds hat eigene Fugen, die nicht zu den Wandquadern passen.
- Verschlossen öffnet sich nur bis 80° (Lock-Ausladung).
- Die Tür steht in der Zellenmitte; die Kollision hält den Spieler etwa 1 m davor.
- Nicht geprüft: Aussehen in einem echten Seed-Level, Treffer-Blitz/Tint für Boss-Glas, Ton (Quietschen, Kette) – brauchen Hörproben vor der Integration.
- Lokal im Spiel nur über Umgebungsvariablen gesteuert, kein Spielcode, kein Test.
