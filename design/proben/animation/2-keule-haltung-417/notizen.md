# Animation, Probe 2: Keule als Schlagwaffe halten und schwingen (Issue #417, Teil 1)

Probestück außerhalb des Spiels. Blatt: `keule-haltung-417.png` (Ruhe heute/A/B/C, Schnellschlag, aufgeladener Schlag, alles aus der Ich-Sicht 854 × 480 mit dem echten Spiel fotografiert). Kein Spielcode, kein Modell, nichts in `Core`/`balance.json` geändert.

## Ausgangslage

Die Keule läuft heute über `WeaponPose.Blade(...)` wie die Klinge: Kopf zeigt in die Bildtiefe (+z), nur um 20° gerollt. Das liest sich als Stoßwaffe. Schlag = Gier 50°, Sprung der Spitze nach rechts, 12° nach unten (Klingenschlag).

## Haltungen (Kamera-Raum: x rechts, y oben, z nach vorn; Winkel in Grad)

Die Pose ist `FileToCamera * RotZ(-Roll) * RotX(-Hoch) * RotY(Gier) * Translate(x, y, z)` (System.Numerics, Zeilenvektoren, wie `Blade`). Drehpunkt ist der Griff (Ursprung von `club.glb`); `Hoch` > 0 hebt den Kopf, `Gier` > 0 dreht ihn nach rechts. Der Ärmel folgt wie bisher: `ForearmTurn(pose, Blade(0,0,0), pivot, BladeShoulder)` (Rest-Bezug ist die alte Ruhe, weil die Datei darauf gebaut ist).

| Haltung | Hoch | Gier | Roll | Griff x / y / z (m) |
|---|---|---|---|---|
| Heute (Blade Ruhe) | 0 | 0 | 20 | -0,11 / -0,095 / 0,19 |
| **A leicht** | 35 | -12 | 0 | -0,12 / -0,11 / 0,20 |
| **B mittel (empfohlen)** | 55 | -20 | 0 | -0,13 / -0,12 / 0,21 |
| **C stark** | 70 | -25 | 0 | -0,13 / -0,12 / 0,21 |
| Treffer (Schlaghöhe, Wert 1) | 0 | 45 | 0 | -0,02 / -0,09 / 0,24 |
| Ausholen voll (Wert 1) | 65 | -50 | 10 | -0,18 / -0,17 / 0,20 |

Die Pose zwischen zwei Haltungen ist die lineare Mischung jedes Wertes: `Wert = Ruhe + (Treffer - Ruhe) * Schlag + (Ausholen - Ruhe) * Aufladen`. `Schlag` und `Aufladen` sind dieselben Werte 0..1 wie heute (`WeaponPose.SwingCurve`, `_windUp`).

Warum B: Der Kopf zeigt klar schräg nach oben außen, die Silhouette ist ein Hammer in der Faust und kein Spieß; er bleibt ganz im Bild und der Ärmel mit Manschette bleibt sichtbar. A (35°) liest sich noch als nach vorn gehaltener Stab. C (70°) steht fast senkrecht, die Manschette kippt so weit, dass unten eine dunkle Fläche sichtbar wird (Knick Manschette/Unterarm, dasselbe Artefakt wie bei der Klinge mit 95°).

## Zeitverlauf (Schnellschlag, 17 Ticks, unverändert)

`SwingCurve` bleibt: Tick 0 Loslassen (Core trifft hier), Ticks 0 bis 4 Schlag (`1-(1-u)^3`; Tick 1 = 0,55, Tick 2 = 0,85, Tick 3 = 0,98), Ticks 4 bis 7 Halten mit Überschwingen (Höchstwert 1,05 bei Tick 5 bis 6), Ticks 7 bis 17 Rückweg (smoothstep, kleiner Unterschwinger ab 55 % des Rückwegs). Das Gewicht kommt aus dem steilen Anfang und dem langen Rückweg. Die Kopfspitze beschreibt dabei eine Diagonale von oben links (Kopf hoch, außen) nach rechts (Kopf waagerecht, Gier 45): ein Bogen, kein Stoß nach vorn. Hinweis an die Leitung: Die Sichtbare Treffer-Pose (Tick 4) liegt wie bei der Klinge 4 Ticks nach dem Trefferzeitpunkt in `Core`; das ist so gewollt und bleibt unberührt.

Aufgeladener Schlag: Beim Halten wächst `Aufladen` von 0,25 auf 1 (wie heute `0.25 + 0.75 * Fraction`). Beim Loslassen gilt `Aufladen = 1 - Schlag` in den ersten 4 Ticks (der Kopf schwingt aus der Ausholstellung durch denselben Bogen); danach wie der Schnellschlag. Voll aufgeladen zittert die Hand wie heute (`tremble`, ±1,2 mm).

Optional (nicht nötig, Entscheidung der Leitung): mehr Wucht der Keule gegen die Klinge über `SwingOvershoot` 0,10 statt 0,06 nur für die Keule.

## Prüfung

- Alle Phasen mit dem echten Spiel fotografiert (`rendern.sh` + `blatt.ps1`, Bilder im Blatt): Ruhe A/B/C, Schlag Tick 0/1/2/4/6/11, Aufladen 25/60/100 %, Loslassen Tick 1/2/4. Der Ärmel läuft links aus dem Bild; keine dunkle Fläche in B und A, in C schon (unten Mitte).
- Reichweite und Nahebene gerechnet mit `pruefung.py` (Hüllbox der Keule, Verkleinern um die Hälfte, Randzugabe wie `WeaponPoseTests`): weitester Punkt im Bild in allen B-Phasen höchstens 0,254 m (Grenze `Reach` 0,30). Nur Näherung; es gibt noch keinen Reichweitentest für `club.glb`.
- Der Kopf schneidet im Treffer die Pistole (Tick 2 bis 6): laut Auftrag vorerst kein Problem.

## Unsicher / für Teil 2

- **Ausholen unterscheidet sich von Ruhe B nur mäßig** (Kopf 65°/-50° gegenüber 55°/-20°, Hand tiefer links). Stärker (80°/-60° oder 85°/-45°) rückt der Kopf so nah an die Augen, dass er das Bild füllt (getestet, verworfen). Mehr Ankündigung eher über die Zeit (Zittern) als über den Winkel.
- Keule-Tests fehlen: In Teil 2 `WeaponPoseTests` um `club.glb` und `club_hand.glb` erweitern (Reichweite, Nahebene, Knick des Unterarms), vor allem im aufgeladenen Schlag.
- Das Blatt nutzt Ärmel und Modell unverändert; Hände nicht modelliert.
- Teil 2 setzt dafür eine eigene Keulen-Pose neben `Blade(...)` (z. B. `WeaponPose.Club(swing, windUp, tremble)`) und `ClubForearm(...)`; `WeaponViews.DrawBlade` ruft sie für `WeaponKind.Club`.

## Dateien

- `probe-haltung.patch`: vorübergehender Umschalter über Umgebungsvariablen (`SA_CLUB_REST`, `SA_CLUB_HIT`, `SA_CLUB_WIND` = `Hoch,Gier,Roll,x,y,z`; `SA_CLUB_T`, `SA_CLUB_W` setzen Schlag und Aufladen fest). Nicht im Spiel, nur für die Probe; die Funktion `ClubProbePose` im Patch ist der Vorläufer der Teil-2-Pose.
- `rendern.sh`, `blatt.ps1`, `pruefung.py`, `keule-haltung-417.png`.

## Runde 2: Keule am Griff abgewinkelt, Ärmel bleibt (Urteil des Auftraggebers)

Blatt: `keule-haltung-417-runde2.png`. Der Schlagbogen aus Runde 1 ist abgenommen und bleibt. Dateien: `probe-haltung-runde2.patch`, `rendern-runde2.sh`, `blatt-runde2.ps1`.

**Haltung.** Manschette und Unterarm werden exakt wie heute gezeichnet (`Blade(t, w, tremble)`, `BladeForearm`). Nur das Keulenteil bekommt zusätzlich einen Knick um einen Drehpunkt im Dateiraum bei (0, 0, -0,06), also im Inneren der Manschette (Griff reicht von z -0,136 bis 0). Reihenfolge: Verschieben(-P), RotX(-Neigung), RotY(Auswärts), Verschieben(P), danach die Waffenhaltung.

| Variante | Neigung | Auswärts | Befund Nahaufnahme |
|---|---|---|---|
| A | 20 Grad | 6 Grad | sauber, Knick kaum zu sehen |
| B (empfohlen) | 35 Grad | 10 Grad | sauber, deutlich schräg |
| C | 50 Grad | 15 Grad | Griffende sticht vorn durch die Manschette |

**Schlag aus B** (Zeiten unverändert, 17 Ticks, `SwingCurve`): Knick im Treffer-Moment 0 Grad (Keule gerade in Schlagrichtung), beim vollen Ausholen 20 Grad Neigung und -10 Grad auswärts; dazwischen linear über dieselben Werte `swing` und `windUp` wie die Haltung. Der Ärmel folgt wie heute der Waffe und beginnt und endet in heutiger Lage.

**Geprüft** (Bildfolge, 12 Phasen: Ruhe, Schlag Tick 1/2/4/6/11, Ausholen 25/60/100 %, Loslassen Tick 1/2/4): keine Lücke, kein Blick in die hohle Manschette, keine dunkle Fläche, nichts durch Bildrand oder Nahebene, der Kopf füllt das Bild nie.

**Offen.**
- Der Knick ist nur Probecode. Für den Einbau braucht es Tests wie die `WeaponPoseTests` der Klinge auch für die Keule (Reichweite, Nahebene, Durchstich der Manschette je Phase).
- Die Drehpunkttiefe (-0,06) beim Einbau gegen die Manschettenöffnung prüfen.
- Pistole und Keule überschneiden sich im Ruhebild leicht; wie in Runde 1 hingenommen.
- Der Unterschied Ausholen zu Ruhe ist klein (20 statt 35 Grad); bei Bedarf stärker abheben.
