"""Alt (oben) und neu (unten), Kistenreihe 2,5-fach vergroessert. Nur Probebilder."""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "..", "..", "tools", "models"))
from psx import load_png, save_png  # noqa: E402


def crop(path, x0, x1, y0, y1, k):
    a = np.asarray(load_png(path), float)
    h = a.shape[0]
    c = a[h - y1:h - y0, x0:x1]
    return np.repeat(np.repeat(c, k, 0), k, 1)


def pair(scene, box, name, k=2):
    a = crop(os.path.join(HERE, "alt", scene + ".png"), *box, k)
    b = crop(os.path.join(HERE, "neu", scene + ".png"), *box, k)
    gap = np.zeros((8, a.shape[1], 3))
    save_png(np.concatenate([b, gap, a], axis=0), os.path.join(HERE, name))     # load/save unten->oben: alt oben


pair("chests-4m", (0, 854, 165, 290), "vergleich_kisten_4m.png", 1)
pair("chests-4m", (0, 427, 165, 290), "vergleich_kisten_4m_links.png", 2)
pair("chests-4m", (427, 854, 165, 290), "vergleich_kisten_4m_rechts.png", 2)
