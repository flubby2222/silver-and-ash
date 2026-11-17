# Übergabe Audio – Geräuschpaket 1 (neue Palette)

Stand 2026-11-17. 31 kurze Geräusche, Präfix `p1-`, in [assets/audio/](assets/audio/) neben den bisherigen Dateien (diese bleiben unverändert, damit die Code-Sitzung zwischen altem und neuem Paket umschalten kann). **Ungehört**, nur gemessen: [Übersicht und Messwerte](../proben/audio/paket-1/uebersicht.md). Erzeugung: `python design/proben/audio/paket-1/paket1.py` (braucht SoundCraft 0.20.1 `soundcraft-cli.exe`, `ffmpeg`, Python 3.12; zweimal gerechnet, bitgleich). Palette und Begründung: [palette-neu.md](../proben/audio/soundcraft-versuch/palette-neu.md).

**Format** (wie heute): WAV, 16 Bit, 44 100 Hz, mono, Spitze zwischen −1,5 und −12 dBFS je Kategorie, Anfang und Ende bei Stille. **Lautstärke:** Alle spielen mit `SoundBoard.SoundVolume` = 1 (die Lautheit steckt in den Dateien, Tabelle in der Übersicht). **Prüfsummen:** [assets/audio/audio-paket-1.sha256](assets/audio/audio-paket-1.sha256) im Format von `audio.sha256` (Zeilen in die Liste übernehmen oder zusammenführen).

## Zuordnung zu den Ereignissen von `GameEvent` (heute vorhanden)

| Ereignis | Dateien | Hinweis |
|---|---|---|
| `ShotFired { Weapon: Pistol }` | `p1-schuss-1` … `-3` | je Mal eine, nie zweimal dieselbe hintereinander |
| `ReloadStarted` | `p1-nachladen-start` | |
| `ReloadFinished` | `p1-nachladen-bereit` | |
| `MeleeSwung` ohne Treffer | `p1-schwung-1`, `-2` | Der Schwung gehört zum **Fehlschlag**; trifft der Schlag in demselben Tick (`TargetHitBy { Weapon: Blade }`), spielt nur der Treffer. Die Entscheidung liegt beim Spiel (Code-Sitzung). |
| `TargetHitBy { Weapon: Blade }` | `p1-treffer-1` … `-3` | |
| `PlayerStepped { Foot: Left }` | `p1-schritt-l1` … `-l3` | |
| `PlayerStepped { Foot: Right }` | `p1-schritt-r1` … `-r3` | links und rechts je aus ihren drei Varianten, nie dieselbe zweimal hintereinander |
| `PlayerHit` | `p1-schmerz` | |
| `GhoulWindUp` | `p1-ghul-ankuendigung` | **Muss durchkommen:** höchste Spitze, eigenes Band (300–3 000 Hz); nicht leiser stellen, nicht mit einem anderen Geräusch desselben Ticks überdecken |
| `GhoulDied` | `p1-ghul-sterben` | |
| `DoorOpened` | `p1-tuer-oeffnen` | (`DoorUnlocked` und `WallBroken` bleiben ohne Geräusch, solange es keine Datei dafür gibt) |
| `ChestOpened` | `p1-kiste-oeffnen` | |
| `SilverTaken` | `p1-aufheben-silber` | |
| `PickupTaken { Kind: BloodVial }` | `p1-aufheben-phiole` | |
| `PickupTaken { Kind: ConsecratedAmmo }` | `p1-aufheben-weihmunition` | |
| `KeyTaken` | `p1-aufheben-schluessel` | |

## Fehlende Ereignisse (für die Code-Sitzung; nicht erfunden)

Diese Geräusche sind fertig, aber im Core gibt es keinen Auslöser. Vorschläge für die Ereignisse (Namen und Felder nach dem Muster der vorhandenen):

| Datei | Fehlendes Ereignis (Vorschlag) |
|---|---|
| `p1-ghul-entdecken` | `GhoulNoticed(GhoulId)`: ein Ghul bemerkt zum ersten Mal einen Spieler (beginnt zu verfolgen) |
| `p1-sprung` | `PlayerJumped(PlayerId)` (Sprung und Dash sind in `Player.cs`/`Stamina.cs` vorhanden, melden aber nichts) |
| `p1-dash` | `PlayerDashed(PlayerId)` |
| `p1-landung` | `PlayerLanded(PlayerId)`, sinnvoll erst ab einer Mindest-Fallhöhe |
| `p1-aufheben-ausruestung` | `WeaponPickedUp(PlayerId, WeaponKind)`: eine Waffe oder Ausrüstung vom Boden aufgenommen (heute gibt es nur `WeaponDropped`) |
| `p1-menue-klick` | kein Core-Ereignis nötig: die Menüs sind im Game-Projekt; ein Klick/eine Auswahl im Menü löst ihn aus |

Weitere Ereignisse des Cores haben **keine** Datei (Fledermäuse, Gorm, Stein, Haken, Treppe, Raum versiegelt/befreit, Stufenaufstieg, Beute-Auswahl): sie kommen in einem späteren Paket aus denselben fünf Quellen.

## HERKUNFT (zum Übernehmen nach `assets/HERKUNFT.md`)

Alle `p1-*.wav`: `design/proben/audio/paket-1/paket1.py` (mit `design/proben/audio/soundcraft-versuch/bauen.py` und `bauen9.py`, `design/uebergabe/tools/audio/ps1_klang.py`); rein synthetisch in SoundCraft 0.20.1 (Signalgenerator, Subtractive Synth, Ringmodulator, EQ, Sampler, Raum-Hall), keine Aufnahmen, keine fremden Samples | 2026-11-17.
