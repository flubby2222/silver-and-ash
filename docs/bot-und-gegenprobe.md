# Bot und Gegenprobe

Zwei Werkzeuge für Tester und Reviewer (Issue #573), damit niemand für jeden PR einen eigenen Bot oder ein eigenes Mutations-Skript schreibt:

- **`tools/SilverAndAsh.BotRun`** spielt Ebene 0 ohne Fenster durch: Arena-Türen, Tod und Neustart, der Weg zum Boss, und prüft viele Seeds auf Bau-Fehler.
- **`tools/gegenprobe.sh`** macht eine Stelle im Code absichtlich kaputt, lässt gefilterte Tests laufen, zählt die roten und setzt die Datei zurück. Das ist die *Gegenprobe* aus CLAUDE.md: Ein Test ist nur etwas wert, wenn er rot wird, sobald die geprüfte Stelle nicht mehr stimmt.

## Bot (`tools/SilverAndAsh.BotRun`)

Windows PowerShell 5.1 und Git Bash, aus dem Repository-Ordner:

```
dotnet run --project tools/SilverAndAsh.BotRun -c Release -- arena --seeds 1..6
```

```
dotnet run --project tools/SilverAndAsh.BotRun -c Release -- all --seeds 1..6
dotnet run --project tools/SilverAndAsh.BotRun -c Release -- seeds --seeds 1..1000
```

Aufbau: `<modus> [--seeds VON..BIS] [--repo ORDNER]`. Ohne `--seeds` laufen die Seeds 1 bis 6; `7` oder `1-6` gehen auch. Ohne `--repo` sucht der Bot vom aktuellen Ordner aufwärts nach `assets/rooms` und nimmt Raumvorlagen und Balancing (`assets/balance.json`, wie das Spiel) von dort. Bei einer Balancing-Runde spielt er also schon mit den neuen Werten; Warnungen beim Lesen der Datei stehen auf der Fehlerausgabe.

| Modus | Was er spielt | Dauer (Seeds 1–6) |
|---|---|---|
| `arena` | Für jede Tür eines Kampfraums: hineingehen, das Gitter muss fallen und der Raum versiegeln; vier Fluchtversuche (sofort, sofort mit Sprint, nach 6 Ticks mit Sprint, nach 14 Ticks) müssen drinnen enden; Raum leeren, die Gitter gehen einmal hoch, hinausgehen. Zwei Kampfräume nebeneinander nacheinander. Die Bosstür hat kein Gitter: Sie schwingt zu und muss spätestens ab `doors.bossShutTick` (Balancing) sperren; der Spieler läuft sofort zurück und darf nicht in den Vorraum kommen (#566, #567). | etwa 2 min |
| `death` | Tod in jeder versiegelten Arena und im Bosskampf, danach Neustart: keine Tür verriegelt, kein Raum versiegelt, die Arena versiegelt beim Betreten wieder (#560, #578); nach dem Bosskampf ist der Vorraum frisch und ein zweiter Lauf erreicht den Boss wieder. | etwa 30 s |
| `boss` | Der ganze Weg: Vorraum, Boss-Tor-Teleporter freischalten und hin- und herreisen, Bosstür mit F öffnen, Bossraum versiegelt, die Bosstür sperrt spätestens ab `doors.bossShutTick`, Gorm besiegen, Treppe hinauf, gewonnen. | etwa 5 s |
| `seeds` | Baut jede Ebene zweimal und vergleicht sie (gleicher Seed muss gleiche Ebene geben) und zählt, wie viele Versuche der Generator brauchte. | etwa 3 s; 1 000 Seeds etwa 100 s |
| `all` | alle vier nacheinander | etwa 2 min |

Nebenbei prüft der Bot nach **jedem Tick**, dass weder ein Spieler noch ein Gegner in einer geschlossenen Tür oder (Spieler) in einer festen Zelle steckt.

**Ausgabe** (Englisch): Jeder Befund steht sofort als eigene Zeile da, damit man ihn bei langen Läufen früh sieht:

```
FINDING [death] seed 3 door 12 death: restarted level does not seal again
```

Am Ende stehen die Zähler, sortiert und ohne Zeiten, und eine Zusammenfassung:

```
  arena: cleared: walked out: 152
  arena: sealed on walking in: 760
SUMMARY mode arena, seeds 1..6: 0 finding(s)
```

Wirft das Spiel in einem Szenario eine Ausnahme, wird daraus ein Befund mit Szenario-Angabe; die übrigen Szenarien laufen weiter.

**Exit-Code:** `0` keine Befunde, `1` Befunde, `2` falscher Aufruf oder das Werkzeug passt nicht mehr zum Core (siehe unten).

### PR gegen `main` vergleichen

Weil die Zähler ohne Zeiten ausgegeben werden, lassen sich zwei Läufe direkt vergleichen. Den Stand von `main` holt man in einen zweiten Arbeitsordner (*Worktree*: ein weiterer ausgecheckter Stand desselben Repositorys):

```
git fetch origin
git worktree add ../vergleich-main origin/main
dotnet run --project ../vergleich-main/tools/SilverAndAsh.BotRun -c Release -- arena --repo ../vergleich-main > main.txt
dotnet run --project tools/SilverAndAsh.BotRun -c Release -- arena > pr.txt
git diff --no-index main.txt pr.txt
git worktree remove ../vergleich-main
```

`main.txt` und `pr.txt` nicht ins Repository legen. Solange `main` das Werkzeug noch nicht hat, geht das erst nach dem Merge von #573.

### Warum Reflection

Der Bot ändert nichts am Spielcode. Zwei Dinge sind im Core aber `internal` und werden per *Reflection* aufgerufen (Zugriff auf nicht öffentliche Teile über ihren Namen zur Laufzeit):

- `Player.PlaceAt` und `Player.AddKeys` – den Spieler vor eine Tür stellen und ihm Schlüssel geben, wie es auch das Foto-Studio tut.
- Für die Zahl der Generator-Versuche (`seeds`) die Innereien von `LevelGenerator`. Der Bot prüft dabei selbst, dass die so gebaute Ebene genau der von `LevelGenerator.Generate` gleicht.

Ändert jemand diese Stellen im Core, bricht der Bot laut ab (Exit-Code `2`, Meldung „… is not in SilverAndAsh.Core any more“) statt still etwas anderes zu prüfen. Nur die Versuchszählung fällt weich aus: Dann fehlt die Verteilung, und auf der Fehlerausgabe steht ein Hinweis.

Das Projekt steht in `SilverAndAsh.slnx`, damit die CI es gegen den aktuellen Core baut. Ins Spieler-Paket kommt es nicht, weil das Release nur `src/SilverAndAsh.Game` veröffentlicht.

## Gegenprobe (`tools/gegenprobe.sh`)

Ein Bash-Skript, also in **Git Bash**:

```
tools/gegenprobe.sh src/SilverAndAsh.Core/Gameplay/GameWorld.cs \
  'GameWorld Restarted() => new(_levelWithOpenDoors,' 'GameWorld Restarted() => new(Level,' GrateTests
```

Aus **Windows PowerShell 5.1** über die Bash von Git for Windows (einfache Anführungszeichen, damit PowerShell nichts ersetzt):

```
& "$env:ProgramFiles\Git\bin\bash.exe" tools/gegenprobe.sh src/SilverAndAsh.Core/Gameplay/GameWorld.cs 'GameWorld Restarted() => new(_levelWithOpenDoors,' 'GameWorld Restarted() => new(Level,' GrateTests
```

Aufbau: `tools/gegenprobe.sh [optionen] <datei> <suchen> <ersetzen> <testfilter>`

- `<suchen>` wird wörtlich gesucht und muss **genau einmal** vorkommen (`--all`: jede Stelle; `--regex`: Perl-Ausdruck). Die Quelldateien haben CRLF-Zeilenenden: Ein Suchtext über mehrere Zeilen findet wörtlich nichts (das Skript bricht dann mit „not found“ ab); dafür `--regex` nehmen und die Zeilen mit `\r?\n` verbinden.
- `<testfilter>` ist ein Klassen- oder Testnamen-Teil (wird zu `FullyQualifiedName~<filter>`) oder ein vollständiger Filter für `dotnet test`, wenn er `=`, `~`, `|`, `&` oder `!` enthält.
- `--project <pfad>` wählt ein anderes Testprojekt (Standard `tests/SilverAndAsh.Core.Tests`), `--name <text>` den Namen in der Ausgabe, `--help` zeigt die Hilfe.

Ablauf und Schutz:

1. Bei **unsauberem Arbeitsordner** startet das Skript nicht – sonst könnte das Zurücksetzen fremde Änderungen treffen. Vorher committen.
2. **Kontrolllauf** ohne Änderung: Die gefilterten Tests müssen existieren und grün sein.
3. Die Stelle wird ersetzt, die Tests laufen noch einmal; rote Tests werden mit Namen aufgezählt.
4. Die Datei wird **bytegleich zurückgesetzt** – auch bei Strg+C, Fehler oder Abbruch. Danach prüft das Skript, dass der Arbeitsordner wieder sauber ist.

Ausgabe, gekürzt:

```
   control run: 25 test(s), all green
   result: CAUGHT - 2 of 25 test(s) red:
     ...GrateTests.A_player_who_dies_in_the_sealed_arena_leaves_it_sealed_and_the_restart_brings_the_grates_up
     ...GrateTests.An_arena_next_to_another_seals_again_the_same_way_after_death_and_restart
```

**Exit-Code:** `0` gefangen (mindestens ein Test rot), `1` überlebt (alle grün – die Tests prüfen die Stelle nicht), `2` konnte nicht laufen (Arbeitsordner unsauber, Text nicht gefunden, Kontrolllauf rot …), `3` die Änderung kompiliert nicht (dann sagt das Ergebnis nichts über die Tests).

Den Bot kann man genauso gegenprüfen: eine Spielregel im Core kaputt machen, den passenden Modus laufen lassen, er muss `FINDING`-Zeilen und Exit-Code `1` liefern. Danach die Datei mit `git restore <datei>` zurücksetzen.
