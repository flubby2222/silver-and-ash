# Fassung j (Issue #265): Artefakt im Schlag, Ärmellänge, Knöpfe

Blätter: `blatt_i_j.png` (i | j; Gang, Rückstoß, aufgeladene Klinge), `blatt_phasen_j.png` (Klinge über den Schlag: Ausholen 0 / 0,5 / 1, Schwung 0,25 / 0,5 / 0,75 / 1), `../aermel8/blatt_phasen_i.png` (dieselben Phasen für Fassung i, das „Vorher“). Modelle `modelle_c/` (enthält j), Skripte `pistol.py`, `blade.py`, `arm.py`, `fotos.sh c`, `phasen.sh`, `phasenblatt.py`, `blatt.py`, `simulation.py`.

## Wie ich die Phasen fotografiert habe
Lokale Hilfsänderung **ohne Commit**: `phasen.sh` setzt in `WeaponViews.DrawBlade` für die Dauer des Laufs zwei Umgebungsvariablen (`SA_WIND`, `SA_SWING`), die die Werte `_windUp` und `t` der Szene `room-corridor` überschreiben, und stellt danach `src` aus Git wieder her. Parallel rechnet `simulation.py` die Matrix aus `WeaponPose.Blade` nach (Zeilenvektoren wie `System.Numerics`) und listet, welche Ringe der Ärmel-Mittellinie in welcher Phase im Bild liegen.

## Ursache des Artefakts
Es ist das **verlängerte Rohrende** aus Fassung i (der achte Ring, am Ende ein Deckel mit 6 cm Radius). Die Rechnung zeigt: In Ruhe, beim Ausholen und bis Schwung 0,5 liegen nur die Ringe 0 bis 2 im Bild; bei **Schwung 0,75 und 1 liegen alle acht Ringe im Bild**, auch das Ende (bei Schwung 1 liegt Ring 7 etwa 1,75 m vor der Kamera, 0,4 m über Augenhöhe). Der Grund ist die Drehung um die Hochachse (95° beim Schlag): Das Rohr zeigt in Ruhe nach links hinten; die Drehung dreht den waagerechten Anteil „links“ nach „vorn“. Es ist das Bild oben links bei Schwung 0,75 und 1 in `blatt_phasen_i.png`: ein blauer Hut mit Rohrende, der nach vorn ins Bild ragt. Das passt zur Vermutung der Leitung.

## Lösung (ohne Code-Änderung): das Rohr hängt nach unten
Die Drehung um die Hochachse verschwenkt nur waagerechte Anteile. Ein Rohr, das **ab dem fünften Ring nach unten hängt**, bleibt in jeder Phase unten und läuft aus dem Bild. Die Richtung ist so gewählt, dass sie bei 45° Rollen (Schlag) senkrecht nach unten zeigt (Dateirichtung (−0,71; −0,71; 0)). Die ersten vier Ringe (Manschette und Rohrstück im Bild) sind unverändert; Ruhe und Position bleiben wie in i. Die Fotos der Phasen zeigen: Schwung 0,5 bis 1: Der Ärmel kommt von links an die Manschette, kein Ende ist zu sehen.
- Länge: das Rohr ist etwa 2,1 m lang (Ende bei (−0,87; −0,985; −0,12) in Dateikoordinaten mal 1,8).
- Nahebene: die Ringe 4 bis 7 liegen in allen Phasen hinter oder unter der Kamera; das Rohr ist geschlossen (Deckel am Ende), die Innenseite wird nicht gesehen. Ausnahme siehe unten.

## Knöpfe und Rüsche
- Knöpfe: **entfallen** (auch vergrößert nicht zu erkennen: die zwei Stellen am Ärmel liegen im Gang-Foto hinter der Manschette oder außerhalb des Bildes). Die Naht bleibt.
- Rüsche der Pistole um 14 % dunkler (eigener Atlas der Pistole), im Gang-Foto beige statt weiß.

## Was noch nicht stimmt
- Das Rohr ist **starr mit der Waffe**; beim vollen Schlag zeigt es aus der Manschette nach links und dann nach unten. Ein echter Arm käme von der Schulter, die etwa unter und links hinter der Kamera liegt. Plausibel genug im Bild, aber nicht korrekt.
- Bei Schwung 0,75 und 1 steht über der Manschette ein dunkelblauer „Hut“ (das Rohrstück hinter der Manschette, das nach links oben zeigt, weil die Rolle um 45° es anhebt) und am linken Rand eine blasse Fläche (Rüsche von hinten gesehen, vermutlich an der Nahebene). Sie sind klein und sitzen am Bildrand.
- Bei Schwung 1 sieht man die Manschette von vorn als weiße Scheibe mit Strahlenmuster (die Kappe des Mundes, in der die Klinge steckt).
- Die Pistole schwenkt nicht; ihr Rohr ist wie in i.

## Vorschlag für die Code-Sitzung (nur falls das nicht reicht)
Zwei Knoten: `arm_cuff` (Manschette und die ersten vier Ringe, hängt starr an der Waffe) und `arm_forearm` (der Rest des Rohrs, **Drehpunkt am Mund der Manschette**, also (0, 0, −0,016) in Dateikoordinaten für die Klinge). Regel: `arm_forearm` zeigt in jeder Phase auf einen festen Schulterpunkt im Kameraraum, links hinten unten, etwa (−0,45; −0,40; −0,25) für die Klinge und (0,45; −0,40; −0,25) für die Pistole; der Knoten wird so gedreht, dass seine Längsachse (Dateirichtung (0,3; −0,2; −0,3) in Ruhe) auf diesen Punkt zeigt. Dann hängt der Arm immer von der Schulter zur Hand.
