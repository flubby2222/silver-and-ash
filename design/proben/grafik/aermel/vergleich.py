"""Alt und neu nebeneinander (Ausschnitte der Spielfotos, 2-fach vergroessert). Nur Probebilder."""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "..", "..", "tools", "models"))
from psx import load_png, save_png  # noqa: E402


def crop(path, x0, x1, y0, y1, k):
    a = np.asarray(load_png(path), float)
    h = a.shape[0]
    c = a[h - y1:h - y0, x0:x1]                    # load_png: von unten nach oben
    return np.repeat(np.repeat(c, k, 0), k, 1)


def pair(scene, box, name, k=2):
    a = crop(os.path.join(HERE, "alt", scene + ".png"), *box, k)
    b = crop(os.path.join(HERE, "neu", scene + ".png"), *box, k)
    gap = np.zeros((a.shape[0], 8, 3))
    save_png(np.concatenate([a, gap, b], axis=1), os.path.join(HERE, name))


pair("pistol-recoil", (440, 800, 190, 480), "vergleich_pistole.png")
pair("blade-charged", (0, 330, 200, 480), "vergleich_klinge.png")
