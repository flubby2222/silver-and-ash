"""Blaetter zu Issue #765, Schritt 2: die gebaute Wendeltreppe (Entwurf b, tools/models/stairs.py) im Spiel.

blatt-vorher-nachher.png: je Szene links die Treppe von main (552 Dreiecke, Stufenbloecke), rechts die neue (1228 Dreiecke).
blatt-varianten.png: die drei Steinvarianten a, b, c aus 4 m und im Schauraum (jede Variante in alle drei Dateien gelegt).
Die Fotos entstehen mit dem Spiel (Foto-Modus, Debug, 854 x 480, je Szene ein leerer Ordner); die Treppen von main stammen aus
einer Kopie des Debug-Ordners mit den Dateien von origin/main. Aufruf (Blender): FOTOS=<ordner> mit Unterordnern
vorher-<szene>, nachher-<szene>, vara|varb|varc-<szene>:
    set FOTOS=<ordner> & "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/wendeltreppe-neu/blatt-nachher.py
"""

import os
import tempfile

import blf
import bpy
import imbuf
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
FOTOS = os.environ["FOTOS"]


def load(pattern, scene):
    p = os.path.join(FOTOS, pattern.format(scene), scene + ".png")
    im = bpy.data.images.load(p, check_existing=False)
    w, h = im.size
    a = np.array(im.pixels[:], np.float32).reshape(h, w, 4)[::-1, :, :3] * 255.0
    bpy.data.images.remove(im)
    return a


def half(a):
    return (a[0::2, 0::2] + a[1::2, 0::2] + a[0::2, 1::2] + a[1::2, 1::2]) / 4


def text(img, s, x, y, size=16):
    f = blf.load("C:/Windows/Fonts/arial.ttf")
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    W, H = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((W, H))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "blatt765_txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp, check_existing=False)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(H, W, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + H), min(ww, x + W)
    m = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(m, 235.0, img[y:ye, x:xe])


def sheet(title, rows, cols, out):
    tw, th, gap, top, cap = 427, 240, 6, 30, 22
    sh = np.full((top + len(rows) * (th + cap + gap), len(cols) * (tw + gap) - gap, 3), 18.0)
    text(sh, title, 6, 6, 15)
    for r, scene in enumerate(rows):
        y = top + r * (th + cap + gap)
        for c, (pattern, label) in enumerate(cols):
            sh[y:y + th, c * (tw + gap):c * (tw + gap) + tw] = half(load(pattern, scene))
            text(sh, f"{label} - {scene}", c * (tw + gap) + 6, y + th + 3, 14)
    h, w = sh.shape[:2]
    img = bpy.data.images.new(os.path.basename(out), w, h)
    img.pixels = np.concatenate([np.clip(sh[::-1], 0, 255) / 255.0, np.ones((h, w, 1))], axis=2).ravel()
    img.filepath_raw = out
    img.file_format = "PNG"
    img.save()
    print("Bild:", out)


def main():
    sheet("Wendeltreppe (Issue #765): heute (main) | neu (Entwurf b, 1228 Dreiecke)",
          ["stairs-4m", "stairs-10m", "showroom-stairs", "course-boss-stairs"],
          [("vorher-{}", "main"), ("nachher-{}", "neu")], os.path.join(HERE, "blatt-vorher-nachher.png"))
    sheet("Wendeltreppe neu, die drei Steinvarianten a | b | c",
          ["stairs-4m", "showroom-stairs"],
          [("vara-{}", "Variante a"), ("varb-{}", "Variante b"), ("varc-{}", "Variante c")], os.path.join(HERE, "blatt-varianten.png"))


main()
