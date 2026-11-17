# Foto-Skript

Das Spiel kann sich selbst fotografieren: Es baut eine festgelegte **Szene** mit festem Seed auf, lässt die Simulation eine festgelegte Zahl von Ticks laufen, speichert zwei PNG-Dateien und beendet sich – ohne Maus, Tastatur oder Zuschauer. Gedacht für Tester (Bildschirmfotos zu jedem sichtbaren PR), Designer (Probestücke „wie im Spiel“) und die Leitung (Stil-Vergleiche). Issue #224.

## Aufruf

```
dotnet run --project src/SilverAndAsh.Game -- --photo ghoul-4m
```

```
dotnet run --project src/SilverAndAsh.Game -- --photo all --out D:\fotos
```

```
dotnet run --project src/SilverAndAsh.Game -- --photo list
```

- `--photo <szene>` – eine Szene; `all` – alle nacheinander; `compare` – die Vergleichsszenen in allen Bild-Varianten (siehe unten); `list` – Namen und Beschreibung aller Szenen ausgeben (ohne Fenster).
- `--out <ordner>` – wohin die Bilder kommen. Ohne `--out`: `%LOCALAPPDATA%\SilverAndAsh\photos`. **Nie ins Repository legen.**
- Pro Szene entstehen zwei Dateien:
  - `<szene>.png` – das 3D-Bild in Spielauflösung (854 × 480), so wie es die Grafikkarte zeichnet, ohne HUD.
  - `<szene>-screen.png` – das ganze Fenster (1280 × 720) mit HUD, Karte, Inventar usw.
- Für einen Durchlauf öffnet sich kurz ein Fenster; es schließt sich von selbst. `all` dauert wenige Sekunden.
- Die FPS-Anzeige und die Debug-Anzeige (Geschwindigkeit, Abklingzeiten) fehlen auf den Fotos – sie würden nur Zufallswerte zeigen.

**Nur in der Debug-Version** (`dotnet run`), wie `--give`. Die Release-Version für Spieler bricht mit einer Meldung ab, wenn jemand `--photo` angibt; sie schreibt keine Dateien.

## Szenen

`--photo list` zeigt die aktuelle Liste. Stand bei Einführung:

| Szene | Zeigt |
|---|---|
| `start-room` | Ebene 0, wie ein Durchlauf beginnt: Startraum, Ebenen-Name, Standardwaffen |
| `room-combat`, `room-corridor`, `room-treasure`, `room-secret` | je einen Raum dieser Art aus seiner entferntesten Ecke, Blick zur Raummitte |
| `room-boss` | den Bossraum aus 12 m, Gorm im Blick |
| `ghoul-4m`, `ghoul-10m`, `bat-4m`, `bat-10m` | einen Ghul bzw. eine Fledermaus in einer leeren Halle aus 4 m bzw. 10 m |
| `ghoul-windup` | einen Ghul beim Ausholen (Hälfte der Ausholzeit) |
| `bat-screech` | eine Fledermaus beim Kreischen, der Ankündigung des Sturzflugs (Hälfte der Kreischzeit) |
| `bat-dive` | eine Fledermaus im Sturzflug auf den Spieler |
| `pistol-recoil` | die Pistole gleich nach dem Schuss: hochgerissen, Hahn gefallen, Mündungsfeuer |
| `blade-charged` | die Silberklinge voll aufgeladen: zurückgezogen und zitternd |
| `blade-swing-half` | ein schneller Klingenhieb halb ausgeholt: der Unterarm dreht sich zur Schulter (#265) |
| `blade-swing-full` | ein schneller Klingenhieb auf dem Höhepunkt: der Unterarm dreht sich zur Schulter (#265) |
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
| `inventory` | das offene Inventar (G) mit voller Tasche |
| `map` | die offene Karte (M) zu Beginn eines Durchlaufs |
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
| `model-probe-4m`, `model-probe-10m` | ein einzelnes Ziel auf leerem Boden aus 4 m / 10 m – für Modelle unter Spielbedingungen |
| `stairs-4m`, `stairs-10m` | die Wendeltreppe im Bossraum der Ebene 0 nach Gorms Tod (Issue #300): Gorm stirbt vor dem ersten Tick, die Treppe steigt wie im Spiel auf; das Bild entsteht, sobald sie steht und ihre Lampe leuchtet, aus 4 m bzw. 10 m vom Fuß, mit Blick auf ihre halbe Höhe |
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
dotnet run --project src/SilverAndAsh.Game -- --photo compare --out D:\fotos\vergleich
```

- `--look <1-4>` gilt fürs Spielen und für einzelne Fotos (`--photo ghoul-4m --look 4`).
- `--photo compare` nimmt Gang, Kampfraum, Ghul aus 4 m und 10 m, Ghul beim Ausholen, Fledermaus aus 10 m und Gorm in allen vier Varianten auf (`<szene>-look<n>.png`, `<szene>-look<n>-screen.png`) und legt je Szene ein beschriftetes 2 × 2-Vergleichsbild `<szene>-compare.png` aus den Fensterbildern an.
- Die Werte stehen in [`LookVariants.cs`](../src/SilverAndAsh.Game/Rendering/LookVariants.cs) neben `LookSettings`, die Formeln getestet in [`Dithering.cs`](../src/SilverAndAsh.Core/Presentation/Dithering.cs); der Shader `assets/shaders/level.fs` rechnet dieselben.

## Wie es funktioniert

- **Die Szenen stehen im Core**, an einer Stelle: [`PhotoScenes.cs`](../src/SilverAndAsh.Core/Photos/PhotoScenes.cs). Jede Szene ist ein Name, eine Zeile Beschreibung und eine Funktion, die mit dem [`PhotoStudio`](../src/SilverAndAsh.Core/Photos/PhotoStudio.cs) eine [`PhotoSetup`](../src/SilverAndAsh.Core/Photos/PhotoSetup.cs) baut: die Welt, die Zahl der Ticks, die Eingaben je Tick, wann früher abgedrückt wird, ob die Karte offen ist.
- **Kein Sonderweg in den Spielregeln:** Die Welten sind normale `GameWorld`s (Ebene 0 mit Seed `PhotoStudio.Seed`, oder eine kleine handgebaute Halle). Gewählt wird nur, wo der Spieler steht (`Player.PlaceAt`, `internal` – nur der Core kann es aufrufen) und wohin er schaut (über die normale Blick-Eingabe). Waffen kommen über `Equip`/`Stow`, das Inventar öffnet sich über die Eingabe `ToggleInventory`, wie bei G.
- **Das Spiel zeichnet die Fotos mit demselben Code wie im Spiel** (`DrawWorld`/`DrawScreen` in `Program.cs`): pro Tick werden die Ereignisse gelesen und die Ansichten um 1/60 s weitergestellt, danach wird ein Bild gezeichnet und gespeichert.
- Die Raum-Szenen nehmen den ersten Seed ab `PhotoStudio.Seed`, dessen Ebene so einen Raum hat. Ändert sich der Generator, wandern die Bilder mit – sie bleiben aber für denselben Stand gleich (Tests prüfen das).

## Eine Szene ergänzen

Eine Zeile in `PhotoScenes.All`, zum Beispiel ein Ghul aus 6 m:

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

Und in der `PhotoSetup`: `Ticks` (wie lange die Welt läuft), `Input` (Eingabe je Tick, z. B. `ToggleInventory` in Tick 0), `StopWhen` (früher abdrücken, z. B. sobald ein Ghul ausholt), `MapOpen`, `IsModelProbe`.

Danach: Name in den Test `Every_scene_the_issue_asks_for_is_there` aufnehmen ([`PhotoSceneTests.cs`](../tests/SilverAndAsh.Core.Tests/Photos/PhotoSceneTests.cs)), die Szene einmal laufen lassen und **das Bild ansehen**.

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
