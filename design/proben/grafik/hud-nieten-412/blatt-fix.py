"""Blatt fuer die Nachbesserung zu Issue #412 (Nieten am Lebensbalken beim Kuerzen der Fassung).
Aufruf: blender --background --python blatt-fix.py -- <ausgabe.png>
Je Foto (Maximalleben 235 und Leben 160, 854 x 480 und 1920 x 1080) oben der Fehler (fotos-bug), unten die Loesung (fotos-fix);
Ausschnitt oben links, Punktvergroesserung ohne Glaettung."""
import sys, os
import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[sys.argv.index("--") + 1]


def load(p):
    im = bpy.data.images.load(p)
    w, h = im.size
    return np.array(im.pixels[:], np.float32).reshape(h, w, 4)[::-1]


def crop(img, x0, y0, x1, y1, k):
    return np.repeat(np.repeat(img[y0:y1, x0:x1], k, 0), k, 1)


# (Ordner, Szene, x0, y0, x1, y1, Faktor)
AUS = [("854x480", "tmp-life-235", 0, 0, 300, 52, 4), ("854x480", "tmp-life-160", 0, 0, 300, 52, 4),
       ("1920x1080", "tmp-life-235", 0, 0, 700, 100, 2), ("1920x1080", "tmp-life-160", 0, 0, 700, 100, 2)]
parts = []
for ordner, szene, x0, y0, x1, y1, k in AUS:
    for kind in ("fotos-bug", "fotos-fix"):
        parts.append(crop(load(f"{HERE}/{kind}/{ordner}/{szene}-screen.png"), x0, y0, x1, y1, k))
W = max(p.shape[1] for p in parts) + 20
rows = []
for p in parts:
    pad = np.full((p.shape[0], W, 4), 0.08, np.float32)
    pad[:, :, 3] = 1
    pad[:, 10:10 + p.shape[1]] = p
    rows.append(pad)
    rows.append(np.full((10, W, 4), [0.6, 0.2, 0.2, 1], np.float32))
sheet = np.concatenate(rows, 0)[::-1]
out = bpy.data.images.new("blatt", sheet.shape[1], sheet.shape[0], alpha=True)
out.pixels = sheet.ravel()
out.filepath_raw = OUT
out.file_format = "PNG"
out.save()
