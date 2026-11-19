# Animation, Probe 3: Gorms Haken zielt vor und wird bei langer Verfolgung geworfen (Issue #437)

Probestück außerhalb des Spiels. Blatt: `blatt-gorm-haken.png` (Draufsichten, Zeitleisten, Tabelle, Empfehlung). Erzeugt von `blatt.ps1` (liest `assets/balance.json`, rechnet tickweise, zeichnet mit System.Drawing; zwei Läufe ergeben dieselbe Datei). Rohzahlen: `zahlen.txt`. Kein Spielcode, kein Modell, nichts in `Core`, `Game` oder `balance.json` geändert.

**Hinweis zum Titel:** „fällt“ lese ich im Issue-Text als „Gorm verfällt in den Wurf“ (Abnahmekriterium 2). Die Leitung hat es zusätzlich als „der Haken fällt zu Boden“ beauftragt; beides ist entworfen (Abschnitte 5 und 6 des Blatts).

## Heutiger Ablauf (Core, `Gorm.cs`)

1. **Wahl** (`ChooseAttack`, nur im Zustand Walking): Entfernung über `nearRange` (8 m) bis `farRange` (30 m), freie Sicht Hand zu Spielermitte, letzter Angriff nicht schon ein Wurf. Näher als 8 m: Hammer oder Kettenschwung. Sonst läuft er weiter (2,5 m/s, Phase 2 3,5 m/s).
2. **Ausholen** `throwWindUp` 0,5 s = 30 Ticks (Phase 2: 21). Er dreht sich jeden Tick zur aktuellen Spielerposition (`FaceTowards`), die Richtung wird erst im letzten Tick festgehalten (`_hookDirection = Facing`). Pose `ThrowWindUp`, Haken glüht rot.
3. **Flug** `hookSpeed` 18 m/s = 0,3 m je Tick, **gerade** in Blickrichtung auf Händehöhe 1,6 m, bis Treffer (Spieler: Radius 0,35 + Haken 0,3 = 0,65 m flach, Höhe bis 1,3 m über dem Boden bei Ausweichen), Wand oder 33 m Flugweg (`farRange + 3`). 20 m = 67 Ticks.
4. **Treffer:** 20 Schaden, Zug 3 m in 0,4 s. **Verfehlt:** `HookStopped`, Haken ist sofort weg. Danach Pause `pauseTime` 1,2 s = 72 Ticks (Phase 2: 51).

Folge heute: Wer quer läuft, ist beim Eintreffen längst woanders; der Haken verfehlt immer (Blatt, Abschnitt 1 und 4: „HEUTE verfehlt“ bei 8 bis 30 m). Wer geradeaus wegläuft, wird bis etwa 22 m getroffen (Schließgeschwindigkeit 12 m/s, 33 m Flugweg gibt 1,5 × Abstand). Die Wahl hängt nie an der Dauer der Verfolgung: Läuft der Spieler weg, wächst der Abstand um 3,5 m/s und nach etwa 6 s ist er außerhalb von 30 m; Gorm greift dann nie an.

## (a) Vorhalt: drei Varianten

Zielpunkt Q: Gorm löst nach der Flugzeit t auf `|Q − Hand| = hookSpeed · t` mit `Q = P + k · v · t`, Vorhalt höchstens `cap` Meter. `v` = Geschwindigkeit des Ziels, `k` = Stärke (balance.json, 0 = heute). Q wird im letzten Moment des Ausholens festgelegt (A) oder beim Einrasten (B, C). Die Ausweichfenster sind tickweise simuliert: Spieler läuft 6 m/s quer, reagiert nach 15 Ticks (0,25 s, **Annahme**), bleibt dann stehen (Rampe 6 Ticks) oder dasht (4 m in 9 Ticks); Fenster = späteste Reaktion, mit der der Abstand zum Haken über 0,65 m bleibt, minus früheste Reaktion (Cue + 15).

| | Stärke k | Vorhalt höchstens | Einrasten (Cue vor Wurf) | Zeichen für den Spieler |
|---|---|---|---|---|
| A Roh | 1,0 | unbegrenzt | keins, Cue = Wurf | nur der Haken, der fliegt |
| B Einrasten | 1,0 | 6 m | 12 Ticks (40 % von 30) | Gorm friert 12 Ticks ein: Kopf, Rumpf und Kettenarm starr, dumpfes Klacken (Eisen auf Stein, tief, nicht schrill), kleine Staubwolke am Fuß. Haken glüht wie heute |
| C Einrasten + Spur | 1,0 | 4 m | 18 Ticks (60 %) | wie B, dazu helle Staubspur (Knochenweiß, nicht Rot) vom Haken Richtung Zielpunkt, nur während Einrasten und Flug |

Ergebnisse für den Querläufer (Fenster in Ticks, Stopp / Dash; „verfehlt“ = trifft nicht, auch wenn er weiterläuft):

| Wurf aus | HEUTE | A | B | C | Flugzeit |
|---|---|---|---|---|---|
| 8 m | verfehlt | −1 / 7 | 11 / 19 | 17 / 25 | 25 |
| 12 m | verfehlt | 13 / 21 | 25 / 33 | 31 / 39 | 39 |
| 17 m | verfehlt | 31 / 39 | 43 / 51 | verfehlt | 57 |
| 22 m | verfehlt | 49 / 57 | verfehlt | verfehlt | 74 |
| 30 m | verfehlt | 77 / 85 | verfehlt | verfehlt | 103 |

Lesart: **A ist bei 8 m nicht fair** (−1 Tick fürs Stehenbleiben: Der Spieler hat erst ab dem Wurf einen Hinweis, und der Haken ist nach 25 Ticks da). Das Einrasten (B, C) schenkt die 12 bis 18 Ticks Vorlauf. Die Grenze „höchstens 6 m / 4 m Vorhalt“ ergibt ohne zusätzliche Regel, dass ferne Würfe auf einen Dauerläufer verfehlen (B ab etwa 18 m, C ab etwa 14 m): ein Läufer wird auf Nähe bestraft, auf Ferne belohnt. Lesbarkeit: Das Einrasten ist ein kurzer Stillstand mit Klacken, kein Dauersignal; Rot kommt nur im bestehenden Glühen des Hakens vor.

Phase 2 (Ausholen 21 Ticks): Einrasten 40 % = 8 Ticks (B), 60 % = 13 Ticks (C); die Fenster schrumpfen entsprechend um 4 bis 5 Ticks, bei 8 m dann fürs Stehenbleiben knapp unter null. Vorschlag: In Phase 2 das Einrasten nicht verkürzen (12 Ticks fest, Ausholen entsprechend), sonst wird 8 m unfair; das kostet kein Spielwert, aber Ausholen bliebe bei 21 Ticks und das Einrasten läge im Ausholen. Frage an die Leitung, ob die Ausholzeit in Phase 2 steigen darf.

Fehlt noch eine Messung: Ausweichen mit dem Ducken. Der Haken fliegt auf 1,6 m, das Ausweich-Ducken hat 1,0 m Körperhöhe (Treffer bis 1,3 m): **Wer sich duckt (Ausweichen am Boden), wird nicht getroffen.** Das ist heute schon so und bleibt. Der Entwurf lässt es zu.

## (b) Dauerverfolgung und der liegende Haken

**Messgröße:** `_chaseTicks` zählt Ticks im Zustand Walking, die keinen Angriff begonnen haben; jeder Angriffsbeginn und Tod des Ziels setzt zurück. Bei Ablauf: Gorm bleibt stehen, Wurf wie gewohnt (gleiches Ausholen), der Vorhalt aus (a) gilt. Zusätzlich muss freie Sicht bestehen und das Ziel in `farRange` (30 m) sein; sonst bleibt der Zähler auf Schwelle und Gorm läuft weiter, bis es geht (kein Wurf in die Wand). Die Regel „nie zweimal derselbe Angriff hintereinander“ gilt für diesen Wurf nicht, sonst wäre die Verfolgung nach einem Wurf endlos.

| | Schwelle | Vorwarnung | Bemerkung |
|---|---|---|---|
| A kurz | 300 Ticks (5 s) | keine | Wurf kommt ohne Ansage außer dem Ausholen |
| B gemessen | 480 Ticks (8 s) | ab Tick 360 (2 s lang): Kette klirrt in jedem Schritt lauter, der Haken schwingt am Arm weiter aus (10° bis 35°) | **empfohlen**. Warnung ist keine Farbe |
| C je nach Abstand | `480 − 10 · (Abstand − 8)` Ticks, mindestens 240 | keine | 10 m: 456, 20 m: 336, 30 m: 240. Wer weit weg läuft, wird früher gestellt; eine Formel mehr |

Zur Schwelle gerechnet: Läuft der Spieler 6 m/s von 10 m Abstand an weg, ist er nach 5 s bei 27,5 m (A wirft noch), nach 8 s bei 38 m (B wirft nicht mehr, dort greift nur die Sichtprüfung nicht; Gorm holt auf: nie, denn 2,5 < 6). **Offene Frage:** B löst gegen einen Dauerläufer weg von Gorm nie aus. Ist das gewünscht, oder soll die Schwelle den Abstand mitrechnen (C)?

**Haken liegt (alle Varianten gleich, nur Darstellung):** Nach `HookStopped` (Wand, Reichweite) fällt der Haken aus 1,5 m Höhe (Fallzeit aus `jumpHeight`/`jumpDuration` gerechnet: 26 Ticks, rutscht dabei noch etwa 4 m vor und kippt nach vorn), liegt dann, und die letzten 6 Ticks der Pause holt Gorm die Kette ein (Glieder laufen auf ihn zu, Haken verschwindet). Länge: Pause 72 Ticks gibt 26 + 40 liegen + 6 einholen; Phase 2 (51 Ticks) gibt 26 + 19 + 6. Gorm bewegt sich dabei nicht, die Pause bleibt unverändert, also kein Spielwert betroffen. Bei Treffer gibt es kein Liegen (Zug 0,4 s).

## Vorläufige Werte für `balance.json` (Vorschlag, nicht eingetragen)

```
"hookLead": 1.0,            // Staerke des Vorhalts, 0 = heutiges Verhalten
"hookLeadMax": 6,           // m, Vorhalt hoechstens so weit voraus
"hookLockTime": 0.2,        // s Einrasten vor dem Wurf (12 Ticks)
"chaseThrowTime": 8,        // s Verfolgung ohne Angriff bis zum Wurf (0 = aus)
"chaseWarnTime": 2          // s Vorwarnung davor
```

Alle vorläufig; Bot-Kampf (Abnahmekriterium 5) und Spielgefühl entscheiden.

## Was der Einbau braucht

- **Core (`Gorm.cs`, `Balance.cs`):** Zielpunkt beim Einrasten berechnen (Zeit aus Quadrat-Gleichung oder wenige Iterationen, wie im Skript); `FaceTowards` im Einrasten aussetzen (neuer Unterzustand oder `StateProgress`-Schwelle); `_chaseTicks`; neues Ereignis `GormLocked` (Klacken, Staub) und `HookDropped` (Position); Spielerwert `Velocity` aus `Player` (tatsächliche Verschiebung der letzten Ticks ist fairer als die Eingabe, ändert Fenster nur um wenige Ticks). „Ein Spieler“: nächstes lebendes Ziel wie heute.
- **Game/Presentation:** `GormPose` um eine Einrastpose ergänzen (Rest von `ThrowWindUp` halten), Haken-Pendeln in der Vorwarnung, fallender und liegender Haken in `GormView`/`ModelGormView`, Staubspur nur für C; Klang `GormLockClack` (tief, kurz) und lauteres Klirren, Hörprobe vor Integration (Regel für neue Geräusch-Kategorie).
- **Tests (tickweise):** (1) Querläufer wird mit `hookLead` 1 getroffen, mit 0 nicht (Gegenprobe Zielpunkt aus). (2) Stehenbleiben oder Dash nach dem Einrasten verfehlt aus 8, 12 und 17 m. (3) Ducken verfehlt. (4) Zähler: Angriff setzt zurück, Wurf nach Schwelle nur bei Sicht und Reichweite. (5) Haken liegt nie über das Pausenende hinaus. (6) Phase 2: Einrasten höchstens Ausholzeit.
- **Fotos:** Foto-Szene „Haken Einrasten“ und „Haken liegt“ für Bildfolge, sobald es ein Foto-Werkzeug mit Phasenwert gibt.

## Unsicher

- Reaktionszeit 15 Ticks ist eine Annahme, keine Messung.
- Die Draufsichten sind eine Rechnung, keine Fotos aus dem Spiel; die Bildfolge folgt mit dem Einbau.
- Fallweite und Klang sind Vorschläge. Die Fenster in der Tabelle gelten für den Querläufer bei 6 m/s; schräge Bahnen und Springen sind nicht gerechnet.

## Dateien

`blatt.ps1` (Aufruf: `powershell -File blatt.ps1 -Out blatt-gorm-haken.png [-Zahlen zahlen.txt]`), `blatt-gorm-haken.png`, `zahlen.txt`.
