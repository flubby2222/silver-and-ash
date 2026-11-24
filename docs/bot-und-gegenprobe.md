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
| `slam` | Die Bosstür schlägt zu, während ein Körper in ihrem Durchgang steht (#629): nacheinander ein Spieler, ein Ghul, eine Fledermaus und Gorm, jeweils an jeder Stelle quer zum Durchgang alle 5 cm. Die Tiefe längs des Durchgangs ist die erste freie Stelle; wo der Körper nirgends frei steht, zählt der Bot die Stelle als übersprungen. Der Körper wird jeden Tick neu an seinen Platz gestellt, bis die Tür sperrt; danach darf er nicht im Stein des Portals stecken. Dass er im Tick des Zuschlagens wirklich in der Türbox stand, prüft der Bot daran, wohin die Tür ihn geschoben hat: genau Radius + 1 cm hinter ihre Box (`GameWorld.OnThreshold`), sonst ein Befund „not in the doorway at the slam“ (#647). **Mit Schwung** (#647): Spieler, Ghul und Fledermaus starten alle 10 cm quer zum Durchgang 0,6 m vor der Türbox; der Spieler läuft los, Ghul und Fledermaus (vorher in ihrer Arena geweckt) fliegt ein aufgeladener Keulenschlag hinein. Ein Probelauf misst, wann der Körper im Durchgang ist und wann die Tür sperrt; der echte Lauf lässt ihn so spät los, dass er im Tick des Zuschlagens im Durchgang ist. Gorm läuft nicht mit Schwung hinein (kein Rückstoß, und seine eigenen Schritte hingen vom Starttick ab) – eine bekannte Lücke. | etwa 1,5 min |
| `bat` | Eine wache Fledermaus am Bossportal (#627, #629): an jeder freien Stelle eines Rasters vor und hinter dem Portal in acht Richtungen weggeschlagen; um einen Spieler herum geflogen, der an vier Stellen im Portal steht; tot auf einen Sockel des Portals fallen gelassen, bis sie verschwunden ist. | etwa 25 s |
| `seeds` | Baut jede Ebene zweimal und vergleicht sie (gleicher Seed muss gleiche Ebene geben) und zählt, wie viele Versuche der Generator brauchte. | etwa 3 s; 1 000 Seeds etwa 100 s |
| `all` | alle sechs nacheinander | etwa 5 min |

Nebenbei prüft der Bot nach **jedem Tick**, dass weder ein Spieler noch ein Gegner (Ghul, Fledermaus, Gorm) in einer geschlossenen Tür oder im Stein eines Bossportals steckt und kein Spieler in einer festen Zelle. Für den Stein prüft er bei jedem Körper die Achse und 24 Punkte 5 mm innerhalb seines Rands (für jeden Körper gleich, auch für Gorm), in Höhen von den Füßen bis zum Kopf (`BossPortal.Contains`); ein Körper, der den Stein nur berührt, ist kein Befund. Der Abstand der Höhen folgt aus `BossPortalShape.Blocks`: vier Fünftel des dünnsten Blocks (heute der Kämpfer, 0,125 m, also 0,1 m). `slam` und `bat` prüfen zu Beginn selbst, dass für keine ihrer Körperhöhen ein Block zwischen zwei Messhöhen durchrutscht („the stone check misses the block …“, #647) – bekommt das Portal einen dünneren Block, rücken die Höhen von selbst enger. Auch eine tote Fledermaus zählt, solange sie noch fällt oder liegt.

**Übersprungene Plätze:** `slam` und `bat` lassen Plätze aus, an denen der Körper nicht frei stehen kann, und zählen sie als „… (skipped)“. Jeder solche Zähler hat einen Gegenzähler der gespielten Plätze (`Report.Tally`): Werden je Seed und Körperart (bei `bat` je Teil) auf einer Seite der Portalmitte weniger Plätze gespielt als die Untergrenze, ist das ein Befund („fewer than N place(s) played on a side of the middle“). So endet ein Modus, der nach einer Änderung am Portal nichts mehr prüft, nicht still mit „0 finding(s)“, und ein halb so feines Raster fällt auf. Die Untergrenzen sind gemessen (#647): drei Viertel dessen, was die Seeds 1–6 heute je Seite spielen (aufgerundet).

| Modus, Teil | heute je Seed und Seite (Seeds 1–6) | Untergrenze |
|---|---|---|
| `slam` stehend: Spieler / Ghul / Fledermaus / Gorm | 16 / 14 / 21 / 3 | 12 / 10 / 15 / 2 |
| `slam` mit Schwung: Spieler / Ghul / Fledermaus | 9 / 13 / 17 | 7 / 10 / 13 |
| `bat` Schläge | 36 | 27 |
| `bat` tote Fledermaus | 2 | 2 |

Ändert sich das Portal so, dass weniger Plätze frei sind, die Zähler neu messen und die Untergrenzen im selben PR anpassen (`DoorSlam.Bodies`, `PortalBat` in `tools/SilverAndAsh.BotRun/BossPortalRuns.cs`).

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

Ein Agent, der selbst schon in einem Worktree arbeitet, legt keinen zweiten an. Er holt den Stand von `main` für einzelne Dateien mit `git checkout origin/main -- <pfad>`, lässt den Modus laufen und holt seinen eigenen Stand mit `git checkout HEAD -- <pfad>` zurück.

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
- `--bot` prüft mit einem Bot-Lauf statt mit Tests (#647); das vierte Feld sind dann die Argumente des Bots, siehe [Den Bot gegenprüfen](#den-bot-gegenprüfen).

Was man kaputt macht: nicht nur eine Bedingung entfernen (`&& player.IsDead` anhängen, einen Aufruf streichen), sondern auch einen Schwellwert abschwächen, etwa `>= x` zu `>= 0` oder eine Grenze auf einen offensichtlich falschen Wert. Ein Test, der nur „die Regel gibt es“ prüft, bleibt dabei grün; erst wenn er auch beim abgeschwächten Schwellwert rot wird, prüft er die Grenze selbst. Prüft ein Test eine Eigenschaft nur an Nachbarzellen (etwa die Decke vor und hinter einer Tür), nimmt die Gegenprobe die Zellen selbst aus (Issue #603: `!doorCells.Contains((cell.X, cell.Z)) &&` in die Auswahl der Zellen). Jede neue Toleranz oder Rundungsgrenze im Code wird mit Wert 0 und einmal mit verschobener Grenze gegengeprüft (etwa der Rand, ab dem ein Strahl kurze Schritte macht: `IsNear(point, StepLength)` zu `IsNear(point, 0f)`, Issue #642).

Ablauf und Schutz:

1. **Nur ein Lauf je Arbeitsordner** (Issue #604): Läuft im selben Arbeitsordner schon eine Gegenprobe, bricht ein zweiter Lauf sofort mit Exit-Code `2` und der Meldung „another gegenprobe is already running in this working folder“ ab – sonst hielte er die gerade kaputte Datei für das Original. Proben also nacheinander starten; verschiedene Worktrees stören sich nicht. Die Sperre ist der Ordner `gegenprobe.lock` im Git-Ordner des Arbeitsordners (bei einem Worktree `.git/worktrees/<name>/`), `git status` zeigt sie nicht. Das Skript entfernt sie am Ende, auch bei Strg+C. Bleibt sie nach einem harten Abbruch liegen, erkennt der nächste Lauf am fehlenden Prozess, dass sie veraltet ist, und übernimmt sie (Issue #639, [`tools/gegenprobe-lock.sh`](../tools/gegenprobe-lock.sh)). Die Übernahme gelingt genau einem Lauf, auch wenn zwei gleichzeitig starten: Wer übernimmt, benennt den Sperrordner zuerst mit `mv` um – das schafft nur einer –, prüft im umbenannten Ordner, dass es wirklich die veraltete Sperre ist, und löscht ihn erst dann. Hat er versehentlich eine frische Sperre erwischt, gibt er sie zurück und bricht mit Exit-Code `2` ab. Der Selbsttest `tools/gegenprobe.tests.sh` spielt diesen Wettlauf mit zwei Läufen nach. Hat inzwischen ein anderes Programm dieselbe Prozessnummer bekommen, greift die Sperre weiter: Läuft sicher keine Gegenprobe, den Ordner aus der Meldung löschen (`rm -rf '<pfad>'`), dann neu starten. **Bekannte Grenze:** Treffen sich drei Läufe genau in der Lücke der Übernahme, kann der dritte eine frische, noch leere Sperre (ohne `pid`) mit `mv -T` ersetzen; dann laufen zwei Proben im selben Ordner. Ohne neues Werkzeug lässt sich das nicht schließen, denn eine leere Sperre muss als gehalten gelten (Issue #651) – Proben deshalb nacheinander starten, nicht zu dritt.
2. Bei **unsauberem Arbeitsordner** startet das Skript nicht – sonst könnte das Zurücksetzen fremde Änderungen treffen. Vorher committen.
3. **Kontrolllauf** ohne Änderung: Die gefilterten Tests müssen existieren und grün sein.
4. Die Stelle wird ersetzt, die Tests laufen noch einmal; rote Tests werden mit Namen aufgezählt.
5. Die Datei wird **bytegleich zurückgesetzt** – auch bei Strg+C, Fehler oder Abbruch. Danach prüft das Skript, dass der Arbeitsordner wieder sauber ist.
6. **Die gebaute DLL enthält danach noch die kaputte Stelle.** Wurde die Änderung gebaut, sagt das Skript am Ende „the build output still holds the changed code - build again before running anything with --no-build“. Vor dem nächsten Lauf mit `--no-build` (Tests, Bot) also `dotnet build -c Release`; sonst meldet er auf der alten DLL fälschlich Grün (Issue #639). Nach Exit-Code `3` wurde nichts mit der Änderung gebaut; dann meldet das Skript stattdessen „the change did not compile - the build output is still the one of the control run“ (Issue #651).
7. Auch eine **früh geschlossene Ausgabe** (`… 2>&1 | head`) bricht das Aufräumen nicht ab: Datei und Sperre werden trotzdem zurückgesetzt (Issue #651).

Ausgabe, gekürzt:

```
   control run: 25 test(s), all green
   result: CAUGHT - 2 of 25 test(s) red:
     ...GrateTests.A_player_who_dies_in_the_sealed_arena_leaves_it_sealed_and_the_restart_brings_the_grates_up
     ...GrateTests.An_arena_next_to_another_seals_again_the_same_way_after_death_and_restart
```

**Exit-Code:** `0` gefangen (mindestens ein Test rot), `1` überlebt (alle grün – die Tests prüfen die Stelle nicht), `2` konnte nicht laufen (anderer Lauf im selben Arbeitsordner, Arbeitsordner unsauber, Text nicht gefunden, Kontrolllauf rot …), `3` die Änderung kompiliert nicht (dann sagt das Ergebnis nichts über die Tests).

### Mehrere Gegenproben aus einer Liste

Sollen viele Stellen geprüft werden, gehören sie in eine Listendatei statt in einzelne Aufrufe von Hand. Eine Zeile je Probe, die vier Felder durch Tabulatoren getrennt (Datei, Suchen, Ersetzen, Testfilter); Zeilen mit `#` am Anfang sind Kommentare. Die Proben laufen **nacheinander** (die Sperre erlaubt nur einen Lauf je Arbeitsordner), am Ende steht je Probe eine Zeile mit dem Exit-Code.

**Liste und Ergebnis liegen außerhalb des Repositorys**, hier in `~/gegenproben`. Eine neue Datei im Repository macht den Arbeitsordner unsauber, und jede weitere Probe bricht dann mit Exit-Code `2` ab („the working folder is not clean“). In Git Bash, aus dem Repository-Ordner:

```
ordner=~/gegenproben
mkdir -p "$ordner"
printf '%s\t%s\t%s\t%s\n' \
  src/SilverAndAsh.Core/Gameplay/GameWorld.cs \
  'GameWorld Restarted() => new(_levelWithOpenDoors,' 'GameWorld Restarted() => new(Level,' GrateTests \
  > "$ordner/proben.tsv"
rm -f "$ordner/ergebnis.txt"
while IFS=$'\t' read -r datei suchen ersetzen filter; do
  case "$datei" in ''|'#'*) continue ;; esac
  tools/gegenprobe.sh "$datei" "$suchen" "$ersetzen" "$filter" < /dev/null
  echo "$? $datei | $suchen" >> "$ordner/ergebnis.txt"
done < "$ordner/proben.tsv"
cat "$ordner/ergebnis.txt"
```

Weitere Proben hängt man mit einem weiteren `printf … >> "$ordner/proben.tsv"` an oder schreibt sie mit einem Editor hinein (Tabulatoren, keine Leerzeichen). `< /dev/null` hält die Testläufe davon ab, die restlichen Zeilen der Liste zu lesen. Für `--regex` oder `--all` ein fünftes Feld mit den Optionen anhängen und ungequotet vor `"$datei"` einsetzen. Exit-Code `1` in `ergebnis.txt` heißt überlebt – siehe nächster Abschnitt.

Auch Wegwerf-Testdateien (ein Test nur für eine Probe) erst anlegen, **nachdem** alle Gegenproben gelaufen sind – sonst ist der Arbeitsordner unsauber und jede Probe endet mit Exit-Code `2`.

### Überlebt? Erst breit wiederholen

Eine überlebende Gegenprobe ist erst dann eine **Lücke**, wenn sie auch mit einem breiten Filter überlebt. Der enge Filter (eine Testklasse) trifft oft nicht die Tests, die die Stelle wirklich prüfen: In PR #623 überlebte `break;` statt des Aufrufs von `PushedAcross` mit dem Filter der neuen Testklasse, obwohl andere Tests die Stelle fingen. Darum vor dem Wort „Lücke“ dieselbe Probe noch einmal mit einem breiten Filter laufen lassen, etwa dem Namensraum (`SilverAndAsh.Core.Tests.Gameplay`) oder allen Tests (`FullyQualifiedName~SilverAndAsh`, dauert einige Minuten). Erst wenn sie auch dann überlebt, ist es eine Lücke; im PR beide Läufe mit Filter nennen.

### Pflichtfall bei Kollisionsregeln

Ändert ein PR eine Kollisionsregel (was einen Körper aufhält, hinauf- oder hinunterlässt), braucht er einen Test **für jede Körperart mit ihrer echten Stufenhöhe**: gehende Körper (Spieler, Ghul, Gorm) mit `BodySizes.Player.StepHeight` (0,4 m; `CharacterCollision` nimmt sie als Vorgabe), fliegende (Fledermaus) mit unendlicher Stufenhöhe. Ein Test nur mit dem Spieler reicht nicht: In PR #623 hing es an `CharacterCollision.StepOntoStone`, ob eine Fledermaus mit ihrer unendlichen Stufenhöhe auf den Stein des Portals steigt; ein Test nur mit der Stufenhöhe des Spielers sieht diesen Zweig nie. Die Stufenhöhe nicht als Zahl in den Test schreiben, sondern aus `BodySizes` holen: `BodySizes.Player.StepHeight` für die Gehenden, `BodySizes.Bat.StepHeight` (unendlich) für die Fledermaus (#647).

### Den Bot gegenprüfen

Den Bot kann man genauso gegenprüfen: eine Spielregel im Core (oder eine Stelle im Bot selbst) kaputt machen, den passenden Modus laufen lassen, er muss `FINDING`-Zeilen und Exit-Code `1` liefern. Das erledigt `tools/gegenprobe.sh --bot` (#647) mit demselben Schutz wie bei Tests (Sperre, sauberer Arbeitsordner, bytegleiches Zurücksetzen); das vierte Feld sind die Argumente des Bots:

```
tools/gegenprobe.sh --bot tools/SilverAndAsh.BotRun/BossPortalRuns.cs \
  'PortalSite.Steps(-1.95f, 1.95f, 0.05f)' 'PortalSite.Steps(-1.95f, 1.95f, 0.1f)' 'slam --seeds 1'
```

Das Skript baut `tools/SilverAndAsh.BotRun` (mit dem Core) in Release und startet den Bot. Der Kontrolllauf muss ohne Befund enden, sonst Exit-Code `2`. Mit der Änderung heißt ein Befund „CAUGHT“ (Exit-Code `0`, die ersten zehn `FINDING`-Zeilen), keiner „SURVIVED“ (Exit-Code `1`). Endet der Bot selbst mit Exit-Code `2` (falscher Aufruf, Werkzeug passt nicht zum Core), endet auch das Skript mit `2`; kompiliert die Änderung nicht, mit `3`. Die Argumente werden an Leerzeichen getrennt, Anführungszeichen darin gibt es nicht.

**Nach jeder Änderung neu bauen**, bevor man mit `--no-build` startet – auch nach dem Zurücksetzen und nach einem fehlgeschlagenen Build (etwa einer Probe, die nicht kompiliert). Sonst läuft die alte DLL und meldet fälschlich Exit-Code `0`.

Belegte Proben für #647, jeweils mit `--bot`, alle gefangen (Exit-Code `0`):

| Datei, Änderung | Bot | Befunde |
|---|---|---|
| `BossPortalRuns.cs`, Raster stehend `Steps(-1.95f, 1.95f, 0.05f)` → `0.1f` | `slam --seeds 1` | Untergrenze: Spieler 8, Ghul 7, Fledermaus 11 je Seite |
| `BossPortalRuns.cs`, Raster mit Schwung `Steps(-1.9f, 1.9f, 0.1f)` → `0.2f` | `slam --seeds 1` | Untergrenze: Spieler 5, Ghul 7, Fledermaus 9 je Seite |
| `BossPortalRuns.cs`, Schläge `Steps(-2.6f, 2.6f, 0.4f)` → `0.8f` | `bat --seeds 1` | Untergrenze: 21 und 15 statt 27 |
| `BossPortalRuns.cs`, stehender Körper 2 m vor seinem Platz (`site.At(across, along - 2f)`) | `slam --seeds 1` | „not in the doorway at the slam“ |
| `BossPortalRuns.cs`, Körper mit Schwung 20 Ticks zu spät losgelassen (`… ?? shut) + 20`) | `slam --seeds 1` | „not in the doorway at the slam“ |
| `Play.cs`, Messhöhen doppelt so weit (`* 0.8f` → `* 1.6f`) | `slam --seeds 1` | „the stone check misses the block 3.075..3.200“ (Kämpfer) und weitere |
| `Play.cs`, Messhöhen ohne 1 cm Abstand zu den Füßen | `bat --seeds 1` | 4 × „dead bat in the stone“ auf dem Sockel |

Dazu ohne `--bot`: `BodySizes.Bat.StepHeight` von unendlich auf `0.4f` macht 3 von 140 Tests mit Filter `Bat` rot (`BossPortalNeighbourTests`).

Belegte Proben für `slam` (#629), jeweils `slam --seeds 1`:

| Änderung in `src/SilverAndAsh.Core/Gameplay/GameWorld.cs` | Ergebnis |
|---|---|
| In `PushedAcross`: `return portal.PushedAcross(across, radius);` → `_ = portal;` (das Portal wird nicht beachtet) | Exit `1`, 2 Befunde „player in the stone of the boss portal“ |
| In `PushOutOf`, Zweig der Gegner: `enemy.PlaceAt(PushedInto(…, enemy.Body.Radius));` → `enemy.PlaceAt(PushedInto(…, enemy.Body.Radius) + new Vector3(0.6f, 0f, 0.6f));` | Exit `1`, „in the stone of the boss portal“: Gorm 7, Ghul 13, Fledermaus 15; dazu Ghule und Fledermäuse in der sperrenden Tür |

Überlebt die Probe, zuerst das Raster prüfen: Ein Fehler, der nur an einer schmalen Stelle auftritt (etwa am Rand eines Bogens, #629), fängt der Bot nur, wenn sein Raster bis dorthin reicht. `slam` geht deshalb quer zum Durchgang alle 5 cm und nimmt die erste freie Tiefe, statt feste Punkte abzulaufen. Dass das Raster fein genug bleibt, sichern die Untergrenzen (siehe *Übersprungene Plätze*).
