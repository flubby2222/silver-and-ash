# Throwing Knives (#204): Bewegungsprobe Wurf und Nachladen

Probestück außerhalb des Spiels, vorbereitend für #821. Zwei Entwürfe für den Wurf, ein Nachladen, die Haltungen mit 3, 2, 1 und 0 Messern. Nichts hier ändert Spielcode, Modelle oder `assets/balance.json`.

## Ausgangslage

- Ruhehaltung s12 (abgenommen): drei Handmodelle `messer-e-hand-s12.glb`, Tiefe 12 mm, Drehpunkt des Fächers 62 mm, Versatz 8 mm, Staffel 0/6/12 mm, Fächer 17 Grad.
- Zeiten aus `assets/balance.json`: Wurfabstand 60 Ticks, Nachladen 120 Ticks (60 Ticks je Sekunde).
- Das Messer entsteht im Tick des Feuerns am Auge und fliegt 0,5 m je Tick. Vor dem Loslassen gibt es deshalb keine Zeit zum Ausholen: Tick 0 ist das Loslassen, die Bewegung der Hand ist Nachschwung.
- Das Nachladen läuft über den Fortschritt 0 bis 1, weil Gegenstände die Dauer verkürzen können.
- Keine Hände: Die Manschette führt die Bewegung, die Messer stecken im Ärmel.

## Wie eine Haltung gebaut ist

Wie `WeaponPose` im Spiel: Manschette und Messer drehen gemeinsam um den Haltepunkt des Ärmels (pitch, yaw, roll in Grad) und werden dann verschoben (Meter, vor `ShrinkTowardsEye`). Der Unterarm dreht um seinen eigenen Drehpunkt nur so weit, dass er weiter zur Schulter zeigt (wie `WeaponPose.ForearmTurn`). Achsen: x zur Bildmitte, y oben, z vorn. Alle Kurven stehen in `bewegung.py` und kommen ohne Blender aus.

Reihenfolge der Würfe: links (zur Bildmitte), rechts, zuletzt das gerade Messer in der Mitte. So bleibt bis zum Schluss ein Messer, das nach vorn zeigt.

## Wurf A „Schnapp“ (Empfehlung)

18 Ticks, danach 42 Ticks Ruhe bis zum nächsten Wurf.

| Ticks | Phase | Verlauf |
|---|---|---|
| 0 bis 2 | Hand schnappt nach vorn, Spitzen kippen | sofort: 45, 85, 100 Prozent |
| 2 bis 4 | hält | steht |
| 4 bis 14 | Rückweg | weich an beiden Enden |
| 14 bis 18 | schwingt einmal leicht nach hinten aus (12 Prozent) | Sinusbogen |

Höhepunkt: Verschiebung (0,012; -0,012; 0,040) m, pitch -24 Grad, yaw 6 Grad, roll 0.

## Wurf B „Durchzug“

40 Ticks, danach 20 Ticks Ruhe.

| Ticks | Phase | Verlauf |
|---|---|---|
| 0 bis 6 | Arm zieht nach vorn, innen und unten | beginnt bei 30 Prozent, bremst ab |
| 6 bis 10 | läuft aus (bis 108 Prozent) | weich |
| 10 bis 34 | Rückweg auf einem Bogen: erst hoch, dann nach außen | weich an beiden Enden |
| 34 bis 40 | schwingt aus (6 Prozent) | Sinusbogen |

Höhepunkt: Verschiebung (0,085; -0,050; 0,060) m, pitch -38 Grad, yaw 8 Grad, roll 24 Grad.

## Nachladen (Fortschritt 0 bis 1, bei 120 Ticks)

| Fortschritt | Ticks | Phase | Verlauf |
|---|---|---|---|
| 0 bis 0,12 | 0 bis 14 | leere Manschette taucht nach rechts unten weg | beschleunigt |
| 0,12 bis 0,50 | 15 bis 60 | unter dem Bildrand; bei 0,30 erscheinen die drei Messer | steht |
| 0,50 bis 0,72 | 60 bis 86 | kommt hoch, Fächer 15 Grad; schwingt um 0,62 bis 0,82 um 12 mm über | bremst ab |
| 0,72 bis 0,88 | 86 bis 106 | Fächer öffnet auf 17 Grad, kurz leicht darüber | rasch, dann ruhig |
| 0,90 bis 1,00 | 108 bis 120 | kleiner Ruck (3 Grad): Messer sitzen | Sinusbogen, klingt ab |

Weg-Haltung: Verschiebung (-0,060; -0,240; -0,030) m, pitch -35 Grad, roll -15 Grad. Die Messer werden nicht einzeln gesteckt (das bräuchte Finger); sie kommen als enger Fächer hoch und öffnen sich.

## Prüfung (`pruefe.py`, Zahlen in `zahlen.txt`)

Geprüft wird jeder Tick, nicht nur die Schlüsselhaltungen: Wurf A 19 Ticks und Wurf B 41 Ticks je für 3, 2 und 1 Messer, Nachladen 121 Ticks (auch im strengsten Fall mit immer drei Messern).

- Dreieckspaare Messer gegen Manschette und Unterarm (Regel 30): 0 in allen Ticks.
- Sichtbare Punkte hinter dem Deckel der Manschette (der Schnitt des Handmodells): 0 in allen Ticks.
- Offenes Ende des Unterarms im Bild: 0 in allen Ticks.
- Überdeckung der Messer und der Wicklungen im Bild (Masken je Messer wie `staffel.overlap`): 0 Bildzeilen und 0 Bildpunkte in allen Schlüsselhaltungen.
- Nächster Punkt am Auge: 8,6 cm (A), 7,9 cm (B), 8,2 cm (Nachladen); Grenze 5 cm. Weitester Punkt 25,0 cm; Grenze 30 cm (`WeaponPose.Reach`).
- Nachladen, Ticks 15 bis 60: keine Ecke von Manschette oder Messern im Bild; der Tausch leer gegen voll ist nicht zu sehen.
- Fächer: bis 13 Grad überdecken sich die Messer im Bild, ab 14 Grad stehen sie frei. Der geschlossene Fächer beim Nachladen hat 15 Grad. Mit 14 Grad lagen bei Fortschritt 0,62 zwei Bildpunkte der Wicklungen übereinander; die Prüfung hat das gefunden.

## Vergleich und Empfehlung

| | A Schnapp | B Durchzug |
|---|---|---|
| Hand ruht wieder nach | 18 von 60 Ticks | 40 von 60 Ticks |
| kleinster Abstand zur Silver Blade | 81 mm | 19 mm |
| Haltepunkt wandert im Bild | 45 px | 160 px |
| größter Sprung je Tick | 22 px | 54 px |
| größter Knick Manschette/Unterarm | 21,6 Grad | 26,5 Grad |
| Ärmel und Messer im Zielfenster 120 x 120 px, höchstens | 3894 px | 5090 px |

Empfehlung: **A**. Messwert: Der Abstand zur Silver Blade bleibt bei 81 mm; bei B sinkt er auf 19 mm, die rechte Manschette drängt im Bild an die linke. A passt außerdem zum Kern, in dem das Messer ohne Ausholen sofort fliegt.

## Clips (`clip.py`)

Drei GIFs der Ich-Sicht: `wurf-a-schnapp.gif`, `wurf-b-durchzug.gif` (je 854 x 480) und `wurf-a-und-b.gif` (beide nebeneinander, je 427 x 240). Ablauf: 30 Ticks Ruhe, drei Würfe im Abstand von 60 Ticks (3, 2, 1, 0 Messer), 120 Ticks Nachladen, 30 Ticks Ruhe; zusammen 360 Ticks = 6 Sekunden in Echtzeit.

- Gezeigt ist jeder zweite Tick bei 30 Bildern je Sekunde (180 Bilder). GIF kennt nur ganze Hundertstelsekunden je Bild; 60 Bilder je Sekunde lassen sich damit nicht halten.
- Das fliegende Messer liegt, wo es der Kern führt: am Auge gestartet, 0,5 m je Tick geradeaus, Spitze nach vorn, ohne Drehung. Von hinten gesehen ist es nur ein kleiner Punkt in der Bildmitte; gezeigt bis Tick 10 nach dem Loslassen.
- Das Nachladen beginnt im Clip 60 Ticks nach dem dritten Wurf; wann es im Spiel beginnt, legt der Kern fest.
- Die GIFs schreibt ffmpeg aus den gerenderten Bildern (Palette je Clip, ohne Rasterung). Sie liegen außerhalb des Repositorys.

## Unsicher

- Die Drehung des fliegenden Messers auf dem Blatt ist nur Anschauung; der Kern kennt keine Drehung des Geschosses.
- Die Probe zeigt Standbilder. Ob A mit 18 Ticks Gewicht hat oder zu knapp wirkt, entscheidet sich erst in Bewegung (Clip oder Spiel).
- Die Nachladeanzeige des HUD ist nicht mitgeprüft.
- Beim Nachladen nach nur einem oder zwei Würfen tauchen die verbliebenen Messer mit weg; geprüft ist das als strengster Fall mit drei Messern.

## Dateien

| Datei | Zweck |
|---|---|
| `bewegung.py` | Kurven und Haltungen, reine Rechnung |
| `szene.py` | Szene, Haltung auf das Modell legen, Messungen |
| `pruefe.py` | Prüfung jeder Phase, schreibt `zahlen.txt` |
| `blatt_bewegung.py` | Bildblatt `blatt-bewegung.png` mit 36 Bildern |
| `blatt.py`, `messer_gemeinsam.py`, `runde-4/versatz.py`, `runde-5/staffel.py`, `runde-5/*.glb` | unverändert aus der Grafikprobe der Wurfmesser kopiert (Runde 4 und 5) |

Aufruf, im Ordner der Probe:

```
blender --background --python pruefe.py
blender --background --python blatt_bewegung.py -- <Ordner für Blatt und Einzelbilder>
blender --background --python clip.py -- <Ordner für Clips> a
blender --background --python clip.py -- <Ordner für Clips> b
blender --background --python clip.py -- <Ordner für Clips> r
blender --background --python clip.py -- <Ordner für Clips> gif
```

Die drei Render-Schritte der Clips können gleichzeitig laufen; der letzte braucht ffmpeg im Suchpfad.

Die Prüfung braucht etwa zehn Minuten, das Blatt etwa fünf.
