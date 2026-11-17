"""Blaetter der Treppen (Blender-Python wegen numpy): setzt die Spielfotos aus fotos/ zusammen.
Balken oben: rot = heute (Kasten-Platzhalter), gruen = Entwurf 1 Wendeltreppe, blau = Entwurf 2 Abgangsportal. Spalten immer heute | Wendeltreppe | Abgangsportal.
  blatt_wand_a.png, blatt_wand_b.png  Zeilen 4 m, 10 m im 6-m-Raum, Wandvariante a bzw. b
  blatt_nah.png                       Zeilen: aus 2,6 m, von oben (1,9 m davor), von der Seite (5,5 m); Wand a
  blatt_wand_c_gang.png               Zeilen: Wand c 4 m (Raum 6 m), Wand a 4 m in einem Raum von nur 4,2 m Hoehe
Aufruf: blender --background --python blatt8.py
"""
import os

import numpy as np
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
FOTOS = os.path.join(HERE, "fotos")
RED, GREEN, BLUE = (205, 60, 60), (60, 170, 80), (70, 110, 210)


def load(name):
    im = bpy.data.images.load(os.path.join(FOTOS, name + ".png"))
    w, h = im.size
    a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, :3] * 255
    bpy.data.images.remove(im)
    return a


def save(arr, path):
    h, w = arr.shape[:2]
    im = bpy.data.images.new("m", w, h)
    im.pixels = np.concatenate([arr[::-1] / 255.0, np.ones((h, w, 1))], axis=2).astype(np.float32).ravel().tolist()
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()


def shrink(a, scale):
    h, w = a.shape[:2]
    nh, nw = int(h * scale), int(w * scale)
    return a[(np.arange(nh) / scale).astype(int).clip(0, h - 1)][:, (np.arange(nw) / scale).astype(int).clip(0, w - 1)]


def tile(name, colour, scale):
    a = shrink(load(name), scale)
    a[:5] = colour
    return a


def sheet(rows, scale, path, gap=6):
    """rows: Liste von Zeilen, jede eine Liste (Bildname, Farbe)."""
    ims = [[tile(n, c, scale) for n, c in row] for row in rows]
    h, w = ims[0][0].shape[:2]
    cols = max(len(r) for r in ims)
    out = np.full((len(ims) * (h + gap) - gap, cols * (w + gap) - gap, 3), 24.0)
    for i, row in enumerate(ims):
        for j, im in enumerate(row):
            out[i * (h + gap):i * (h + gap) + h, j * (w + gap):j * (w + gap) + w] = im
    save(out, path)


def main():
    def row(pattern):
        return [(pattern.format(d=d), c) for d, c in (("heute", RED), ("spiral", GREEN), ("abgang", BLUE))]
    sheet([row("{d}_a_4m"), row("{d}_a_10m")], 0.5, os.path.join(HERE, "blatt_wand_a.png"))
    sheet([row("{d}_b_4m"), row("{d}_b_10m")], 0.5, os.path.join(HERE, "blatt_wand_b.png"))
    sheet([row("{d}_a_nah"), row("{d}_a_oben"), row("{d}_a_seite")], 0.5, os.path.join(HERE, "blatt_nah.png"))
    sheet([row("{d}_c_4m"), row("{d}_gang_4m")], 0.5, os.path.join(HERE, "blatt_wand_c_gang.png"))


main()
