# Herkunft der Assets

Alles unter `assets/` ist per Skript erzeugt, nichts heruntergeladen (`CLAUDE.md`, ADR 0009). **Ausnahme für Klang** (Entscheidung des Menschen, 2026-11-17): Ein Skript darf echte Tonaufnahmen aus den CC0-Paketen von kenney.nl verarbeiten. Das Original einer Aufnahme kommt nie ins Repository, nur das abgeleitete Ergebnis; Quelle, Lizenz, Abrufdatum und SHA-256 des Originals stehen dann in der Tabelle. Jede Bild-, Modell- oder Klangdatei steht hier mit dem Skript, das sie erzeugt, und dem Datum ihres Stands. Wer eine Datei neu erzeugt oder ersetzt, trägt das neue Datum ein. Textdateien (Level, Räume, Shader, `balance.json`) sind von Hand geschrieben und stehen hier nicht.

| Datei | Skript | Datum |
|---|---|---|
| `models/ghoul.glb`, `models/ghoul.png` | `tools/models/ghoul.py` (mit `tools/models/psx.py`) | 2026-11-16 |
| `models/chest_beggar.glb`, `models/chest_guard.glb`, `models/chest_king.glb`, `models/chest_vampire.glb`, `models/chest_beggar.png`, `models/chest_guard.png`, `models/chest_king.png`, `models/chest_vampire.png` | `tools/models/chest.py` (mit `tools/models/psx.py`) | 2026-11-17 |
| `models/pistol.glb`, `models/pistol_hand.glb`, `models/pistol.png` | `tools/models/pistol.py` (mit `tools/models/arm.py` und `tools/models/psx.py`) | 2026-11-17 |
| `models/blade.glb`, `models/blade_hand.glb`, `models/blade.png` | `tools/models/blade.py` (mit `tools/models/arm.py` und `tools/models/psx.py`) | 2026-11-17 |
| `models/bat.glb`, `models/bat.png` | `tools/models/bat.py` (mit `tools/models/psx.py`) | 2026-11-17 |
| `models/gorm.glb`, `models/gorm_body.png`, `models/gorm_body_phase2.png`, `models/gorm_limbs.png` | `tools/models/gorm.py` (mit `tools/models/gorm_base.py`, `tools/models/gorm_heads.py` und `tools/models/psx.py`) | 2026-11-17 |
| `models/door_normal.glb`, `models/door_locked.glb`, `models/door_boss.glb`, `models/door_cracked.glb`, `models/door_normal.png`, `models/door_locked.png`, `models/door_boss.png`, `models/door_cracked.png` | `tools/models/door.py` (mit `tools/models/psx.py`) | 2026-11-17 |
| `models/door_frame_normal_a.glb`, `models/door_frame_normal_a.png`, `models/door_frame_normal_b.glb`, `models/door_frame_normal_b.png`, `models/door_frame_normal_c.glb`, `models/door_frame_normal_c.png`, `models/door_frame_locked_a.glb`, `models/door_frame_locked_a.png`, `models/door_frame_locked_b.glb`, `models/door_frame_locked_b.png`, `models/door_frame_locked_c.glb`, `models/door_frame_locked_c.png`, `models/door_frame_boss_a.glb`, `models/door_frame_boss_a.png`, `models/door_frame_boss_b.glb`, `models/door_frame_boss_b.png`, `models/door_frame_boss_c.glb`, `models/door_frame_boss_c.png` | `tools/models/door.py` (mit `tools/models/psx.py`; liest `textures/catacomb_wall*.png` und `palette/hauptpalette.json`) | 2026-11-17 |
| `models/stairs_spiral_a.glb`, `models/stairs_spiral_a.png`, `models/stairs_spiral_b.glb`, `models/stairs_spiral_b.png`, `models/stairs_spiral_c.glb`, `models/stairs_spiral_c.png` | `tools/models/stairs.py` (mit `tools/models/psx.py`; liest `textures/catacomb_wall*.png`, `textures/catacomb_floor*.png` und `palette/hauptpalette.json`) | 2026-11-17 |
| `models/shock_wave.glb`, `models/shock_wave.png` | `tools/models/shock_wave.py` (mit `tools/models/psx.py`; liest `textures/catacomb_wall.png` und `palette/hauptpalette.json`) | 2026-11-18 |
| `models/cracked_wall_a.glb`, `models/cracked_wall_b.glb`, `models/cracked_wall_c.glb`, `models/cracked_wall_a.png`, `models/cracked_wall_b.png`, `models/cracked_wall_c.png` | `tools/models/cracked_wall.py` (mit `tools/models/psx.py`; liest `textures/catacomb_wall*.png`) | 2026-11-17 |
| `textures/catacomb_wall.png`, `textures/catacomb_floor.png`, `textures/catacomb_ceiling.png` | `tools/textures/catacomb.py` (mit `tools/models/psx.py`) | 2026-11-16 |
| `textures/catacomb_wall_b.png`, `textures/catacomb_wall_c.png`, `textures/catacomb_floor_b.png`, `textures/catacomb_floor_c.png`, `textures/catacomb_ceiling_b.png`, `textures/catacomb_ceiling_c.png` | `tools/textures/catacomb_variants.py` (mit `tools/textures/catacomb.py` und `tools/models/psx.py`) | 2026-11-17 |
| `palette/hauptpalette.json`, `palette/hauptpalette.gpl` | `tools/palette/hauptpalette.py` (reines Python; `--ableiten` belegt die Stein-Reihe aus `textures/catacomb_*.png`) | 2026-11-17 |
| `audio/pistole.wav`, `audio/schritt-1.wav`, `audio/schritt-2.wav`, `audio/schritt-3.wav`, `audio/schritt-4.wav`, `audio/ghul-ankuendigung.wav` | `tools/audio/erzeugen.py` (mit `geraeusche_hp3.py`, `synth.py`, `ps1_klang.py`, `ableiten.py`); rein synthetisch | 2026-11-17 |
| `audio/klinge-1.wav`, `audio/klinge-2.wav`, `audio/klinge-3.wav` | `tools/audio/erzeugen.py` (Funktion `klinge`, mit `ableiten.py`, `ps1_klang.py`); rein synthetisch | 2026-11-17 |
| `audio/ambiente-katakomben.ogg` | `tools/audio/erzeugen.py` (mit `ambiente_hp1.py`, `ambiente_hp3.py`, `ps1_klang.py`); Fläche synthetisch, **Tropfen aus einer Aufnahme:** `impactTin_medium_004.ogg` aus „Impact Sounds“ von Kenney, <https://kenney.nl/assets/impact-sounds>, Lizenz CC0 1.0 (<https://creativecommons.org/publicdomain/zero/1.0/>; Satz aus `License.txt`: „This content is free to use in personal, educational and commercial projects.“), abgerufen 2026-11-16, SHA-256 des Originals `fd53e24efec3d5c1cee5a46fee70f97efd4bd01e53ac5d9b5f3b6719dfe9f5aa`. Das Original liegt nicht im Repository. Seit Issue #271 spielt das Spiel diese Datei nicht mehr (die Katakomben-Musik ersetzt sie); sie wird mit den neuen Geräuschen ersetzt oder entfernt. | 2026-11-17 |
| `audio/musik-hauptmenue.ogg`, `audio/musik-katakomben.ogg` | `tools/audio/musik_erzeugen.py` (mit `musik_klang.py`, `ambiente_hp3.py`, `ambiente_hp1.py`, `ps1_klang.py`, `synth.py`); rein synthetisch, keine Aufnahmen | 2026-11-17 |
| `audio/p1-aufheben-ausruestung.wav`, `audio/p1-aufheben-phiole.wav`, `audio/p1-aufheben-schluessel.wav`, `audio/p1-aufheben-silber.wav`, `audio/p1-aufheben-weihmunition.wav`, `audio/p1-dash.wav`, `audio/p1-ghul-ankuendigung.wav`, `audio/p1-ghul-entdecken.wav`, `audio/p1-ghul-entdecken-2.wav`, `audio/p1-ghul-sterben.wav`, `audio/p1-ghul-sterben-2.wav`, `audio/p1-kiste-oeffnen.wav`, `audio/p1-landung.wav`, `audio/p1-menue-klick.wav`, `audio/p1-nachladen-bereit.wav`, `audio/p1-nachladen-start.wav`, `audio/p1-schmerz.wav`, `audio/p1-schritt-l1.wav`, `audio/p1-schritt-l2.wav`, `audio/p1-schritt-l3.wav`, `audio/p1-schritt-r1.wav`, `audio/p1-schritt-r2.wav`, `audio/p1-schritt-r3.wav`, `audio/p1-schuss-1.wav`, `audio/p1-schuss-2.wav`, `audio/p1-schuss-3.wav`, `audio/p1-schuss-geweiht-1.wav`, `audio/p1-schuss-geweiht-2.wav`, `audio/p1-schwung-1.wav`, `audio/p1-schwung-2.wav`, `audio/p1-sprung.wav`, `audio/p1-treffer-1.wav`, `audio/p1-treffer-2.wav`, `audio/p1-treffer-3.wav`, `audio/p1-tuer-oeffnen.wav`, `audio/p1-weihe-ende.wav` | `tools/audio/paket1/paket1.py` (mit `bauen.py`, `bauen9.py`, den Bauplänen in `skripte/` und `tools/audio/ps1_klang.py`, `synth.py`); eigene Synthese: sechs Quell-Klänge, in SoundCraft 0.20.1 gebaut, keine Aufnahmen, keine fremden Samples. **Braucht SoundCraft, läuft nicht in der CI** | 2026-11-17 |
| `audio/p1-fledermaus-biss.wav`, `audio/p1-fledermaus-fehlsturz.wav`, `audio/p1-fledermaus-flattern-1.wav`, `audio/p1-fledermaus-flattern-2.wav`, `audio/p1-fledermaus-kreischen.wav`, `audio/p1-fledermaus-spuck-ankuendigung.wav`, `audio/p1-fledermaus-spucken.wav`, `audio/p1-fledermaus-sterben.wav`, `audio/p1-fledermaus-treffer.wav` | `tools/audio/paket1/paket1.py` (Abschnitt Fledermaus; mit `bauen.py`, `bauen9.py`, den Bauplänen in `skripte/` und `tools/audio/ps1_klang.py`, `synth.py`); eigene Synthese aus denselben Quell-Klängen (Hauch, Glocke, Pfeife, Rauschschlag), in SoundCraft 0.20.1 gebaut, keine Aufnahmen, keine fremden Samples. **Braucht SoundCraft, läuft nicht in der CI** | 2026-11-17 |
| `audio/p1-gorm-ankuendigung-hammer.wav`, `audio/p1-gorm-ankuendigung-schwung.wav`, `audio/p1-gorm-ankuendigung-wurf.wav`, `audio/p1-gorm-aufschlag.wav`, `audio/p1-gorm-bruellen.wav`, `audio/p1-gorm-haken-treffer.wav`, `audio/p1-gorm-haken-wurf.wav`, `audio/p1-gorm-kette.wav`, `audio/p1-gorm-schritt-1.wav`, `audio/p1-gorm-schritt-2.wav`, `audio/p1-gorm-sterben.wav`, `audio/p1-gorm-treffer-1.wav`, `audio/p1-gorm-treffer-2.wav`, `audio/p1-gorm-treffer-kopf.wav` | `tools/audio/paket1/paket1.py` (Abschnitt Gorm; mit `bauen.py`, `bauen9.py`, den Bauplänen in `skripte/` und `tools/audio/ps1_klang.py`, `synth.py`); eigene Synthese aus denselben Quell-Klängen (Rauschschlag, Glocke, Pfeife, Hauch, Stimme), in SoundCraft 0.20.1 gebaut, keine Aufnahmen, keine fremden Samples. **Braucht SoundCraft, läuft nicht in der CI** | 2026-11-17 |

## Neu erzeugen

Modelle mit Blender 5.3 ohne Oberfläche; das Skript schreibt nach `assets/models/`:

```
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/ghoul.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/chest.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/pistol.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/blade.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/bat.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/gorm.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/door.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/cracked_wall.py
```

Level-Texturen ebenso; das Skript schreibt nach `assets/textures/`:

```
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/textures/catacomb.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/textures/catacomb_variants.py
```

Geräusche mit Python 3.12 (nur Standardbibliothek) und `ffmpeg`, Dauer etwa 50 s; zwei Läufe ergeben byte-gleiche Dateien. Für das Ambiente braucht das Skript das Kenney-Original außerhalb des Repositorys: Paket „Impact Sounds“ von <https://kenney.nl/assets/impact-sounds> laden und nach `%LOCALAPPDATA%\SilverAndAsh\klangquellen\kenney\impact-sounds\inhalt\` entpacken (oder `--quellen <ordner>`). Stimmt die SHA-256 des Originals nicht, bricht das Skript ab. `tools/audio/pruefen.py` misst Länge, Spitze, Ränder und die Naht des Ambientes:

```
python tools/audio/erzeugen.py --aus assets/audio
python tools/audio/pruefen.py assets/audio
```

Die Musik ebenso, nur Standardbibliothek und `ffmpeg`, Dauer etwa 5 Minuten je Stück (`--nur menue` oder `--nur katakomben` für eines); `tools/audio/musik_pruefen.py` misst Länge, Pegel, Lautheit, Naht, Frequenzbänder und Tonart:

```
python tools/audio/musik_erzeugen.py --aus assets/audio
python tools/audio/musik_pruefen.py assets/audio
```

Klangpaket 1 (`p1-*.wav`) **braucht SoundCraft** (`C:/Program Files/ArtCraft/SoundCraft/soundcraft-cli.exe`, Version 0.20.1) und läuft deshalb **nicht in der CI**. Das Skript baut die sechs Quell-Klänge in SoundCraft, spielt sie je Geräusch in der Tonhöhe verschoben und kopiert die 59 Dateien nach `assets/audio/`; Messwerte und Zuordnung stehen in `tools/audio/paket1/uebersicht.md` (dort noch mit den Pfaden der Übergabe):

```
python tools/audio/paket1/paket1.py
```

**Danach immer die Prüfsummen-Listen `models/models.sha256`, `textures/textures.sha256` und `audio/audio.sha256` neu schreiben** (normales Python 3, ein Aufruf für alle drei). Ohne passenden Eintrag lädt das Spiel die Datei nicht und zeichnet den Platzhalter bzw. die Fläche einfarbig oder bleibt bei diesem Geräusch stumm; ein Test wird rot:

```
python tools/models/checksums.py
```

Am 2026-11-16 geprüft: `ghoul.glb` und `ghoul.png` kommen byte-gleich zur eingecheckten Fassung heraus. Wie das Spiel die Modelle lädt, steht in [`docs/modelle.md`](../docs/modelle.md).

Am 2026-11-16 geprüft: `catacomb_wall.png`, `catacomb_floor.png` und `catacomb_ceiling.png` kommen byte-gleich zur eingecheckten Fassung heraus (SHA-256 vor und nach dem Lauf verglichen).

Am 2026-11-17 geprüft: Die vier Kisten (`chest_*.glb`, `chest_*.png`) kommen aus `tools/models/chest.py` byte-gleich zur eingecheckten Fassung heraus (SHA-256 vor und nach dem Lauf verglichen).

Am 2026-11-17 geprüft: Pistole und Klinge mit ihren Händen (`pistol*.glb`, `blade*.glb`, `pistol.png`, `blade.png`) kommen aus `tools/models/pistol.py` und `tools/models/blade.py` byte-gleich zur eingecheckten Fassung heraus (SHA-256 vor und nach dem Lauf verglichen). Gegenüber der Übergabe des Designers backen beide Skripte jetzt den Maßstab in die Ecken ein (`bake_scale` in `psx.py`); die `.glb`-Dateien unterscheiden sich deshalb von der Übergabe, das Bild nicht.

Am 2026-11-17 ersetzt (Issues #256 und #257): Pistole und Klinge tragen statt Handschuh einen langen Mantelärmel (`build_sleeve4` in `tools/models/arm.py`), der Hahn der Pistole ist kleiner und hat einen hellen Feuerstein mit eigenem Texturfeld. Die sechs Dateien kommen aus der Übergabe des Designers (Commit `6ebf63f`) und aus `pistol.py` und `blade.py` byte-gleich heraus (SHA-256 vor und nach dem Lauf verglichen). `arm.py` ist nur um neue Funktionen ergänzt, `psx.py` unverändert; die übrigen Modelle nutzen `arm.py` nicht.

Am 2026-11-17 ersetzt (Issue #265, Fassung k): Die Ärmel sind dunkelblaue Mantelärmel mit heller Rüschenmanschette, ohne Hand, und bestehen aus zwei Teilen – `arm_cuff` (Manschette, starr an der Waffe) und `arm_forearm` (Unterarm, vom Spiel zur Schulter gedreht; `build_cuff_k` und `build_forearm_k` in `tools/models/arm.py`). Die sechs Dateien kommen aus der Übergabe des Designers (Commit `61a6a43`) und aus `pistol.py` und `blade.py` byte-gleich heraus (SHA-256 vor und nach dem Lauf verglichen). Neu ist das Hilfsmodul `tools/models/simulation.py` (Waffenhaltungen in Python, Prüfrechnung des Designers); es erzeugt keine Dateien.

Am 2026-11-17 geprüft: Die Fledermaus (`bat.glb`, `bat.png`) kommt aus `tools/models/bat.py` byte-gleich zur eingecheckten Fassung heraus (SHA-256 vor und nach dem Lauf verglichen).

Am 2026-11-17 geprüft: Gorm (`gorm.glb`, `gorm_body.png`, `gorm_body_phase2.png`, `gorm_limbs.png`) kommt aus `tools/models/gorm.py` byte-gleich zur eingecheckten Fassung heraus (SHA-256 vor und nach dem Lauf verglichen).

Am 2026-11-17 geprüft: Die sechs Varianten (`catacomb_*_b.png`, `catacomb_*_c.png`) kommen aus `tools/textures/catacomb_variants.py` byte-gleich zur Übergabe des Designers heraus (Commit `a54272f`, SHA-256 vor und nach dem Lauf verglichen).

Am 2026-11-17 übernommen (Issue #267): Die zehn Klangdateien und die Skripte unter `tools/audio/` kommen aus der Übergabe des Audiodesigners (Commit `5c9bf66`), byte-gleich. Laut Übergabe ergeben zwei Läufe von `erzeugen.py` byte-gleiche Dateien (dort mit SHA-256 geprüft); in diesem Schritt nicht erneut erzeugt.

Am 2026-11-17 übernommen (Issue #271): `musik-hauptmenue.ogg` und `musik-katakomben.ogg` sowie `musik_erzeugen.py`, `musik_klang.py`, `musik_pruefen.py` und die angepasste `pruefen.py` kommen aus der Übergabe des Audiodesigners, byte-gleich: `musik-katakomben.ogg`, `musik_klang.py`, `musik_pruefen.py` und `pruefen.py` aus Commit `fb2aee3`, `musik-hauptmenue.ogg` (Fassung ohne Herzschlag) und `musik_erzeugen.py` aus Commit `77eac53`. Ein erneuter Lauf von `musik_erzeugen.py` ergab byte-gleiche Dateien (SHA-256 verglichen).

Am 2026-11-17 übernommen (Issue #245): Die Türen kommen aus den Probestücken des Grafikdesigners (Commit `412096d`): Normal, Locked und Boss aus Runde 2 (`design/proben/grafik/tueren2/door2.py`, Bosstür Entwurf A, im Spiel `door_boss`), die Risswand aus Runde 1 (`design/proben/grafik/tueren/door.py`). `tools/models/door.py` fasst beide zusammen; Entwurf B (Fallgitter) ist verworfen und fehlt. Alle Bilder und `door_normal.glb`, `door_locked.glb`, `door_cracked.glb` kommen byte-gleich zur Übergabe heraus (SHA-256 verglichen). `door_boss.glb` ist 4 Byte kürzer: Nur der Name der Textur heißt `door_boss` statt `door_boss_a`; Geometrie und Binärteil sind byte-gleich.

Am 2026-11-17 übernommen (Issue #246): Die rissige Wand (`cracked_wall_a/b/c`) kommt aus dem Probestück des Grafikdesigners (Commit `2769789`, `design/proben/grafik/risswand/risswand.py`, Stärke „deutlich“). `tools/models/cracked_wall.py` erzeugt nur noch diese Stärke und teilt Platte und Brocken in Vorder- und Rückseite (`panel_front`, `panel_back`, `debris_front`, `debris_back`), weil beide Flächen der Zelle verschiedene Varianten zeigen können; Geometrie und Zufallszahlen sind unverändert. Die drei Bilder kommen byte-gleich zur Übergabe heraus (SHA-256 verglichen), ein zweiter Lauf ergibt byte-gleiche Modelle und Bilder. Die Texturen sind aus `textures/catacomb_wall*.png` gemalt: Ändert sich eine Wand-Textur, muss `cracked_wall.py` neu laufen. Das prüft ein Test (`tools/models/cracked_wall_sources.sha256` gegen `textures/textures.sha256`).

Am 2026-11-17 übernommen (Issue #303): Die Steinrahmen der Holz-, Eisen- und Bosstür kommen aus dem Probestück des Grafikdesigners (Commit `39886aa`, `design/proben/grafik/tuerrahmen/`, Bosstor in der hellen „blauen“ Fassung). Abweichung von der Probe: Sie legte Rahmen und Tür in eine Datei; mit drei Wand-Spielarten je Seite reichten dafür die 32 Farben eines Atlas nicht. Deshalb steht der Stein in eigenen Dateien `door_frame_*` (Wand-Kachel und Werkstein-Kachel, 27–28 Farben), `door_normal/locked/boss` behalten Flügel, Schloss und die übrigen Teile des Portals. Flügel, Schloss und Drehpunkte von Holz- und Eisentür sind unverändert (Knoten und Grenzen verglichen); die Flügel des Bosstors sind dem engeren Stufenportal der Probe angepasst (Drehpunkt ±0,49 statt ±0,525 m, Flügel 0,975 statt 1,04 m breit, 1,875 statt 1,88 m hoch); `door_cracked.glb` und `door_cracked.png` sind byte-gleich zum Stand vorher. Zwei Läufe ergeben byte-gleiche Dateien (SHA-256 verglichen); die Bilder der Rahmen von Holz- und Eisentür sind byte-gleich (gewollt). Die Wand-Hälfte jedes Atlas ist byte-gleich zur Wand-Textur. Ändert sich eine Wand-Textur, muss `door.py` neu laufen; das prüft ein Test (`tools/models/door_frame_sources.sha256` gegen `textures/textures.sha256`).

Am 2026-11-17 übernommen (Issue #300): Die Wendeltreppe aus Stein (`stairs_spiral_a/b/c`) kommt aus dem Probestück des Grafikdesigners (Commit `6ea274a`, `design/proben/grafik/treppe/treppe.py`, Entwurf 1 „Wendeltreppe“; das Abgangsportal ist nicht übernommen). `tools/models/stairs.py` erzeugt nur noch diesen Entwurf; die Geometrie ist unverändert (Knoten, Eckenzahl und Grenzen gegen die Übergabe verglichen). Abweichung von der Probe: Sie las ihre Kacheln aus einem Probenordner (6 bis 8 Farben je Kachel); das Skript liest die Kacheln, die das Spiel zeichnet (`textures/catacomb_wall*.png` und `catacomb_floor*.png`, je um 24 Farben), damit Fugen und Platten im Raster weiterlaufen. Dadurch hat jeder Atlas 54 bis 58 Farben statt 23 bis 25; neue Farben kommen nicht hinzu (nur die beiden Kacheln, Werkstein und Messing, das prüft das Skript). Den Werkstein liest es aus der Reihe der Hauptpalette. Zwei Läufe ergeben byte-gleiche Dateien (SHA-256 verglichen). Ändert sich eine Wand- oder Boden-Textur, muss `stairs.py` neu laufen; das prüft ein Test (`tools/models/stairs_sources.sha256` gegen `textures/textures.sha256`).

Am 2026-11-17 übernommen (Issue #279): Die Hauptpalette (Fassung A, Runde 3) kommt aus den Probestücken des Grafikdesigners (Commit `289d86e`, `design/proben/grafik/palette2/paletten2.py` und `palette3/stein.py`); `tools/palette/hauptpalette.py` fasst beide bereinigt zusammen. Die Reihen sind wertgleich zu `palette3/paletten/palette_stein.json` (verglichen), zwei Läufe ergeben byte-gleiche Dateien (SHA-256 verglichen), `--ableiten` rechnet dieselbe Stein-Reihe aus den neun Texturen nach. Keine Textur und kein Modell geändert.

Am 2026-11-17 übernommen (Issue #290): Die 34 Dateien `p1-*.wav` und die Skripte unter `tools/audio/paket1/` kommen aus der Übergabe des Audiodesigners (Commit `2a87efb`), die Klangdateien byte-gleich (SHA-256 gegen die Liste der Übergabe verglichen). In `paket1.py` und `bauen.py` sind nur die Pfade auf den neuen Ort angepasst; in diesem Schritt nicht neu erzeugt. `p1-schritt-l3.wav` und `p1-schritt-r2.wav` waren byte-gleich (seit Paket 1.1 nicht mehr).

Am 2026-11-17 übernommen (Issue #291, Paket 1.1): `p1-ghul-ankuendigung.wav` (Fassung A, tiefer), `p1-schmerz.wav` (tiefer, kürzer) und `p1-schritt-l3.wav` (eigene Tonhöhe G2) ersetzt, `p1-ghul-entdecken-2.wav` und `p1-ghul-sterben-2.wav` neu (Quelle `stimme2`). Alle fünf kommen aus der Übergabe des Audiodesigners (Commit `db7c912`), byte-gleich (SHA-256 verglichen); `paket1.py` und die Baupläne in `skripte/` sind auf Paket 1.1 nachgezogen, die Pfade auf den neuen Ort angepasst; in diesem Schritt nicht neu erzeugt. Fassung B der Ankündigung (`-leise`) und die Dateien für Fledermaus und Gorm sind nicht übernommen.

Am 2026-11-17 übernommen (Issue #297): die neun Dateien `p1-fledermaus-*.wav` aus derselben Übergabe (Commit `db7c912`), byte-gleich (SHA-256 gegen `audio-paket-1.sha256` der Übergabe verglichen); der Abschnitt Fledermaus in `paket1.py` und seine Baupläne in `skripte/` sind übernommen; in diesem Schritt nicht neu erzeugt.

Am 2026-11-17 übernommen (Issue #298): die 14 Dateien `p1-gorm-*.wav` aus derselben Übergabe (Commit `db7c912`), byte-gleich (SHA-256 gegen `audio-paket-1.sha256` der Übergabe verglichen); der Abschnitt Gorm in `paket1.py` mit den Pegeln `boss` und `boss_schritt` und seine Baupläne in `skripte/` sind übernommen; in diesem Schritt nicht neu erzeugt.

Am 2026-11-17 übernommen (Issue #299): Die Schlagwelle von Gorms Hammer kommt aus dem Probestück des Grafikdesigners (Commit `222f95f`, `design/proben/grafik/schlagwelle/welle.py`). Der Mensch hat Fassung A gewählt, den Ring aus aufbrechenden Steinplatten mit schmalem rotem Riss; nur sie wird erzeugt, Fassung B (die Druckwelle) nicht. Geometrie, Farben und Zufallszahlen sind die der Probe. Abweichung: Der Stein kommt aus der Wand-Textur des Spiels (`textures/catacomb_wall.png`, Variante A) statt aus der Probe-Kachel. Zwei Läufe ergeben byte-gleiche Dateien (SHA-256 verglichen). Ändert sich die Wand-Textur, muss `shock_wave.py` neu laufen; anders als bei den Türrahmen prüft das noch kein Test.

Am 2026-11-18 geändert (Issue #314, Urteil des Menschen: kein Rot auf der Welle): Der rote Riss ist weg. Statt seiner liegt vorn an jeder Plattengruppe eine 0,16 m hohe Bruchkante in den hellsten Stufen der Reihe Stein (12 bis 14); ihre Vorderkante ist der äußerste Punkt und liegt auf dem Trefferradius. Außenseite und oberes Bruchende jeder Platte sind ebenso hell, dahinter liegt eine dunkle Furche (Stufen 1 bis 3). Benutzt werden nur Farben der Wandkachel und der Reihe Stein; das prüft der Erzeuger selbst. Werkstein bleibt den Portalen vorbehalten (Art Bible, Farben, Regel 6). Zwei Läufe ergeben byte-gleiche Dateien (SHA-256 verglichen).
