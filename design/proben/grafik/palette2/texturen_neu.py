"""Malt Wand, Boden und Decke der Katakomben (alle Varianten a, b, c) mit den Rampen einer Palette NEU (Probestueck, Issue #279).

Lokale Kopie der Skripte tools/textures/catacomb.py und catacomb_variants.py: dieselbe Zeichnung (Steinlagen, Fugen, Flecken,
Feuchtigkeit, Risse, Pflaster) mit denselben Zufallszahlen; nur der letzte Schritt `finish` ist ersetzt: statt "24 freie Farben
per k-Means" wird die Helligkeit des gemalten Bildes in Stufen der Rampen gelegt. Die Skripte in tools/ bleiben unveraendert.

Warum so: Eine Palette hat Stufen mit ΔE von etwa 13; die Textur von heute schwankt in ihrer Helligkeit nur um etwa +-12 %, also
unter einer Stufe. Deshalb: Helligkeit des Bildes (Lage, Fugen, Flecken) in Stufen (ln der Helligkeit / ln des Stufenverhaeltnisses
mal Verstaerkung), der Farbton je Stein (die Zufallsfarbe des Steins) waehlt die kalte oder die warme Reihe.

Aufruf: blender --background --python texturen_neu.py -- <palette_key | alle>
Schreibt nach palette2/neu/<key>/textures/catacomb_{wall,floor,ceiling}[_b|_c].png
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
TOOLS = os.path.join(ROOT, "tools", "textures")
GAIN = 0.85                                     # Verstaerkung des Kontrasts in Stufen

ARG = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "alle"
KEYS = [f[len("palette_"):-5] for f in sorted(os.listdir(os.path.join(HERE, "paletten"))) if f.endswith(".json")] if ARG == "alle" else [ARG]

# --- Skripte laden (ohne ihre main-Aufrufe) -----------------------------------------------------------------------
src = open(os.path.join(TOOLS, "catacomb.py"), encoding="utf-8").read()
src = src.replace("\nmain()\n", "\n").replace("__file__", repr(os.path.join(TOOLS, "catacomb.py")))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
exec(compile(src, "catacomb.py", "exec"))

vsrc = open(os.path.join(TOOLS, "catacomb_variants.py"), encoding="utf-8").read()
a = vsrc.index("def paint_wall_b")
b = vsrc.index("def main3")
exec(compile(vsrc[a:b], "catacomb_variants.py", "exec"))

# --- Rampen --------------------------------------------------------------------------------------------------------
LAB_W = np.array([0.3, 0.59, 0.11])
CURRENT = {}


def load_rows(key):
    data = json.load(open(os.path.join(HERE, "paletten", f"palette_{key}.json"), encoding="utf-8"))
    return [np.array(r["farben"], float) for r in data["reihen"]]


def lum(c):
    return np.asarray(c, float) @ LAB_W


def kind_of(base):
    m = float(np.asarray(base, float).mean())
    refs = {"wall": float(np.mean(WORLD_WALL)), "floor": float(np.mean(WORLD_FLOOR)),
            "ceiling": float(np.mean(WORLD_CEILING)) * CEILING_DARKER}
    return min(refs, key=lambda k: abs(refs[k] - m))


def finish(img, base, seed):
    """Ersatz fuer catacomb.finish: Helligkeit in Stufen der Rampen der Palette."""
    small = img.reshape(SIZE, PAINT, SIZE, PAINT, 3).mean(axis=(1, 3))
    kind = kind_of(base)
    cold, warm = CURRENT["rows"][0], CURRENT["rows"][1]
    # Leiter: je Stufe die kalte und die warme Farbe; Mitte = wo die Leiter die mittlere Helligkeit der alten Flaeche trifft
    target = lum(base)
    ladder = np.array([lum(c) for c in cold])
    centre = float(np.interp(target, ladder, np.arange(8)))
    lo, hi = {"wall": (1, 6), "floor": (1, 5), "ceiling": (0, 3)}[kind]
    ratio = float(np.mean(np.log(ladder[2:7] / ladder[1:6])))          # mittlerer ln-Abstand der Stufen
    y = small @ LAB_W
    t = np.log(np.maximum(y, 1) / y.mean()) / ratio
    step = np.clip(np.round(centre + GAIN * t), lo, hi).astype(int)
    # einzelne Pixel, die sich von allen vier Nachbarn unterscheiden (Rauschen), den Nachbarn angleichen; Fugen (Linien) bleiben
    for _ in range(2):
        nb = [np.roll(step, sh, axis=ax) for ax in (0, 1) for sh in (1, -1)]
        alone = np.all([step != n for n in nb], axis=0)
        step = np.where(alone, np.median(np.stack(nb), axis=0).round().astype(int), step)
    # kalt oder warm: nach dem Rot-Blau-Unterschied des gemalten Bildes (je Stein und Feuchtigkeit anders)
    tint = small[..., 0] - small[..., 2]
    use_warm = tint > np.percentile(tint, 90) if kind != "ceiling" else np.zeros_like(tint, bool)
    out = np.where(use_warm[..., None], warm[step], cold[step])
    return out


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for key in KEYS:
        CURRENT["rows"] = load_rows(key)
        outdir = os.path.join(HERE, "neu", key, "textures")
        os.makedirs(outdir, exist_ok=True)
        jobs = (("catacomb_wall", paint_wall), ("catacomb_floor", paint_floor), ("catacomb_ceiling", paint_ceiling),
                ("catacomb_wall_b", paint_wall_b), ("catacomb_wall_c", paint_wall_c), ("catacomb_floor_b", paint_floor_b),
                ("catacomb_floor_c", paint_floor_c), ("catacomb_ceiling_b", paint_ceiling_b), ("catacomb_ceiling_c", paint_ceiling_c))
        for name, paint in jobs:
            arr = paint()
            save_image(name, arr, os.path.join(outdir, name + ".png"))
            print("TEX", key, name, "Farben", len({tuple(p) for p in arr.reshape(-1, 3).astype(int)}), "mittel", arr.mean(axis=(0, 1)).round(0))


main()
