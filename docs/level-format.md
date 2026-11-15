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

**Standard ist ein Durchlauf auf Ebene 0** („THE CATACOMBS“): eine aus den Raum-Vorlagen erzeugte Ebene (siehe unten), bei jedem Start und nach jedem Tod mit **neuem Seed**, also neuem Grundriss. Der Seed steht im Log. Mit `--seed` spielt man einen bestimmten Grundriss; gleicher Seed = gleiche Ebene, gleiche Gegner:

```
dotnet run --project src/SilverAndAsh.Game -- --seed 123
```

**Sieg (vorläufig):** Ist der Bossraum geräumt, ist der Durchlauf gewonnen. Troll und Wendeltreppe kommen in v0.10.

**Ende eines Durchlaufs:** Nach dem Tod (2 s später) bzw. dem Sieg (2 s später) erscheint „YOU DIED“ / „VICTORY“ mit der Statistik des Durchlaufs und der Wahl „New run“ / „Title“. Die Statistik wird außerdem als Zeile in `%LOCALAPPDATA%\SilverAndAsh\runs.log` geschrieben.

Die alten **handgebauten Level** aus `assets/levels/` startet man mit `--level <name>` (Dateiname ohne `.txt`). Sie haben keine Räume und Türen, alle Gegner sind von Anfang an wach, und nach dem Tod beginnt dasselbe Level von vorn:

```
dotnet run --project src/SilverAndAsh.Game -- --level test
```

```
dotnet run --project src/SilverAndAsh.Game -- --level heights
```

Die beiden `--` trennen die Optionen für `dotnet` von denen für das Spiel. Erlaubt sind Namen aus Kleinbuchstaben, Ziffern und `-`. `--level generated` ist dasselbe wie gar kein `--level`.

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
- In **Räumen** (nicht in Gängen) ist auch **links und rechts** dieses Bodenfelds Boden: Eine Tür sitzt nie bündig an einer Seitenwand, man läuft also nie direkt an einer Wand entlang hinein.

  ```
  erlaubt:   verboten:
  ###+###    #####+#
  #.....#    #.....#
  ```
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

Zu jeder erzeugten Ebene gibt es neben den Zellen eine Liste der **Räume** (Art, Fläche, Abstand zum Start in Räumen, Gegnerplätze) und der **Türen** (Lage, welche zwei Räume). Die Gegner setzt danach [`EnemyPlacement`](../src/SilverAndAsh.Core/World/EnemyPlacement.cs) auf die Gegnerplätze (siehe „Arenen und Schwierigkeit“).

## Türen im Spiel

Jede Tür des Generators wird im Spiel zu einer echten Tür ([`Door.cs`](../src/SilverAndAsh.Core/Gameplay/Door.cs)):

- **Alle Türen starten geschlossen.** Eine geschlossene Tür füllt ihre Türzelle aus. Die Welt behandelt diese Zelle dann als Wand, deshalb blockiert sie Laufen, Sicht der Gegner, Schüsse, Blutgeschosse und die Wegsuche der Gegner, ganz ohne Sonderregeln.
- **F** öffnet die Tür, auf die man schaut, aus höchstens 2 m (von den Augen bis zur Türfläche). Dafür erscheint der Hinweis „[F] Open“. Offen bleibt offen; ein neuer Durchlauf beginnt wieder mit geschlossenen Türen.
- **Arten:** normal (Holz) und Boss (die Tür zum Bossraum). Verschlossen (Schlüssel, v0.9) ist schon vorgesehen.
- **Gitter:** Jede Tür kann zusätzlich **vergittert** sein (Arena, siehe unten). Dann blockiert sie, auch wenn sie offen ist, und F wirkt nicht.
- F ist die **eine** Interaktionstaste. Alles, womit man interagieren kann, ist ein `IInteractable` (heute nur Türen, später Kisten, Schlösser, Personen).

## Arenen und Schwierigkeit

**Gegner aufstellen** ([`EnemyPlacement`](../src/SilverAndAsh.Core/World/EnemyPlacement.cs)):

- Nur **Kampf-** und **Bossräume** bekommen Gegner; Startraum und Gänge bleiben leer.
- Wie viele der Gegnerplätze einer Vorlage besetzt werden, hängt vom **Abstand zum Start** ab. Im nächstgelegenen Kampfraum gelten die „near“-Anteile, im entferntesten (und im Bossraum) die „far“-Anteile, dazwischen steigt es gleichmäßig. Die Werte stehen in `balance.json` unter `difficulty`:

  | Wert | Standard | Bedeutung |
  |---|---|---|
  | `ghoulShareNear` | 0,5 | Hälfte der Ghul-Plätze im nächsten Kampfraum |
  | `ghoulShareFar` | 1 | alle Ghul-Plätze ganz hinten |
  | `batShareNear` | 0 | keine Fledermaus am Anfang |
  | `batShareFar` | 1 | alle Fledermaus-Plätze ganz hinten |

- Jeder Kampfraum bekommt **mindestens einen** Gegner. Welche Plätze besetzt werden, entscheidet der Seed: **gleicher Seed = gleiche Gegner**.
- Die Kampf-Vorlagen haben je **5 Ghul- und 3 Fledermausplätze**, der Bossraum **6 und 3**.

**Arena** ([`RoomState`](../src/SilverAndAsh.Core/Gameplay/RoomState.cs), Logik in `GameWorld`):

1. Gegner in Kampf- und Bossräumen **schlafen**: Sie stehen bzw. hängen still, bis der Raum betreten wird. Trifft man einen schlafenden Gegner (z. B. durch die offene Tür), wacht sein ganzer Raum auf, wird aber nicht versiegelt.
2. **Betreten** heißt: mit dem **ganzen Körper** im Raum, nicht mehr in der Türzelle. Sonst würde das Gitter auf den Spieler fallen.
3. Beim **ersten Betreten** eines Kampf- oder Bossraums mit lebenden Gegnern fallen an **allen** seinen Türen Gitter herunter, die Gegner erwachen (Ereignis `RoomSealed`).
4. Sind alle Gegner des Raums tot, ist er **geräumt** (`RoomCleared`). Die Gitter heben sich, die Tür, durch die man kam, **öffnet sich von selbst**, die anderen lassen sich mit F öffnen.
5. Ein geräumter Raum versiegelt sich **nie wieder**. Ein Kampfraum ohne Gegner gilt beim Betreten sofort als geräumt.

## Karte

Ein Raum (auch ein Gang) gilt als **entdeckt**, sobald ein Spieler auf einem seiner Bodenfelder steht – die Türzelle allein reicht nicht. Die **Karte auf M** zeigt nur entdeckte Räume, dazu die Türen, die an einen entdeckten Raum grenzen, und die eigene Position mit Blickrichtung. Ein neuer Durchlauf beginnt ohne Entdeckungen. Handgebaute Level haben keine Räume, also auch keine Karte.
