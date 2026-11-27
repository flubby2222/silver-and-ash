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

Aufbau: `<modus> [--seeds VON..BIS] [--repo ORDNER]`. Ohne `--seeds` laufen die Seeds 1 bis 6; `7` oder `1-6` gehen auch. Ohne `--repo` sucht der Bot vom aktuellen Ordner aufwärts nach `assets/rooms` und nimmt Raumvorlagen und Balancing (`assets/balance.json`, wie das Spiel) von dort. Bei einer Balancing-Runde spielt er also schon mit den neuen Werten. Gibt die Datei beim Lesen eine Warnung (unbekannter Name, Wert außerhalb seines Bereichs, `chargedKnockback` unter `knockback` …), spielt der Bot nicht, sondern nennt die Warnungen auf der Fehlerausgabe und endet mit Exit-Code `3` (Issue #688): Das Spiel nähme dann Standardwerte statt der Werte aus der Datei, und der Lauf prüfte etwas anderes, als in der Datei steht.

| Modus | Was er spielt | Dauer (Seeds 1–6) |
|---|---|---|
| `arena` | Für jede Tür eines Kampfraums: hineingehen, das Gitter muss fallen und der Raum versiegeln; vier Fluchtversuche (sofort, sofort mit Sprint, nach 6 Ticks mit Sprint, nach 14 Ticks) müssen drinnen enden; Raum leeren, die Gitter gehen einmal hoch, hinausgehen. Zwei Kampfräume nebeneinander nacheinander. Die Bosstür hat kein Gitter: Sie schwingt zu und muss spätestens ab `doors.bossShutTick` (Balancing) sperren; der Spieler läuft sofort zurück und darf nicht in den Vorraum kommen (#566, #567). | etwa 2 min |
| `death` | Tod in jeder versiegelten Arena und im Bosskampf, danach Neustart: keine Tür verriegelt, kein Raum versiegelt, die Arena versiegelt beim Betreten wieder (#560, #578); nach dem Bosskampf ist der Vorraum frisch und ein zweiter Lauf erreicht den Boss wieder. | etwa 30 s |
| `boss` | Der ganze Weg: Vorraum, Boss-Tor-Teleporter freischalten und hin- und herreisen, Bosstür mit F öffnen, Bossraum versiegelt, die Bosstür sperrt spätestens ab `doors.bossShutTick`, Gorm besiegen, Treppe hinauf, gewonnen. | etwa 5 s |
| `slam` | Die Bosstür schlägt zu, während ein Körper in ihrem Durchgang steht (#629): nacheinander ein Spieler, ein Ghul, eine Fledermaus und Gorm, jeweils an jeder Stelle quer zum Durchgang alle 5 cm. Die Tiefe längs des Durchgangs ist die erste freie Stelle; wo der Körper nirgends frei steht, zählt der Bot die Stelle als übersprungen. Der Körper wird jeden Tick neu an seinen Platz gestellt, bis die Tür sperrt; danach darf er nicht im Stein des Portals stecken. Dass er im Tick des Zuschlagens wirklich in der Türbox stand, prüft der Bot daran, wohin die Tür ihn geschoben hat: genau Radius + `GameWorld.ThresholdGap` (1 cm) hinter ihre Box (`GameWorld.OnThreshold`), sonst ein Befund „not in the doorway at the slam“ (#647). **Mit Schwung** (#647): Spieler, Ghul und Fledermaus starten alle 10 cm quer zum Durchgang eine Körperbreite (zweimal ihr Radius aus `BodySizes`) vor der Türbox; der Spieler läuft los, Ghul und Fledermaus (vorher in ihrer Arena geweckt) fliegt ein aufgeladener Keulenschlag hinein. Ein Probelauf misst, wann der Körper im Durchgang ist und wann die Tür sperrt; der echte Lauf lässt ihn so spät los, dass er im Tick des Zuschlagens im Durchgang ist. **Der Schlag muss ihn tragen** (#676): Wache Ghule und Fledermäuse laufen auch von selbst in den Durchgang. Erreicht ein Körper den Durchgang im Probelauf erst, wenn sein Rückstoß vorbei ist (der Stein des Gewändes hat ihn aufgehalten, oder der Stoß ist kürzer als der Anlauf), zählt die Stelle als übersprungen, und zwar in einer eigenen Zeile „not carried into the doorway, it walked in after its knock (skipped)“ mit gemessener Zahl (#688): je Seed und Seite der Mitte genau 5 beim Ghul (±1,0 bis ±1,4 m), 0 bei Fledermaus und Spieler, keine in der Mitte. Jede Abweichung ist ein Befund („the reason skips other places than measured“); so fällt eine Kollision auf, die geschlagene Körper einmal zu früh stoppt. Ist der Rückstoß im echten Lauf beim Zuschlagen vorbei, ist das ein Befund („not carried into the doorway“). Die Prüfung „getragen“ prüft sich im Probelauf selbst: Im Tick des Loslassens muss sie wahr sein, nach dem Ende des Stoßes (`club.knockbackTime`) falsch – sonst „still carried … - the check of the run cannot tell a knock from walking in“. Für die Fledermaus ist sie die einzige Wache. Der Anlauf hängt am Körper, nicht an der Keule: Ein Anlauf, der mit dem Rückstoß schrumpft, ließe einen zu kurzen Stoß nie auffallen. Wird `club.chargedKnockback` kleiner als eine Körperbreite (heute 0,6 bis 0,8 m) oder wirkt der Rückstoß nicht mehr, fallen die Plätze unter die Untergrenze. Für eine solche Probe `club.knockback` mitsenken: Liegt `chargedKnockback` darunter, nimmt der Lader beide Standardwerte, und der Bot endet mit Exit-Code `3`. Gorm läuft nicht mit Schwung hinein (kein Rückstoß, und seine eigenen Schritte hingen vom Starttick ab) – eine bekannte Lücke. | etwa 1,5 min |
| `bat` | Eine wache Fledermaus am Bossportal (#627, #629): an jeder freien Stelle eines Rasters vor und hinter dem Portal in acht Richtungen weggeschlagen; um einen Spieler herum geflogen, der an vier Stellen im Portal steht; tot auf einen Sockel des Portals fallen gelassen, bis sie verschwunden ist. | etwa 25 s |
| `stuck` | In jeder Arena und im Bossraum still stehen, erst an der Tür, dann in der Raummitte: kein Gegner (Ghul, Fledermaus, Gorm), der einen Spieler jagt, darf länger als das Doppelte seiner längsten erlaubten Pause aus der Balance hängen, ohne sich zu bewegen oder anzugreifen (#591, siehe [Hängende Gegner](#hängende-gegner-stuck)). Findet nur Gegner, die hängen, während der Spieler stillsteht. | etwa 1 min; 40 Seeds etwa 6 min |
| `seeds` | Baut jede Ebene zweimal und vergleicht sie (gleicher Seed muss gleiche Ebene geben) und zählt, wie viele Versuche der Generator brauchte. | etwa 3 s; 1 000 Seeds etwa 100 s |
| `fight` | Ein **sterblicher** Spieler mit einer echten Waffe kämpft sich vom Start bis zu Gorm (#803, siehe [Kampf-Bot](#kampf-bot-fight)). Nicht in `all`. Mit `--weapon blade\|pistol\|axe\|whip\|crossbow\|all` (Standard `all`). | etwa 1 min für Seeds 1..6 und alle fünf Waffen |
| `all` | alle sieben nacheinander, in der Reihenfolge der Tabelle (auch `stuck`, aber nicht `fight`) | etwa 6 min |

Nebenbei prüft der Bot nach **jedem Tick**, dass weder ein Spieler noch ein Gegner (Ghul, Fledermaus, Gorm) in einer geschlossenen Tür oder im Stein eines Bossportals steckt und kein Spieler in einer festen Zelle. Für den Stein prüft er bei jedem Körper die Achse und 24 Punkte 5 mm innerhalb seines Rands (für jeden Körper gleich, auch für Gorm), in Höhen von den Füßen bis zum Kopf (`BossPortal.Contains`); ein Körper, der den Stein nur berührt, ist kein Befund. Der Abstand der Höhen folgt aus `BossPortalShape.Blocks`: vier Fünftel des dünnsten Blocks (heute der Kämpfer, 0,125 m, also 0,1 m). `slam` und `bat` prüfen zu Beginn selbst, dass für keine ihrer Körperhöhen ein Block zwischen zwei Messhöhen durchrutscht („the stone check misses the block …“, #647) – bekommt das Portal einen dünneren Block, rücken die Höhen von selbst enger. Auch eine tote Fledermaus zählt, solange sie noch fällt oder liegt.

**Übersprungene Plätze:** `slam` und `bat` lassen Plätze aus, an denen der Körper nicht frei stehen kann, und zählen sie als „… (skipped)“. Jeder solche Zähler hat einen Gegenzähler der gespielten Plätze (`Report.Tally`): Werden je Seed und Körperart (bei `bat` je Teil) auf einer Seite der Portalmitte weniger Plätze gespielt als die Untergrenze, ist das ein Befund („fewer than N place(s) played on a side of the middle“). So endet ein Modus, der nach einer Änderung am Portal nichts mehr prüft, nicht still mit „0 finding(s)“, und ein halb so feines Raster fällt auf. Die Untergrenzen sind gemessen (#647): drei Viertel dessen, was die Seeds 1–6 heute je Seite spielen (aufgerundet).

| Modus, Teil | heute je Seed und Seite (Seeds 1–6) | Untergrenze |
|---|---|---|
| `slam` stehend: Spieler / Ghul / Fledermaus / Gorm | 16 / 14 / 21 / 3 | 12 / 10 / 15 / 2 |
| `slam` mit Schwung: Spieler / Ghul / Fledermaus | 9 / 9 / 17 | 7 / 7 / 13 |
| `bat` Schläge | 36 | 27 |
| `bat` tote Fledermaus | 2 | 2 |

Beim Ghul mit Schwung waren es bis #676 13 je Seite: An je fünf Stellen (±1,0 / 1,1 / 1,2 / 1,3 / 1,4 m neben der Mitte) hält das gestufte Gewände den Stoß auf, und der Ghul lief erst danach von selbst hinein – das zählt seitdem nicht mehr als Lauf mit Schwung. Netto fielen 4 je Seite weg. Diese fünf zählt seit #688 eine eigene Zeile mit fester Zahl (`DoorSlam.Bodies`, Feld `NotCarried`, siehe oben).

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

**Exit-Code:** `0` keine Befunde, `1` Befunde, `2` falscher Aufruf oder das Werkzeug passt nicht mehr zum Core (siehe unten), `3` `assets/balance.json` gab eine Warnung, nichts wurde gespielt (Issue #688). `tools/gegenprobe.sh --bot` meldet Exit-Code `3` des Bots wie jeden anderen Code außer 0 und 1: „the bot could not run with the change (bot exit code 3) - no statement“, Exit-Code `2`, dazu die letzten Zeilen des Bots mit der Warnung – nicht mehr SURVIVED.

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

### Kampf-Bot (`fight`)

Der Modus `fight` (Issue #803) misst, wie weit ein sehr einfacher Spieler kommt, wenn er **sterblich** ist: Er spielt mit den Werten aus `assets/balance.json` (nicht mit dem unsterblichen `Play.Balance` der anderen Modi), mit echten Waffen und echten Gegnern, vom Start durch die Räume in der Reihenfolge der Route bis zu Gorm.

```
dotnet run --project tools/SilverAndAsh.BotRun -c Release -- fight --seeds 1..6 --weapon whip
```

**Ausgabe:** je Seed und Waffe eine Zeile (gewonnen oder verloren, Dauer in Spielzeit, Stufe, geräumte Kampfräume, Todesort mit Raum oder Gorm, wer den letzten Schaden verursacht hat, erlittener und ausgeteilter Schaden), am Ende je Waffe eine Zeile `WIN RATE`. Gleicher Seed und gleiche Waffe geben dieselbe Ausgabe. Ein Lauf endet nach spätestens 20 Minuten Spielzeit mit „TICK LIMIT“, oder nach 30 Sekunden ohne jede Veränderung (keine neue Zelle, kein Gegnerschaden, kein Raum, keine Tür) mit „STUCK (no progress)“. Beides ist kein Befund; ein Befund ist nur ein Fehler der Spiel-Regeln, den der Bot nebenbei findet (die Prüfungen nach jedem Tick laufen mit).

**Eine untere Schranke, kein Maß für das Spiel:** Der Bot spielt einfach und immer gleich (`FightPolicy`, seit #805 mit `FightRetreat`): gehen oder zielen, angreifen, nachladen, heilen (unter der Hälfte des Lebens und mit einer Flasche in 25 m, solange kein Gegner in Schlagweite steht), den nächsten Gegner in Sicht bekämpfen (seit #809 nur, was er sieht), sonst zur zuletzt gesehenen Stelle gehen oder den Raum absuchen, sonst durch den nächsten Raum der Route zu Gorm. Er wechselt die Waffe nicht, besucht keine Nebenräume und wählt bei einem Stufenaufstieg immer die erste Gabe. Was er gewinnt oder verliert, sagt, was *dieser* Bot schafft; ein Mensch schafft mehr. Die Ausgabe nennt das selbst („NOTE … lower bound“).

**Abstand halten (#805):** Eine Nahkampfwaffe schlägt zu und tritt aus der Reichweite zurück, sobald ein Gegner einen Schlag ansagt (Ghul im Ausholen, Fledermaus im Kreischen) und näher als 3 m steht; ist Ausdauer für zwei Abschnitte da, löst er dabei zusätzlich den Ausweichschritt (`Dash` am Boden) aus. Eine Schusswaffe hält mindestens 4,5 m Abstand zum Ziel und schießt im Rückwärtsgehen; Nachladen läuft beim Zurückweichen weiter. Wohin er tritt, entscheidet `FightRetreat` aus 16 Richtungen: erst die, in der der Endpunkt keinem angekündigten Schlag mehr liegt (Zone aus Reichweite, Blickrichtung und Halbwinkel des Ghuls aus den Balancing-Werten), dann der größere Abstand zu den Körpern, dann die Richtung weg vom nächsten Gegner. Wege gegen eine Wand zählen nicht; ist keiner frei, kämpft er stehend.

**Was der Bot liest** (seit #809 nur, was ein Mensch am Bildschirm sieht – jede Größe einzeln):

| Größe | Quelle im Bot | Sieht der Mensch sie? |
|---|---|---|
| Lage, Größe, Zustand der Gegner (Ghul im Ausholen, Blickrichtung, Fledermaus im Kreischen) | `FightMemory.Look` → `Seen` | Ja, aber nur für Gegner mit freier Sichtlinie von den Augen zur Körpermitte (`LineOfSight`). Das gilt für jeden Gegner, auch im selben Raum; außerhalb des Raums bis 12 m. |
| Stelle, wo ein Gegner zuletzt stand | `FightMemory.LastSeenSpot` | Ja, der Mensch merkt sie sich. Der Bot vergisst sie, sobald er daneben steht (1 m) oder der Gegner tot ist. |
| Schlüssel (Lage, Id) | `FightMemory.KnowsKey` | Nur mit freier Sichtlinie oder einmal gesehen. Core kennt kein `KnownKeys` wie `KnownBloodVials`; der Bot merkt sie sich selbst. |
| Blutflaschen | `GameWorld.KnownBloodVials` | Ja: die Karte zeigt sie, sobald der Raum entdeckt ist. |
| Eigenes Leben, Ausdauer, Munition, Nachladen, Schlüssel, Gabenangebot, Stufe, am Boden | `Player` | Ja, Anzeige am Bildschirm. |
| Raum: Zellen, Art, „geräumt“ | `RoomState` | Ja: Grundriss und Karte; dass ein Kampfraum nicht geräumt ist, zeigen die verschlossenen Türen und Gitter. **Wo** der Gegner darin steht, liest der Bot nicht. |
| Türen (offen, Art, kann man bedienen, Lage, Räume dahinter), Treppe | `GameWorld.Doors`, `Stairs` | Ja, sichtbar. Der Weg über die Türen zum Bossraum ist die dokumentierte Route (unverändert). |
| Raster der Zellen für freie Wege | `Pathfinder` | Grundriss ja; **Grenze:** der Wegfinder kennt auch Zellen jenseits der Sicht (eine Vereinfachung, die den Bot eher stärker macht). |
| Ereignisse, Ticks | `DamageSource` u. a. | Nur Ausgabe (wer hat zuletzt getroffen), nie Eingabe der Entscheidung. |

Nicht gelesen werden innere Zeitgeber der Gegner, Zufallswerte und die Lage von Gegnern, die der Bot nicht sieht. **Grenze der Sicht:** Sie ist eine reine Sichtlinie ohne Blickkegel; ein Gegner hinter dem Bot gilt als gesehen (ein Mensch dreht sich um oder hört ihn). Eine Flasche, zu der er 3 s lang nicht näher kommt, gibt er auf.

**Gedächtnis und Suche (#809):** Der Zustand gehört dem Lauf (`FightMemory`, vom `FightBot` gehalten), nie einem `static`-Feld. Sieht der Bot keinen Gegner, geht er zur zuletzt gesehenen Stelle (`FightGoal.Approach`). Gibt es keine, und der Raum ist ein Kampf- oder Bossraum, der nicht geräumt ist, sucht er ihn (`FightGoal.Search`) nach einer festen Regel ab: erst die Mitte des Raums, dann die nächste Zelle, die er weder gesehen noch erreicht hat; alle 10 Ticks gelten die Zellen in Sicht (auf 0,9 m Höhe) als abgesucht, eine Zelle gilt als erreicht ab 1 m, nach 300 Ticks gibt er sie auf, unerreichbare Zellen zählt er als abgesucht, und sind alle abgesucht, fängt er von vorn an. Die Reihenfolge am Weg: Schlüssel in Reichweite, Suche, Route zu Gorm. Konstanten stehen oben in `FightMemory` (`SightRange`, `ArriveDistance`, `LookEvery`, `SearchPatience`, `LookHeight`); jede hat eine Gegenprobe (siehe PR #809). Die Stufe `Search` kennt `FightView.HasSearch`; `FightView.TargetDistance` ist ohne Ziel in Sicht der Abstand zur gemerkten Stelle.

**Messung #805** (Seeds 1..20, Median geräumter Kampfräume): SilverBlade 0 → 3, Pistol 2 → 2, HuntersAxe 0 → 3,5, Whip 0 → 3,5, StakeCrossbow 1 → 2. Das Ziel (mindestens drei Waffen mit Median 4) ist nicht erreicht. Offen: mehrere Ghule, die gleichzeitig ausholen, drängen den Bot in Ecken; Fernkampf-Läufe endeten teils als STUCK (seit #807 behoben, siehe unten); Geschosse (Fledermäuse) sind bei der Axt die häufigste letzte Schadensquelle. Der Kern kennt keine Unverwundbarkeit im Ausweichschritt, keinen Block und keinen sichtbaren Ausholzeitgeber.

**Wie er handelt:** Nur über `PlayerInput` im Tick (ADR 0003). Das Zielen ist die Ausnahme der Form halber: Im Spiel dreht `Player.Look` je Bild außerhalb des Ticks, darum dreht der Bot wie die übrigen Modi mit `PhotoStudio.Face` (`Play.Face`) zum Ziel. Der Weg führt über den Wegfinder (`Pathfinder`); Türen öffnet er mit `Interact`, Treppen ebenso. Steht er beim Gehen still (etwa an einem Sockel), weicht er 40 Ticks zur Seite aus.

**Was gesetzt wird:** nichts außer der Start-Waffe. Sie kommt auf dem Weg von `--give` ins Spiel (`GiveOptions.GiveTo`); die beiden Start-Waffen (Silberklinge, Pistole) hat der Spieler schon. Kein erhöhtes Leben, kein Sonderschaden.

**Was Core für den Bot nicht hergibt** (nicht gebaut, nur genannt): kein Ereignis, das bei einem Treffer die Quelle nennt (`PlayerHit` hat nur den Schaden, der Bot liest sie aus den Ereignissen desselben Ticks); keine Abfrage „wohin muss ich als Nächstes“ (der Bot sucht den Weg selbst über `Doors` und `Rooms`); keine Abfrage, ob ein Gegner den Spieler gerade treffen würde (darum weicht der Bot nicht aus); Hindernisse im Raum (Sockel, Kisten) kennt der Wegfinder nicht, darum das Ausweichen zur Seite.

**Warum Schusswaffen hingen (#807):** Alle 12 STUCK-Läufe aus den Seeds 1..40 (Pistole 4, Armbrust 8; Klinge, Axt und Peitsche keiner) hatten dieselbe Ursache, nicht Sockel oder Truhen: Ein lauernder Ghul stand 4,6 bis 4,9 m entfernt hinter genau einer Wandzelle im selben Raum (Raum 1, 2, 3, 8 oder 9). Der Bot sieht ihn dort nicht, hält ihn aber für das Ziel (`Visible` nimmt jeden Gegner im Raum). Eine Schusswaffe wich bei weniger als 4,5 m zurück, auch ohne Sicht; dort war der Abstand wieder über 4,5 m, das Ziel nicht in Sicht, also ging er hin, und so fort, bis die STUCK-Erkennung nach 30 s ohne Veränderung griff. Seitdem weicht eine Schusswaffe nur noch vor einem Ziel zurück, das sie sieht (`FightPolicy.ShouldStepBack` verlangt `TargetInSight`); ohne Sicht geht sie über den Wegfinder um die Wand herum, bis sie es sieht. Gelesen wird dafür nichts Neues: `TargetInSight` (Sichtlinie von den Augen zur Mitte des Ziels, `LineOfSight`) las der Bot schon für `Approach`. Die STUCK-Erkennung ist unverändert (30 s, `Progress`). Bleibt ein Ziel ohne Weg unerreichbar, hängt der Lauf weiter und endet als STUCK.

**Messung #807** (Seeds 1..40, vorher → nachher, alle fünf Waffen): STUCK-Läufe Pistole 4 → 0, Armbrust 8 → 0, die drei Nahwaffen 0 → 0 (ihre Ausgabe ist Zeile für Zeile gleich geblieben). Median geräumter Kampfräume Pistole 2 → 3, Armbrust 3 → 3. Gewonnen hat weiterhin keiner. Die Zahlen von Pistole und Armbrust sind damit nicht mehr durch Hänger verzerrt.

**Messung #809** (Seeds 1..40, vorher → nachher; STUCK, TICK LIMIT und Siege waren vorher und nachher bei allen fünf Waffen 0): Median geräumter Kampfräume SilverBlade 3 → 3, Pistol 3 → 3, HuntersAxe 3,5 → 3, Whip 3,5 → 3, StakeCrossbow 3 → 2,5; Median der Dauer 60,5 → 64,5 s, 65,2 → 59,7 s, 68,2 → 63,4 s, 71,4 → 65,6 s, 83,3 → 65,0 s. Die Zahlen sind eine Messung, kein Ziel. Der Bot weiß jetzt weniger als zuvor (nichts über Gegner hinter Wänden) und kommt im Median gleich weit oder eine halbe Raumstufe kürzer; die Läufe enden weiter nur durch den Tod.

### Hängende Gegner (`stuck`)

Der Modus `stuck` (Issue #591) sucht Gegner, die einen Spieler jagen, aber weder zu ihm kommen noch angreifen – etwa weil sie an einer Säule, Kiste oder Stufe festhängen. Der Bot betritt jede Arena (versiegelt) und den Bossraum und steht dort still: erst an der Tür, dann in der Mitte des Raums. Er schlägt und schießt nicht, damit die Gegner frei handeln.

**Grenze:** Der Modus findet nur Gegner, die hängen, während der Spieler stillsteht. Wer nur hängen bleibt, wenn ein Spieler läuft, springt oder ausweicht (etwa beim Umweg um eine Säule hinter einem fliehenden Spieler), fällt hier nicht auf.

**Wann ein Gegner hängt:** Er lebt, ist treffbar, schläft nicht und jagt (Ghul und Fledermaus nicht mehr lauernd und nicht im Rückstoß; Gorm immer). Er sieht einen lebenden Spieler nach seiner eigenen Sichtregel oder steht mit ihm im selben Raum. Und er bleibt `N` Ticks lang (flach gemessen) innerhalb seines eigenen Körperradius und löst keines seiner Angriffsereignisse aus. Jeder Befund nennt Seed, Raum, Ticks, Gegner mit Nummer, Zustand, Ort mit Zelle, den Abstand zum Spieler und zum nächsten Gegner.

**Die Schwelle `N` kommt aus der Balance:** das Doppelte der längsten Pause, in der ein Gegner zu Recht stillsteht. Ändert jemand die Werte in `assets/balance.json`, wandert die Schwelle mit.

| Gegner | Längste erlaubte Pause (Balance) | `N` bei den aktuellen Werten |
|---|---|---|
| Ghul | `WindUpTime` + `RecoverTime` + `RepathTime` | 192 Ticks |
| Fledermaus | `SpitInterval` + `SpitWindUpTime` + `ScreechTime` + `CircleHeightMax` / `ClimbSpeed` + `RepathTime` | 472 Ticks |
| Gorm | `HammerWindUp` + `ShockwaveRadius` / `ShockwaveSpeed` + `StuckTime` + `PauseTime` + `RoarTime` + `RepathTime` | 900 Ticks |

**Anstehende Ghule:** Ghule drängen sich vor einem stillstehenden Spieler; wer hinter einem anderen wartet, hängt nicht. Steht ein Ghul bei Erreichen der Schwelle an einen anderen Gegner gedrückt, zählt der Bot das als „pressed against another enemy (not counted as hanging)“ und fängt neu an zu messen. Fledermaus und Gorm haben keine solche Ausnahme.

**Untergrenzen:** Wird in einem Seed kein jagender Ghul oder kein Gorm beobachtet, oder über alle Seeds keine Fledermaus, ist das ein Befund – sonst könnte der Modus unbemerkt nichts mehr prüfen.

**Längster Stillstand** (#699): Am Ende steht je Gegnerart, wie viele beobachtet wurden und wie lange der längste Stillstand ohne Angriff unter der Schwelle war – mit Gegner und Nummer, Seed und Ort (Raum, an der Tür oder in der Mitte), der Schwelle und wie viele Ticks bis zu ihr fehlten. Liegt eine Gegnerart knapp darunter, lässt sich der Fall so ohne Suchen mit genau diesem Seed nachspielen:

```
stuck: ghoul: 3078 watched, longest still without an attack 188 ticks (ghoul 33, seed 11, room 12 in the middle), hangs from 192 ticks (4 short)
```

Gleich lange Stillstände nennen den ersten, also den kleinsten Seed des Laufs. Ticks, in denen ein Ghul an einen anderen Gegner gedrückt steht (siehe oben), zählen hier nicht mit; ein Ghul, der sich erst kurz vor dem Ende seines Stillstands vom Nachbarn löst, aber schon. So einer ist der Ghul von Seed 11: Mit der Schwelle auf 182, 144 oder 96 Ticks herabgesetzt stand er beim Erreichen gedrückt und wurde kein Befund (#699). Ausgewertet wird in `LongestStillSpells` (`src/SilverAndAsh.Core/Diagnostics/`), getestet in `LongestStillSpellsTests`.

**Dauer:** etwa 8 bis 10 Sekunden je Seed (40 Seeds etwa 6 min).

Gegenproben (Issue #591; nach jeder neu bauen):

| Änderung | Aufruf | Ergebnis |
|---|---|---|
| Ghul kriecht nicht (`Ghoul.cs`: `Move(level, Facing * _ghoul.Speed, …)` → `Vector3.Zero`) | `stuck --seeds 1` | gefangen |
| Fledermaus kreist, spuckt und stößt nicht (`Bat.cs`, Zustand `Circling`) | `stuck --seeds 1..3` | gefangen |
| Gorm greift nie an (`Gorm.cs`: `ChooseAttack(…) ?? ChaseThrow(…)` → `null`) | `stuck --seeds 1` | gefangen |
| Bot beobachtet nie einen jagenden Gegner (`HangingEnemies.cs`) | `stuck --seeds 1` | gefangen (Untergrenzen) |
| Schwelle herabgesetzt (`HangingEnemies.cs`: `Margin` 2 → 0,3, beim Ghul 29 statt 192 Ticks, bei Gorm 135 statt 900; #699) | `stuck --seeds 11` | gefangen (45 Befunde, jeder mit „seed 11“) |

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

- `<suchen>` wird wörtlich gesucht und muss **genau einmal** vorkommen (`--all`: jede Stelle; `--regex`: Perl-Ausdruck). Kommt er ohne `--all` mehrmals vor, endet das Skript mit Exit-Code `2`, ohne etwas zu ändern. Die Quelldateien haben CRLF-Zeilenenden: Ein Suchtext über mehrere Zeilen findet wörtlich nichts (das Skript bricht dann mit „not found“ ab); dafür `--regex` nehmen und die Zeilen mit `\r?\n` verbinden.
- `<ersetzen>` wird wörtlich eingesetzt. Mit `--regex` setzen `$1`, `$2` … oder `${1}` die Gruppen (Klammern) aus `<suchen>` ein; `$0` den ganzen Treffer, eine nicht getroffene Gruppe gibt leeren Text; `\$` schreibt ein einfaches `$`, `\\` einen einfachen Backslash, jeder andere Backslash bleibt stehen (`"\n"` bleibt `"\n"`) (Issue #604). Verweist `<ersetzen>` auf eine Gruppe, die es nicht gibt, bricht das Skript vor dem Kontrolllauf mit Exit-Code `2` ab. Beispiel – dieselbe Probe wie oben, der Anfang der Zeile bleibt per `${1}` stehen:

  ```
  tools/gegenprobe.sh --regex src/SilverAndAsh.Core/Gameplay/GameWorld.cs \
    '(GameWorld Restarted[(][)] => new[(])_levelWithOpenDoors,' '${1}Level,' GrateTests
  ```

  Aus PowerShell 5.1 `<ersetzen>` in einfache Anführungszeichen setzen, sonst ersetzt PowerShell `$1` selbst. Klassen wie `[(]` und `[.]` statt `\(` und `\.` sind dort sicherer, weil Backslashes beim Weiterreichen an `bash.exe` verloren gehen können.

  Den Ersetzungsschritt erledigt `tools/gegenprobe-replace.pl`. Der Selbsttest `tools/gegenprobe.tests.sh` (Git Bash, ohne dotnet, etwa drei Minuten: gemessen 172 s, 178 s und 195 s, Issue #733) prüft ihn an kleinen Dateien: wörtlicher Modus, `$1`, `${1}`, `$0`, `\n`, `\$`, `\\`, nicht getroffene und fehlende Gruppe, `--all`, CRLF, und dass `--regex` Treffer zählt, nicht Gruppen (ein Treffer mit zwei Gruppen ist einer, Issue #676). Dazu spielt er die Sperre und mit einem Ersatz-`dotnet` jeden Ausgang des ganzen Laufs durch, auch mit `--bot` und `--message` (Meldung entschlüsselt, erster roter Test der Liste, 30-Zeilen-Grenze, keine Meldung, Abbruch zusammen mit `--bot`) und `--timeout` (Vorgabe 120 s und eigener Wert an beiden Läufen, Vorgabe in der Hilfe, `0` ohne Grenze, hängender Test mit englischer und deutscher Meldung, ohne Ablaufdatei, ohne Ergebnisdatei, mit roten Tests davor, hängender Kontrolllauf, falsche Werte, Abbruch zusammen mit `--bot`) und Optionen ohne Wert. Nach jeder Änderung an einem der Skripte laufen lassen; Exit-Code `0` heißt alles bestanden. **Jede neue Option und jeder neue Ausgang von `gegenprobe.sh` bekommt einen Fall im Selbsttest** (Issue #660), mit Gegenprobe im PR: Der Fall wird rot, wenn man die Stelle im Skript ausbaut. Solche Gegenproben macht man an einer Kopie der Werkzeuge in einem Scratch-Ordner (`perl -0pi -e 's/…/…/'`). Vorsicht: `s///` ohne `/g` ändert nur die erste Fundstelle, und das kann ein gleichlautender Kommentar sein statt des Codes – den Unterschied zum Original mit `diff` ansehen, bevor man das Ergebnis liest. Fällt im Selbsttest ein Warte-Haken der Sperre aus, wartet ein Fall höchstens 10 s statt 60 s (`GEGENPROBE_TEST_WAIT_SECONDS`).
- `<testfilter>` ist ein Klassen- oder Testnamen-Teil (wird zu `FullyQualifiedName~<filter>`) oder ein vollständiger Filter für `dotnet test`, wenn er `=`, `~`, `|`, `&` oder `!` enthält.
- `--project <pfad>` wählt ein anderes Testprojekt (Standard `tests/SilverAndAsh.Core.Tests`), `--name <text>` den Namen in der Ausgabe, `--help` zeigt die Hilfe. Fehlt der Wert einer Option (`--timeout`, `--project` oder `--name` als letztes Argument, bei `--project` und `--name` auch ein leerer Wert), endet das Skript mit Exit-Code `2` und „… needs … after it“, bevor etwas läuft (Issue #733). Früher kam hier Exit-Code `1`, der in einer Liste wie „überlebt“ aussah.
- `--bot` prüft mit einem Bot-Lauf statt mit Tests (#647); das vierte Feld sind dann die Argumente des Bots, siehe [Den Bot gegenprüfen](#den-bot-gegenprüfen).
- `--message` zeigt bei CAUGHT zusätzlich die Fehlermeldung des ersten roten Tests der Liste (Issue #682), so wie sie in der Ergebnisdatei steht: höchstens 30 Zeilen, danach „... and N more line(s)“, Pfade gekürzt wie bei den Baufehlern. Damit sieht man, ob die Meldung eines neuen Tests im Fehlerfall verständlich ist, ohne den Fehler von Hand einzubauen. Mit `--bot` bricht das Skript mit Exit-Code `2` ab; der Bot zeigt seine Befunde ohnehin.
- `--timeout <sekunden>` begrenzt, wie lange ein einzelner Test laufen darf (Issue #712, Vorgabe `120`, bis #733 `60`). Das Skript reicht den Wert als `--blame-hang-timeout <n>s --blame-hang-dump-type none` an beide Testläufe weiter; `dotnet test` beendet dann einen hängenden Test samt Testlauf, statt ewig zu warten. `--timeout 0` schaltet die Grenze ab, wie vor #712. Ein Abbild des Testprozesses (Dump) entsteht nicht. Nur ganze Sekunden; mit `--bot` bricht das Skript mit Exit-Code `2` ab, denn der Bot-Lauf kennt keine solche Grenze (warum, steht unter [Den Bot gegenprüfen](#den-bot-gegenprüfen)). Siehe Punkt 9 unten.

Was man kaputt macht: nicht nur eine Bedingung entfernen (`&& player.IsDead` anhängen, einen Aufruf streichen), sondern auch einen Schwellwert abschwächen, etwa `>= x` zu `>= 0` oder eine Grenze auf einen offensichtlich falschen Wert. Ein Test, der nur „die Regel gibt es“ prüft, bleibt dabei grün; erst wenn er auch beim abgeschwächten Schwellwert rot wird, prüft er die Grenze selbst. Prüft ein Test eine Eigenschaft nur an Nachbarzellen (etwa die Decke vor und hinter einer Tür), nimmt die Gegenprobe die Zellen selbst aus (Issue #603: `!doorCells.Contains((cell.X, cell.Z)) &&` in die Auswahl der Zellen). Jede neue Toleranz oder Rundungsgrenze im Code wird mit Wert 0 und einmal mit verschobener Grenze gegengeprüft (etwa der Rand, ab dem ein Strahl kurze Schritte macht: `IsNear(point, StepLength)` zu `IsNear(point, 0f)`, Issue #642). Bei gekürzten Meldungen (die ersten N Einträge vollständig, der Rest gezählt) gehört die Grenze N/N+1 in den Test: genau N Einträge ohne Zusatz, N+1 mit „... and 1 more“ – sonst überlebt `>` zu `>=` (Issue #682). Das gilt auch für die Werkzeuge: Jede Kürzung bekommt im Selbsttest einen Fall genau auf der Grenze N und einen auf N+1 (bei `--message` 30 und 31 Zeilen).

Ablauf und Schutz:

1. **Nur ein Lauf je Arbeitsordner** (Issue #604): Läuft im selben Arbeitsordner schon eine Gegenprobe, bricht ein zweiter Lauf sofort mit Exit-Code `2` und der Meldung „another gegenprobe is already running in this working folder“ ab – sonst hielte er die gerade kaputte Datei für das Original. Proben also nacheinander starten; verschiedene Worktrees stören sich nicht. Die Sperre ist der Ordner `gegenprobe.lock` im Git-Ordner des Arbeitsordners (bei einem Worktree `.git/worktrees/<name>/`), `git status` zeigt sie nicht. Die Meldung über eine liegengebliebene Sperre nennt deren absoluten Pfad mit Absicht, damit man ihn löschen kann; diesen Pfad nicht in einen PR-Kommentar oder ein Issue kopieren. Das Skript entfernt sie am Ende, auch bei Strg+C. Bleibt sie nach einem harten Abbruch liegen, erkennt der nächste Lauf am fehlenden Prozess, dass sie veraltet ist, und übernimmt sie (Issue #639, [`tools/gegenprobe-lock.sh`](../tools/gegenprobe-lock.sh)). Die Übernahme gelingt genau einem Lauf, auch wenn zwei gleichzeitig starten: Wer übernimmt, benennt den Sperrordner zuerst mit `mv` um – das schafft nur einer –, prüft im umbenannten Ordner, dass es wirklich die veraltete Sperre ist, und löscht ihn erst dann. Hat er versehentlich eine frische Sperre erwischt, gibt er sie zurück und bricht mit Exit-Code `2` ab. Der Selbsttest `tools/gegenprobe.tests.sh` spielt diesen Wettlauf mit zwei Läufen nach; die beiden warten dabei über Dateien aufeinander, nicht über feste Wartezeiten, damit der Fall auch auf einem ausgelasteten Rechner hält (Issue #657). Hat inzwischen ein anderes Programm dieselbe Prozessnummer bekommen, greift die Sperre weiter: Läuft sicher keine Gegenprobe, den Ordner aus der Meldung löschen (`rm -rf '<pfad>'`), dann neu starten. **Bekannte Grenze:** Treffen sich drei Läufe genau in der Lücke der Übernahme, kann der dritte eine frische, noch leere Sperre (ohne `pid`) mit `mv -T` ersetzen; dann laufen zwei Proben im selben Ordner. Ohne neues Werkzeug lässt sich das nicht schließen, denn eine leere Sperre muss als gehalten gelten (Issue #651) – Proben deshalb nacheinander starten, nicht zu dritt.
2. Bei **unsauberem Arbeitsordner** startet das Skript nicht – sonst könnte das Zurücksetzen fremde Änderungen treffen. Vorher committen.
3. **Kontrolllauf** ohne Änderung: Die gefilterten Tests müssen existieren und grün sein.
4. Die Stelle wird ersetzt, die Tests laufen noch einmal; rote Tests werden mit Namen aufgezählt.
5. Die Datei wird **bytegleich zurückgesetzt** – auch bei Strg+C, Fehler oder Abbruch. Danach prüft das Skript, dass der Arbeitsordner wieder sauber ist.
6. **Die gebaute DLL enthält danach noch die kaputte Stelle.** Wurde die Änderung gebaut, sagt das Skript am Ende „the build output still holds the changed code - build again before running anything with --no-build“. Vor dem nächsten Lauf mit `--no-build` (Tests, Bot) also `dotnet build -c Release`; sonst meldet er auf der alten DLL fälschlich Grün (Issue #639). Nach Exit-Code `3` kam kein Ergebnis heraus. Das hat drei Ursachen: Die Änderung kompiliert nicht (dann wurde nichts mit ihr gebaut), eine gebaute Datei ließ sich nicht ins Testprojekt kopieren (von einem anderen Prozess gesperrt), oder das Testprojekt (bei `--bot` der Bot) baut nicht – in den beiden letzten Fällen ist die Änderung zumindest teilweise schon gebaut. Das Skript nennt darum alle drei und rät auch dann zum Neubau (Issue #657); welche es war, zeigen die Fehlerzeilen darüber (`error CS…` für den Code, `error MSB3021`/`MSB3027` für eine gesperrte Datei). Absolute Pfade in diesen Zeilen kürzt das Skript auf `...\<name>` (Issue #660), damit kein Temp-Ordner und kein Benutzername in einen PR-Kommentar gerät. Zuerst ersetzt es die bekannten Wurzeln wörtlich (`$TEMP`, `$TMP` durch `<temp>`, `$USERPROFILE`, `$HOME` durch `~`, jeweils auch in den Schreibweisen von `cygpath`), so greift die Kürzung auch bei einem Benutzernamen mit Leerzeichen. **Nicht gekürzt** werden: ein Pfad mit Leerzeichen außerhalb dieser Wurzeln (der Teil ab dem Leerzeichen bleibt stehen), ein Pfad direkt nach `,` `:` oder `<`, und ein Benutzername außerhalb der Wurzeln (Ordner eines anderen Benutzers, UNC-Freigabe). Vor dem Einfügen in einen Kommentar die Ausgabe darum trotzdem ansehen.
7. Auch eine **früh geschlossene Ausgabe** (`… 2>&1 | head`) bricht das Aufräumen nicht ab: Datei und Sperre werden trotzdem zurückgesetzt (Issue #651).
8. **Abgebrochener Testlauf** (Issue #657): Laufen mit der Änderung weniger Tests als im Kontrolllauf, meldet das Skript „ABORTED - only 0 of 25 test(s) ran with the change“ und endet mit Exit-Code `4`, nicht mit „SURVIVED“. So sieht es aus, wenn die Änderung den Testprozess abstürzen lässt (etwa durch Endlosrekursion): `dotnet test` schreibt dann eine Ergebnisdatei mit `total="0"`. Die grünen Tests sagen in diesem Fall nichts; rote, die es vor dem Abbruch gab, zählt das Skript mit auf, dazu die Ursache und das Ende der Testausgabe. Weil nach einem Absturz am Ende nur Stackzeilen stehen, greift es die Zeilen mit `Stack overflow`, `crashed`, `abgebrochen` oder `aborted` von weiter oben heraus (Issue #660); den Pfad der Ergebnisdatei kürzt es auf `...\results.trx`. Dasselbe Ergebnis kommt, wenn die Änderung Tests wegnimmt oder die Zahl der Testfälle absichtlich verschiebt (etwa Testdaten einer `Theory` aus `MemberData`) – dann ist Exit-Code `4` kein Absturz, sondern die Probe zählt anders; eine Probe wählen, die die Zahl der Fälle gleich lässt.
9. **Hängender Test** (Issue #712): Macht die Änderung aus einer Schleife im Test eine Endlosschleife (etwa `while (player.IsDodging)`, wenn der Ausweich-Countdown nicht mehr zählt), beendet `dotnet test` den Test nach dem Zeitlimit (`--timeout`, Vorgabe 120 s). Das Skript meldet dann „TIMED OUT - a test ran longer than 120 s with the change and was ended (endless loop?)“, nennt den Test, zählt rote Tests von davor auf und endet mit Exit-Code `4`: Die Tests nach dem hängenden liefen nicht, das Ergebnis sagt also nichts über sie. **Für die Frage der Gegenprobe heißt TIMED OUT praktisch „bemerkt“:** Die CI hat kein Zeitlimit und bliebe an dieser Stelle hängen, die Änderung käme also nicht unbemerkt durch. Exit-Code `4` bleibt trotzdem, weil das Skript nicht weiß, ob der Test nur länger braucht. Wurde nach dem Zeitlimit keine Ergebnisdatei geschrieben, bleibt es TIMED OUT (nicht „does not compile“), nur die Zahl der fertigen Tests ist dann unbekannt (Issue #733). Dass es das Zeitlimit war und kein Absturz, liest das Skript aus der Ausgabe von `dotnet test` (englisch „inactivity time“ oder deutsch „Inaktivitätszeit“); welcher Test hing, aus der Ablaufdatei des Blame-Sammlers (`Sequence_*.xml`, der Test mit `Completed="False"`). Fehlt sie, steht dort `?`. Bei einer anderen Sprache von `dotnet` kommt statt „TIMED OUT“ das „ABORTED“ aus Punkt 8 – auch dann endet der Lauf. Hängt schon der Kontrolllauf, endet das Skript mit Exit-Code `2`, bevor es etwas ändert. Braucht ein Test nur länger als 120 s, die Grenze anheben (`--timeout 300`). Datei, Sperre und Arbeitsordner werden wie bei jedem anderen Ausgang zurückgesetzt. Ohne Zeitlimit (`--timeout 0`) blockiert ein hängender Test die Probe wie früher, bis man sie mit Strg+C abbricht.

   **Warum 120 s** (Issue #733): Der langsamste Test, `EmergenceTests.After_sealing_every_enemy_of_the_room_comes_out_in_the_room_away_from_its_doors_and_the_room_can_be_cleared`, braucht allein 43 bis 46 s (drei Läufe mit engem Filter), im vollen Lauf laut Review von PR #732 78 s. Mit einem engen Filter läuft er fast allein, und bei belastetem Rechner konnten 60 s reißen – das kostete einen Lauf mit falschem „TIMED OUT“. 120 s sind mehr als das Doppelte seiner Dauer allein und liegen über dem Wert aus dem vollen Lauf. Der Preis: Ein echter Hänger kostet bis zum Ende Zeitlimit plus Bau und Kontrolllauf (mit 60 s waren es gemessen 85 s, mit 120 s also etwa zweieinhalb Minuten). **Für Gegenproben an Schleifen**, bei denen man einen Hänger erwartet, darum `--timeout 20` nehmen, wenn kein Test im Filter so lange braucht.

   **Abbruch von außen:** Strg+C im Terminal trifft die ganze Prozessgruppe, also auch `dotnet test`; das Skript räumt sofort auf. Ein `kill -TERM` nur an das Skript wirkt dagegen erst, wenn der laufende `dotnet test` endet, denn Bash führt den Trap erst nach dem Befehl im Vordergrund aus. Bei einem hängenden Test ist das nach dem Zeitlimit; mit `--timeout 0` endet er nie, und solange bleiben Datei geändert und Sperre liegen. Darum zum Abbrechen Strg+C im Terminal nehmen; sobald `dotnet test` endet, räumt das Skript wie gewohnt auf.

Ausgabe, gekürzt:

```
   control run: 25 test(s), all green
   result: CAUGHT - 2 of 25 test(s) red:
     ...GrateTests.A_player_who_dies_in_the_sealed_arena_leaves_it_sealed_and_the_restart_brings_the_grates_up
     ...GrateTests.An_arena_next_to_another_seals_again_the_same_way_after_death_and_restart
```

**Exit-Code:** `0` gefangen (mindestens ein Test rot), `1` überlebt (alle grün – die Tests prüfen die Stelle nicht), `2` konnte nicht laufen (falscher Aufruf, etwa eine Option ohne Wert, anderer Lauf im selben Arbeitsordner, Arbeitsordner unsauber, Text nicht gefunden oder ohne `--all` mehrmals gefunden, Kontrolllauf rot oder über dem Zeitlimit, der Bot endete mit einem anderen Code als 0 oder 1 …), `3` kein Ergebnis mit der Änderung (sie kompiliert nicht, eine gebaute Datei ließ sich nicht kopieren, oder das Testprojekt baut nicht – das Ergebnis sagt nichts über die Tests), `4` der Testlauf mit der Änderung brach ab (weniger Tests als im Kontrolllauf, etwa weil der Testprozess abstürzte, oder ein Test lief länger als `--timeout` – ebenfalls keine Aussage über die Tests).

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

Das Skript baut `tools/SilverAndAsh.BotRun` (mit dem Core) in Release und startet den Bot. Der Kontrolllauf muss ohne Befund enden, sonst Exit-Code `2`. Mit der Änderung heißt ein Befund „CAUGHT“ (Exit-Code `0`, die ersten zehn `FINDING`-Zeilen), keiner „SURVIVED“ (Exit-Code `1`). Den Exit-Code des Bots selbst nennt die Ausgabe dazu („bot exit code 1“ bei Befunden, Issue #660), weil er sich vom Exit-Code des Skripts unterscheidet. Baut schon der Kontrolllauf des Bots nicht, endet das Skript mit `2`, bevor es etwas ändert. Endet der Bot selbst mit Exit-Code `2` (falscher Aufruf, Werkzeug passt nicht zum Core) oder mit einem anderen Code als 0 oder 1 (etwa `134` nach einem Absturz), endet auch das Skript mit `2` und nennt den echten Code („bot exit code 134“) samt den letzten fünf Zeilen der Bot-Ausgabe; kompiliert die Änderung nicht, mit `3`. Die Argumente werden an Leerzeichen getrennt, Anführungszeichen darin gibt es nicht.

**Kein Zeitlimit für den Bot** (Issue #733): `--timeout` gilt nur für `dotnet test`. Der Bot braucht kein eigenes, denn jede seiner Schleifen über Ticks ist begrenzt (`Play.Ticks(n, …)`, `Play.TicksUntil(max, …)` und die `for`-Schleifen in `BossPortalRuns.cs` und `ArenaDoors.cs` mit fester Obergrenze); eine Änderung, nach der eine Tür nie sperrt oder ein Gegner nie stirbt, wird so ein Befund und kein Hänger. Hängen kann er nur, wenn die Änderung einen einzelnen Tick im Core endlos laufen lässt – eine solche Stelle prüft man mit Tests, deren Zeitlimit sie fängt. Ein festes Limit müsste außerdem je Modus und Seed-Zahl anders sein (`boss` etwa 5 s, `all` etwa 6 min, `stuck` mit 40 Seeds etwa 6 min). Hängt ein Bot-Lauf doch, mit Strg+C abbrechen (wie oben unter Punkt 9: ein `kill -TERM` nur an das Skript wirkt erst, wenn der Bot endet).

**Nach jeder Änderung neu bauen**, bevor man mit `--no-build` startet – auch nach dem Zurücksetzen und nach einem fehlgeschlagenen Build (etwa einer Probe, die nicht kompiliert). Sonst läuft die alte DLL und meldet fälschlich Exit-Code `0`.

Belegte Proben für #647, jeweils mit `--bot`, alle gefangen (Exit-Code `0`). Die Suchtexte kommen in ihrer Datei genau einmal vor und lassen sich so übernehmen:

| Datei, was | `<suchen>` → `<ersetzen>` | Bot | Befunde |
|---|---|---|---|
| `tools/SilverAndAsh.BotRun/BossPortalRuns.cs`, Raster stehend halb so fein | `PortalSite.Steps(-1.95f, 1.95f, 0.05f)` → `PortalSite.Steps(-1.95f, 1.95f, 0.1f)` | `slam --seeds 1` | Untergrenze: Spieler 8, Ghul 7, Fledermaus 11 je Seite |
| `BossPortalRuns.cs`, Raster mit Schwung halb so fein | `PortalSite.Steps(-1.9f, 1.9f, 0.1f)` → `PortalSite.Steps(-1.9f, 1.9f, 0.2f)` | `slam --seeds 1` | Untergrenze: Spieler 5, Ghul 7, Fledermaus 9 je Seite |
| `BossPortalRuns.cs`, Schläge halb so fein | `PortalSite.Steps(-2.6f, 2.6f, 0.4f)` → `PortalSite.Steps(-2.6f, 2.6f, 0.8f)` | `bat --seeds 1` | Untergrenze: 21 und 15 statt 27 |
| `BossPortalRuns.cs`, stehender Körper 2 m vor seinem Platz | `var at = body.Place(play.World.Level, site.At(across, along));` → `var at = body.Place(play.World.Level, site.At(across, along - 2f));` | `slam --seeds 1` | „not in the doorway at the slam“ |
| `BossPortalRuns.cs`, Körper mit Schwung 20 Ticks zu spät losgelassen | `var hold = shut - (inDoorway ?? shut);` → `var hold = shut - (inDoorway ?? shut) + 20;` | `slam --seeds 1` | „not in the doorway at the slam“ |
| `src/SilverAndAsh.Core/Gameplay/GameWorld.cs`, die Tür schiebt 1 cm zu weit | `const float Gap = ThresholdGap;` → `const float Gap = ThresholdGap * 2f;` | `slam --seeds 1` | „not in the doorway at the slam“ |
| `tools/SilverAndAsh.BotRun/Play.cs`, Messhöhen doppelt so weit | `block.Top - block.Bottom) * 0.8f;` → `block.Top - block.Bottom) * 1.6f;` | `slam --seeds 1` | „the stone check misses the block 3.075..3.200“ (Kämpfer) und weitere |
| `Play.cs`, Messhöhen ohne 1 cm Abstand zu den Füßen | `var y = body.Position.Y + PortalStoneInset + up;` → `var y = body.Position.Y + up;` | `bat --seeds 1` | 4 × „dead bat in the stone“ auf dem Sockel |

Belegte Proben für #688, jeweils `--bot` mit `slam --seeds 1`:

| Datei, was | `<suchen>` → `<ersetzen>` | Ergebnis |
|---|---|---|
| `assets/balance.json`, nur der aufgeladene Rückstoß gesenkt | `"chargedKnockback": 6,` → `"chargedKnockback": 0.5,` | Exit `2`, „bot exit code 3“ mit der Warnung „'club.knockback' is above 'club.chargedKnockback'“ (vorher SURVIVED) |
| `assets/balance.json`, beide Rückstöße gesenkt (`--regex`) | `"knockback": 3,([^\n]*\n\s*)"chargedKnockback": 6,` → `"knockback": 0.5,${1}"chargedKnockback": 0.5,` | gefangen: Untergrenze bei Ghul und Fledermaus, „not carried“ 14 statt 5 und 13 statt 0 je Seite |
| `BossPortalRuns.cs`, der Filter „nicht getragen“ schluckt ±0,9 m | `carried = running.Carried(probeEnemy);` → `carried = running.Carried(probeEnemy) && MathF.Abs(across) is < 0.85f or > 0.95f;` | gefangen: Ghul 6 statt 5, Spieler und Fledermaus 1 statt 0 je Seite |
| `BossPortalRuns.cs`, Fledermaus immer „getragen“ | `"bat", report), KnockIn, IsKnocked, KnockTicks)` → `"bat", report), KnockIn, _ => true, KnockTicks)` | gefangen: „still carried 66 ticks after it was let go, its knock lasts 15“ |

Den Abstand selbst (`GameWorld.ThresholdGap`) liest der Bot aus dem Core: Wird er absichtlich geändert, bleibt der Bot still; nur eine Tür, die anders schiebt als dieser Wert sagt, ist ein Befund.

Dazu ohne `--bot`: `BodySizes.Bat.StepHeight` von unendlich auf `0.4f` macht 4 von 137 Tests mit Filter `Bat` rot (`BossPortalNeighbourTests`).

Belegte Proben für `slam` (#629), jeweils `slam --seeds 1`:

| Änderung in `src/SilverAndAsh.Core/Gameplay/GameWorld.cs` | Ergebnis |
|---|---|
| In `PushedAcross`: `return portal.PushedAcross(across, radius);` → `_ = portal;` (das Portal wird nicht beachtet) | Exit `1`, 2 Befunde „player in the stone of the boss portal“ |
| In `PushOutOf`, Zweig der Gegner: `enemy.PlaceAt(PushedInto(…, enemy.Body.Radius));` → `enemy.PlaceAt(PushedInto(…, enemy.Body.Radius) + new Vector3(0.6f, 0f, 0.6f));` | Exit `1`, „in the stone of the boss portal“: Gorm 7, Ghul 13, Fledermaus 15; dazu Ghule und Fledermäuse in der sperrenden Tür |

Überlebt die Probe, zuerst das Raster prüfen: Ein Fehler, der nur an einer schmalen Stelle auftritt (etwa am Rand eines Bogens, #629), fängt der Bot nur, wenn sein Raster bis dorthin reicht. `slam` geht deshalb quer zum Durchgang alle 5 cm und nimmt die erste freie Tiefe, statt feste Punkte abzulaufen. Dass das Raster fein genug bleibt, sichern die Untergrenzen (siehe *Übersprungene Plätze*).
