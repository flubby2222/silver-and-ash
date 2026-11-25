# Foto-Skript

Das Spiel kann sich selbst fotografieren: Es baut eine festgelegte **Szene** mit festem Seed auf, lässt die Simulation eine festgelegte Zahl von Ticks laufen, speichert zwei PNG-Dateien und beendet sich – ohne Maus, Tastatur oder Zuschauer. Gedacht für Tester (Bildschirmfotos zu jedem sichtbaren PR), Designer (Probestücke „wie im Spiel“) und die Leitung (Stil-Vergleiche). Issue #224.

## Aufruf

```
dotnet run --project src/SilverAndAsh.Game -- --photo ghoul-4m
```

```
dotnet run --project src/SilverAndAsh.Game -- --photo all --out D:\fotos\lauf-1
```

```
dotnet run --project src/SilverAndAsh.Game -- --photo list
```

- `--photo <szene>` – eine Szene; `all` – alle nacheinander; `compare` – die Vergleichsszenen in allen Bild-Varianten (siehe unten); `list` – Namen und Beschreibung aller Szenen ausgeben (ohne Fenster).
- `--out <ordner>` – wohin die Bilder kommen. Ohne `--out`: `%LOCALAPPDATA%\SilverAndAsh\photos`. **Nie ins Repository legen.** Der Ordner von `--out` muss **leer oder neu** sein (Issue #639); liegt schon etwas darin, bricht das Spiel vor dem ersten Bild mit Exit-Code `1` und der Meldung „The folder for the pictures is not empty: …“ ab (ohne den Vorsatz „Could not load the level“ anderer Startfehler, Issue #651) – sonst lägen Bilder eines früheren Laufs neben den neuen und würden versehentlich mit ihnen verglichen. Für jeden Lauf also einen neuen Ordner nehmen (`lauf-2`, `vergleich-2` …). Der Standardordner ohne `--out` wird nicht geprüft; dort überschreibt jede Szene nur ihre eigenen zwei Dateien. Clips (`--clip`) räumen ihre Einzelbilder weiter selbst weg (siehe unten). **Bekommt eine Option eine neue Bedingung** wie diese, gehören alle Beispiele der Option in der Doku in denselben PR (`git grep -n -e "--out" -- docs`); sonst führen alte Beispiele beim zweiten Lauf in den neuen Fehler (Issue #651).
- Pro Szene entstehen zwei Dateien:
  - `<szene>.png` – das 3D-Bild in Spielauflösung (854 × 480), so wie es die Grafikkarte zeichnet, ohne HUD.
  - `<szene>-screen.png` – das ganze Fenster (1280 × 720) mit HUD, Karte, Inventar usw. **Schilder** (Musterraum, Prüfstrecke) und der Name des Levels stehen nur hier: Sie werden über das 3D-Bild gelegt und fehlen in `<szene>.png`.
  - Blickwinkel (70° senkrecht), Höhe des 3D-Bilds (480 Zeilen) und Startfenster (1280 × 720) stehen an einer Stelle, `CameraFrame` in Core (Issue #736); Spiel und Tests der Fotos rechnen damit.
- `--window <breite>x<höhe>` – andere Fenstergröße, z. B. `--window 2240x960` für 21:9. Das 3D-Bild behält 480 Zeilen, seine Breite folgt der Form des Fensters (21:9 → 1120, 16:10 → 768, 4:3 → 640; #350). Das Fenster muss ganz auf den Bildschirm passen, sonst ist das Fensterbild abgeschnitten. **Schärfer wird das 3D-Bild dadurch nicht:** `--window` vergrößert nur das Fensterbild (`-screen.png`); das 3D-Bild bleibt bei 480 Zeilen, nur seine Breite folgt der Form (gemessen: `--window 1600x600` gibt `-screen.png` 1600 × 600 und das 3D-Bild 1280 × 480). Für mehr Bildpunkte im 3D-Bild gibt es keinen Schalter.
- Für einen Durchlauf öffnet sich kurz ein Fenster; es schließt sich von selbst. `all` dauert wenige Sekunden.
- Die FPS-Anzeige und die Debug-Anzeige (Geschwindigkeit, Abklingzeiten) fehlen auf den Fotos – sie würden nur Zufallswerte zeigen.

**Nur in der Debug-Version** (`dotnet run`), wie `--give`. Die Release-Version für Spieler bricht mit einer Meldung ab, wenn jemand `--photo` angibt; sie schreibt keine Dateien.

## Szenen

`--photo list` zeigt die aktuelle Liste. Die Szene `entrance` steht nicht darin: Sie gehört zu einer einzelnen Raumvorlage und geht nur zusammen mit `--room` (`--room <datei> --photo entrance`, siehe [level-format.md](level-format.md); dafür wird nur diese eine Vorlage geladen, Issue #534). Stand bei Einführung:

| Szene | Zeigt |
|---|---|
| `start-room` | Ebene 0, wie ein Durchlauf beginnt: Startraum, Ebenen-Name, Standardwaffen |
| `room-combat`, `room-corridor`, `room-treasure`, `room-secret` | je einen Raum dieser Art aus seiner entferntesten Ecke, Blick zur Raummitte |
| `room-antechamber` | den Vorraum vom Eingang aus: seitlich die Bosstür, in der fernen Ecke der Boss-Tor-Teleporter (Issue #565) |
| `room-boss` | den Bossraum aus 12 m, Gorm im Blick |
| `ghoul-4m`, `ghoul-10m`, `bat-4m`, `bat-10m` | einen Ghul bzw. eine Fledermaus in einer leeren Halle aus 4 m bzw. 10 m |
| `ghoul-windup` | einen Ghul beim Ausholen (Hälfte der Ausholzeit) |
| `bat-screech` | eine Fledermaus beim Kreischen, der Ankündigung des Sturzflugs (Hälfte der Kreischzeit) |
| `bat-dive` | eine Fledermaus im Sturzflug auf den Spieler |
| `pistol-recoil` | die Pistole gleich nach dem Schuss: hochgerissen, Hahn gefallen, Mündungsfeuer |
| `blade-charged` | die Silberklinge voll aufgeladen: zurückgezogen und zitternd |
| `blade-swing-half` | ein schneller Klingenhieb halb ausgeholt: der Unterarm dreht sich zur Schulter (#265) |
| `blade-swing-full` | ein schneller Klingenhieb auf dem Höhepunkt: der Unterarm dreht sich zur Schulter (#265) |
| `club-rest` | die Keule als Modell in der Hand, in Ruhe, gehalten wie die Klinge (#403) |
| `club-swing-half` | ein schneller Keulenhieb halb ausgeholt, dieselbe Bewegung wie bei der Klinge (#403) |
| `showroom-club` | die Keule in der Hand vor der liegenden Keule (Worn) im Schauraum (#403) |
| `gorm` | Gorm aus 6 m in seinem Bossraum |
| `gorm-swing-windup` | Gorm wirbelt die Kette über dem Kopf: die Ankündigung des Kettenschwungs, die Kette gerade zu seiner Rechten; in einer 6 m hohen Halle wie sein Bossraum (#254) |
| `gorm-swing` | Gorm im Kettenschwung von seiner Linken zu seiner Rechten, ein Drittel des Bogens; in einer 6 m hohen Halle wie sein Bossraum (#254) |
| `gorm-throw-windup` | Gorm vor dem Hakenwurf, der Haken glüht rot: die Ankündigung, zur Hälfte vorbei; in einer 6 m hohen Halle wie sein Bossraum (#254) |
| `gorm-hammer-windup` | Gorm reißt die Keule hoch über den Kopf: die Ankündigung des Hammerschlags, kurz vor dem Höhepunkt; in einer 6 m hohen Halle wie sein Bossraum (#254) |
| `gorm-shockwave` | Gorms Schlagwelle halb ausgelaufen (Radius 6 m) aus 14 m, wie das Ende von `gorm-slam-far` angehalten; mit `--bench` misst man, was das Zeichnen der Welle kostet (#299) |
| `gorm-phase2` | Gorm in Phase 2 (rote Male) aus 10 m in seinem Bossraum, am Ende seines Brüllens |
| `hud-weapons` | das HUD mit allen Waffenplätzen belegt (Keule, Pistole, Gewehr, Donnerbüchse) |
| `hud-rifle` | wie `hud-weapons`, aber mit dem Gewehr (2) in der Hand |
| `hud-reload` | die Pistole im Startraum leergeschossen: „Reload [R]“ unter dem Fadenkreuz (Issue #259) |
| `hud-texts` | alle Schriftzüge zugleich, jeder in seiner Zeile: „Reload [R]“ und „[F] Take Rifle (Forged)“ unter dem Fadenkreuz, „[X] Choose boon“ und die Weihezeit unten links (Issue #340) |
| `hud-skillpoints` | das HUD mit dem ersten Skillpunkt: Platzhalter-Text „1 skill point“ rechts neben dem Erfahrungsbalken, alle Boons gewählt (Issue #502) |
| `inventory` | die Menüseite „Equipment“ (G) mit voller Tasche, oben die beiden Reiter (Issue #348) |
| `map` | die Menüseite „Map“ (Tab) früh im Durchlauf: links Werte und fünf gewählte Boons, rechts die Karte (Issues #347, #348) |
| `map-skillpoint` | die Menüseite „Map“ (Tab), während ein Bonus wartet: oben links der gelbe Hinweis „[X] to level up“ über der Werte-Tafel (Issue #387) |
| `chests-4m` | die Kisten aller vier Stufen nebeneinander aus 4 m, geschlossen und verschlossen, dazu eine offene (Issue #249) |
| `door-normal`, `door-locked`, `door-boss` | die Tür dieser Art geschlossen in einer leeren Halle aus 6 m (Issue #245) |
| `door-normal-half`, `door-locked-half`, `door-boss-half` | dieselbe Tür halb aufgeschwungen (Fortschritt 0,5, im Bild geglättet) |
| `door-normal-open`, `door-locked-open`, `door-boss-open` | dieselbe Tür ganz offen |
| `door-normal-10m`, `door-locked-10m`, `door-boss-10m` | dieselbe Tür geschlossen aus 10 m: Ist die Türart noch zu erkennen? |
| `door-cracked` | die rissige Wand zum Geheimraum, ungebrochen |
| `cracked-wall-2m`, `cracked-wall-4m`, `cracked-wall-10m` | die erste rissige Wand der Ebene 0 (ab Seed 1), wie der Generator sie gesetzt hat, aus 2, 4 und 10 m von außen: sitzt die Platte ohne Naht in der Wand (Issue #246)? |
| `cracked-wall-flat` | dieselbe Art Wand von 1 m vor der Fläche und 6 m daneben, also sehr flach (etwa 9°): flimmert die Platte? |
| `cracked-wall-a-4m`, `cracked-wall-b-4m`, `cracked-wall-c-4m` | je eine rissige Wand, deren Fläche die Wandvariante A, B bzw. C zeigt, aus 4 m |
| `cracked-wall-sideways-4m` | eine rissige Wand in einem Ost-West-Durchgang aus 4 m: die Platten um 90° gedreht |
| `cracked-wall-secret-4m` | eine rissige Wand von innen, aus dem Geheimraum, aus 4 m |
| `cracked-wall-break`, `cracked-wall-break-after` | eine rissige Wand aus 2 m vom Geheimraum aus mit voll aufgeladener Klinge zerschlagen: 8 bzw. 100 Ticks nach dem Schlag (Brocken im Flug bzw. am Boden). Im Geheimraum, weil kein Gegner hineinkommt und das Bild stört |
| `door-sideways-half` | eine Holztür in einem Ost-West-Durchgang, halb offen: das Modell ist um 90° gedreht |
| `door-level-seed1`, `door-level-seed2-boss` | die erste Holztür in Seed 1 bzw. das Bosstor in Seed 2, aus 2–3 Zellen Abstand |
| `door-normal-open-below`, `door-barred-open-below` | der Spitzbogen einer Holztür mit halb geöffneten Flügeln, 1,5 m vor ihrer Ebene stehend und schräg nach oben auf den Bogenansatz (den Kämpfer, 0,9 m neben der Mitte, 2 m hoch) blickend; die Hände der Waffe verdecken ihn nicht. Die zweite Szene ist dieselbe Tür mit hochgezogenem Fallgitter (`HasGrate`): Das Gitter selbst wird nicht gezeichnet, der Führungsring schon, und er schaut am Bogenansatz hervor; die beiden Bilder unterscheiden sich nur dort (Issues #730, #722) |
| `door-level-barred` | eine offene Tür des ersten Kampfraums (wie die, durch die man kam) von innen aus 2–3 Zellen Abstand: Der Raum hat beim Betreten versiegelt, das Bild entsteht, sobald das Fallgitter ganz unten ist. Zeigt das Gitter im Level, ohne Modell den Platzhalter (Issue #558) |
| `door-level-barred-shut` | dasselbe mit einer geschlossenen Tür des Kampfraums: Ist das Gitter vor den Flügeln zu sehen? (Issue #572) |
| `menu-title`, `menu-pause`, `menu-settings` | das Titelbild, das Esc-Menü und die Einstellungen (aus dem Esc-Menü) über dem Startraum, gezeichnet wie im Spiel (Issue #349) |
| `teleporter-locked-3m`, `teleporter-unlocked-3m`, `teleporter-locked-10m`, `teleporter-unlocked-10m` | ein Teleporter gesperrt bzw. freigeschaltet in einer leeren Halle aus 3 m bzw. 10 m, Blick auf den Kristall (Issue #480) |
| `teleporter-hint` | Ebene 0, alle Teleporter freigeschaltet, man steht auf dem im Startraum: „[T] Travel“ unter dem Fadenkreuz |
| `well-hint` | der Brunnen „Sirius' Relief“ aus 2 m in einer leeren Halle, in Reichweite: „[F] Use Sirius' Relief“ unter dem Fadenkreuz (Issue #716) |
| `menu-travel` | wie `teleporter-hint`, dazu die offene Zielwahl (T): Karte, gewähltes Ziel und Knopf „Travel“ |
| `menu-travel-sealed` | die Zielwahl mit einem Boss-Tor-Teleporter in einem versiegelten Kampfraum, dessen Gegner leben (wie im Koop, wenn ein anderer Spieler dort kämpft): das Ziel steht als „Room sealed“ da. Seit dem Vorraum (Issue #565) steht dieser Teleporter in echten Ebenen nie in einem Kampfraum; die Szene setzt ihn dafür in den Kampfraum vor dem Vorraum (erster Seed ohne Gang davor) |
| `model-probe-4m`, `model-probe-10m` | ein einzelnes Ziel auf leerem Boden aus 4 m / 10 m – für Modelle unter Spielbedingungen |
| `stairs-4m`, `stairs-10m` | die Wendeltreppe im Bossraum der Ebene 0 nach Gorms Tod (Issue #300): Gorm stirbt vor dem ersten Tick, die Treppe steigt wie im Spiel auf; das Bild entsteht, sobald sie steht und ihre Lampe leuchtet, aus 4 m bzw. 10 m vom Fuß, mit Blick auf ihre halbe Höhe |
| `showroom-stairs` | die Wendeltreppe im Raumteil des Musterraums aus 5 m, mit Blick auf zwei Drittel ihrer Höhe: Treppe und Schild „Spiral stairs“ unter der 6-m-Decke, nichts ragt hindurch (Issue #727) |
| `showroom-boss-door` | die Bosstür im Abschnitt A des Musterraums aus 4,5 m vor ihrer Mitte, mit Blick auf 2,5 m Höhe: das Portal über zwei Zellen mit seinem Schild (Issue #599) |
| `course-boss-stairs` | der Bossraum der Prüfstrecke nach dem Sieg (Issue #727): Gorm stirbt vor dem ersten Tick, die Treppe steigt wie im Spiel auf; das Bild entsteht, sobald sie steht, aus 6 m zwischen Portal und Treppe, mit Blick auf zwei Drittel ihrer Höhe bis unter die Decke; das Bild entsteht erst, wenn der Name des Levels verblasst ist (`CourseScenes.BannerTicks`, 215 Ticks, aus `LevelNameBanner.Seconds` in Core berechnet, Issue #736). Das Schild der Station 6 hängt im Vorraum, hinter der Südwand und weiter als 16 m entfernt, und kann hier nicht im Bild sein (Issue #722) |
| `equipment-rifle` | das Gewehr (Forged) im Musterraum aus 0,8 m: „[F] Take Rifle (Forged)“ in Stahlblau (Issue #317) |
| `equipment-worn`, `equipment-forged`, `equipment-royal`, `equipment-vampiric` | je einen Gegenstand dieser Stufe im Musterraum aus 0,8 m: sein Schein und „[F] Take …“ in der Farbe seiner Stufe |
| `equipment-taken-royal`, `equipment-taken-vampiric` | Hunter's Eye (Royal) bzw. Leech (Vampiric) im Musterraum mit F genommen: die Karte links, 45 bzw. 15 Ticks danach – beim vampirischen Fund noch mit dem blutroten Schein vom Rand |
| `column-intact-6m`, `column-hit1-6m`, `column-hit2-6m`, `column-broken-6m` (auch `-10m`) | je eine Stufe der zerbrechenden Säule im Musterraum aus 6 m bzw. 10 m: unversehrt (Z0), nach einem Schlag (Z1), vor dem letzten Schlag (Z2), zerbrochen (Z3); die Schlagseite zeigt zur Kamera (Issue #601) |
| `showroom-a-corridor`, `showroom-a-room`, … `showroom-c-room` | je einen Teil des Musterraums (Issue #283, [musterraum.md](musterraum.md)): Wandvariante A, B oder C, der Gangteil (3,5 m) mit den Türen und der rissigen Wand bzw. der Raumteil (6 m) mit den Stücken, vom Westende des südlichen Laufwegs schräg auf die Nordwand, mit Schildern |

## Bild-Varianten vergleichen (`--look`, `--photo compare`)

Für ADR 0010 gab es vier Bild-Varianten zum Vergleich (#234). Der Mensch hat Variante 2 gewählt; sie ist seit #237 der Standard in Debug und Release. Die anderen bleiben als Debug-Werkzeug:

| `--look` | Variante |
|---|---|
| `1` | ohne Körnung: Nebel, volle Farbtiefe, 854 × 480 (bis #237 der Standard) |
| `2` | 15 Bit (5 je Kanal) mit festem 4 × 4-Bayer-Muster, Nebel bleibt (**Standard**, auch ohne `--look`) |
| `3` | wie 2, aber kein Nebel: ab 9 m fallen Pixel nach dem Muster weg (bei 18,2 m alle), die Laterne dunkelt weiter ab |
| `4` | wie 3 in 640 × 360 |

```
dotnet run --project src/SilverAndAsh.Game -- --look 3
```

```
dotnet run --project src/SilverAndAsh.Game -- --photo compare --out D:\fotos\vergleich-1
```

- `--look <1-4>` gilt fürs Spielen und für einzelne Fotos (`--photo ghoul-4m --look 4`).
- `--photo compare` nimmt Gang, Kampfraum, Ghul aus 4 m und 10 m, Ghul beim Ausholen, Fledermaus aus 10 m und Gorm in allen vier Varianten auf (`<szene>-look<n>.png`, `<szene>-look<n>-screen.png`) und legt je Szene ein beschriftetes 2 × 2-Vergleichsbild `<szene>-compare.png` aus den Fensterbildern an.
- Die Werte stehen in [`LookVariants.cs`](../src/SilverAndAsh.Game/Rendering/LookVariants.cs) neben `LookSettings`, die Formeln getestet in [`Dithering.cs`](../src/SilverAndAsh.Core/Presentation/Dithering.cs); der Shader `assets/shaders/level.fs` rechnet dieselben.

## Wie es funktioniert

- **Die Szenen stehen im Core**, an einer Stelle: [`PhotoScenes.cs`](../src/SilverAndAsh.Core/Photos/PhotoScenes.cs). Jede Szene ist ein Name, eine Zeile Beschreibung und eine Funktion, die mit dem [`PhotoStudio`](../src/SilverAndAsh.Core/Photos/PhotoStudio.cs) eine [`PhotoSetup`](../src/SilverAndAsh.Core/Photos/PhotoSetup.cs) baut: die Welt, die Zahl der Ticks, die Eingaben je Tick, wann früher abgedrückt wird, welches Menü (Titel, Pause, Einstellungen) darüber liegt.
- **Kein Sonderweg in den Spielregeln:** Die Welten sind normale `GameWorld`s (Ebene 0 mit Seed `PhotoStudio.Seed`, oder eine kleine handgebaute Halle). Gewählt wird nur, wo der Spieler steht (`Player.PlaceAt`, `internal` – nur der Core kann es aufrufen) und wohin er schaut (über die normale Blick-Eingabe). Waffen kommen über `Equip`/`Stow`, das Menü öffnet sich über die Eingabe `PressMenu` (`MenuKey.Equipment` wie bei G, `MenuKey.Map` wie bei Tab).
- **Das Spiel zeichnet die Fotos mit demselben Code wie im Spiel** (`DrawWorld`/`DrawScreen` in `Program.cs`): pro Tick werden die Ereignisse gelesen und die Ansichten um 1/60 s weitergestellt, danach wird ein Bild gezeichnet und gespeichert. Was sich mit der Zeit bewegt (etwa das Pulsieren von Vampiric), folgt dieser Uhr der Ansichten, nicht der Zeit seit Fensterstart – sonst wäre z. B. „[F] Take …“ in `equipment-vampiric-screen.png` bei jedem Lauf etwas anders gefärbt (Issue #639). Zwei Läufe derselben Szene geben so bytegleiche Bilder (geprüft mit `cmp` an `equipment-vampiric` und `map`, seit Issue #651 auch an `inventory` und `menu-travel`: Das Inventar nimmt das Pulsieren aus derselben Uhr, die Zielwahl zählt den Doppelklick mit ihrer eigenen Uhr seit dem Öffnen). Im Darstellungs-Code steht kein `Raylib.GetTime()` mehr; neue Bewegungen bekommen die Uhr der Ansichten übergeben.
- Die Raum-Szenen nehmen den ersten Seed ab `PhotoStudio.Seed`, dessen Ebene so einen Raum hat. Ändert sich der Generator, wandern die Bilder mit – sie bleiben aber für denselben Stand gleich (Tests prüfen das).

## Eine Szene ergänzen

Eine Zeile in der passenden Themenliste in [`PhotoScenes.cs`](../src/SilverAndAsh.Core/Photos/PhotoScenes.cs) (etwa `GhoulScenes()`, `HudScenes()`, `MenuScenes()`; `PhotoScenes.All` setzt sich aus diesen Listen zusammen, Issue #589), zum Beispiel ein Ghul aus 6 m:

```csharp
new("ghoul-6m", "A ghoul 6 m away in a bare hall.",
    studio => new PhotoSetup(studio.Arena('G', 6))),
```

Bausteine im `PhotoStudio`:

- `StartRun()` – Ebene 0 wie zu Beginn eines Durchlaufs.
- `InRoom(RoomKind.…)` – Ebene 0, Spieler in einem Raum dieser Art.
- `BeforeGorm(metres)` – Ebene 0, Spieler im Bossraum, so weit von Gorm, Blick auf ihn.
- `Arena(figur, metres)` – leere Halle, eine Figur (`G` Ghul, `B` Fledermaus, `T` Gorm, `D` Puppe) in dieser Entfernung (Vielfaches von 2 m).
- `PhotoStudio.Face(player, punkt)` – Blick auf einen Punkt richten.

Und in der `PhotoSetup`: `Ticks` (wie lange die Welt läuft), `Input` (Eingabe je Tick, z. B. `PressMenu = MenuKey.Equipment` in Tick 0), `StopWhen` (früher abdrücken, z. B. sobald ein Ghul ausholt), `Menu` (Titel, Pause oder Einstellungen darüber), `IsModelProbe`.

Danach: Name in den Test `Every_scene_the_issue_asks_for_is_there` aufnehmen ([`PhotoSceneTests.cs`](../tests/SilverAndAsh.Core.Tests/Photos/PhotoSceneTests.cs)), die Szene einmal laufen lassen und **das Bild ansehen**.

Die Aufnahme [`photo-scenes.txt`](../tests/SilverAndAsh.Core.Tests/Photos/photo-scenes.txt) hält jede Szene in der Reihenfolge von `--photo all` als eine Zeile fest: Name, Ticks, Menü, Schalter, Beschreibung. Eine neue Szene bekommt dort **genau eine neue Zeile** an ihrer Stelle; ist der Test `Every_scene_keeps_its_place_name_description_and_setup_as_recorded` rot, nennt er Zeilennummer, aufgenommene und jetzige Zeile. Ändert sich dabei die Zeile einer **anderen** Szene, ist das ein Warnsignal, kein Grund zum Abnicken. Aufnahme-Tests vergleichen nur, was eine Szene selbst festlegt – nichts, was aus dem Balancing folgt (wie lange sie läuft, wo der Spieler am Ende steht).

## Bewegte Vorschau (`--clip`)

Ein Foto zeigt einen Augenblick. Ob eine Bewegung stimmt – der Klingenschwung, das Hochreißen der Pistole, das Aufschwingen einer Tür –, sieht man erst im Ablauf. `--clip` filmt dafür eine **Bewegungs-Szene**: feste Eingaben je Tick, alle zwei Ticks ein Bild (30 Bilder pro Sekunde Spielzeit). Issue #286.

```
dotnet run --project src/SilverAndAsh.Game -- --clip blade-charge
```

```
dotnet run --project src/SilverAndAsh.Game -- --clip all
```

```
dotnet run --project src/SilverAndAsh.Game -- --clip list
```

- `--clip <szene>` – eine Bewegungs-Szene; `all` – alle nacheinander; `list` – Namen, Bildrate und Beschreibung ausgeben (ohne Fenster).
- Die Bilder landen in `%LOCALAPPDATA%\SilverAndAsh\clips\<szene>\frame-0000.png`, `frame-0001.png` … (mit `--out <ordner>` in `<ordner>\<szene>\`). Das erste Bild zeigt den Zustand vor dem ersten Tick. Alte Bilder im Szenen-Ordner werden vorher gelöscht.
- Gezeichnet wird wie beim Foto das ganze Fenster mit HUD (1280 × 720), mit demselben Code wie im Spiel.
- Wie `--photo` nur in der Debug-Version; nicht mit `--photo` oder `--bench` kombinierbar.

**Clip zusammensetzen** (braucht [ffmpeg](https://ffmpeg.org/) im PATH, z. B. per `winget install Gyan.FFmpeg`; fehlt es, sagt das Skript das):

```
powershell -ExecutionPolicy Bypass -File tools\clip.ps1 "$env:LOCALAPPDATA\SilverAndAsh\clips\blade-charge"
```

```
powershell -ExecutionPolicy Bypass -File tools\clip.ps1 "$env:LOCALAPPDATA\SilverAndAsh\clips\blade-quick" -Half -Gif
```

- Ergebnis neben dem Ordner: `blade-charge.mp4`, mit `-Half` `…-half.mp4` (halbe Geschwindigkeit, für schnelle Bewegungen), mit `-Gif` ein GIF (640 px breit, öffnet sich überall).
- `-Sheet` macht statt eines Clips ein **Übersichtsbild** `<szene>-sheet.png`: bis zu 16 Bilder gleichmäßig über die Szene verteilt, 4 × 4, von links oben nach rechts unten zu lesen. Gedacht für alle, die Bilder ansehen, aber kein Video abspielen können – etwa eine Claude-Sitzung oder ein PR-Kommentar. Einzelne Bilder (`frame-0050.png`) stehen daneben im Szenen-Ordner.
- `-Fps` ändert die Bildrate; Standard 30 passt zur Schrittweite der Szenen (`--clip list` zeigt sie).
- **Bilder und Clips nie ins Repository legen.**

| Bewegungs-Szene | Zeigt |
|---|---|
| `blade-charge` | die Silberklinge gedrückt gehalten, bis sie voll aufgeladen ist, eine Weile zitternd, dann losgelassen: der volle Schwung und zurück in Ruhe |
| `blade-quick` | ein schneller Klingenhieb (ein Tick gedrückt), der Schwung und zurück in Ruhe |
| `pistol-shot` | ein Pistolenschuss: Hochreißen, Hahn, Mündungsfeuer, die Pistole kommt zur Ruhe |
| `pistol-reload` | Nachladen (R) mit einem verbrauchten Schuss, von der Taste bis zum vollen Magazin |
| `pistol-shot-in-swing` | ein Pistolenschuss mitten im Klingenhieb: beide Hände gleichzeitig |
| `pistol-reload-in-swing` | Nachladen (R) und mitten darin ein Klingenhieb |
| `pistol-reload-cancelled` | Nachladen, nach der Hälfte die Büchse gewählt (2, das Nachladen bricht ab), dann wieder die Pistole (1) |
| `rifle-reload-to-pistol` | Büchse gewählt (2) und geschossen, Nachladen (R), nach der Hälfte die Pistole (1): sie darf die Nachladehaltung nicht zeigen |
| `door-normal-opening` | eine hölzerne Doppeltür aus 6 m von vorn, geöffnet (F): der ganze Schwung, bis sie offen steht |
| `door-boss-opening` | dasselbe mit dem Bosstor |
| `stairs-rising` | die Wendeltreppe im Bossraum nach Gorms Tod aus 6 m: der ganze Aufstieg aus dem Boden, bis sie steht und ihre Lampe leuchtet |
| `gorm-slam` | Gorms Hammerschlag aus der Nähe: der Spieler steht 8 m vor ihm, hinter ihm stehend, Blick auf Gorm; die Welle aus aufbrechendem Stein läuft auf ihn zu und trifft ihn, sobald der rote Riss seine Füße erreicht (#299) |
| `gorm-slam-far` | derselbe Schlag aus 14 m, außerhalb der Reichweite (12 m): die ganze Welle von vorn, bis sie vor dem Spieler ausläuft, Boden Variante A |
| `gorm-slam-far-b`, `gorm-slam-far-c` | dasselbe auf Boden Variante B bzw. C (C, das Kopfsteinpflaster, liegt im Spiel nie im Bossraum) |
| `column-breaking` | die zerbrechende Säule im Musterraum aus 6 m, von der Hallenseite in voller Höhe, ohne Waffen in der Hand, Raumtitel und Schilder (`PhotoSetup.ShowsOnlyTheWorld`), alle drei Ticks ein Bild: der Bruch, der Fall der Stücke (81 Ticks) mit der Staubwolke und der liegende Stumpf (Issue #601) |

**Eine Bewegungs-Szene ergänzen:** ein Eintrag in `MotionScenes.All` ([`MotionScenes.cs`](../src/SilverAndAsh.Core/Photos/MotionScenes.cs)) – dieselben Bausteine wie bei den Fotos, nur mit `Input` je Tick und genug `Ticks` für den ganzen Ablauf, zum Beispiel:

```csharp
new("blade-double", "Two quick blade swings in a row.",
    studio => new PhotoSetup(studio.Arena('D', 10))
    {
        Ticks = 100,
        Input = tick => PlayerInput.None with { Melee = tick is 12 or 50 },
    }),
```

Optional als vierter Wert eine andere Schrittweite (`FrameEvery`, Standard 2 Ticks). Danach den Namen in den Test `Every_clip_the_issue_asks_for_is_there` aufnehmen ([`MotionSceneTests.cs`](../tests/SilverAndAsh.Core.Tests/Photos/MotionSceneTests.cs)), die Szene filmen und **den Clip ansehen**. Die Tests prüfen für jede Szene, dass zweimal Filmen dieselben Ticks, Zustände und Ereignisse liefert. Bildprüfsummen gibt es nicht, weil Zeichnen ein Fenster mit Grafikkarte braucht, das die CI nicht hat.

## Modell-Probe

`model-probe-4m` und `model-probe-10m` zeigen den Ghul als geladenes Modell (`assets/models/ghoul.glb`, siehe [`modelle.md`](modelle.md)), genau wie im Spiel. Fehlt das Modell, zeigen sie – wie das Spiel – den Platzhalter; die Warnung steht dann im Log.

## Bildzeit messen (`--bench`)

Bei jedem neuen Modell oder jeder neuen Textur stellt sich die Frage, ob die Bildrate einbricht. `--bench <szene>` beantwortet sie:

```
dotnet run --project src/SilverAndAsh.Game -- --bench room-combat
```

- Die Szene wird aufgebaut wie beim Foto. Die Simulation steht danach still.
- Gezeichnet wird ohne VSync und ohne Bildraten-Grenze: zuerst 100 Bilder zum Aufwärmen, dann 1 000 gemessene. Jedes Bild entsteht vollständig wie im Spiel, also Welt und dann Bildschirm mit HUD.
- Am Ende stehen auf der Konsole und im Log die mittlere Bildzeit (mit fps), der Median und das 99. Perzentil in Millisekunden. Danach schließt sich das Fenster, der Exit-Code ist 0.
- Die Auswertung steht in [`FrameTimeStatistics`](../src/SilverAndAsh.Core/Diagnostics/FrameTimeStatistics.cs) im Core. Das 99. Perzentil ermittelt sie nach dem Nearest-Rank-Verfahren und testet es.
- Wie `--photo` geht `--bench` nur in der Debug-Version. Die Zahlen gelten also für den Debug-Build und sind nur untereinander vergleichbar, nicht mit der Release-Version.

**Vorher/Nachher im selben Build:** Für den Vergleich mit dem Stand vor einer neuen Textur oder einem neuen Modell benennst du die Datei im Ausgabeordner (`src/SilverAndAsh.Game/bin/Debug/net10.0/assets/…`) um. Dann greift der Rückfall: Die Fläche wird einfarbig gezeichnet bzw. der Platzhalter erscheint. Miss jede Szene mehrmals, denn einzelne Läufe streuen um etwa ±1 ms. Die Grafikkarte steht im Konsolen-Text von raylib (`Renderer:`).
