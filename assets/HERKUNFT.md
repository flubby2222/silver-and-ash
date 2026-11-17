# Herkunft der Assets

Alles unter `assets/` ist per Skript erzeugt, nichts heruntergeladen (`CLAUDE.md`, ADR 0009). Jede Bild-, Modell- oder Klangdatei steht hier mit dem Skript, das sie erzeugt, und dem Datum ihres Stands. Wer eine Datei neu erzeugt oder ersetzt, trägt das neue Datum ein. Textdateien (Level, Räume, Shader, `balance.json`) sind von Hand geschrieben und stehen hier nicht.

| Datei | Skript | Datum |
|---|---|---|
| `models/ghoul.glb`, `models/ghoul.png` | `tools/models/ghoul.py` (mit `tools/models/psx.py`) | 2026-11-16 |
| `models/chest_beggar.glb`, `models/chest_guard.glb`, `models/chest_king.glb`, `models/chest_vampire.glb` und je `.png` | `tools/models/chest.py` (mit `tools/models/psx.py`) | 2026-11-16 |
| `textures/catacomb_wall.png`, `textures/catacomb_floor.png`, `textures/catacomb_ceiling.png` | `tools/textures/catacomb.py` (mit `tools/models/psx.py`) | 2026-11-16 |

## Neu erzeugen

Modelle mit Blender 5.3 ohne Oberfläche; das Skript schreibt nach `assets/models/`:

```
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/ghoul.py
```

Level-Texturen ebenso; das Skript schreibt nach `assets/textures/`:

```
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/textures/catacomb.py
```

**Danach immer die Prüfsummen-Listen `models/models.sha256` und `textures/textures.sha256` neu schreiben** (normales Python 3, ein Aufruf für beide). Ohne passenden Eintrag lädt das Spiel die Datei nicht und zeichnet den Platzhalter bzw. die Fläche einfarbig; ein Test wird rot:

```
python tools/models/checksums.py
```

Am 2026-11-16 geprüft: `ghoul.glb` und `ghoul.png` kommen byte-gleich zur eingecheckten Fassung heraus. Wie das Spiel die Modelle lädt, steht in [`docs/modelle.md`](../docs/modelle.md).

Am 2026-11-16 geprüft: `catacomb_wall.png`, `catacomb_floor.png` und `catacomb_ceiling.png` kommen byte-gleich zur eingecheckten Fassung heraus (SHA-256 vor und nach dem Lauf verglichen).

Am 2026-11-17 geprüft: Die vier Kisten (`chest_*.glb`, `chest_*.png`) kommen aus `tools/models/chest.py` byte-gleich zur eingecheckten Fassung heraus (SHA-256 vor und nach dem Lauf verglichen).
