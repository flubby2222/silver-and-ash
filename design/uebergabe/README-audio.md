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

## Musik (Stand 2026-11-17, zweite Runde): Menü, Katakomben, Boss, Händler

Urteile des Menschen: zur Hörprobe 6 „b ist als Theme im Hauptmenü top. Bedarf später noch Überarbeitung.“; zur Musik „Wenn der Herzschlag aus dem Theme-Sound raus könnte, wär das gut (ist mir zu überladen). Für ingame soll er eigene Samples bauen. (Bossfight, Händler extra).“ Maßstab daraus: lieber eine Schicht weniger. **Alles vorläufig und wird später überarbeitet; nichts davon ist gehört** (nur gemessen).

| Datei | Wo | Länge | Tonart, Tempo | Schichten (höchstens drei gleichzeitig) | Vorschlag 0–1 |
|---|---|---|---|---|---|
| `musik-hauptmenue.ogg` | Titelbildschirm | 72,0 s | e-Moll, 60/min | Glocke, Chor (ohne Herzschlag) | 0,8 |
| `musik-katakomben.ogg` | im Lauf, Standard (Erkunden und Kämpfen) | 70,0 s | e-Moll, 60/min | Chor, seltene Glocke | 1,0 |
| `musik-boss.ogg` | Bosskampf gegen Gorm | 67,2 s | e-phrygisch, 100/min | Trommelpuls, Kettenakzent, Chor | 1,0 |
| `musik-boss-phase2.ogg` | ab Gorms zweiter Phase | 67,2 s, **synchron zu `musik-boss.ogg`** | wie Boss | dieselben drei Schichten, dichter (mehr Akzente, Geisterschläge) | 1,0 |
| `musik-haendler.ogg` | Händlerraum | 46,7 s | G-Dur, 72/min | Chor („a“), leise Glocken | 1,0 |

Alle Dateien: OGG, stereo, nahtlose Schleife; dazu Gesamtlautstärke 0,7. Lautheit (LUFS): Menü −22,3, Boss −26,2 (Phase 2 −25,3), Händler −27,5, Katakomben −32,2, damit die Katakomben-Musik am tiefsten unter dem Spielgeschehen liegt. Jedes Stück ist eine eigene Komposition mit eigenem Einfall (kein Auszug aus dem Menü-Thema).

### Woran hängt welche Musik (aus dem Core gelesen, kein Code geändert)

| Musik | Zustand / Ereignis | Im Core? |
|---|---|---|
| Menü | Titelbildschirm | Zustand des Spiels im Game-Projekt, kein Core-Ereignis nötig |
| Katakomben | Lauf läuft, kein Bosskampf, nicht im Händlerraum | `GameWorld` läuft; „kein Boss“ über `GameWorld.IsBossFightOn == false` (da) |
| Boss | Beginn: Ereignis `BossFightStarted` (kommt, wenn der Boss-Raum versiegelt wird und Gorm lebt); Zustand `GameWorld.IsBossFightOn`; Ende: `GormDied` | **da** |
| Boss Phase 2 | Ereignis `GormRoared` (Gorm fällt unter `gorm.phase2Health` = 0,5 und brüllt); Zustand `Gorm.IsInPhase2` | **da** |
| Händler | Spieler betritt den Händlerraum, verlässt ihn | **fehlt:** `RoomKind` kennt keine Händler-Raumart (Start, Combat, Corridor, Boss, Treasure, Secret; ADR 0008 plant „Merchant“, v0.11) und der Core meldet das Betreten eines Raums nicht als Ereignis (`RoomState.Enter()` läuft in `GameWorld.EnterRooms`, gemeldet werden nur `RoomSealed` und `RoomCleared`). Nötig: `RoomKind.Merchant` und ein Ereignis wie `RoomEntered(RoomIndex, Kind)` samt Gegenstück beim Verlassen (oder eine Abfrage `GameWorld.RoomKindAt(Position)`). |
| Ende des Laufs | `RunWon`, `RunLost` | da: Musik ausblenden, danach Menü-Musik |

**Überblenden (Vorschlag, nicht erprobt):** Raylib hat je Stück einen `Music`-Stream; mehrere laufen gleichzeitig (`UpdateMusicStream` je Bild für jeden spielenden Stream), das Überblenden macht das Spiel mit `SetMusicVolume` von Hand. Katakomben → Boss: Katakomben in 2 s aus, Boss in 2 s ein. **Boss → Phase 2 bleibt im Takt**, weil beide Dateien gleich lang sind, im selben Tempo und mit derselben Akkordfolge: Beide beim Bosskampf-Beginn **gleichzeitig starten** (Phase 2 mit Lautstärke 0) und bei `GormRoared` in 1,5 s überblenden (Boss 1 → 0, Phase 2 0 → 1). Nach `GormDied` die Boss-Musik in 3 s ausblenden. Katakomben ↔ Händler: je 2 s. Titel → Lauf: Menü in 1,5 s aus, Katakomben in 3 s ein.
**Prüfsummenliste:** Das Spiel prüft die Klang-Dateien mit `audio.sha256`; die neuen Dateinamen müssen dort und in `SoundBoard.AllFiles` stehen. `ambiente-katakomben.ogg` bleibt als Rückfall, bis die Katakomben-Musik eingebaut ist.

### Palette von b – vorläufiger Maßstab

| | |
|---|---|
| Tonart, Tempo | e-Moll (E Fis G A H C D), 60 Schläge/min, Grundton E (Menü, Katakomben); Boss e-phrygisch (F statt Fis), 100/min; Händler G-Dur (Paralleltonart), 72/min |
| Klangerzeuger | **tiefe Glocke** (neun unharmonische Teiltöne eines Glockenprofils), **Chor-Pad** (drei verstimmte Stimmquellen, Vokalfilter u → o → u, beim Händler a), **Trommel** (Sinus mit fallender Tonhöhe), **Kettenakzent** (kurzer Glockenschlag, 0,5 s, auf A4 und E5) |
| Hall | **einer** für alles: Schroeder-Hall, RT60 2,8 s, Dämpfung 2,5 kHz, 50 % Send, 70 % Anteil (links und rechts mit verschiedener Zufallszahl). Bei den vier neuen Stücken ist der Send bei 150 Hz hochpassgefiltert (siehe unten) |
| Frequenzrahmen | trägt unter etwa 300 Hz; die Mitte 300–3 000 Hz liegt in den Katakomben mindestens 15 dB unter dem Gesamtpegel, über 3 kHz fast nichts (Ausnahme Händler: heller) |
| Kette | PS1: 22 050 Hz, ADPCM, Gauß-Interpolation, kein Hall der Kette |

### Skripte für die Musik

`python tools/audio/musik_erzeugen.py --aus assets/audio [--nur menue|katakomben|boss|boss2|haendler]` (mit `musik_klang.py`, `ambiente_hp3.py`, `ambiente_hp1.py`, `ps1_klang.py`, `synth.py`); je Stück 5 bis 10 Minuten (reines Python). Prüfung: `python tools/audio/musik_pruefen.py assets/audio`. Die Anfänge zum Vorhören macht `python tools/audio/musik_vorhoeren.py assets/audio <Zielordner>` aus den fertigen OGG-Dateien (nur `ffmpeg`).

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
| `musik-boss-phase2.ogg` | 67.2 s | -9.1 dBFS | -25.4 dBFS | -25.3 LUFS | 0.00119 ≤ 0.00546; 0.00031 ≤ 0.00876 | -26 | -39 | -70 | -26 | -13.3 dB | 17.6 % | A (44 %) | bestanden |
| `musik-boss.ogg` | 67.2 s | -8.9 dBFS | -26.0 dBFS | -26.2 LUFS | 0.00104 ≤ 0.00531; 0.00110 ≤ 0.00861 | -26 | -41 | -72 | -26 | -15.1 dB | 21.3 % | A (32 %) | bestanden |
| `musik-haendler.ogg` | 46.7 s | -15.6 dBFS | -30.0 dBFS | -27.5 LUFS | 0.00104 ≤ 0.00992; 0.00320 ≤ 0.00800 | -43 | -36 | -79 | -34 | -1.8 dB | 0.0 % | G (41 %) | bestanden |
| `musik-hauptmenue.ogg` | 72.0 s | -7.8 dBFS | -23.5 dBFS | -22.3 LUFS | 0.00391 ≤ 0.00790; 0.00308 ≤ 0.00629 | -25 | -37 | -88 | -24 | -12.5 dB | 0.0 % | E (62 %) | bestanden |
| `musik-katakomben.ogg` | 70.0 s | -22.2 dBFS | -34.0 dBFS | -32.2 LUFS | 0.00089 ≤ 0.00150; 0.00031 ≤ 0.00143 | -39 | -54 | -99 | -38 | -15.9 dB | 0.0 % | D (54 %) | bestanden |

Spalten: Pegel in dBFS je Band (< 300 Hz, 300–3 000 Hz, > 3 000 Hz, gesamt), *Abstand Mitte* = Mitte minus gesamt; Naht = Sprung vom letzten zum ersten Sample gegenüber dem 99,9-%-Abstand zweier Nachbar-Samples (je Kanal); Tonklassen ab 82 Hz aus der fertigen Datei (Boss mit e-phrygischer Tonleiter gerechnet). Das 15-dB-Ziel für die freie Mitte gilt für die Katakomben (erreicht, −15,9 dB); Boss −15,1 dB, Menü −12,5 dB, Händler −1,8 dB (hell, bewusst).

**Byte-Gleichheit:** Für jedes Stück ergaben zwei vollständige Läufe dieselbe SHA-256 (gleicher Rechner, gleiche `ffmpeg`-Fassung); das Menü ist nach dem Umbau des Skripts byte-gleich zur vorigen Fassung geblieben.

- `musik-hauptmenue.ogg`: `00c16d25434707500b1610e9edf0b0a3a36c31ae8fe9748bb2a77d343ae15a7f`
- `musik-katakomben.ogg`: `0c5eb10a8d354116423a2f5107bba6f3ff74b9598e1a87b7a2ae26d882c12497`
- `musik-boss.ogg`: `e24155674be5c96897504a22d1e02b7036ea9dd0118ff64ede8caeb6e67099a3`
- `musik-boss-phase2.ogg`: `5bb7b2386c1bd78c1fa87de20e70f6f4eef6025ae487f209eca4e2206618418f`
- `musik-haendler.ogg`: `bfe4b8fda14d09fb4e682c7a75501af180693fffb83874d6a27c6a7e91a0380a`

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
| `audio/musik-hauptmenue.ogg`, `audio/musik-katakomben.ogg`, `audio/musik-boss.ogg`, `audio/musik-boss-phase2.ogg`, `audio/musik-haendler.ogg` | `tools/audio/musik_erzeugen.py` (mit `musik_klang.py`, `ambiente_hp3.py`, `ambiente_hp1.py`, `ps1_klang.py`, `synth.py`); rein synthetisch, keine Aufnahmen | 2026-11-17 |

## Was mir nicht gefällt oder was ich nicht prüfen konnte

**Zur Musik (alles ungehört):**
- **Naht:** Gemessen ist nur, dass der Sprung vom letzten zum ersten Sample nicht größer ist als sonst im Stück; ob man die Wiederholung hört, weiß ich nicht.
- **Hall färbt die Bässe (gefunden, nur teilweise behoben):** Die Kammfilter des gemeinsamen Halls schwingen bei etwa 45, 55, 90 und 110 Hz mit. Bei der ersten Fassung des Bosses lag ein großer Teil der Energie über 82 Hz auf Fis2 (92 Hz), das gegen die Akkorde F und G steht. Deshalb ist der Send der vier neuen Stücke bei 150 Hz hochpassgefiltert (Menü unverändert, damit die vom Menschen gelobte Datei bleibt). Ob der Bass jetzt sauber klingt, ist ungehört; der Hall selbst sollte später überarbeitet werden.
- **Die Trommel ist nicht auf den Grundton gestimmt:** Der Tonhöhenabfall (100 → 41 Hz, in 20 ms) hat seinen Energieschwerpunkt bei etwa 55 Hz (A1), nicht bei E1 (41 Hz). Als Schlag unbestimmter Tonhöhe ist das unkritisch, aber meine frühere Aussage „Sub-Ton auf dem Grundton“ stimmte nicht. Die Tonklassen-Messung der Mischung wird davon verfälscht (Boss: 21 % „außerhalb“, stärkste Klasse A); der Chor allein liegt auf den Akkordtönen (gemessen).
- **Katakomben: Tonart uneindeutig.** Stärkste Tonklasse ist D (54 %), nicht E: Die Akkorde Em – G – D/A – C/G – Bm enthalten D3 dreimal. Klingt es nach e-Moll, nach D-Dur oder nach etwas Schwebendem? Das hört nur der Mensch.
- **Katakomben fast nur Bass** (unter 300 Hz −39 dB, Mitte −52 dB, Gesamtpegel −34 dBFS im Mittel): Auf kleinen Lautsprechern kann sie kaum zu hören sein.
- **Boss:** „Drängend“ ist mein Versuch über Tempo 100/min, Trommelmuster und Achtel-Akzente; der „Kettenakzent“ ist ein kurzer Glockenschlag und klingt vielleicht nicht nach Kette. Phase 2 ist ein vollständiges Stück (damit ein Überblenden genügt), keine Zusatzschicht; eine getrennte Zusatzschicht habe ich nicht geliefert.
- **Händler:** „Wärmer und heller“ entsteht nur über Dur, Vokal „a“, höhere Lage und leise Glocken; ob das nach Ruhepunkt statt nach Spieluhr klingt, ist offen. Die Mitte liegt hier bewusst nicht frei (−1,8 dB).
- **Das Überblenden** und die Lautstärken sind Vorschläge, nicht erprobt.
- Frühere Zwischenfassung: ein Gleichanteil (die Stimmquelle ist einseitig) machte das Spektrum zu bassig; er ist kreisförmig herausgefiltert.

**Zu den Geräuschen (unverändert):**

- Alles ist ungehört. Das Urteil „b“ des Menschen galt in allen vier Kategorien; zum Ghul sagte er am 2026-11-17 „Passt“, präzisiert: „Den Ghoul-Sound passen wir später nochmal an. Zum Testen ist er ok.“ Er ist also **vorläufig und wird später überarbeitet**, nicht bestätigt. Die Datei ist byte-gleich zur Fassung `5-ghul.wav` der Kurzprobe.
- Die Klinge ist die Hörprobe-3-Klinge mit helleren Parametern. Die drei Varianten hier hat der Mensch nicht gehört, nur die eine aus der Kurzprobe.
- Der Ghul-Laut ist sehr dunkel (fast nichts über 2 kHz) und hat zwei Schichten plus gesättigte Kopie.
- Die Tropfen aus der Blech-Aufnahme klingen vielleicht nicht nach Wasser; er hat sie in der Kurzprobe trotzdem gewählt.
- Der Schuss hat nur eine Fassung (Wiederholung fällt auf), Schritte haben vier, die Klinge drei.
