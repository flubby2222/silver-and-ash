# Herkunft der Assets

Alles unter `assets/` ist per Skript erzeugt, nichts heruntergeladen (`CLAUDE.md`, ADR 0009). Jede Bild-, Modell- oder Klangdatei steht hier mit dem Skript, das sie erzeugt, und dem Datum ihres Stands. Wer eine Datei neu erzeugt oder ersetzt, trägt das neue Datum ein. Textdateien (Level, Räume, Shader, `balance.json`) sind von Hand geschrieben und stehen hier nicht.

| Datei | Skript | Datum |
|---|---|---|
| `models/ghoul.glb`, `models/ghoul.png` | `tools/models/ghoul.py` (mit `tools/models/psx.py`) | 2026-11-16 |
| `models/chest_beggar.glb`, `models/chest_guard.glb`, `models/chest_king.glb`, `models/chest_vampire.glb`, `models/chest_beggar.png`, `models/chest_guard.png`, `models/chest_king.png`, `models/chest_vampire.png` | `tools/models/chest.py` (mit `tools/models/psx.py`) | 2026-11-17 |
| `models/pistol.glb`, `models/pistol_hand.glb`, `models/pistol.png` | `tools/models/pistol.py` (mit `tools/models/arm.py` und `tools/models/psx.py`) | 2026-11-17 |
| `models/blade.glb`, `models/blade_hand.glb`, `models/blade.png` | `tools/models/blade.py` (mit `tools/models/arm.py` und `tools/models/psx.py`) | 2026-11-17 |
| `models/bat.glb`, `models/bat.png` | `tools/models/bat.py` (mit `tools/models/psx.py`) | 2026-11-17 |
| `models/gorm.glb`, `models/gorm_body.png`, `models/gorm_body_phase2.png`, `models/gorm_limbs.png` | `tools/models/gorm.py` (mit `tools/models/gorm_base.py`, `tools/models/gorm_heads.py` und `tools/models/psx.py`) | 2026-11-17 |
| `textures/catacomb_wall.png`, `textures/catacomb_floor.png`, `textures/catacomb_ceiling.png` | `tools/textures/catacomb.py` (mit `tools/models/psx.py`) | 2026-11-16 |
| `textures/catacomb_wall_b.png`, `textures/catacomb_wall_c.png`, `textures/catacomb_floor_b.png`, `textures/catacomb_floor_c.png`, `textures/catacomb_ceiling_b.png`, `textures/catacomb_ceiling_c.png` | `tools/textures/catacomb_variants.py` (mit `tools/textures/catacomb.py` und `tools/models/psx.py`) | 2026-11-17 |

## Neu erzeugen

Modelle mit Blender 5.3 ohne Oberfläche; das Skript schreibt nach `assets/models/`:

```
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/ghoul.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/chest.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/pistol.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/blade.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/bat.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/gorm.py
```

Level-Texturen ebenso; das Skript schreibt nach `assets/textures/`:

```
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/textures/catacomb.py
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/textures/catacomb_variants.py
```

**Danach immer die Prüfsummen-Listen `models/models.sha256` und `textures/textures.sha256` neu schreiben** (normales Python 3, ein Aufruf für beide). Ohne passenden Eintrag lädt das Spiel die Datei nicht und zeichnet den Platzhalter bzw. die Fläche einfarbig; ein Test wird rot:

```
python tools/models/checksums.py
```

Am 2026-11-16 geprüft: `ghoul.glb` und `ghoul.png` kommen byte-gleich zur eingecheckten Fassung heraus. Wie das Spiel die Modelle lädt, steht in [`docs/modelle.md`](../docs/modelle.md).

Am 2026-11-16 geprüft: `catacomb_wall.png`, `catacomb_floor.png` und `catacomb_ceiling.png` kommen byte-gleich zur eingecheckten Fassung heraus (SHA-256 vor und nach dem Lauf verglichen).

Am 2026-11-17 geprüft: Die vier Kisten (`chest_*.glb`, `chest_*.png`) kommen aus `tools/models/chest.py` byte-gleich zur eingecheckten Fassung heraus (SHA-256 vor und nach dem Lauf verglichen).

Am 2026-11-17 geprüft: Pistole und Klinge mit ihren Händen (`pistol*.glb`, `blade*.glb`, `pistol.png`, `blade.png`) kommen aus `tools/models/pistol.py` und `tools/models/blade.py` byte-gleich zur eingecheckten Fassung heraus (SHA-256 vor und nach dem Lauf verglichen). Gegenüber der Übergabe des Designers backen beide Skripte jetzt den Maßstab in die Ecken ein (`bake_scale` in `psx.py`); die `.glb`-Dateien unterscheiden sich deshalb von der Übergabe, das Bild nicht.

Am 2026-11-17 ersetzt (Issues #256 und #257): Pistole und Klinge tragen statt Handschuh einen langen Mantelärmel (`build_sleeve4` in `tools/models/arm.py`), der Hahn der Pistole ist kleiner und hat einen hellen Feuerstein mit eigenem Texturfeld. Die sechs Dateien kommen aus der Übergabe des Designers (Commit `6ebf63f`) und aus `pistol.py` und `blade.py` byte-gleich heraus (SHA-256 vor und nach dem Lauf verglichen). `arm.py` ist nur um neue Funktionen ergänzt, `psx.py` unverändert; die übrigen Modelle nutzen `arm.py` nicht.

Am 2026-11-17 geprüft: Die Fledermaus (`bat.glb`, `bat.png`) kommt aus `tools/models/bat.py` byte-gleich zur eingecheckten Fassung heraus (SHA-256 vor und nach dem Lauf verglichen).

Am 2026-11-17 geprüft: Gorm (`gorm.glb`, `gorm_body.png`, `gorm_body_phase2.png`, `gorm_limbs.png`) kommt aus `tools/models/gorm.py` byte-gleich zur eingecheckten Fassung heraus (SHA-256 vor und nach dem Lauf verglichen).

Am 2026-11-17 geprüft: Die sechs Varianten (`catacomb_*_b.png`, `catacomb_*_c.png`) kommen aus `tools/textures/catacomb_variants.py` byte-gleich zur Übergabe des Designers heraus (Commit `a54272f`, SHA-256 vor und nach dem Lauf verglichen).
