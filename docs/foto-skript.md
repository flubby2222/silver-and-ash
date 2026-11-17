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
| `gorm` | Gorm aus 6 m in seinem Bossraum |
| `gorm-swing-windup` | Gorm wirbelt die Kette über dem Kopf: die Ankündigung des Kettenschwungs, die Kette gerade zu seiner Rechten |
| `gorm-swing` | Gorm im Kettenschwung von seiner Linken zu seiner Rechten, ein Drittel des Bogens |
| `gorm-throw-windup` | Gorm vor dem Hakenwurf, der Haken glüht rot: die Ankündigung, zur Hälfte vorbei |
| `gorm-phase2` | Gorm in Phase 2 (rote Male) aus 10 m in seinem Bossraum, am Ende seines Brüllens |
| `hud-weapons` | das HUD mit allen Waffenplätzen belegt (Keule, Pistole, Gewehr, Donnerbüchse) |
| `hud-rifle` | wie `hud-weapons`, aber mit dem Gewehr (2) in der Hand |
| `hud-reload` | die Pistole im Startraum leergeschossen: „Reload [R]“ unter dem Fadenkreuz (Issue #259) |
| `inventory` | das offene Inventar (G) mit voller Tasche |
| `map` | die offene Karte (M) zu Beginn eines Durchlaufs |
| `chests-4m` | die Kisten aller vier Stufen nebeneinander aus 4 m, geschlossen und verschlossen, dazu eine offene (Issue #249) |
| `model-probe-4m`, `model-probe-10m` | ein einzelnes Ziel auf leerem Boden aus 4 m / 10 m – für Modelle unter Spielbedingungen |

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
