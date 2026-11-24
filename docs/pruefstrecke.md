# Prüfstrecke

Die Prüfstrecke (englisch *test course*, Issue #718; Begriffe im [Glossar](glossar.md)) ist ein von Hand gebautes Level, in dem alle offenen Urteile eines Releases als **Stationen** hintereinander auf einem geraden Weg stehen. Anders als im [Musterraum](musterraum.md) gelten hier die **echten Spielregeln**: Gegner leben und greifen an, das Fallgitter sperrt die Arena, die Bosstür fällt zu, Gorm kämpft. So sieht und hört der Mensch beim [Spieltest](release.md) jedes Urteil so, wie es im Spiel vorkommt, ohne erst ein passendes Level suchen zu müssen.

## Starten

```powershell
dotnet run --project src/SilverAndAsh.Game -- --course
```

Nach dem Titelbildschirm stehst du am Westende des Wegs und blickst nach Osten. Dazu passt nur `--look <1-4>`; jeden anderen Schalter lehnt `--course` mit einer Meldung ab (geprüft in `CourseOptions.Check`, nicht als weiterer Zweig in `LaunchOptions.Parse`).

**Nur in der Debug-Version** (`dotnet run` baut Debug). Wie der Musterraum ist die Prüfstrecke ein Werkzeug für die Entwicklung. Die Release-Version meldet „works only in the debug version“.

**Der Spieler stirbt nicht:** Das Leben fällt nie unter 1 (`Player.CannotDie`). Jeder Treffer wird trotzdem gezeigt und gespielt, auch der Klang. Wer alle Waffen sehen will, hat sie von Anfang an (wie im Musterraum).

**Kein Eintrag in `runs.log`:** Wer auf der Strecke Gorm besiegt und die Treppe hinaufsteigt, sieht den Siegbildschirm wie im Spiel. Die Welt ist aber eine Testwelt (`GameWorld.IsTestWorld`, wie jeder Musterraum), und `RunLog.AppendRun` schreibt für sie keine Zeile. Die Balancing-Runde wertet `runs.log` aus; ein unsterblicher, voll bewaffneter Lauf darf dort nicht stehen.

| Taste | Wirkung |
|---|---|
| **L** | Schilder aus- und einblenden |
| **F** | Türen öffnen, Gegenstände aufheben, Teleporter und Brunnen benutzen |
| **K** | Klangpaket wechseln (wie im Spiel) |
| **N** | Ton und Musik stumm |
| **1 / 2 / 3**, Mausrad | Waffe wechseln; weitere liegen im Inventar (**G**) |

## Die Stationen

Über dem Eingang jeder Station hängt ein Schild mit Nummer und Namen. Die Stationen liegen von West nach Ost, getrennt durch Holztüren:

| Nr. | Station | Raumart | Was zu sehen ist |
|---|---|---|---|
| 1 | **Arms** | Start | Alle Waffen in einer Reihe an der Nordwand, die Gegenstände *Leech* und *Ashen Heart* an der Südwand, je mit `[F]`-Hinweis; der Teleporter des Starts auf dem Weg |
| 2 | **Columns** | Start | Zwei Säulen links und rechts des Wegs, Silber neben einer, dahinter ein wacher Ghul |
| 3 | **Cracked wall** | Start | Rissige Wand in der Mitte der Nordwand, dahinter die geheime Kammer mit Truhe. **So geht sie auf:** dicht vor die Wand treten, sie ansehen, die linke Maustaste halten, bis die Silberklinge voll aufgeladen ist und zittert, dann loslassen. Nur ein voll aufgeladener Nahkampfhieb aus Klingenreichweite bricht sie auf; **F**, normale Hiebe und Schüsse tun nichts |
| 4 | **Portcullis** | Arena | Betritt ein Spieler sie, fallen die Gitter; ein Ghul und eine Fledermaus kommen bei der Säule heraus |
| 5 | **Teleporter** | Start | Ein zweiter Teleporter (zum Eingang gedreht) und der Brunnen „Sirius' Relief“ neben dem Weg |
| 6 | **Boss** | Vorraum | Silber und eine Blutphiole vor dem Bossportal, dahinter der Bossraum mit Gorm |

**Eine neue Station** ist ein Eintrag in `TestCourse.Stations` (`src/SilverAndAsh.Core/Course/TestCourse.cs`): Name, Raumart, Breite in Zellen und eine Methode, die sie einrichtet (`CourseRoom.Ghoul`, `Column`, `Weapon`, `Teleporter`, `Well`, `SecretRoomNorth`, `BossRoomSouth` …). Tür und Schild entstehen von selbst. Säulen prüft dieselbe Regel wie in den erzeugten Levels (`ColumnPlacement.Check`); passt eine nicht, bricht der Aufbau mit einer Meldung ab, statt sie still wegzulassen.

### Abweichungen vom Spiel

- **Teleporter:** Der zweite Teleporter (Station 5) trägt den Ort `BossGate`, weil das Spiel nur Start und Bosstor als Orte kennt. So gibt es zwei Teleporter, zwischen denen man reisen kann.
- **Zerbrechende Säulen:** Säulen zerbrechen nur durch Gorms Angriff. Die Säulen in Station 2 und 4 bleiben stehen; das Zerbrechen ist im Bossraum (Station 6) zu sehen, wo das Spiel selbst Säulen stellt.
- **Vampir-Waffe:** Eine Waffe mit der Eigenschaft *Vampiric* gibt es (noch) nicht als Fund. Station 1 zeigt stattdessen die beiden Gegenstände der Stufe *Vampiric*: *Leech* (heilt um einen Teil des Schadens) und *Ashen Heart* (bewahrt einmal vor dem Tod).

## Fotos

Für jede Station gibt es ein Foto mit Schildern (siehe [foto-skript.md](foto-skript.md)):

```powershell
dotnet run --project src/SilverAndAsh.Game -- --photo course-1-arms --out <leerer Ordner>
```

Die Namen sind `course-1-arms`, `course-2-columns`, `course-3-cracked-wall`, `course-4-portcullis`, `course-5-teleporter` und `course-6-boss`. Die Türen auf dem Weg werden vor dem Bild mit **F** geöffnet, wie ein Spieler es täte; das Foto der Arena zeigt sie versiegelt. Die Probenblätter des Issues liegen unter `design/proben/grafik/pruefstrecke/`.

## Tests

`tests/SilverAndAsh.Core.Tests/Course/TestCourseTests.cs` läuft Tick für Tick vom Start durch alle Türen bis in den Bossraum und prüft die Stationen einzeln (Funde, rissige Wand, Gitter, Teleporter, Bosstür und Gorm, Bruch einer Säule im Bossraum). Ein weiterer Lauf geht bis zum Sieg auf der Treppe und prüft, dass er nicht ins Lauf-Protokoll kommt. Sie rechnen mit `Balance.Default`.
