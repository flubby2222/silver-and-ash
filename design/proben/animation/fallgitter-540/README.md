# Fallgitter der Arena: Bewegungsentwurf zu Issue #540

Probestück außerhalb des Spiels. Kein Spielcode, nichts unter `assets/`, kein Ton. Das Gitter selbst (Form, Maße) ist das angenommene Modell aus `design/proben/grafik/fallgitter-540/` (`gitter_breit`, Eisen der vorhandenen Textur).

## Erzeugen

```
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/animation/fallgitter-540/fallgitter_bewegung.py
```

`TEMP` vorher auf einen Ordner außerhalb des Repos setzen; `FGB_OUT` wählt einen anderen Ausgabeordner, `FGB_TABLE_ONLY=1` erzeugt nur die Zeitverläufe (Python genügt). GIFs setzt `ffmpeg` aus den Spielbildern zusammen. Die Szene (Wand, Rahmen, Boden, Licht, Körnung) ist die des Grafik-Probestücks, geladen ohne dessen `main()`. Zweimal gelaufen: `zeitverlauf.csv` und `zeitverlauf.txt` sind byte-gleich (Hashes in der Abgabe).

## Ansicht der Bilder

Gitter in der Türöffnung, Wand der Ebene drumherum, Kamera 3 m vor der Türebene auf Augenhöhe 1,6 m, 70 Grad, 854 x 480, Spiel-Licht mit Körnung. Unter jedem Bild steht Tick, Zeit, Höhe der Unterkante und der Zustand der Sperre (Vorschlag, siehe unten).

- `fallen-a-echtzeit.gif`, `fallen-a-halbe-geschwindigkeit.gif`
- `fallen-b-echtzeit.gif`, `fallen-b-halbe-geschwindigkeit.gif`
- `heben-echtzeit.gif`, `heben-halbe-geschwindigkeit.gif`
- `streifen-fallen-a.png`, `streifen-fallen-b.png`, `streifen-heben.png`: je 6 Schlüsselbilder, Ticks in der Kopfzeile

Echtzeit heißt 50 Bilder je Sekunde (GIF kennt nur Hundertstel; ein Bild alle 1,2 Ticks), vorn 8 und hinten 14 Bilder Halt. Das Spiel selbst zeichnet jeden Tick.

## Begriffe

Höhe = Unterkante (Spitzen) des Gitters über dem Boden. Offen = 3,40 m: dort liegt die Unterkante über dem Scheitel des Bogens (3,32), das Gitter ist hinter dem Sturz. Alle Zeiten in Ticks (60 je Sekunde) ab dem Auslöser.

**Schwelle der Sperre: 1,85 m.** Der Spieler ist 1,80 m hoch; liegen die Spitzen unter 1,85 m, kommt er nicht mehr durch, darüber hat er 5 cm Luft. Die Öffnung ist 1,75 m, die Spieler-Höhe die engere Grenze, also gilt sie. Das Bild "liest zu" für den Spieler ab dem Tick, an dem die Spitzen unter 1,85 m liegen, und "liest frei", sobald sie wieder darüber liegen.

## Fallen, Fassung a: schwer und schnell, kurzes Nachfedern

| Phase | Ticks | Verlauf | Höhe |
|---|---|---|---|
| Fall | 0 bis 24 | beschleunigt (freier Fall im Bild, rund 4 g): h = 3,40 (1 - (t/24)^2) | 3,40 bis 0 |
| Aufschlag | 24 | Spitzen auf dem Boden, Geschwindigkeit dort 0,28 m je Tick | 0 |
| Sprung 1 | 24 bis 35 | Parabel | bis 0,060 |
| Sprung 2 | 35 bis 40 | Parabel | bis 0,009 |
| Ruhe | ab 40 | | 0 |

Dauer 40 Ticks (0,67 s); "zu" liest ab Tick 24. Die Spitzen gehen nie unter den Boden: der Aufschlag ist ein Berühren, das Nachfedern ein Abprall. Gewicht entsteht über die Zeit: der Fall ist schnell, der Abprall zeigt, dass Eisen auf Stein trifft. Der Sprung von 6 cm sind aus 3 m rechnerisch etwa 7 Pixel (114 Pixel je Meter in Gittertiefe), der zweite etwa 1 Pixel (als Zittern lesbar); am bewegten GIF zu urteilen.

## Fallen, Fassung b: zwei Rucke, die Kette gibt nach

| Phase | Ticks | Verlauf | Höhe |
|---|---|---|---|
| Ruck 1 | 0 bis 12 | beschleunigt: h = 3,40 - 0,85 (t/12)^2 | 3,40 bis 2,55 |
| Kette fängt | 12 bis 24 | gedämpftes Schwingen, Tiefpunkt 2,52 bei Tick 14, Hochpunkt 2,56 bei Tick 18 | um 2,55 |
| Halt | 24 bis 26 | steht | 2,55 |
| Ruck 2 | 26 bis 47 | beschleunigt: h = 2,55 (1 - ((t-26)/21)^2) | 2,55 bis 0 |
| Aufschlag und Sprünge | 47 bis 63 | wie in a, Sprung 1 bis 0,050 in 11 Ticks, Sprung 2 bis 0,009 in 5 | |

Dauer 63 Ticks (1,05 s); "zu" liest ab Tick 47. Vom Gitter ist beim Ruck 1 nur der untere Rand mit der ersten Querstrebe zu sehen. Es hängt dann sichtbar in der Kette, bevor es endgültig fällt: das erzählt "etwas gibt nach" und kündigt den Aufschlag an. Der Preis ist, dass die Arena eine Sekunde braucht, bis sie zu ist; bei a sind es 0,4 s.

## Heben: gleichmäßig mit Anlauf

| Phase | Ticks | Verlauf | Höhe |
|---|---|---|---|
| Kette strafft sich | 0 bis 6 | Stillstand | 0 |
| Anlauf | 6 bis 18 | Geschwindigkeit wächst gleichmäßig | 0 bis 0,18 |
| gleichmäßig | 18 bis 126 | 0,0298 m je Tick (1,79 m/s) | 0,18 bis 3,40 |

Dauer 126 Ticks (2,1 s). Die Unterkante steigt über 1,85 m bei Tick 75 ("frei liest"). Zum Vergleich: Türen öffnen in 0,9 bis 2,2 s.

Warum gleichmäßig und nicht in Rucken: Das Fallen ist ein einmaliges, hartes Ereignis; das Heben ist die Auflösung, der Spieler soll es als ruhiges, verlässliches Zeichen "Kampf vorbei, der Weg ist frei" lesen und dabei schon loslaufen. Rucke beim Heben läsen sich im Bild als Stocken oder Ruckeln (Schritte von 0,28 m bei 60 Tick/s wären Sprünge), und sie wiederholten das Motiv des Falls. Das Rasseln der Kette trägt später der Ton. Das Anlaufen mit Stillstand von 6 Ticks gibt dem Spieler Zeit, die Bewegung zu bemerken, bevor sie die Spitzen vom Boden hebt.

Die letzten etwa 13 Ticks sind aus 3 m nicht mehr zu sehen: die Unterkante verschwindet hinter dem Sturz bei etwa 3,0 m (Kamera 3 m davor; im Streifenbild bei Tick 112, 2,98 m, nur noch die Stabspitzen). Beim Fallen sind aus demselben Grund die ersten etwa 8 Ticks leer: sichtbar wird das Gitter bei Tick 12 (2,55 m, im Streifenbild) deutlich, die Spitzen erscheinen um Tick 8 bis 9. Der Ton darf deshalb mit dem Auslöser beginnen, das Bild folgt etwa 0,13 s später. Aus Gängen oder von hinten ist es dieselbe Bewegung, dort ist die Unterkante bis 3,32 zu sehen.

## Sperre der Physik gegen das Bild

Heute gilt (Code gelesen, nichts geändert): `Door.Bar()` setzt `IsBarred` im Auslösetick, `Door.Lift()` hebt es im Tick des letzten Gegners auf. `Blocks` ist ein Schalter für die ganze Zelle von Boden bis Decke, ohne Dauer. Für Türflügel macht das nichts (der Flügel schwingt vor dem Spieler weg). Beim Gitter gibt es zwei Lücken:

1. **Fallen:** Die Sperre beginnt bei Tick 0, das Bild zeigt "zu" erst bei Tick 17 (a) bzw. 38 (b) für den Spieler, ganz zu bei 24 bzw. 47. Eine Wand aus Luft für 0,28 s bzw. 0,63 s. Bei a bleibt das im Alltag meist unbemerkt, weil der Spieler beim Schließen noch in den Raum hineingeht; bei b ist es zu lang.
2. **Heben:** Die Sperre endet bei Tick 0, das Gitter steht aber noch bis Tick 75 vor dem Spieler. Er liefe durch die Stäbe. Das ist nicht tragbar.

Damit die Sperre nie früher endet und nie später beginnt als das Bild: Der Zustand der Sperre folgt der Höhe, nicht dem Auslöser. Vorschlag für `Core` (Spiellogik, nicht von mir geändert): `Door` zählt Ticks seit dem Auslöser (wie `_openedTicks`) und `Blocks` ist wahr, solange `GratePose.Height(ticks, falling) < 1.85`. Das ergibt:

| Bewegung | Sperre beginnt | Bild liest "zu" |
|---|---|---|
| a | Tick 17 | ab 17 für den Spieler, ganz zu bei 24 |
| b | Tick 38 | ab 38 für den Spieler, ganz zu bei 47 |

| Bewegung | Sperre endet | Bild liest "frei" |
|---|---|---|
| Heben | Tick 75 | Spitzen über 1,85 m ab Tick 75; offen (hinter dem Sturz) bei 126 |

Eine Folge für den Kampf: Der Spieler kann nach dem letzten Gegner nach 75 Ticks (1,25 s) durch die Tür gehen; die Arena schließt 17 (a) bzw. 38 (b) Ticks nach dem Auslöser. Das liegt im Spielverlauf, nicht am Bild; wenn das nicht gewollt ist, ist es eine Entscheidung der Leitung (Dauern, Schwelle). Bleibt `Core` unverändert, ist a mit kleiner Lücke vertretbar, aber das Heben muss dann schlagartig sein, oder das Gitter bliebe für den Spieler zu früh frei; das empfehle ich nicht.

Alle Werte stehen unter `Zeitverlauf` auch als Tabelle in `zeitverlauf.csv` (Tick, Sekunde, Höhe).

## Umsetzung im Spiel

- **Reine Funktion, keine neue Abhängigkeit:** `GratePose` in `Core/Presentation/` (wie `DoorPose`) mit `Height(int ticks, GrateMotion motion)`; die Formeln stehen in `fallgitter_bewegung.py` (`fall_a`, `fall_b`, `lift`) und sind kurz genug für Code ohne Tabelle. Die Tabelle dient als Test: Tick -> Höhe auf 1 mm.
- **Zustand:** `Door` bekommt einen Zähler seit dem Auslöser (nur Ganzzahl, vom Tick getrieben, wie `_openedTicks`). Das Bild ist eine reine Funktion dieses Zählers: Pause, Speichern und Laden brauchen nichts Eigenes.
- **Darstellung nur in `Game`:** `DoorView` verschiebt das Gitter-Modell um `Height` in y (Modell hat Ursprung Unterkante, Mitte) und zeichnet die Führung unbewegt; kein Gitter-Teil wird gedreht.
- **Auslöser als Ereignis aus `Core`:** `RoomSealed(room)` gibt es schon, es genügt als Auslöser für das Fallen aller Türen des Raums; für das Heben `RoomCleared(room)`. Ein Ereignis je Tür (`GrateStarted(door, falling)`) wäre sauberer für Klang an der Tür (gerichteter Ton, #491).
- **Fassung b** braucht nur eine zweite Formel; welche Fassung gilt, ist ein Schalter in `GratePose`, kein Balancing-Wert.

## Ereignisse für den Klang (nur benannt)

| Ereignis | a | b | Heben |
|---|---|---|---|
| Auslösen (Riegel, Kette löst sich) | 0 | 0 | 0 |
| Kette fängt (Ruck, Rasseln) | | 12 | |
| Zweites Lösen | | 26 | |
| Aufschlag (Eisen auf Stein, schwer) | 24 | 47 | |
| Nachfedern (kleiner Klang) | 35 | 58 | |
| Kette läuft (Dauerton) | | | 6 bis 126 |
| Einrasten oben | | | 126 |

Ort jeweils die Tür (gerichteter Klang). Der Aufschlag ist das Ereignis, das der Spieler hören muss; er sollte auf dem Tick des Bildes liegen. Neue Geräusch-Kategorie: Hörprobe vor dem Einbau.

## Empfehlung

Fallen: **a**, wenn die Arena sofort schließen soll und der Schreck zählt (0,4 s); **b**, wenn die Szene als Ankündigung wirken soll (Ruck 1 ist ein Warnzeichen mit 0,4 s Vorlauf, bevor es endgültig fällt). Ich empfehle b: Der Spieler hört und sieht den ersten Ruck, kann reagieren, und die Zeit bis "zu" (1,05 s) bleibt in der Reichweite einer Tür (Boss 2,2 s). Wenn der Mensch die Arena als Falle will, die ohne Vorwarnung zuschnappt: a. Heben: die gleichmäßige Fassung.

## Unsicher

- Die Sperre nach Höhe ist eine Änderung in `Core`, die ich nur vorschlage. Ohne sie passen Bild und Physik nicht zusammen (siehe oben).
- Die Fallbeschleunigung (rund 4 g) ist geschätzt, nicht gemessen; echter freier Fall aus 3,4 m wäre 50 Ticks und läse sich schlaff.
- Der Sprung 2 (9 mm) ist aus 3 m etwa 1 Pixel; ob er als Zittern oder als Flimmern gelesen wird, ist am bewegten Bild zu urteilen.
- GIF bei 50 Bildern je Sekunde ist nur die Vorauswahl; im Spiel läuft es mit 60.
- Nur aus 3 m frontal geprüft; schräg und aus 8 m nicht in Bewegung geprüft. Die Unterkante ist aus 3 m erst ab etwa 3,0 m sichtbar.
- Nicht geprüft: das schmale Gitter (Boss-Rahmen), gleiche Zeitverläufe; das Gitter im 6-m-Raum.
