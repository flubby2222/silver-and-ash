"""Effekt-Probestueck Fackel, Runde 2 (Issue #353).

Urteil zu Runde 1: Flamme aus A (gemaltes Daumenkino) mit dem unruhigen Licht aus B, dessen Mitte wandert.
Hier zeigen alle drei Varianten genau das; es aendert sich nur die Staerke des Flackerns:

    A  ruhig   - Helligkeit +-15 %, Mitte wandert bis 2,1 / 1,6 cm
    B  mittel  - Helligkeit +-25 %, Mitte wandert bis 3,5 / 2,7 cm
    C  stark   - Helligkeit +-32 % wie B aus Runde 1, Mitte wandert bis 4,5 / 3,5 cm

Flamme, Funken (4, einheitlich), Szene, Kamera und Rauschfolge sind bei allen gleich. Szene, Zeichnen, Koernung
und die PNG- und GIF-Kodierer kommen aus ../fackel.py. Aufruf aus dem Repository-Wurzelverzeichnis:

    "<Blender-Ordner>\\5.3\\python\\bin\\python.exe" design/proben/effekte/fackel/runde2/fackel_runde2.py
"""

import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import fackel as F  # noqa: E402

AMPLITUDES = {"A": 0.15, "B": 0.25, "C": 0.32}
JITTER_AT_032 = np.array([0.045, 0.035])          # wandernde Mitte bei +-32 % (wie B in Runde 1)
SPARKS = 4

F.LABELS = {
    "A": "A  RUHIG: FLACKERN +-15 PROZENT, MITTE WANDERT WENIG.  FLAMME DAUMENKINO, 4 FUNKEN",
    "B": "B  MITTEL: FLACKERN +-25 PROZENT, MITTE WANDERT MITTEL.  FLAMME DAUMENKINO, 4 FUNKEN",
    "C": "C  STARK: FLACKERN +-32 PROZENT (WIE B AUS RUNDE 1), MITTE WANDERT WEIT.  FLAMME DAUMENKINO, 4 FUNKEN",
}


def variant(scene, t, flip, amplitude):
    n_i, n_x, n_y = F.B_NOISE
    intensity = 1.0 + amplitude * F.sample_loop(n_i, t)
    jx, jy = JITTER_AT_032 * (amplitude / 0.32)
    jitter = np.array([jx * F.sample_loop(n_x, t), jy * F.sample_loop(n_y, t), 0])
    cv = F.Canvas(scene, F.torch_light(scene, F.FLAME_BASE + np.array([0, 0.1, 0]) + jitter, intensity))
    cv.sprite(F.FLAME_BASE, flip[int(math.floor(t * 10)) % len(flip)], F.A_TEXEL, F.A_COLOURS)
    for k in range(SPARKS):                            # 4 Funken, je 0,6 s Lebenszeit, um 0,25 s versetzt
        a = ((t + k / SPARKS) % 1.0) / 0.6
        if a >= 1:
            continue
        side = (-1, 1, 0.4, -0.5)[k]
        pos = F.FLAME_BASE + np.array([side * (0.03 + 0.12 * a) + 0.02 * math.sin(7 * a + k),
                                       0.3 + 0.5 * a, 0.02 * side])
        cv.square(pos, 0.016, F.GOLD[7] if a < 0.6 else F.GOLD[5], min_px=3)
    return cv.img, intensity


def main():
    scene = F.build_scene()
    flip = F.flipbook_images()
    blocks = []
    for name, amp in AMPLITUDES.items():
        frames, intensities = [], []
        for i in range(F.FRAMES):
            img, inten = variant(scene, i / F.FPS, flip, amp)
            frames.append(F.quantize(img, scene["thr"]))
            intensities.append(inten)
        block = F.strip_block(name, frames, intensities)
        F.save_png(block, os.path.join(HERE, f"fackel-r2-{name}-streifen.png"))
        F.save_gif(frames, os.path.join(HERE, f"fackel-r2-{name}.gif"), 100 // F.FPS)
        blocks.append(block)
    title = np.tile(F.BG, (44, blocks[0].shape[1], 1))
    F.text(title, "FACKEL RUNDE 2 - FLAMME A, LICHT NACH B, NUR DIE STAERKE DES FLACKERNS AENDERT SICH.  "
                  "BALKEN = HELLIGKEIT", 10, F.GAP, 3, F.INK)
    sep = np.tile(F.BG, (12, blocks[0].shape[1], 1))
    sheet = np.concatenate([title] + [x for b in blocks for x in (b, sep)], 0)
    F.save_png(sheet, os.path.join(HERE, "fackel-r2-blatt.png"))


if __name__ == "__main__":
    main()
