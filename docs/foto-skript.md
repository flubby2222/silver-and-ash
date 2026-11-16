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

- `--photo <szene>` – eine Szene; `all` – alle nacheinander; `list` – Namen und Beschreibung aller Szenen ausgeben (ohne Fenster).
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
| `gorm` | Gorm aus 6 m in seinem Bossraum |
| `hud-weapons` | das HUD mit allen Waffenplätzen belegt (Keule, Pistole, Gewehr, Donnerbüchse) |
| `inventory` | das offene Inventar (G) mit voller Tasche |
| `map` | die offene Karte (M) zu Beginn eines Durchlaufs |
| `model-probe-4m`, `model-probe-10m` | ein einzelnes Ziel auf leerem Boden aus 4 m / 10 m – für Modelle unter Spielbedingungen |

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

`model-probe-4m` und `model-probe-10m` zeigen heute den Platzhalter-Ghul. Das Spiel zeichnet das Ziel dieser Szenen über `ModelProbeView()` in `Program.cs`. Sobald Modelle geladen werden können, kommt dort die Ansicht mit dem geladenen Modell hinein – die Szene selbst bleibt gleich.
