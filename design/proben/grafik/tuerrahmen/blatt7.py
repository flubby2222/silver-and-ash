"""Blaetter der Tuerrahmen (Blender-Python wegen numpy): setzt die Spielfotos aus fotos/ zu Uebersichten zusammen.
Markierung ohne Text: roter Balken oben = heute (Modelle aus main), gruener Balken = neu (dieser Rahmen), blauer Balken = neu, hohe Fassung.

  blatt_normal.png, blatt_locked.png, blatt_boss.png   Spalten: heute zu | neu zu | heute offen | neu offen;
                                                       Zeilen: Wand a 4 m, Wand a 10 m, Wand b 4 m, Wand b 10 m
  blatt_boss_hell.png                                  Boss A, nur zur Ansicht: Spalten heute | neu (Rampe) | hell (Werkstein heller als die Rampe); Zeilen Wand a 4 m, 10 m, Wand b 4 m, 10 m
  blatt_nahaufnahme.png                                Spalten: heute Kaempfer | neu Kaempfer | heute Sockel | neu Sockel; Zeilen Normal, Locked, Boss
  blatt_wand_c.png                                     Wand c, 4 m, Spalten heute | neu; Zeilen Normal, Locked, Boss
  blatt_seitlich.png                                   Tuer in einer Ost-West-Wand (um 90 Grad gedreht): Normal (Wand a) und Boss (Wand b), heute | neu
  blatt_raum.png                                       Boss A im 6-m-Raum von der Raumseite; Spalten heute | neu | neu mit hohem Relief; Zeilen 4 m, 6 m
Aufruf: blender --background --python blatt7.py
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
    for kind in ("normal", "locked", "boss"):
        rows = []
        for var in "ab":
            for dist in (4, 10):
                rows.append([(f"heute_{kind}_{var}_{dist}m_zu", RED), (f"neu_{kind}_{var}_{dist}m_zu", GREEN),
                             (f"heute_{kind}_{var}_{dist}m_offen", RED), (f"neu_{kind}_{var}_{dist}m_offen", GREEN)])
        sheet(rows, 0.5, os.path.join(HERE, f"blatt_{kind}.png"))
    rows = [[(f"heute_boss_{v}_{d}m_zu", RED), (f"neu_boss_{v}_{d}m_zu", GREEN), (f"hell_boss_{v}_{d}m_zu", BLUE)]
            for v in "ab" for d in (4, 10)]
    sheet(rows, 0.5, os.path.join(HERE, "blatt_boss_hell.png"))
    rows = [[(f"heute_{k}_a_nah_kaempfer", RED), (f"neu_{k}_a_nah_kaempfer", GREEN),
             (f"heute_{k}_a_nah_sockel", RED), (f"neu_{k}_a_nah_sockel", GREEN)] for k in ("normal", "locked", "boss")]
    sheet(rows, 0.5, os.path.join(HERE, "blatt_nahaufnahme.png"))
    rows = [[(f"heute_{k}_c_4m_zu", RED), (f"neu_{k}_c_4m_zu", GREEN)] for k in ("normal", "locked", "boss")]
    sheet(rows, 0.5, os.path.join(HERE, "blatt_wand_c.png"))
    rows = [[(f"heute_seitlich_normal_a_4m", RED), (f"neu_seitlich_normal_a_4m", GREEN),
             (f"heute_seitlich_boss_b_4m", RED), (f"neu_seitlich_boss_b_4m", GREEN)]]
    sheet(rows, 0.5, os.path.join(HERE, "blatt_seitlich.png"))
    rows = [[(f"raum_boss_a_{d}m_heute", RED), (f"raum_boss_a_{d}m_neu", GREEN), (f"raum_boss_a_{d}m_hoch", BLUE)] for d in (4, 6)]
    sheet(rows, 0.5, os.path.join(HERE, "blatt_raum.png"))


main()
