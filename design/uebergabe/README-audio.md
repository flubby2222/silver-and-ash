# Übergabe Audio für die Code-Sitzung

Stand 2026-11-17, nach dem Urteil „b“ zu Hörprobe 5 kurz. **Ungehört:** Weder ich noch die Leitung haben diese Dateien gehört. Gemessen ist nur der technische Rahmen (unten); ob sie im Spiel passen, entscheidet der Mensch beim Spielen. Kein Spielcode wurde geändert.

Ablage im Spiel: Dateien aus `design/uebergabe/assets/audio/` nach `assets/audio/`, Skripte aus `design/uebergabe/tools/audio/` nach `tools/audio/`. Wiedergabe über Raylib-cs (`LoadSound`/`PlaySound`, Ambiente `LoadMusicStream`), kein neues Paket. Raumklang (#225) gibt es noch nicht: erst feste Lautstärke.

## Datei → Ereignis → Lautstärke

| Datei | Ereignis (`SilverAndAsh.Core.Gameplay`) | Vorschlag 0–1 | Hinweis |
|---|---|---|---|
| `pistole.wav` | `ShotFired` (nur `Weapon == Pistol`; andere Fernwaffen später mit eigener Datei) | 1,0 | lauteste Datei (Spitze −1,5 dBFS) |
| `klinge-1.wav` … `klinge-3.wav` | Treffer auf Gegner: `GhoulHit`, `BatHit`, `GormHit`, `DummyHit`; zufällig eine der drei, nie dieselbe zweimal hintereinander | 1,0 | Anmerkung 1 |
| `ghul-ankuendigung.wav` | `GhoulWindUp` (Ort: der Ghul mit dieser Id) | 0,9 | Anmerkung 2; **vorläufig, wird später überarbeitet** |
| `schritt-1.wav` … `schritt-4.wav` | kein Ereignis in Core (Anmerkung 3); abwechselnd zufällig | 1,0 | leise Dateien (Spitze −12 dBFS) |
| `ambiente-katakomben.ogg` | Schleife, solange ein Lauf läuft; als Stream (`LoadMusicStream`, `UpdateMusicStream` je Bild, `SetMusicVolume`) | 1,0 | sehr leise Fläche (Mittel −38 dBFS) |

Die Dateien tragen das Pegelverhältnis schon in sich (Schuss −1,5, Klinge −4 bis −5, Ghul −3, Schritte −12 dBFS Spitze). Deshalb überall 1,0 und eine **Gesamtlautstärke** (Vorschlag 0,7). Für den Test sollte das Ambiente einen eigenen Regler haben, weil ich seine Lautstärke im Spiel nicht beurteilen kann.

**Anmerkung 1 – Treffer-Ereignisse.** `GhoulHit` und Co. sagen nicht, womit getroffen wurde; ein Pistolenschuss erzeugt dasselbe Ereignis. Vorschlag: Klingengeräusch nur, wenn im selben Tick ein `MeleeSwung` vorkam. Sonst muss Core das Ereignis um die Waffe ergänzen (`WeaponKind`, wie bei `MeleeSwung`). Für den Klingen*schwung* ohne Treffer habe ich keine Datei.

**Anmerkung 2 – Ankündigungszeit des Ghuls.** `assets/balance.json`, `ghoul.windUpTime` = **0,5 s** (geprüft; `Ghoul.cs` setzt `GhoulWindUp` beim Beginn des Ausholens). Die Datei ist auf den Schlag bei **0,50 s** gebaut (0,62 s lang). Ändert jemand `windUpTime`, passt die Uhr nicht mehr: die Datei nicht strecken, sondern das Skript mit neuer Zeit neu laufen lassen (`HIT` in `geraeusche_hp3.py`) oder `windUpTime` bei 0,5 lassen.

**Anmerkung 3 – Schritte.** Es gibt kein Core-Ereignis. Sauber wäre ein `PlayerStepped(PlayerId, Foot)` aus der gelaufenen Strecke am Boden (`IsGrounded`). Als Hilfsweg im Game: aus der Strecke am Boden, etwa alle 0,5 s im normalen Lauf (so war die Folge in der Hörprobe gebaut); kein Schritt in der Luft. Den Abstand muss die Code-Sitzung gegen die Laufgeschwindigkeit prüfen; ich kenne ihn nicht.

## Musik (neu, 2026-11-17): Hauptmenü und Katakomben

Urteil des Menschen zur Hörprobe 6: „b ist als Theme im Hauptmenü top. Bedarf später noch Überarbeitung.“ **Alles Folgende ist vorläufig und wird später überarbeitet; nichts davon ist gehört** (nur gemessen).

| Datei | Wo | Länge | Vorschlag 0–1 |
|---|---|---|---|
| `musik-hauptmenue.ogg` | Titelbildschirm (Hauptmenü), als Schleife (`LoadMusicStream`, `SetMusicLooping` bzw. beim Stream-Ende neu starten) | 72 s, stereo, nahtlos, **ohne Herzschlag** (Urteil 2026-11-17: „zu überladen“) | 0,8 |
| `musik-katakomben.ogg` | im Lauf (Katakomben) **statt** `ambiente-katakomben.ogg`, als Schleife | 64 s, stereo, nahtlos | 1,0 |

- **Lautstärke:** Das Menü-Thema ist die lautere Datei (Mittel −23,5 dBFS, −22,3 LUFS; Glocke und Chor sind seit dem Weglassen des Herzschlags unverändert laut, nur insgesamt fehlt dessen Anteil), die Katakomben-Musik absichtlich etwa 12 LU leiser (Mittel −34 dBFS, −32,5 LUFS), damit Schritte, Ankündigungen und Treffer darüber stehen. Auch hier: Gesamtlautstärke 0,7; ein eigener Regler für die Musik ist im Test sinnvoll, weil ich das Verhältnis zu den Geräuschen nicht hören kann.
- **Übergang Titel → Lauf (Vorschlag):** Menü-Musik in etwa 1,5 s ausblenden, dann den Lauf laden, die Katakomben-Musik in etwa 3 s einblenden (Lautstärke 0 → 1,0, von Hand je Bild mit `SetMusicVolume`); beim Zurück zum Titel umgekehrt. Beide Stücke beginnen auf demselben Grundton (E) im selben Hall, deshalb klingt ein weiches Überblenden stimmig; ein hartes Umschalten dürfte stören. Ungehört.
- **Das alte Ambiente** (`ambiente-katakomben.ogg`) ersetzt die Katakomben-Musik; ob die Tropfen bleiben, entscheidet sich mit den Geräuschen (die Tropfen waren in die Ambiente-Datei eingebaut).
- **Nahtlos:** Alle Noten und der Hallschwanz laufen kreisförmig, die PS1-Kette bekam vorne das Ende und hinten den Anfang angehängt. Naht geprüft: Sprung vom letzten zum ersten Sample höchstens so groß wie 99,9 % der Nachbar-Abstände (unten).
- **Skripte:** `python tools/audio/musik_erzeugen.py --aus assets/audio [--nur menue|katakomben]` (mit `musik_klang.py`, `ambiente_hp3.py`, `ambiente_hp1.py`, `ps1_klang.py`, `synth.py`); Dauer je Stück etwa 5 Minuten (reines Python). Prüfung: `python tools/audio/musik_pruefen.py assets/audio`. Zwei Läufe ergeben byte-gleiche Dateien (siehe unten).

### Palette von b – vorläufiger Maßstab

| | |
|---|---|
| Tonart, Tempo | e-Moll (E Fis G A H C D), 60 Schläge/min, Takt 4/4; Grundton E |
| Klangerzeuger | **tiefe Glocke** (neun unharmonische Teiltöne eines Glockenprofils, Unterton und Prim leicht schwebend), **Chor-Pad** (drei verstimmte Stimmquellen, Vokalfilter u → o → u), **Herzschlag-Trommel** (Sinus, Tonhöhe fällt auf den Grundton E1 bzw. H1) |
| Hall | **einer** für alles: Schroeder-Hall, RT60 2,8 s, Dämpfung 2,5 kHz, 50 % Send je Spur, 70 % Hallanteil (links und rechts mit verschiedener Zufallszahl) |
| Frequenzrahmen | Musik trägt unter etwa 300 Hz; die Mitte 300–3 000 Hz liegt in den Katakomben mindestens 15 dB unter dem Gesamtpegel, über 3 kHz fast nichts |
| Kette | PS1: 22 050 Hz, ADPCM, Gauß-Interpolation, kein Hall der Kette |

## Skripte

`python tools/audio/erzeugen.py --aus assets/audio [--quellen <ordner>]` erzeugt alle Geräusche und das alte Ambiente. **Zwei Läufe ergeben byte-gleiche Dateien** (mit SHA-256 geprüft; die OGG-Datei mit `ffmpeg -fflags +bitexact`). Nur Python 3.12 (Standardbibliothek) und `ffmpeg`; Dauer etwa 50 s.

`python tools/audio/pruefen.py assets/audio` misst Länge, Spitze, Ränder und die Naht des Ambientes:

| Datei | Format | Länge | Spitze | Mittel | Ränder / Naht | Ergebnis |
|---|---|---|---|---|---|---|
| `ambiente-katakomben.ogg` | OGG, 44 100 Hz, stereo | 30,0 s | −22,6 dBFS | −37,9 dBFS | Naht L/R: 0,00040 ≤ 0,00156; 0,00015 ≤ 0,00156 | bestanden |
| `ghul-ankuendigung.wav` | WAV, 44 100 Hz, mono | 0,620 s | −3,0 dBFS | −18,3 dBFS | 0 / 0, Rest −105 dB | bestanden |
| `klinge-1.wav` … `-3.wav` | WAV, 44 100 Hz, mono | 0,240 s | −4,1 … −4,9 dBFS | −22,6 … −23,7 dBFS | 0 / 0, Rest −51/−52 dB | bestanden |
| `pistole.wav` | WAV, 44 100 Hz, mono | 0,400 s | −1,5 dBFS | −18,2 dBFS | 0 / 0, Rest −74 dB | bestanden |
| `schritt-1.wav` … `-4.wav` | WAV, 44 100 Hz, mono | 0,160 s | −12,1 … −13,5 dBFS | −29,4 … −31,8 dBFS | 0 / 0, Rest −43 … −47 dB | bestanden |

### Prüfung der Musik (`musik_pruefen.py`) und Byte-Gleichheit

| Datei | Länge | Spitze | Mittel | Lautheit | Naht L / R (Sprung ≤ 99,9 %) | < 300 Hz | 300–3 000 Hz | > 3 000 Hz | gesamt | Abstand Mitte | außerhalb e-Moll | stärkste Tonklasse | Ergebnis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `musik-hauptmenue.ogg` (ohne Herzschlag) | 72,0 s | −7,8 dBFS | −23,5 dBFS | −22,3 LUFS | 0,00391 ≤ 0,00790; 0,00308 ≤ 0,00629 | −25 | −37 | −88 | −24 | −12,5 dB | 0,0 % | E (52 %) | bestanden |
| `musik-katakomben.ogg` | 64,0 s | −20,7 dBFS | −34,0 dBFS | −32,5 LUFS | 0,00104 ≤ 0,00131; 0,00021 ≤ 0,00131 | −37 | −56 | −98 | −37 | **−19,0 dB** | 0,0 % | E (59 %) | bestanden |

Das 15-dB-Ziel für die freie Mitte ist in den Katakomben erreicht (−19,0 dB); im Menü ist die Mitte ohne Herzschlag etwas stärker (−12,5 statt −15,2 dB), dort gilt kein Ziel. **Prüfung der Herzschlag-Entfernung:** In den Fenstern ohne Herzschlag (Takt 1–3, Takt 16–18) ist die Datei gegenüber der Fassung mit Herzschlag um ±0,0 dB gleich laut (Glocke und Chor unverändert); in den Herzschlag-Teilen ist sie um 2,3–5,1 dB leiser, wie zu erwarten. **Byte-Gleichheit:** zwei vollständige Läufe ergaben dieselbe SHA-256: `musik-hauptmenue.ogg` `00c16d25434707500b1610e9edf0b0a3a36c31ae8fe9748bb2a77d343ae15a7f`, `musik-katakomben.ogg` `dcf144b6357b400b43688e74003edbf9012d34c869545e14611d03e500d1ebb0` (gleicher Rechner, gleiche `ffmpeg`-Fassung).

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

| `audio/musik-hauptmenue.ogg`, `audio/musik-katakomben.ogg` | `tools/audio/musik_erzeugen.py` (mit `musik_klang.py`, `ambiente_hp3.py`, `ambiente_hp1.py`, `ps1_klang.py`, `synth.py`); rein synthetisch, keine Aufnahmen | 2026-11-17 |

## Was mir nicht gefällt oder was ich nicht prüfen konnte

**Zur Musik:**
- **Ungehört**, auch die Naht der Schleife: Gemessen ist nur, dass der Sprung am Übergang nicht größer ist als sonst im Stück; ob man die Wiederholung hört, weiß ich nicht.
- **Die Erweiterung des Menü-Themas von 24 auf 72 s** ist meine Setzung: Teil A ist die Skizze (gleiche Akkordfolge Em – C/E – Am/E – Em, Glocke), die Akkorde sind aber neu gesetzt (die Stimmführung wurde neu berechnet, damit sie über die ganze Schleife keine parallelen Quinten oder Oktaven hat). Teil B (G/D – D/F# – C) und Teil C (Am/E – Bm, führt nach Em) sind neu und nicht vom Menschen gehört. Ob 72 s trägt oder zu lang/gleichförmig ist, weiß ich nicht.
- **Katakomben-Musik:** Ob sie „karg und leise“ genug ist und trotzdem nach Stimmung klingt, ob die Katakomben nicht einfach dröhnen (sehr tiefer Chor, Mitte −19 dB, fast alles unter 300 Hz): ungehört. Auf kleinen Lautsprechern könnte sie kaum zu hören sein.
- **Das Überblenden zwischen Titel und Lauf** ist ein Vorschlag, nicht erprobt.
- Eine Zwischenfassung hatte versehentlich einen Gleichanteil (die Stimmquelle ist einseitig) und war nach der Messung fast reiner Bass (Mitte 28 dB unter dem Gesamtpegel); er ist jetzt entfernt, die Messung zeigt es.

**Zu den Geräuschen (unverändert):**

- Alles ist ungehört. Das Urteil „b“ des Menschen galt in allen vier Kategorien; zum Ghul sagte er am 2026-11-17 „Passt“, präzisiert: „Den Ghoul-Sound passen wir später nochmal an. Zum Testen ist er ok.“ Er ist also **vorläufig und wird später überarbeitet**, nicht bestätigt. Die Datei ist byte-gleich zur Fassung `5-ghul.wav` der Kurzprobe.
- Die Klinge ist die Hörprobe-3-Klinge mit helleren Parametern. Die drei Varianten hier hat der Mensch nicht gehört, nur die eine aus der Kurzprobe.
- Der Ghul-Laut ist sehr dunkel (fast nichts über 2 kHz) und hat zwei Schichten plus gesättigte Kopie.
- Die Tropfen aus der Blech-Aufnahme klingen vielleicht nicht nach Wasser; er hat sie in der Kurzprobe trotzdem gewählt.
- Der Schuss hat nur eine Fassung (Wiederholung fällt auf), Schritte haben vier, die Klinge drei.
