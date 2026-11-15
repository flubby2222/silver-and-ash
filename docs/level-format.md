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
| `boss` | Bossraum | 6 m | **genau ein** Türplatz |

Zusätzlich zu den Zeichen oben gibt es:

| Zeichen | Bedeutung |
|---|---|
| `+` | **Türplatz** in der Außenwand. Der Generator setzt hier eine Tür, wenn er einen Raum anschließt; ungenutzte Türplätze bleiben Wand. |

Regeln für Vorlagen:

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
