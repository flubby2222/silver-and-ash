"""Vorher/Nachher-Blatt fuer Issue #412 (Nieten).
Aufruf: blender --background --python blatt.py -- <vorher-ordner> <nachher-ordner> <ausgabe.png>
Je Ausschnitt oben vorher, unten nachher, Punktvergroesserung ohne Glaettung."""
import sys
import bpy
import numpy as np

VOR, NACH, OUT = sys.argv[sys.argv.index("--") + 1:][:3]


def load(p):
    im = bpy.data.images.load(p)
    w, h = im.size
    d = np.array(im.pixels[:], np.float32).reshape(h, w, 4)[::-1]  # oben zuerst
    return d


def crop(img, x0, y0, x1, y1, k):
    c = img[y0:y1, x0:x1]
    return np.repeat(np.repeat(c, k, 0), k, 1)


# (Datei, x0, y0, x1, y1, Faktor)
AUS = [("hud-calm-screen.png", 0, 0, 360, 120, 3),
       ("map-screen.png", 440, 0, 1180, 140, 2),
       ("inventory-screen.png", 440, 0, 1180, 140, 2),
       ("hud-calm-screen.png", 0, 960, 400, 1080, 2),
       ("hud-calm-screen.png", 1440, 960, 1920, 1080, 2),
       ("hud-boss-screen.png", 560, 0, 1360, 120, 2)]
rows = []
for f, x0, y0, x1, y1, k in AUS:
    v = crop(load(f"{VOR}/{f}"), x0, y0, x1, y1, k)
    n = crop(load(f"{NACH}/{f}"), x0, y0, x1, y1, k)
    rows.append((v, n))
W = max(r[0].shape[1] for r in rows) + 20
parts = []
for v, n in rows:
    for im in (v, n):
        pad = np.full((im.shape[0], W, 4), 0.08, np.float32)
        pad[:, :, 3] = 1
        pad[:, 10:10 + im.shape[1]] = im
        parts.append(pad)
    parts.append(np.full((14, W, 4), [0.6, 0.2, 0.2, 1], np.float32))
sheet = np.concatenate(parts, 0)[::-1]
out = bpy.data.images.new("blatt", sheet.shape[1], sheet.shape[0], alpha=True)
out.pixels = sheet.ravel()
out.filepath_raw = OUT
out.file_format = "PNG"
out.save()
