# Übergabe Audio – Geräuschpaket 1 (neue Palette)

Stand 2026-11-17, **Paket 1.1** (siehe Abschnitt unten). 60 kurze Geräusche (davon drei Vorschläge für geweihte Munition, 9 für die Fledermaus, 14 für Gorm), Präfix `p1-`, in [assets/audio/](assets/audio/) neben den bisherigen Dateien (diese bleiben unverändert, damit die Code-Sitzung zwischen altem und neuem Paket umschalten kann). **Ungehört**, nur gemessen: [Übersicht und Messwerte](../proben/audio/paket-1/uebersicht.md). Erzeugung: `python design/proben/audio/paket-1/paket1.py` (braucht SoundCraft 0.20.1 `soundcraft-cli.exe`, `ffmpeg`, Python 3.12; zweimal gerechnet, bitgleich). Palette und Begründung: [palette-neu.md](../proben/audio/soundcraft-versuch/palette-neu.md).

**Format** (wie heute): WAV, 16 Bit, 44 100 Hz, mono, Spitze zwischen −1,5 und −12 dBFS je Kategorie, Anfang und Ende bei Stille. **Lautstärke:** Alle spielen mit `SoundBoard.SoundVolume` = 1 (die Lautheit steckt in den Dateien, Tabelle in der Übersicht). **Prüfsummen:** [assets/audio/audio-paket-1.sha256](assets/audio/audio-paket-1.sha256) im Format von `audio.sha256` (Zeilen in die Liste übernehmen oder zusammenführen).

## Zuordnung zu den Ereignissen von `GameEvent` (heute vorhanden)

| Ereignis | Dateien | Hinweis |
|---|---|---|
| `ShotFired { Weapon: Pistol, Consecrated: false }` | `p1-schuss-1` … `-3` | je Mal eine, nie zweimal dieselbe hintereinander |
| `ShotFired { Weapon: Pistol, Consecrated: true }` | `p1-schuss-geweiht-1`, `-2` | **Vorschlag** (nicht bestellt): derselbe Rauschschlag mit kurzem Glockenton, damit man die Wirkung hört; zwei Varianten, nie dieselbe zweimal hintereinander. Geweihte Munition verdoppelt die Feuerrate (`ConsecratedAmmoBalance.FireRateMultiplier` = 2, Dauer 10 s), die Schüsse überlappen also bei Dauerfeuer |
| `ReloadStarted` | `p1-nachladen-start` | |
| `ReloadFinished` | `p1-nachladen-bereit` | |
| `MeleeSwung` ohne Treffer | `p1-schwung-1`, `-2` | Der Schwung gehört zum **Fehlschlag**; trifft der Schlag in demselben Tick (`TargetHitBy { Weapon: Blade }`), spielt nur der Treffer. Die Entscheidung liegt beim Spiel (Code-Sitzung). |
| `TargetHitBy { Weapon: Blade }` | `p1-treffer-1` … `-3` | |
| `PlayerStepped { Foot: Left }` | `p1-schritt-l1` … `-l3` | |
| `PlayerStepped { Foot: Right }` | `p1-schritt-r1` … `-r3` | links und rechts je aus ihren drei Varianten, nie dieselbe zweimal hintereinander |
| `PlayerHit` | `p1-schmerz` | **neu in 1.1:** tiefer (H2), kürzer, trockener; gleicher Dateiname |
| `GhoulWindUp` | `p1-ghul-ankuendigung` (**Fassung A**, die die Code-Sitzung einbaut); `p1-ghul-ankuendigung-leise` (**Fassung B**, eine Stufe tiefer und zurückhaltender, bleibt in der Übergabe) | **neu in 1.1:** tiefer und sanfter, kein schriller Anstieg. **Muss durchkommen:** höchste Spitze, eigenes Band (300–3 000 Hz); nicht leiser stellen, nicht mit einem anderen Geräusch desselben Ticks überdecken |
| `GhoulDied` | `p1-ghul-sterben`, `p1-ghul-sterben-2` | zwei Varianten, nie dieselbe zweimal hintereinander |
| (fehlt, siehe unten) | `p1-ghul-entdecken`, `p1-ghul-entdecken-2` | zwei Varianten |
| `DoorOpened` | `p1-tuer-oeffnen` | (`DoorUnlocked` und `WallBroken` bleiben ohne Geräusch, solange es keine Datei dafür gibt) |
| `ChestOpened` | `p1-kiste-oeffnen` | |
| `SilverTaken` | `p1-aufheben-silber` | |
| `PickupTaken { Kind: BloodVial }` | `p1-aufheben-phiole` | |
| `PickupTaken { Kind: ConsecratedAmmo }` | `p1-aufheben-weihmunition` | |
| `KeyTaken` | `p1-aufheben-schluessel` | |

### Fledermaus (Issue #297) und Gorm (Issue #298): Zuordnung

| Ereignis (heute vorhanden) | Datei | Hinweis |
|---|---|---|
| `BatSpitWindUp` | `p1-fledermaus-spuck-ankuendigung` | Glocke H4, Ankündigung; muss durchkommen |
| `BatSpat` | `p1-fledermaus-spucken` | |
| `BatScreeched` | `p1-fledermaus-kreischen` | Ankündigung des Sturzflugs: Pfeifton mit fallender Tonfahrt, lauteste Ankündigung |
| `BatBit` | `p1-fledermaus-biss` | |
| `BatDiveMissed` | `p1-fledermaus-fehlsturz` | |
| `BatHit` | `p1-fledermaus-treffer` | |
| `BatDied` | `p1-fledermaus-sterben` | |
| `GormWindUp { Attack: Hammer }` | `p1-gorm-ankuendigung-hammer` | tiefer schwellender Pfeifton |
| `GormWindUp { Attack: Swing }` | `p1-gorm-ankuendigung-schwung` | wirbelndes Rauschen |
| `GormWindUp { Attack: Throw }` | `p1-gorm-ankuendigung-wurf` | ein tiefer Glockenschlag |
| `GormSlammed` | `p1-gorm-aufschlag` | |
| `GormSwung` | `p1-gorm-kette` | bei `Hit: true` zusätzlich Schmerz der Spielfigur (`PlayerHit`) |
| `GormThrew` | `p1-gorm-haken-wurf` | |
| `HookHit` | `p1-gorm-haken-treffer` | |
| `GormHit { Critical: false }` | `p1-gorm-treffer-1`, `-2` | |
| `GormHit { Critical: true }` | `p1-gorm-treffer-kopf` | |
| `GormRoared` | `p1-gorm-bruellen` | Phase 2 |
| `GormDied` | `p1-gorm-sterben` | |

Vorhandene Gorm-Ereignisse **ohne** Datei: `ShockwaveHit`, `HookStopped`, `ClubStuck`, `ClubFreed`, `StoneFalling`, `StoneLanded`, `StoneHit`, `BossFightStarted`, `StairsRose`. Fehlend (kein Ereignis): Flügelschlag der Fledermaus (`p1-fledermaus-flattern-1`, `-2`) und Gorms Schritte (`p1-gorm-schritt-1`, `-2`), siehe die Liste unten.

## Im Core nachgesehen (2026-11-17): Beute- und Aufheb-Ereignisse

Wirklich vorhanden (`GameWorld.cs`, `Inventory.cs`): `PickupDropped`, `SilverDropped`, `KeyDropped`, `WeaponDropped` (Fallenlassen) und `SilverTaken`, `KeyTaken`, `PickupTaken { Kind }` (Aufheben; `ConsumableKind` hat genau `BloodVial` und `ConsecratedAmmo`), dazu `ChestOpened`, `ChestUnlocked`, `ChestRose`, `DoorUnlocked`, `DoorOpened`, `WeaponSwitched`. **Kein** Ereignis gibt es für das Aufnehmen einer Waffe oder Ausrüstung vom Boden (es gibt nur `WeaponDropped`) und keines für das Ende der geweihten Munition. Dateien für die Ereignisse des Fallenlassens (`PickupDropped`, `SilverDropped`, `KeyDropped`, `WeaponDropped`) gibt es nicht.

## Fehlende Ereignisse (für die Code-Sitzung; nicht erfunden)

Diese Geräusche sind fertig, aber im Core gibt es keinen Auslöser. Vorschläge für die Ereignisse (Namen und Felder nach dem Muster der vorhandenen):

| Datei | Fehlendes Ereignis (Vorschlag) |
|---|---|
| `p1-ghul-entdecken` | `GhoulNoticed(GhoulId)`: ein Ghul bemerkt zum ersten Mal einen Spieler (beginnt zu verfolgen) |
| `p1-sprung` | `PlayerJumped(PlayerId)` (Sprung und Dash sind in `Player.cs`/`Stamina.cs` vorhanden, melden aber nichts) |
| `p1-dash` | `PlayerDashed(PlayerId)` |
| `p1-landung` | `PlayerLanded(PlayerId)`, sinnvoll erst ab einer Mindest-Fallhöhe |
| `p1-fledermaus-flattern-1`, `-2` | `BatFlapped(BatId)`: ein Flügelschlag, nur in Hörweite und nicht bei jedem Tick (etwa alle 0,3 bis 0,5 s) |
| `p1-gorm-schritt-1`, `-2` | `GormStepped(GormId)`: ein schwerer Schritt Gorms (heute meldet `Gorm.cs` nichts) |
| `p1-aufheben-ausruestung` | `WeaponPickedUp(PlayerId, WeaponKind)`: eine Waffe oder Ausrüstung vom Boden aufgenommen (heute gibt es nur `WeaponDropped`) |
| `p1-weihe-ende` (**Vorschlag**, leiser tiefer Glockenton als Marke) | `ConsecrationEnded(PlayerId)`: die Wirkung geweihter Munition ist abgelaufen. Heute zählt `Player.cs` nur `_consecratedTicksLeft` herunter (ab `Consecrate(Duration)`), ohne Ereignis; `IsConsecrated` und `ConsecratedSecondsLeft` lassen sich abfragen, aber ein Ereignis ist für den Ton sauberer |
| `p1-menue-klick` | kein Core-Ereignis nötig: die Menüs sind im Game-Projekt; ein Klick/eine Auswahl im Menü löst ihn aus |

Weitere Ereignisse des Cores haben **keine** Datei (Fledermäuse, Gorm, Stein, Haken, Treppe, Raum versiegelt/befreit, Stufenaufstieg, Beute-Auswahl): sie kommen in einem späteren Paket aus denselben fünf Quellen.

## HERKUNFT (zum Übernehmen nach `assets/HERKUNFT.md`)

Alle `p1-*.wav`: `design/proben/audio/paket-1/paket1.py` (mit `design/proben/audio/soundcraft-versuch/bauen.py` und `bauen9.py`, `design/uebergabe/tools/audio/ps1_klang.py`); rein synthetisch in SoundCraft 0.20.1 (Signalgenerator, Subtractive Synth, Ringmodulator, EQ, Sampler, Raum-Hall), keine Aufnahmen, keine fremden Samples | 2026-11-17.

## Paket 1.1 (2026-11-17): was sich geändert hat

Nach dem Anspielen des Pakets im Spiel: „Sound ist gut“; der Angriffslaut der Ghule und der Schmerzlaut der Spielfigur waren zu hoch, die Sprachlaute „gut, aber ausbaufähig“.

| Datei | Was ist anders |
|---|---|
| `p1-ghul-ankuendigung` (ersetzt) | Fassung A: D3 statt E4, Anstieg +3 statt +7 Halbtöne, Tiefpass 1,8 kHz, Hochpass 0,2 kHz, Spitze weiter −1,5 dBFS |
| `p1-ghul-ankuendigung-leise` (neu) | Fassung B: H2, Anstieg +2, Tiefpass 1,4 kHz, Spitze −4,5 dBFS; liegt unter 300 Hz |
| `p1-schmerz` (ersetzt) | H2 statt G3, 0,4 s statt 0,6 s, trockener |
| `p1-ghul-entdecken-2`, `p1-ghul-sterben-2` (neu) | Variante 2 mit der erweiterten Stimme (drei Formanten, schwebend, Hauch darunter) |
| `p1-schritt-l3` (ersetzt) | war byte-gleich zu `p1-schritt-r2` (Befund der Review); jetzt Ton 43 (G2) statt 46 |
| `p1-fledermaus-*` (9, neu), `p1-gorm-*` (14, neu) | Issue #297 und #298, Zuordnung oben |

Alle Dateien wieder zweimal gerechnet, bitgleich (60 von 60). Die SoundCraft-Skripte in `design/proben/audio/` enthalten keine lokalen Pfade mehr (Platzhalter `{TMP}`).
