"""Blatt zu Issue #765: das heutige Modell und die drei Entwuerfe a, b, c im Spiel (Foto-Modus, 854 x 480, halbe Groesse).

Spalten: aus 4 m (stairs-4m), aus 10 m (stairs-10m), von unten nach oben (showroom-stairs), rechts ein Ausschnitt der 4-m-Aufnahme
in voller Aufloesung. Die Fotos entstehen mit dem Spiel (Debug, je Szene ein leerer Ordner, nie `--photo all`), wobei die
Probe-GLB nur in einer Kopie von main ausserhalb des Repositorys unter stairs_spiral_a/b/c.glb lag (mit angepasster models.sha256 dieser Kopie).
Aufruf (Blender): FOTOS=<ordner> mit Unterordnern main-<szene> (heute) und e<a|b|c>-<szene> (Entwuerfe):
    set FOTOS=<ordner> & "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/wendeltreppe-neu/blatt-entwuerfe.py
"""

import os
import tempfile

import blf
import bpy
import imbuf
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
FOTOS = os.environ["FOTOS"]
SCENES = ["stairs-4m", "stairs-10m", "showroom-stairs"]
ROWS = [("main-{}", "heute (main): Stufenbloecke, nicht geschlossen, 552 Dreiecke"),
        ("ea-{}", "Entwurf a: Platten, ohne Gelaender, 904 Dreiecke"),
        ("eb-{}", "Entwurf b: Platten + Eisengelaender, 1268 Dreiecke"),
        ("ec-{}", "Entwurf c: Platten + steinerne Wange mit Bruestung, 1116 Dreiecke")]
CROP = (330, 20, 570, 260)                                                          # x0, y0, x1, y1 der 4-m-Aufnahme


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


def main():
    tw, th, gap, top, cap = 427, 240, 6, 30, 26
    cw = CROP[2] - CROP[0]
    width = 3 * tw + cw + 3 * gap
    sheet = np.full((top + len(ROWS) * (th + cap + gap), width, 3), 18.0)
    text(sheet, "Wendeltreppe, Entwuerfe (Issue #765): aus 4 m | aus 10 m | von unten nach oben | Ausschnitt 4 m in voller Aufloesung", 6, 6, 15)
    for r, (pattern, label) in enumerate(ROWS):
        y = top + r * (th + cap + gap)
        for c, scene in enumerate(SCENES):
            a = load(pattern, scene)
            x = c * (tw + gap)
            sheet[y:y + th, x:x + tw] = half(a)
            if c == 0:
                x0, y0, x1, y1 = CROP
                sheet[y:y + th, 3 * (tw + gap):3 * (tw + gap) + cw] = a[y0:y1, x0:x1]
        text(sheet, label, 6, y + th + 4, 15)
    out = os.path.join(HERE, "blatt-entwuerfe.png")
    h, w = sheet.shape[:2]
    img = bpy.data.images.new("blatt", w, h)
    img.pixels = np.concatenate([np.clip(sheet[::-1], 0, 255) / 255.0, np.ones((h, w, 1))], axis=2).ravel()
    img.filepath_raw = out
    img.file_format = "PNG"
    img.save()
    print("Bild:", out)


main()
