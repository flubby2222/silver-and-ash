"""Gemeinsame Bausteine fuer Probebilder und Blatt: Szenen rendern (einmal, danach zwischengespeichert), Anker
berechnen, Effekte auftragen, Bilder speichern und beschriften (Beschriftung nur auf dem Blatt, nicht im Spiel)."""

import os
import tempfile

import blf
import bpy
import imbuf
import numpy as np

import effekte as E
import szene_ghul as S

ORDER5 = ["marked", "holy_fire", "grave_chill", "stun", "leech"]
_cache = {}


def anchors_for(x):
    """Pixelanker eines Ghuls bei x (Spiel-Achsen), nach dem Ausrichten der Kamera."""
    pts = {"head_top": (x, 0.95, 0.33), "head": (x, 0.82, 0.33), "chest": (x, 0.47, 0.32), "ground": (x, 0.0, 0.0),
           "shoulderL": (x + 0.22, 0.70, 0.14), "shoulderR": (x - 0.22, 0.70, 0.14)}
    return {k: S.project(S.V(*v))[:2] for k, v in pts.items()}


def scene(kind, cam_z):
    """kind 'line' (fuenf Ghule nebeneinander) oder 'one' (ein Ghul in der Mitte). Rueckgabe: Bild, Masken, Anker, Abstand."""
    key = (kind, cam_z)
    if key in _cache:
        return _cache[key]
    S.start(room_half=4.0, back=-4.0)
    xs = [0.0] if kind == "one" else [2.2, 1.1, 0.0, -1.1, -2.2]
    for x in xs:
        S.add_ghoul(x, 0.0)
    img, masks = S.render(cam_z)
    anchors = []
    for x in xs:
        anchors.append(anchors_for(x))
    # links nach rechts: aufsteigende Bild-x
    order = np.argsort([a["chest"][0] for a in anchors])
    res = (img, [masks[i] for i in order], [anchors[i] for i in order], float(cam_z))
    _cache[key] = res
    return res


def frame(kind, cam_z, effects_per_ghoul, t=0.0, grain=True):
    """effects_per_ghoul: Liste je Ghul (von links) von Listen (effekt, variante). Gibt das fertige 854 x 480 Bild."""
    base, masks, anchors, d = scene(kind, cam_z)
    img = base.copy()
    for i, effs in enumerate(effects_per_ghoul):
        if effs:
            E.apply(img, masks[i], anchors[i], d, effs, t=t, seed=10 + i)
    return E.bayer15(img) if grain else img


def crop_around(img, anchor, d, w=150, h=130):
    """Ausschnitt um einen Ghul (Mitte etwas ueber der Brust), Zeilen von oben."""
    cx, cy = anchor["chest"]
    S_ = E.px_per_metre(d)
    cx, cy = int(cx), int(cy - 0.12 * S_)
    x0 = min(max(cx - w // 2, 0), img.shape[1] - w)
    y0 = min(max(cy - h // 2, 0), img.shape[0] - h)
    return img[y0:y0 + h, x0:x0 + w]


def up(a, f):
    return np.repeat(np.repeat(a, f, axis=0), f, axis=1)


def save(arr, path):
    """arr: Zeilen von oben, 0..255."""
    h, w = arr.shape[:2]
    im = bpy.data.images.new(os.path.basename(path), w, h)
    im.pixels = np.concatenate([np.clip(arr, 0, 255)[::-1] / 255.0, np.ones((h, w, 1))], axis=2).ravel()
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)
    print("Bild:", path)


_font = {}


def text(img, s, x, y, size=18, color=(235, 230, 240), bold=False):
    """Beschriftung des Blatts (Windows-Schrift Arial; nur Annotation, nicht Teil des Spiels)."""
    fn = "C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf"
    f = blf.load(fn)   # nicht zwischenspeichern: nach dem Szenen-Reset sind alte Schrift-IDs ungueltig
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    Wd, Hd = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((Wd, Hd))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "statuseffekte_txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(Hd, Wd, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + Hd), min(ww, x + Wd)
    if ye <= y or xe <= x:
        return
    mk = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(mk, np.array(color, float), img[y:ye, x:xe])
