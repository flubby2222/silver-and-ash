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
| `T` | Boden, auf dem **Gorm** (der Troll, Boss) startet; er schaut zum Spielerstart | 0 m | 3,5 m |

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

**Sieg:** Im Bossraum wartet **Gorm, the Chained** (ADR 0007). Er greift nah mit Hammer (Schockwelle: **springen**) oder Rundumschwung (Kette auf Brusthöhe: **Dodge**) an, aus der Ferne mit dem Kettenwurf (**zur Seite** oder hinter eine Säule). Jeder Angriff wird angekündigt, danach ruht er kurz. Verfehlt der Hammer, steckt die Keule fest und Gorm nimmt doppelten Schaden. Alle Werte in `balance.json` unter `gorm`. Ist er tot, ist der Bossraum geräumt: In seiner Mitte fährt in 3 s eine **Wendeltreppe** aus dem Boden. **„[F] Climb the stairs“** gewinnt den Durchlauf.

**Ende eines Durchlaufs:** Nach dem Tod (2 s später) bzw. dem Sieg (2 s später) erscheint „YOU DIED“ / „VICTORY“ mit der Statistik des Durchlaufs und der Wahl „New run“ / „Title“. Die Statistik wird außerdem als Zeile in `%LOCALAPPDATA%\SilverAndAsh\runs.log` geschrieben.

Die alten **handgebauten Level** aus `assets/levels/` startet man mit `--level <name>` (Dateiname ohne `.txt`). Sie haben keine Räume und Türen, alle Gegner sind von Anfang an wach, und nach dem Tod beginnt dasselbe Level von vorn:

```
dotnet run --project src/SilverAndAsh.Game -- --level test
```

```
dotnet run --project src/SilverAndAsh.Game -- --level heights
```

**Waffen ausprobieren**, bevor man sie finden kann: Mit `--give <waffe>` beginnt jeder Durchlauf mit dieser Waffe. Eine Nahkampfwaffe kommt in die Hand, eine Fernwaffe auf den ersten freien Platz. Das geht auch mehrfach und lässt sich mit `--seed` kombinieren. Es wirkt **nur in der Debug-Version** (`dotnet run`), nicht im Release:

```
dotnet run --project src/SilverAndAsh.Game -- --give club
```

**Eine einzelne Raum-Vorlage begehen** (Issue #528), etwa um eine neue Vorlage zu prüfen: `--room <datei>` baut ein geschlossenes Level aus genau dieser Datei aus `assets/rooms/`. Der Ordner bestimmt die Raumart wie im Generator. Um die Vorlage läuft ein Ring aus Wand. Man steht im **ersten Türplatz** (`+`, in Leserichtung zuerst) und blickt in den Raum; alle anderen Türplätze sind Wand. Gegner und Kisten werden nach denselben Regeln wie auf Ebene 0 verteilt. Ein Kampfraum versiegelt sich beim Betreten, seine Gegner kommen hervor, ein Bossraum hat Gorm. Nach dem Tod beginnt derselbe Raum von vorn. Die Option geht auch im Release; mit `--seed` und `--give` lässt sie sich kombinieren, nicht mit `--level` oder `--showroom`:

```
dotnet run --project src/SilverAndAsh.Game -- --room assets/rooms/combat/l-hall.txt
```

Mit `--no-enemies` bleibt der Raum **ohne Gegner** (Kisten gibt es trotzdem), um ihn in Ruhe anzusehen:

```
dotnet run --project src/SilverAndAsh.Game -- --room assets/rooms/combat/l-hall.txt --no-enemies
```

Das **Foto vom Eingang** (nur Debug-Version, siehe [foto-skript.md](foto-skript.md)) zeigt das erste Bild im Türplatz und heißt nach der Vorlage, hier `room-combat-l-hall.png`:

```
dotnet run --project src/SilverAndAsh.Game -- --room assets/rooms/combat/l-hall.txt --photo entrance --out fotos
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
| `combat` | Kampfraum | 6 m | groß und verwinkelt (10–18 Zellen je Richtung), mind. 30 freie Felder abseits der Türen |
| `corridor` | Gang | 3,5 m | schmal, gern mit Knick |
| `boss` | Bossraum | 6 m | **genau ein** Türplatz (gilt später für alle Räume mit nur einem Eingang) |

Den **Vorraum** (`RoomKind.Antechamber`, Issue #565) gibt es nicht als Ordner: Seine Form ist fest eingebaut ([`Antechamber.cs`](../src/SilverAndAsh.Core/World/Antechamber.cs)), ein Ordner `antechamber` wird abgelehnt.

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
- `G` und `B` sind in Vorlagen erlaubt, werden aber **nicht mehr benutzt**: Seit dem Spawn-Budget stehen Gegner auf zufälligen freien Feldern (siehe „Arenen und Schwierigkeit“). Die mitgelieferten Vorlagen haben keine mehr.
- `C` ist ein **Kistenplatz** (Boden), nur in Kampf-, Schatz- und Secret-Räumen. Ob dort wirklich eine Kiste steht, entscheidet der Seed (siehe „Kisten“). Jede Kampf-Vorlage hat einen, jede Schatz-Vorlage einen oder zwei.
- **Schatzräume** (`assets/rooms/treasure/`) und **Secret Rooms** (`assets/rooms/secret/`) haben **genau einen** Türplatz, **keine** Gegnerplätze und **mindestens einen** Kistenplatz.
- Sie sind **Schutzräume**: Gegner betreten sie nie, auch nicht ihre Tür, und sehen keinen Spieler darin. Für Bewegung, Sicht und Wegsuche der Gegner sind diese Felder Wand (`GameWorld.EnemyLevel`).
- `P` gibt es **nur im Startraum**, Trainingspuppen (`D`) gar nicht.
- `T` ist **Gorms Platz** und nur im Bossraum erlaubt, höchstens einmal. Fehlt er, steht Gorm auf dem Feld nahe der Raummitte. Die Raummitte selbst bleibt frei für die Treppe.
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

1. **Startraum** hinlegen (zufällige Vorlage, gedreht/gespiegelt). Er bekommt **genau eine Tür**: Sobald der erste Raum an ihm hängt, bleiben seine übrigen Türplätze Wand. So liegt der Ausgang je Seed in einer anderen Richtung, und durch den Startraum führt keine Schleife.
2. **Kampfräume** an freie Türplätze anhängen, bis die Zahl aus `balance.json` (`layout.combatRoomsMin` … `combatRoomsMax`) erreicht ist. Mit der Chance `layout.corridorChance` liegt ein **Gang** dazwischen, sonst teilen sich die Räume eine Wand mit Tür. Bei `0` gibt es gar keine Gänge.
3. **Schleifen** (höchstens `layout.maxLoops`): Der Generator sucht gezielt Stellen, an denen ein neuer Raum – oder ein Raum plus Gang, oder nur ein Gang – zwei freie Türplätze verbindet. So gibt es einen zweiten Weg herum. Zwei Räume, die schon verbunden sind (Tür oder Gang), werden nicht ein zweites Mal verbunden.
4. **Vorraum und Bossraum** hinter einem der Räume, die am weitesten vom Start liegen (Issue #565). Der **Vorraum** ist immer gleich: 4 × 5 Zellen (8 × 10 m), 6 m hoch, ohne Gegner, ohne Truhe, ohne Brunnen, nie eine Arena. Sein **Eingang** sitzt mittig an einer langen Seite (5 Zellen), die **Bosstür** an der anstoßenden kurzen Seite (4 Zellen) auf dem mittleren Feld, das näher am Eingang liegt – der Weg zum Boss knickt also einmal. Der Vorraum wird gedreht und gespiegelt, bis er passt; dafür wird keine Zufallszahl gezogen. Direkt dahinter (nie über einen Gang) liegt der **Bossraum**. Er ist damit **immer der entfernteste Raum** und hat **genau einen Eingang**. Passt hinter keinen der entferntesten Räume ein Vorraum samt Bossraum, scheitert der Versuch und der Generator beginnt neu (siehe unten); klappt das 50-mal nicht, bricht er mit `InvalidOperationException` ab.
5. **Secret Rooms** (`layout.secretRooms`, Standard 1): direkt an einem **Kampfraum**, nie über einen Gang, näher am Start als der Boss. Ihr Zugang ist keine Tür, sondern eine **rissige Wand** (`DoorKind.Cracked`).
6. **Schatzräume** zuletzt: `layout.treasureRooms` mit normaler Tür und `layout.lockedTreasureRooms` hinter einer **verschlossenen Tür** (Standard je 1). Sie sind **Sackgassen**, hängen **nie am Startraum** und liegen immer **näher am Start als der Boss**. Hinter einem Schloss liegt so nie etwas Wichtiges: Der Boss ist immer ohne Schlüssel erreichbar.

Regeln, die immer gelten (und über viele Seeds getestet werden):

- Räume **überlappen nicht**. Sie dürfen sich eine Wand teilen, aber Boden berührt nie Boden eines anderen Raums – auch nicht über Eck.
- Räume sind **nur über Türen** verbunden; jede Tür verbindet **genau zwei Räume**.
- **Alles ist erreichbar.**
- Ein **Gang** führt immer von einem Raum zu einem anderen, nie ins Leere und nie direkt in einen weiteren Gang.
- Räume mit **nur einem Eingang** (Boss, Schatz und Secret, später Händler, Altar) bekommen nie eine zweite Tür.
- Der **Vorraum** hat genau zwei Türen (Eingang und Bosstür) und bekommt keine Schleife.
- Der **Boss-Tor-Teleporter** steht im Vorraum in der Ecke gegenüber der Bosstür (das erlaubte Feld, das am weitesten von ihr weg ist) und ist zur Raummitte gedreht. Das ist eine **vorläufige Entscheidung der Leitung** (Issue #565).
- Ungenutzte Türplätze bleiben Wand. Türen sind 3,5 m hoch, auch in hohen Räumen.
- Bleibt der Generator stecken (kein Raum passt mehr), versucht er es mit den nächsten Zufallszahlen **desselben** Seeds noch einmal – das Ergebnis bleibt also gleich.

Zu jeder erzeugten Ebene gibt es neben den Zellen eine Liste der **Räume** (Art, Fläche, Abstand zum Start in Räumen) und der **Türen** (Lage, welche zwei Räume). Die Gegner setzt danach [`EnemyPlacement`](../src/SilverAndAsh.Core/World/EnemyPlacement.cs) mit dem Spawn-Budget (siehe „Arenen und Schwierigkeit“).

## Türen im Spiel

Jede Tür des Generators wird im Spiel zu einer echten Tür ([`Door.cs`](../src/SilverAndAsh.Core/Gameplay/Door.cs)):

- **Alle Türen starten geschlossen.** Eine geschlossene Tür füllt ihre Türzelle aus. Die Welt behandelt diese Zelle dann als Wand, deshalb blockiert sie Laufen, Sicht der Gegner, Schüsse, Blutgeschosse und die Wegsuche der Gegner, ganz ohne Sonderregeln.
- **F** öffnet die Tür, auf die man schaut, aus höchstens 2 m (von den Augen bis zur Türfläche). Dafür erscheint der Hinweis „[F] Open“. Offen bleibt offen; ein neuer Durchlauf beginnt wieder mit geschlossenen Türen.
- **Arten:** normal (Holz), Boss (die Tür zum Bossraum) und verschlossen (Eisen mit Vorhängeschloss: F öffnet sie nur mit Schlüssel und verbraucht einen).
- **Rissige Wand** (Zugang zum Secret Room): sieht aus wie Wand mit feinen Rissen und ist in jeder Hinsicht Wand. F wirkt nicht, die Arena vergittert sie nicht, und die Karte zeigt sie nicht. Nur ein **voll aufgeladener Klingenhieb** aus Klingenreichweite bricht sie auf; danach ist die Öffnung frei. Normale Hiebe und Schüsse tun nichts.
- **Gitter:** Jede Tür kann zusätzlich **vergittert** sein (Arena, siehe unten). Dann blockiert sie, auch wenn sie offen ist, und F wirkt nicht.
- F ist die **eine** Interaktionstaste. Alles, womit man interagieren kann, ist ein `IInteractable` (heute Türen und Kisten, später Personen).

## Kisten

Kisten setzt [`ChestPlacement`](../src/SilverAndAsh.Core/World/ChestPlacement.cs) nach den Gegnern auf die Kistenplätze `C` (ADR 0006):

- Etwa **jeder 4. Kampfraum** bekommt eine Kiste (`chests.combatRoomChance`). Sie **steigt aus dem Boden, wenn der Raum geräumt ist**.
- **Secret Rooms** bekommen **eine** stehende Kiste nach `secretShare`: Guard's 35 %, King's 50 %, Vampire's 15 %. Es gibt keine Beggar's, aber auch keine sichere Vampire's.
- **Schatzräume** bekommen `chests.treasureChestsMin` … `treasureChestsMax` Kisten (Standard 1–2), die von Anfang an stehen. Die Stufe folgt `treasureShare` bzw. hinter dem Schloss `lockedTreasureShare` – dort ist es besser.
- **Stufe:** Beggar's, Guard's, King's oder Vampire's Chest. Im nächsten Kampfraum gelten die `combatShareNear`-Anteile, im entferntesten die `combatShareFar`-Anteile, dazwischen gleitend. Weiter hinten wird es also etwas besser.
- **Verschluss und Inhalt** (Silber, vielleicht eine Blutphiole) werden je Stufe gleich beim Bauen der Ebene gewürfelt: **gleicher Seed = gleiche Kisten**, egal wie man spielt.
- **Öffnen** mit F (aus der Nähe auf die Kiste schauen). Eine verschlossene Kiste braucht einen Schlüssel. Der Inhalt springt zum Spieler heraus und wird durch Drüberlaufen aufgenommen; die offene Kiste bleibt leer stehen.
- Alle Werte stehen in `balance.json` unter `chests`, pro Stufe ein eigener Block.

## Arenen und Schwierigkeit

**Gegner aufstellen** ([`EnemyPlacement`](../src/SilverAndAsh.Core/World/EnemyPlacement.cs)):

- Nur **Kampf-** und **Bossräume** bekommen Gegner. Startraum, Gänge, Schatz- und Secret-Räume bleiben leer.
- **Spawn-Budget:** Jeder Kampfraum bekommt ein Budget. Es steigt mit dem **Abstand zum Start**: `budgetNear` im nächsten Kampfraum, `budgetFar` im entferntesten, dazwischen gleichmäßig. Der Bossraum hat sein eigenes (`budgetBoss`, Standard 0: Gorm kämpft allein).
- Der Raum **kauft** davon Gegner: Ein Ghul kostet `ghoulCost`, eine Fledermaus `batCost`. Welche Art als Nächstes kommt, entscheiden **Gewichte**, die ebenfalls von „near“ nach „far“ gleiten; weiter hinten gibt es mehr Fledermäuse.
- **Freie Plätze:** Jeder Gegner steht auf einem zufälligen Bodenfeld des Raums, mindestens `doorDistance` (4 m) von seinen Türen und `spacing` (2,5 m) von anderen Gegnern entfernt. Gibt es keinen freien Platz mehr, endet der Einkauf.
- So sieht derselbe Raum in jedem Durchlauf anders aus. Jeder Kampfraum bekommt **mindestens einen** Gegner. **Gleicher Seed = gleiche Gegner.**
- Alle Werte stehen in `balance.json` unter `difficulty`; der Balance-Report zeigt Budget und Gegner pro Kampfraum.

**Arena** ([`RoomState`](../src/SilverAndAsh.Core/Gameplay/RoomState.cs), Logik in `GameWorld`):

1. Gegner in **Kampfräumen** sind vor dem Verriegeln **noch nicht da** (Issue #424): Sie warten auf ihrer Startzelle im Boden (Ghule) bzw. in der Decke (Fledermäuse), werden nicht gezeichnet, sind kein Ziel und kein Hindernis. Durch die offene Tür kann man also niemanden treffen oder herauslocken. Ihre Startzellen hält die Platzierung `difficulty.doorDistance` von den Türen fern. Beim Verriegeln sind die Plätze der Spieler bekannt (Issue #445): Liegt eine Startzelle näher als `difficulty.playerDistance` an irgendeinem Spieler, kommt der Gegner auf der nächsten freien Zelle des Raums heraus, die diesen Abstand zu allen Spielern und `doorDistance` zu den Türen hält; gibt es keine, auf der freien Zelle, die am weitesten von allen Spielern entfernt ist. „Frei“ heißt nur, dass keine andere Figur des Raums auf der Zelle steht. Ein ausweichender Gegner hält deshalb `difficulty.spacing` zu den anderen nicht zwingend ein. Gegner im **Bossraum** (Gorm) **schlafen** wie bisher: Sie stehen still, bis der Raum betreten wird; trifft man einen, wacht der Raum auf, wird aber nicht versiegelt.
2. **Betreten** heißt: mit dem **ganzen Körper** im Raum, nicht mehr in der Türzelle. Sonst würde das Gitter auf den Spieler fallen.
3. Beim **ersten Betreten** eines Kampf- oder Bossraums mit lebenden Gegnern fallen an **allen** seinen Türen Gitter herunter, die Gegner erwachen (Ereignis `RoomSealed`). Im Kampfraum **erscheint** dabei die erste **Welle** (Ereignis `WaveEmerged`, noch ohne Geräusch): Ghule wachsen aus dem Boden, Fledermäuse sinken aus der Decke, in `difficulty.emergeTime` Sekunden. Solange greifen sie nicht an und nehmen keinen Schaden. Ein Kampfraum hat eine Liste von Wellen; heute genau eine mit allen seinen Gegnern. Ist eine Welle tot und eine weitere übrig, erscheint die nächste.
   - **Gegner im Durchgang:** Steht beim Versiegeln ein Gegner (jeder Art) ganz oder teilweise in einer Türzelle des Raums, wird er unverletzt knapp hinter die Tür in den Raum geschoben (`GameWorld.PushOutOf`). Quer zum Durchgang bleibt er, wo er war, aber innerhalb der Breite der Türzelle, damit er nicht in einer Wand landet.
   - **Vorläufige Entscheidung der Leitung (Issue #572, Urteil beim Spieltest):** Das gilt auch für **fremde** Gegner, etwa einen Ghul aus dem Gang oder dem Startraum, der einem Spieler bis in den Durchgang gefolgt ist. Er landet in der Arena und kämpft dort mit, zählt aber nicht zu ihren Gegnern und hält das Räumen nicht auf.
4. Sind alle Gegner des Raums tot (alle Wellen), ist er **geräumt** (`RoomCleared`). Die Gitter heben sich, die Tür, durch die man kam, ist wieder frei, **sobald ihr Gitter über `grates.blockHeight` hängt** (eine Tür ohne Gitter sofort); die anderen lassen sich mit F öffnen.
5. Ein geräumter Raum versiegelt sich **nie wieder**. Ein Kampfraum ohne Gegner gilt beim Betreten sofort als geräumt.

## Karte

Ein Raum (auch ein Gang) gilt als **entdeckt**, sobald ein Spieler auf einem seiner Bodenfelder steht – die Türzelle allein reicht nicht. Die **Karte auf M** zeigt nur entdeckte Räume, dazu die Türen, die an einen entdeckten Raum grenzen, **liegen gelassene Blutphiolen** in entdeckten Räumen (roter Punkt, `GameWorld.KnownBloodVials`) und die eigene Position mit Blickrichtung. Ein neuer Durchlauf beginnt ohne Entdeckungen. Handgebaute Level haben keine Räume, also auch keine Karte.
