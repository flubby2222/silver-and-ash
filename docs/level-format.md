# Level-Format

Level sind einfache Textdateien in `assets/levels/`. **Ein Zeichen = eine Zelle** (2 × 2 m), **eine Zeile = eine Reihe**.

## Zeichen

| Zeichen | Zelle | Boden | Decke |
|---|---|---|---|
| `#` | Wand | – | – |
| ` ` (Leerzeichen) | Wand (praktisch für unregelmäßige Formen) | – | – |
| `.` | Boden | 0 m | 3,5 m |
| `P` | Boden mit **Spielerstart** – genau einmal pro Level | 0 m | 3,5 m |
| `1` | Podest, niedrig | 0,25 m | 3,5 m |
| `2` | Podest, mittel | 0,5 m | 3,5 m |
| `3` | Podest, hoch | 0,75 m | 3,5 m |
| `_` | Grube | −1 m | 3,5 m |
| `D` | Boden mit **Trainingspuppe** in der Mitte (beliebig oft) | 0 m | 3,5 m |
| `G` | Boden, auf dem ein **Ghul** startet (beliebig oft); er schaut zum Spielerstart | 0 m | 3,5 m |
| `B` | Boden, über dem eine **Vampirfledermaus** schwebt (beliebig oft), ca. 2,2 m hoch und immer mit Abstand zur Decke | 0 m | 3,5 m |

## Regeln

- Kürzere Zeilen werden mit Wand aufgefüllt. Alles außerhalb des Levels gilt als Wand.
- Leere Zeilen am Ende werden ignoriert.
- Fehler (unbekanntes Zeichen, fehlendes oder doppeltes `P`, leeres Level) werden mit **Zeile und Spalte** gemeldet.
- **Trainingspuppen** (`D`) stehen auf normalem Boden (0 m). Man läuft nicht durch sie hindurch; liegt eine am Boden, kann man über sie hinweggehen.

## Was der Spieler mit Höhen machen kann

Die Physik kommt aus [Entscheidung 0002](entscheidungen/0002-spiellogik-von-anfang-an-3d.md) und Issue #21:

- **Stufen bis 0,4 m** steigt man einfach hinauf → eine Treppe `.123` ist begehbar (je 0,25 m).
- **Höhere Kanten** blockieren wie eine Wand – außer man springt hoch genug (Sprunghöhe ca. 1 m).
- **Gruben** blockieren nicht: Man fällt hinein und kann wieder herausspringen (1 m tief, Sprung ca. 1 m).

## Beispiel

```
##########
#P..123..#
#........#
#..___...#
#........#
##########
```
Eine kleine Treppe nach oben (`123`) und eine Grube (`___`).

## Ein Level starten

Standard ist `assets/levels/test.txt`. Ein anderes Level wählt man beim Start mit `--level <name>` (Dateiname ohne `.txt`):

```
dotnet run --project src/SilverAndAsh.Game -- --level heights
```

Die beiden `--` trennen die Optionen für `dotnet` von denen für das Spiel. Erlaubt sind Namen aus Kleinbuchstaben, Ziffern und `-`.

Eine **erzeugte Ebene** (aus den Raum-Vorlagen, siehe unten) startet man mit dem Namen `generated`. Mit `--seed` wählt man den Startwert. Gleicher Seed = gleicher Grundriss; ohne `--seed` gibt es bei jedem Start einen neuen (er steht im Log):

```
dotnet run --project src/SilverAndAsh.Game -- --level generated --seed 123
```

## Im Code

- Einlesen: [`src/SilverAndAsh.Core/World/LevelParser.cs`](../src/SilverAndAsh.Core/World/LevelParser.cs)
- Datenmodell: [`Level.cs`](../src/SilverAndAsh.Core/World/Level.cs), [`Cell.cs`](../src/SilverAndAsh.Core/World/Cell.cs)

## Raum-Vorlagen

Ab v0.8 werden Ebenen aus **Raum-Vorlagen** zusammengesetzt (siehe [Entscheidung 0005](entscheidungen/0005-ebenen-raeume-durchlauf.md)). Eine Vorlage ist eine Textdatei im selben Format wie oben, abgelegt in `assets/rooms/<raumart>/`, z. B. `assets/rooms/combat/pillars.txt`. **Der Ordner bestimmt die Raumart:**

| Ordner | Raumart | Decke | Besonderheit |
|---|---|---|---|
| `start` | Startraum | 6 m | genau ein `P` |
| `combat` | Kampfraum | 6 m | groß und verwinkelt (10–18 Zellen je Richtung), mind. 3 Gegnerplätze |
| `corridor` | Gang | 3,5 m | schmal, gern mit Knick |
| `boss` | Bossraum | 6 m | **genau ein** Türplatz (gilt später für alle Räume mit nur einem Eingang) |

Zusätzlich zu den Zeichen oben gibt es:

| Zeichen | Bedeutung |
|---|---|
| `+` | **Türplatz** in der Außenwand. Der Generator setzt hier eine Tür, wenn er einen Raum anschließt; ungenutzte Türplätze bleiben Wand. |

Regeln für Vorlagen:

- Der **äußere Rand** einer Vorlage ist immer **Wand oder Türplatz** – nie Boden. So grenzt ein Raum nie offen nach außen, und zwei verbundene Räume treffen sich immer Wand an Wand.
- Jede Vorlage hat **mindestens einen Türplatz**. Ein Türplatz liegt **am Rand**, **nicht in einer Ecke**, und **dahinter (nach innen) ist Boden**.
- `G` und `B` sind **Plätze für Gegner** – wie viele davon wirklich besetzt werden, entscheidet später die Schwierigkeit.
- `P` gibt es **nur im Startraum**, Trainingspuppen (`D`) gar nicht.
- Der Boden muss **zusammenhängen** – kein Bodenfeld darf abgeschnitten sein.
- Auf Ebene 0 gibt es noch **keine Höhenunterschiede** (keine `1`/`2`/`3`/`_`), auch wenn der Parser sie schon versteht.
- Der Generator darf Vorlagen **drehen** (in Vierteldrehungen) und **spiegeln** – Türplätze drehen sich mit. Eine Vorlage muss also nicht in jeder Ausrichtung angelegt werden.
- Fehler werden mit **Datei, Zeile und Spalte** gemeldet, z. B. `combat/pillars.txt, line 3: Door slot '+' in column 1 sits in a corner; a door needs a side.`
- Ein Test liest **alle** mitgelieferten Vorlagen ein – eine kaputte Vorlage fällt also schon in der CI auf.

Beispiel (Gang mit Knick, `assets/rooms/corridor/bend.txt`):

```
#+####
#.####
#.####
#.####
#....+
######
```

Im Code: [`RoomTemplateParser.cs`](../src/SilverAndAsh.Core/World/RoomTemplateParser.cs), [`RoomTemplate.cs`](../src/SilverAndAsh.Core/World/RoomTemplate.cs).

## Wie der Generator eine Ebene baut

Der [`LevelGenerator`](../src/SilverAndAsh.Core/World/LevelGenerator.cs) setzt aus den Vorlagen per Seed eine Ebene zusammen:

1. **Startraum** hinlegen (zufällige Vorlage, gedreht/gespiegelt).
2. **Kampfräume** an freie Türplätze anhängen, bis die Zahl aus `balance.json` (`layout.combatRoomsMin` … `combatRoomsMax`) erreicht ist. Mit der Chance `layout.corridorChance` liegt ein **Gang** dazwischen, sonst teilen sich die Räume eine Wand mit Tür. Bei `0` gibt es gar keine Gänge.
3. **Schleifen** (höchstens `layout.maxLoops`): Der Generator sucht gezielt Stellen, an denen ein neuer Raum – oder ein Raum plus Gang, oder nur ein Gang – zwei freie Türplätze verbindet. So gibt es einen zweiten Weg herum. Zwei Räume, die schon verbunden sind (Tür oder Gang), werden nicht ein zweites Mal verbunden.
4. **Bossraum** zuletzt, hinter einem der Räume, die am weitesten vom Start liegen. Er ist damit **immer der entfernteste Raum** und hat **genau einen Eingang**.

Regeln, die immer gelten (und über viele Seeds getestet werden):

- Räume **überlappen nicht**. Sie dürfen sich eine Wand teilen, aber Boden berührt nie Boden eines anderen Raums – auch nicht über Eck.
- Räume sind **nur über Türen** verbunden; jede Tür verbindet **genau zwei Räume**.
- **Alles ist erreichbar.**
- Ein **Gang** führt immer von einem Raum zu einem anderen, nie ins Leere und nie direkt in einen weiteren Gang.
- Räume mit **nur einem Eingang** (heute der Boss, später Schatz, Händler, Altar) bekommen nie eine zweite Tür.
- Ungenutzte Türplätze bleiben Wand. Türen sind 3,5 m hoch, auch in hohen Räumen.
- Bleibt der Generator stecken (kein Raum passt mehr), versucht er es mit den nächsten Zufallszahlen **desselben** Seeds noch einmal – das Ergebnis bleibt also gleich.

Zu jeder erzeugten Ebene gibt es neben den Zellen eine Liste der **Räume** (Art, Fläche, Abstand zum Start in Räumen, Gegnerplätze) und der **Türen** (Lage, welche zwei Räume). Gegner werden noch nicht aufgestellt – das kommt mit den Arenen (#122).
