# Übergabe Audio für die Code-Sitzung

Stand 2026-11-17, nach dem Urteil „b“ zu Hörprobe 5 kurz. **Ungehört:** Weder ich noch die Leitung haben diese Dateien gehört. Gemessen ist nur der technische Rahmen (unten); ob sie im Spiel passen, entscheidet der Mensch beim Spielen. Kein Spielcode wurde geändert.

Ablage im Spiel: Dateien aus `design/uebergabe/assets/audio/` nach `assets/audio/`, Skripte aus `design/uebergabe/tools/audio/` nach `tools/audio/`. Wiedergabe über Raylib-cs (`LoadSound`/`PlaySound`, Ambiente `LoadMusicStream`), kein neues Paket. Raumklang (#225) gibt es noch nicht: erst feste Lautstärke.

## Datei → Ereignis → Lautstärke

| Datei | Ereignis (`SilverAndAsh.Core.Gameplay`) | Vorschlag 0–1 | Hinweis |
|---|---|---|---|
| `pistole.wav` | `ShotFired` (nur `Weapon == Pistol`; andere Fernwaffen später mit eigener Datei) | 1,0 | lauteste Datei (Spitze −1,5 dBFS) |
| `klinge-1.wav` … `klinge-3.wav` | Treffer auf Gegner: `GhoulHit`, `BatHit`, `GormHit`, `DummyHit`; zufällig eine der drei, nie dieselbe zweimal hintereinander | 1,0 | Anmerkung 1 |
| `ghul-ankuendigung.wav` | `GhoulWindUp` (Ort: der Ghul mit dieser Id) | 0,9 | Anmerkung 2; **vorläufig** |
| `schritt-1.wav` … `schritt-4.wav` | kein Ereignis in Core (Anmerkung 3); abwechselnd zufällig | 1,0 | leise Dateien (Spitze −12 dBFS) |
| `ambiente-katakomben.ogg` | Schleife, solange ein Lauf läuft; als Stream (`LoadMusicStream`, `UpdateMusicStream` je Bild, `SetMusicVolume`) | 1,0 | sehr leise Fläche (Mittel −38 dBFS) |

Die Dateien tragen das Pegelverhältnis schon in sich (Schuss −1,5, Klinge −4 bis −5, Ghul −3, Schritte −12 dBFS Spitze). Deshalb überall 1,0 und eine **Gesamtlautstärke** (Vorschlag 0,7). Für den Test sollte das Ambiente einen eigenen Regler haben, weil ich seine Lautstärke im Spiel nicht beurteilen kann.

**Anmerkung 1 – Treffer-Ereignisse.** `GhoulHit` und Co. sagen nicht, womit getroffen wurde; ein Pistolenschuss erzeugt dasselbe Ereignis. Vorschlag: Klingengeräusch nur, wenn im selben Tick ein `MeleeSwung` vorkam. Sonst muss Core das Ereignis um die Waffe ergänzen (`WeaponKind`, wie bei `MeleeSwung`). Für den Klingen*schwung* ohne Treffer habe ich keine Datei.

**Anmerkung 2 – Ankündigungszeit des Ghuls.** `assets/balance.json`, `ghoul.windUpTime` = **0,5 s** (geprüft; `Ghoul.cs` setzt `GhoulWindUp` beim Beginn des Ausholens). Die Datei ist auf den Schlag bei **0,50 s** gebaut (0,62 s lang). Ändert jemand `windUpTime`, passt die Uhr nicht mehr: die Datei nicht strecken, sondern das Skript mit neuer Zeit neu laufen lassen (`HIT` in `geraeusche_hp3.py`) oder `windUpTime` bei 0,5 lassen.

**Anmerkung 3 – Schritte.** Es gibt kein Core-Ereignis. Sauber wäre ein `PlayerStepped(PlayerId, Foot)` aus der gelaufenen Strecke am Boden (`IsGrounded`). Als Hilfsweg im Game: aus der Strecke am Boden, etwa alle 0,5 s im normalen Lauf (so war die Folge in der Hörprobe gebaut); kein Schritt in der Luft. Den Abstand muss die Code-Sitzung gegen die Laufgeschwindigkeit prüfen; ich kenne ihn nicht.

## Skripte

`python tools/audio/erzeugen.py --aus assets/audio [--quellen <ordner>]` erzeugt alle Dateien. **Zwei Läufe ergeben byte-gleiche Dateien** (mit SHA-256 geprüft; die OGG-Datei mit `ffmpeg -fflags +bitexact`). Nur Python 3.12 (Standardbibliothek) und `ffmpeg`; Dauer etwa 50 s.

`python tools/audio/pruefen.py assets/audio` misst Länge, Spitze, Ränder und die Naht des Ambientes:

| Datei | Format | Länge | Spitze | Mittel | Ränder / Naht | Ergebnis |
|---|---|---|---|---|---|---|
| `ambiente-katakomben.ogg` | OGG, 44 100 Hz, stereo | 30,0 s | −22,6 dBFS | −37,9 dBFS | Naht L/R: 0,00040 ≤ 0,00156; 0,00015 ≤ 0,00156 | bestanden |
| `ghul-ankuendigung.wav` | WAV, 44 100 Hz, mono | 0,620 s | −3,0 dBFS | −18,3 dBFS | 0 / 0, Rest −105 dB | bestanden |
| `klinge-1.wav` … `-3.wav` | WAV, 44 100 Hz, mono | 0,240 s | −4,1 … −4,9 dBFS | −22,6 … −23,7 dBFS | 0 / 0, Rest −51/−52 dB | bestanden |
| `pistole.wav` | WAV, 44 100 Hz, mono | 0,400 s | −1,5 dBFS | −18,2 dBFS | 0 / 0, Rest −74 dB | bestanden |
| `schritt-1.wav` … `-4.wav` | WAV, 44 100 Hz, mono | 0,160 s | −12,1 … −13,5 dBFS | −29,4 … −31,8 dBFS | 0 / 0, Rest −43 … −47 dB | bestanden |

## Herkunft

**Das Original einer Kenney-Aufnahme kommt nicht ins Repository.** Das Skript sucht es unter `<quellen>/kenney/impact-sounds/inhalt/impactTin_medium_004.ogg`; `<quellen>` ist `--quellen` oder `%LOCALAPPDATA%\SilverAndAsh\klangquellen`. Beschaffung: Paket „Impact Sounds“ von <https://kenney.nl/assets/impact-sounds> laden und in `<quellen>/kenney/impact-sounds/inhalt/` entpacken. Das Skript bricht ab, wenn die SHA-256 der Originaldatei nicht stimmt:

- Original: `impactTin_medium_004.ogg`, SHA-256 `fd53e24efec3d5c1cee5a46fee70f97efd4bd01e53ac5d9b5f3b6719dfe9f5aa`
- Archiv: `kenney_impact-sounds.zip`, 800 850 Byte, SHA-256 `029d734af1582474edf3a694d1b0cebc97c1c152f2f39fa34d4c2bafc5de77f8` (Details: `design/grundlagen/klangquellen.md`)

**Zeilen für `assets/HERKUNFT.md`** (das Dokument sagt heute „nichts heruntergeladen“; dieser Satz muss für Klang angepasst werden, die Leitung trägt das nach):

| Datei | Skript | Datum |
|---|---|---|
| `audio/pistole.wav`, `audio/schritt-1.wav` … `audio/schritt-4.wav`, `audio/ghul-ankuendigung.wav` | `tools/audio/erzeugen.py` (mit `geraeusche_hp3.py`, `synth.py`, `ps1_klang.py`, `ableiten.py`); rein synthetisch | 2026-11-17 |
| `audio/klinge-1.wav` … `audio/klinge-3.wav` | `tools/audio/erzeugen.py` (Funktion `klinge`, mit `ableiten.py`, `ps1_klang.py`); rein synthetisch | 2026-11-17 |
| `audio/ambiente-katakomben.ogg` | `tools/audio/erzeugen.py` (mit `ambiente_hp1.py`, `ambiente_hp3.py`, `ps1_klang.py`); Fläche synthetisch, **Tropfen aus einer Aufnahme:** `impactTin_medium_004.ogg` aus „Impact Sounds“ von Kenney, <https://kenney.nl/assets/impact-sounds>, Lizenz CC0 1.0 (<https://creativecommons.org/publicdomain/zero/1.0/>; Satz aus `License.txt`: „This content is free to use in personal, educational and commercial projects.“), abgerufen 2026-11-16, SHA-256 des Originals `fd53e24efec3d5c1cee5a46fee70f97efd4bd01e53ac5d9b5f3b6719dfe9f5aa` | 2026-11-17 |

## Was mir nicht gefällt oder was ich nicht prüfen konnte

- Alles ist ungehört. Das Urteil „b“ des Menschen galt in allen vier Kategorien; der Ghul ist unbeurteilt (vorläufig).
- Die Klinge ist die Hörprobe-3-Klinge mit helleren Parametern. Die drei Varianten hier hat der Mensch nicht gehört, nur die eine aus der Kurzprobe.
- Der Ghul-Laut ist sehr dunkel (fast nichts über 2 kHz) und hat zwei Schichten plus gesättigte Kopie.
- Die Tropfen aus der Blech-Aufnahme klingen vielleicht nicht nach Wasser; er hat sie in der Kurzprobe trotzdem gewählt.
- Der Schuss hat nur eine Fassung (Wiederholung fällt auf), Schritte haben vier, die Klinge drei.
