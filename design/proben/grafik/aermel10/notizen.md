# Fassung k (Issue #265): zweiteiliger Ärmel

Blätter: `blatt_j_k.png` (j | k: Gang, Rückstoß, aufgeladene Klinge), `phasen_k/blatt_phasen_k.png` (Klinge über den Schlag: Ausholen 0 / 0,5 / 1, Schwung 0,25 / 0,5 / 0,75 / 1; Spielfotos mit der Regel lokal im Code), `../aermel9/phasen_j/blatt_phasen_j.png` (zum Vergleich: Fassung j). Modelle `modelle_c/` (enthält k), Skripte `pistol.py`, `blade.py`, `arm.py`, `simulation.py`, `phasen_k.py` (lokaler Spieltest, stellt `src` wieder her), `phasenblatt.py`, `blatt.py`.

## Was gebaut ist
- `arm_cuff`: die Manschette aus j (Rüsche mit zwei Lagen und gewelltem Rand) und das Ärmelstück bis zum Ring 3; der Mund hat eine **dunkle Kappe** (sehr dunkles Blau) statt der weißen Scheibe mit Strahlen; die hintere Öffnung ist mit dunkler Kappe geschlossen. 96 Dreiecke.
- `arm_forearm`: gerades, leicht konisches Rohr (3,0 → 5,2 cm), Kuppel 1,8 cm im Inneren der Manschette, dunkler Deckel am Ende, Länge 1,3 m. 96 Dreiecke. Die Achse ist in der Datei auf die Ruherichtung zum Schulterpunkt gelegt, sodass in Ruhe keine Drehung nötig ist.
- Schulterpunkte: Klinge (−0,40; −0,65; −0,20), Pistole (+0,40; −0,65; −0,20). Mein erster Vorschlag (∓0,45; −0,40; −0,25) ließ den Arm in Ruhe zu flach nach hinten laufen; mit den tieferen Punkten sieht Ruhe wie j aus.

## Prüfung
- **Rechnung** (`simulation.py`, `check`): In allen sieben Phasen und im Rückstoß liegt das Ende des Unterarms außerhalb des Bildes (z ≈ −0,5 m); sichtbar ist der Unterarm nur bei Schwung 1 unten am Rand.
- **Im Spiel** (lokal, nicht committet): `phasen_k.py` setzt die Regel in `WeaponViews` ein und fotografiert die Phasen. Der Arm kommt bei Schwung 0,5 bis 1 von links aus dem Bild an die Manschette; kein freies Ende, keine weiße Scheibe. Der Core-Test `WeaponPoseTests` läuft mit den neuen Dateien grün (die zwei Meshes werden gelesen).

## Was noch nicht stimmt
- In Ruhe ist der blaue Ärmel links etwas schmaler als in j (der Unterarm beginnt am Ring 3 und zeigt direkt zur Schulter); an der Pistole ist rechts weniger Blau zu sehen. Es bleibt die gleiche Form, aber nicht pixelgleich.
- Bei Schwung 1 steht links am Bildrand weiter ein kleiner grauer Fleck (die Rüschenkante von hinten); der Arm selbst ist unauffällig.
- Der Code für den Unterarm (Regel in der README) ist hier nur als lokale Foto-Hilfe getestet, nicht als Spielcode mit Tests; die Drehrichtung der Matrix ist dort robust gelöst (beide Vorzeichen probieren).
- Das Rohr bewegt sich nur, wenn der Code es dreht; ohne die Code-Änderung steht es in Ruhe richtig, beim Schlag starr (wie j).
