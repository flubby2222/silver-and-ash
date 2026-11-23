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
| `slam` | Die Bosstür schlägt zu, während ein Körper in ihrem Durchgang steht (#629): nacheinander ein Spieler, ein Ghul, eine Fledermaus und Gorm, jeweils an jeder Stelle quer zum Durchgang alle 5 cm. Die Tiefe längs des Durchgangs ist die erste freie Stelle; wo der Körper nirgends frei steht, zählt der Bot die Stelle als übersprungen. Der Körper wird jeden Tick neu an seinen Platz gestellt, bis die Tür sperrt; danach darf er nicht im Stein des Portals stecken. | etwa 45 s |
| `bat` | Eine wache Fledermaus am Bossportal (#627, #629): an jeder freien Stelle eines Rasters vor und hinter dem Portal in acht Richtungen weggeschlagen; um einen Spieler herum geflogen, der an vier Stellen im Portal steht; tot auf einen Sockel des Portals fallen gelassen, bis sie verschwunden ist. | etwa 25 s |
| `seeds` | Baut jede Ebene zweimal und vergleicht sie (gleicher Seed muss gleiche Ebene geben) und zählt, wie viele Versuche der Generator brauchte. | etwa 3 s; 1 000 Seeds etwa 100 s |
| `all` | alle sechs nacheinander | etwa 3 min |

Nebenbei prüft der Bot nach **jedem Tick**, dass weder ein Spieler noch ein Gegner (Ghul, Fledermaus, Gorm) in einer geschlossenen Tür oder im Stein eines Bossportals steckt und kein Spieler in einer festen Zelle. Für den Stein prüft er bei jedem Körper die Achse und 24 Punkte knapp innerhalb seines Rands, alle 10 cm von den Füßen bis zum Kopf (`BossPortal.Contains`); ein Körper, der den Stein nur berührt, ist kein Befund. Auch eine tote Fledermaus zählt, solange sie noch fällt oder liegt.

**Ein neuer Modus ist eine Zeile** in der Tabelle `modes` oben in `Program.cs` (Name, eine Zeile Hilfe, was er je Seed spielt), kein weiterer Zweig. Hilfe, Aufruf-Prüfung und `all` lesen dieselbe Tabelle.

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
- `IEnemy.PlaceAt(Vector3)` – einen Ghul, eine Fledermaus oder Gorm an eine Stelle setzen (`slam`, `bat`).
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
- `<ersetzen>` wird wörtlich eingesetzt. Mit `--regex` setzen `$1`, `$2` … oder `${1}` die Gruppen (Klammern) aus `<suchen>` ein; `$0` den ganzen Treffer, eine nicht getroffene Gruppe gibt leeren Text; `\$` schreibt ein einfaches `$`, `\\` einen einfachen Backslash, jeder andere Backslash bleibt stehen (`"\n"` bleibt `"\n"`) (Issue #604). Verweist `<ersetzen>` auf eine Gruppe, die es nicht gibt, bricht das Skript vor dem Kontrolllauf mit Exit-Code `2` ab. Beispiel – dieselbe Probe wie oben, der Anfang der Zeile bleibt per `${1}` stehen:

  ```
  tools/gegenprobe.sh --regex src/SilverAndAsh.Core/Gameplay/GameWorld.cs \
    '(GameWorld Restarted[(][)] => new[(])_levelWithOpenDoors,' '${1}Level,' GrateTests
  ```

  Aus PowerShell 5.1 `<ersetzen>` in einfache Anführungszeichen setzen, sonst ersetzt PowerShell `$1` selbst. Klassen wie `[(]` und `[.]` statt `\(` und `\.` sind dort sicherer, weil Backslashes beim Weiterreichen an `bash.exe` verloren gehen können.

  Den Ersetzungsschritt erledigt `tools/gegenprobe-replace.pl`. Der Selbsttest `tools/gegenprobe.tests.sh` (Git Bash, ohne dotnet, wenige Sekunden) prüft ihn an kleinen Dateien: wörtlicher Modus, `$1`, `${1}`, `$0`, `\n`, `\$`, `\\`, nicht getroffene und fehlende Gruppe, `--all`, CRLF. Nach jeder Änderung an einem der beiden Skripte laufen lassen; Exit-Code `0` heißt alles bestanden.
- `<testfilter>` ist ein Klassen- oder Testnamen-Teil (wird zu `FullyQualifiedName~<filter>`) oder ein vollständiger Filter für `dotnet test`, wenn er `=`, `~`, `|`, `&` oder `!` enthält.
- `--project <pfad>` wählt ein anderes Testprojekt (Standard `tests/SilverAndAsh.Core.Tests`), `--name <text>` den Namen in der Ausgabe, `--help` zeigt die Hilfe.

Was man kaputt macht: nicht nur eine Bedingung entfernen (`&& player.IsDead` anhängen, einen Aufruf streichen), sondern auch einen Schwellwert abschwächen, etwa `>= x` zu `>= 0` oder eine Grenze auf einen offensichtlich falschen Wert. Ein Test, der nur „die Regel gibt es“ prüft, bleibt dabei grün; erst wenn er auch beim abgeschwächten Schwellwert rot wird, prüft er die Grenze selbst.

Ablauf und Schutz:

1. **Nur ein Lauf je Arbeitsordner** (Issue #604): Läuft im selben Arbeitsordner schon eine Gegenprobe, bricht ein zweiter Lauf sofort mit Exit-Code `2` und der Meldung „another gegenprobe is already running in this working folder“ ab – sonst hielte er die gerade kaputte Datei für das Original. Proben also nacheinander starten; verschiedene Worktrees stören sich nicht. Die Sperre ist der Ordner `gegenprobe.lock` im Git-Ordner des Arbeitsordners (bei einem Worktree `.git/worktrees/<name>/`), `git status` zeigt sie nicht. Das Skript entfernt sie am Ende, auch bei Strg+C. Bleibt sie nach einem harten Abbruch liegen, erkennt der nächste Lauf am fehlenden Prozess, dass sie veraltet ist, und übernimmt sie. Hat inzwischen ein anderes Programm dieselbe Prozessnummer bekommen, greift die Sperre weiter: Läuft sicher keine Gegenprobe, den Ordner aus der Meldung löschen (`rm -rf '<pfad>'`), dann neu starten.
2. Bei **unsauberem Arbeitsordner** startet das Skript nicht – sonst könnte das Zurücksetzen fremde Änderungen treffen. Vorher committen.
3. **Kontrolllauf** ohne Änderung: Die gefilterten Tests müssen existieren und grün sein.
4. Die Stelle wird ersetzt, die Tests laufen noch einmal; rote Tests werden mit Namen aufgezählt.
5. Die Datei wird **bytegleich zurückgesetzt** – auch bei Strg+C, Fehler oder Abbruch. Danach prüft das Skript, dass der Arbeitsordner wieder sauber ist.

Ausgabe, gekürzt:

```
   control run: 25 test(s), all green
   result: CAUGHT - 2 of 25 test(s) red:
     ...GrateTests.A_player_who_dies_in_the_sealed_arena_leaves_it_sealed_and_the_restart_brings_the_grates_up
     ...GrateTests.An_arena_next_to_another_seals_again_the_same_way_after_death_and_restart
```

**Exit-Code:** `0` gefangen (mindestens ein Test rot), `1` überlebt (alle grün – die Tests prüfen die Stelle nicht), `2` konnte nicht laufen (anderer Lauf im selben Arbeitsordner, Arbeitsordner unsauber, Text nicht gefunden, Kontrolllauf rot …), `3` die Änderung kompiliert nicht (dann sagt das Ergebnis nichts über die Tests).

### Mehrere Gegenproben aus einer Liste

Sollen viele Stellen geprüft werden, gehören sie in eine Listendatei statt in einzelne Aufrufe von Hand. Eine Zeile je Probe, die vier Felder durch Tabulatoren getrennt (Datei, Suchen, Ersetzen, Testfilter); Zeilen mit `#` am Anfang sind Kommentare. Die Proben laufen **nacheinander** (die Sperre erlaubt nur einen Lauf je Arbeitsordner), am Ende steht je Probe eine Zeile mit dem Exit-Code. In Git Bash:

```
# proben.tsv (nicht ins Repository legen), z. B.:
# src/SilverAndAsh.Core/Gameplay/GameWorld.cs<TAB>GameWorld Restarted() => new(_levelWithOpenDoors,<TAB>GameWorld Restarted() => new(Level,<TAB>GrateTests
while IFS=$'\t' read -r datei suchen ersetzen filter; do
  case "$datei" in ''|'#'*) continue ;; esac
  tools/gegenprobe.sh "$datei" "$suchen" "$ersetzen" "$filter" < /dev/null
  echo "$? $datei | $suchen" >> ergebnis.txt
done < proben.tsv
cat ergebnis.txt
```

`< /dev/null` hält die Testläufe davon ab, die restlichen Zeilen der Liste zu lesen. Für `--regex` oder `--all` ein fünftes Feld mit den Optionen anhängen und ungequotet vor `"$datei"` einsetzen. Exit-Code `1` in `ergebnis.txt` heißt überlebt – siehe nächster Abschnitt.

### Überlebt? Erst breit wiederholen

Eine überlebende Gegenprobe ist erst dann eine **Lücke**, wenn sie auch mit einem breiten Filter überlebt. Der enge Filter (eine Testklasse) trifft oft nicht die Tests, die die Stelle wirklich prüfen: In PR #623 überlebte `break;` statt des Aufrufs von `PushedAcross` mit dem Filter der neuen Testklasse, obwohl andere Tests die Stelle fingen. Darum vor dem Wort „Lücke“ dieselbe Probe noch einmal mit einem breiten Filter laufen lassen, etwa dem Namensraum (`SilverAndAsh.Core.Tests.Gameplay`) oder allen Tests (`FullyQualifiedName~SilverAndAsh`, dauert einige Minuten). Erst wenn sie auch dann überlebt, ist es eine Lücke; im PR beide Läufe mit Filter nennen.

### Pflichtfall bei Kollisionsregeln

Ändert ein PR eine Kollisionsregel (was einen Körper aufhält, hinauf- oder hinunterlässt), braucht er einen Test **für jede Körperart mit ihrer echten Stufenhöhe**: gehende Körper (Spieler, Ghul, Gorm) mit der Stufenhöhe aus `CharacterCollision` (0,4 m), fliegende (Fledermaus) mit `FlyingStepHeight` (unendlich). Ein Test nur mit dem Spieler reicht nicht: In PR #623 hing es an `CharacterCollision.StepOntoStone`, ob eine Fledermaus mit ihrer unendlichen Stufenhöhe auf den Stein des Portals steigt; ein Test nur mit der Stufenhöhe des Spielers sieht diesen Zweig nie. Die Höhe nicht als Zahl in den Test schreiben, sondern dort holen, wo das Spiel sie hernimmt.

### Den Bot gegenprüfen

Den Bot kann man genauso gegenprüfen: eine Spielregel im Core kaputt machen, den passenden Modus laufen lassen, er muss `FINDING`-Zeilen und Exit-Code `1` liefern. Danach die Datei mit `git restore <datei>` zurücksetzen. Überlebt die Probe, zuerst das Raster prüfen: Ein Fehler, der nur an einer schmalen Stelle auftritt (etwa am Rand eines Bogens, #629), fängt der Bot nur, wenn sein Raster bis dorthin reicht. `slam` geht deshalb quer zum Durchgang alle 5 cm und nimmt die erste freie Tiefe, statt feste Punkte abzulaufen.
