"""Vorher/Nachher-Blatt zu Issue #416 (Teil Wendeltreppe): die vier Fotoszenen mit der Treppe, links main, rechts der Branch.

Die Fotos entstehen mit dem Spiel (nur Debug, je Szene ein leerer Ordner, nie `--photo all`):
    dotnet run --project src\\SilverAndAsh.Game -- --photo stairs-4m --out <ordner>\\stairs-4m
Aufruf (Blender, aus dem Repository-Wurzelverzeichnis), ALT und NEU nennen die Ordner mit je einem Unterordner je Szene:
    set ALT=<ordner-main> & set NEU=<ordner-branch> & "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/wendeltreppe-416/blatt.py
Unter jedem Bildpaar steht, wie viele Pixel des 854 x 480 Bildes sich unterscheiden (jeder Kanalunterschied zaehlt).
"""

import os
import tempfile

import blf
import bpy
import imbuf
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ALT, NEU = os.environ["ALT"], os.environ["NEU"]
SCENES = [("stairs-4m", "Treppe aus 4 m, Lampe an"), ("stairs-10m", "aus 10 m"),
          ("showroom-stairs", "Schauraum, von unten nach oben"), ("course-boss-stairs", "Bossraum der Pruefstrecke")]
HEADER = "Wendeltreppe geschlossen (Issue #416): vorher 552 Dreiecke, 260 offene Kanten / nachher 306 Dreiecke, 0 offene Kanten"


def load(folder, scene):
    im = bpy.data.images.load(os.path.join(folder, scene, scene + ".png"), check_existing=False)
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
    tmp = os.path.join(tempfile.gettempdir(), "blatt416_txt.png")
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
    tw, th, gap, top, cap = 427, 240, 6, 34, 28
    sheet = np.full((top + len(SCENES) * (th + cap + gap), 2 * tw + gap, 3), 18.0)
    text(sheet, HEADER, 6, 6, 15)
    for r, (scene, title) in enumerate(SCENES):
        a, b = load(ALT, scene), load(NEU, scene)
        diff = int((np.abs(a - b).max(axis=2) > 0).sum())
        y = top + r * (th + cap + gap)
        sheet[y:y + th, :tw] = half(a)
        sheet[y:y + th, tw + gap:] = half(b)
        text(sheet, f"{scene}: {title} - vorher | nachher, {diff} von {a.shape[0] * a.shape[1]} Pixeln weichen ab", 6, y + th + 4, 14)
        print("PIXEL", scene, diff)
    out = os.path.join(HERE, "blatt-treppe.png")
    h, w = sheet.shape[:2]
    img = bpy.data.images.new("blatt", w, h)
    img.pixels = np.concatenate([np.clip(sheet[::-1], 0, 255) / 255.0, np.ones((h, w, 1))], axis=2).ravel()
    img.filepath_raw = out
    img.file_format = "PNG"
    img.save()
    print("Bild:", out)


main()
