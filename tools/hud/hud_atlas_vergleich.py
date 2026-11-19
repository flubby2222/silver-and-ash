"""Vergleichsblaetter fuer den doppelt feinen HUD- und Menue-Atlas (Issue #402).

Aufruf (Blender dient als Python mit numpy und zum Lesen/Schreiben von PNG; nichts wird heruntergeladen):
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/hud/hud_atlas_vergleich.py -- <Ausgabeordner> <Spielfoto 854x480 ohne HUD> [<Atlas-Ordner>]

Spielfoto: das 3D-Bild des Foto-Skripts (z. B. hud-calm.png, ohne HUD). Atlas-Ordner: dort liegen hud/ und menu/ mit den neuen
Dateien (Standard: assets). Die heutigen Atlanten kommen immer aus assets/.
Das Zusammensetzen passiert hier, nicht im Spiel: Teile liegen an ihren layoutX/Y aus der Massetabelle (heute 854 x 480, neu 1708 x 960);
Schrift und Zahlen zeichnet das Spiel und fehlen hier. Fuer 1920 x 1080 werden beide Stufen hart (Nearest) auf Fenstergroesse gezogen.
Ausgabe: hud-854.png, hud-1920.png, menue-854.png, menue-1920.png, teile-neu.png.
"""
import sys, os, json, importlib.util
import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
ARGS = sys.argv[sys.argv.index("--") + 1:]
OUT, PHOTO = ARGS[0], ARGS[1]  # Ausgabeordner zuerst: hud_hochaufgeloest.py legt den ersten Wert nach "--" als Ordner an
NEWROOT = ARGS[2] if len(ARGS) > 2 else os.path.join(REPO, "assets")
os.makedirs(OUT, exist_ok=True)

_spec = importlib.util.spec_from_file_location("hud_hoch", os.path.join(HERE, "hud_hochaufgeloest.py"))
HHM = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(HHM)
HB = HHM.HB
c01 = HHM.c01


def load_rgba(path):
    im = bpy.data.images.load(path)
    im.colorspace_settings.name = "Non-Color"
    im.alpha_mode = "CHANNEL_PACKED"
    w, h = im.size
    a = np.empty(w * h * 4, np.float32)
    im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)[::-1].copy()


class Atlas:
    def __init__(self, png, js):
        self.img = load_rgba(png)
        self.info = json.load(open(js, encoding="utf-8"))
        self.parts = {p["name"]: p for p in self.info["parts"]}
        self.scale = self.info.get("scale", 1)

    def crop(self, name):
        p = self.parts[name]
        return self.img[p["y"]:p["y"] + p["h"], p["x"]:p["x"] + p["w"]]


def put(canvas, im, x, y, clip_w=None):
    """Teil (gerade RGBA) an Pixelposition (x, y) auflegen; clip_w: nur die linken Spalten."""
    h, w = im.shape[:2]
    if clip_w is not None:
        w = max(0, min(w, int(clip_w)))
        im = im[:, :w]
    xa, ya, xb, yb = max(x, 0), max(y, 0), min(x + w, canvas.shape[1]), min(y + h, canvas.shape[0])
    if xb <= xa or yb <= ya:
        return
    sub = im[ya - y:yb - y, xa - x:xb - x]
    a = sub[..., 3:4]
    canvas[ya:yb, xa:xb] = canvas[ya:yb, xa:xb] * (1 - a) + sub[..., :3] * a


def place(canvas, at, name, dx=0, dy=0, clip_frac=None):
    p = at.parts[name]
    im = at.crop(name)
    put(canvas, im, p["layoutX"] + dx, p["layoutY"] + dy, None if clip_frac is None else (p["w"] * clip_frac))


def nine(im, tw, th, sl):
    """Neun Stuecke: Ecken (sl px) 1:1, Kanten und Mitte gestreckt (Nearest)."""
    h, w = im.shape[:2]
    out = np.zeros((th, tw, 4), np.float32)
    xs = [0, sl, tw - sl, tw]; sx = [0, sl, w - sl, w]
    ys = [0, sl, th - sl, th]; sy = [0, sl, h - sl, h]
    for j in range(3):
        for i in range(3):
            src = im[sy[j]:sy[j + 1], sx[i]:sx[i + 1]]
            out[ys[j]:ys[j + 1], xs[i]:xs[i + 1]] = HB.nearest(src, ys[j + 1] - ys[j], xs[i + 1] - xs[i])
    return out


def three(im, tw, sl):
    h, w = im.shape[:2]
    out = np.zeros((h, tw, 4), np.float32)
    out[:, :sl] = im[:, :sl]
    out[:, tw - sl:] = im[:, w - sl:]
    out[:, sl:tw - sl] = HB.nearest(im[:, sl:w - sl], h, tw - 2 * sl)
    return out


def compose_hud(at, photo):
    s = at.scale
    c = HB.nearest(photo, 480 * s, 854 * s).copy()
    P = lambda n, **k: place(c, at, n, **k)  # noqa: E731
    for i in (0, 1):
        P(f"frame_item_{i}")
    P("icon_flask"); P("icon_eye")
    for i, g in enumerate(("blade", "pistol", "rifle", "blunder")):
        act = "_active" if i == 1 else ""
        P(f"frame_weapon_{i}{act}"); P(f"icon_{g}{act}")
    P("frame_vitals"); P("life_empty"); P("life_full", clip_frac=0.68)
    for i, f in enumerate((1.0, 1.0, 1.0, 0.4, 0.0)):
        P(f"stamina_{i}_empty")
        if f > 0:
            P(f"stamina_{i}_full", clip_frac=f)
    P("frame_level"); P("frame_xp"); P("xp_empty"); P("xp_full", clip_frac=0.45)
    for i, g in enumerate(("flame", "shield")):
        P(f"frame_skill_{i}"); P(f"icon_{g}")
    P("icon_skillpoint")
    P("frame_currency"); P("icon_coin"); P("icon_key")
    P("crosshair")
    return c


def compose_menu(at, photo):
    s = at.scale
    c = HB.nearest(photo * 0.42, 480 * s, 854 * s).copy()
    sh, sl = at.info["shapes"], at.info["slice"]
    # Tafel 240 x 250 Einheiten (Koerper) mit Rand der Teilgroesse
    pp = at.parts["panel"]
    mx = (pp["w"] - (sh["panel"][2] - sh["panel"][0])) // 2  # Rand um den Koerper im Teilbild
    bw, bh = 240 * s, 250 * s
    px, py = 307 * s, 164 * s
    put(c, nine(at.crop("panel"), bw + 2 * mx, bh + 2 * mx, sl["panel"]), px - mx, py - mx)
    gp = at.parts["gable"]
    put(c, at.crop("gable"), 427 * s + gp["layoutX"] - sh["gableAnchor"][0], py + gp["layoutY"] - sh["gableAnchor"][1])
    cp = at.parts["cursor"]
    mw, mh = cp["w"] - (sh["cursor"][2] - sh["cursor"][0]), cp["h"] - (sh["cursor"][3] - sh["cursor"][1])  # Rand je Teilbild
    tw = 212 * s + mw
    put(c, three(at.crop("cursor"), tw, sl["cursor"]), 427 * s - tw // 2, 190 * s - mh // 2)
    # Regler (Mitte x 427) mit Knopf bei 50 %
    sp = at.parts["slider_empty"]
    ox, oy = 427 * s - sp["w"] // 2 - sp["layoutX"], 350 * s - sp["layoutY"]
    place(c, at, "slider_empty", ox, oy); place(c, at, "slider_full", ox, oy, clip_frac=0.5)
    place(c, at, "knob", ox, oy)
    place(c, at, "tab_map_active"); place(c, at, "tab_equipment")
    return c


def sheet(rows, name, W):
    """rows: [(Beschriftung, Bild)]; Bilder zentriert auf Breite W."""
    gap, lab = 24, 40
    H = sum(r[1].shape[0] + lab + gap for r in rows) + 24
    img = np.zeros((H, W, 3), np.float32)
    img[:] = c01((34, 31, 42))
    cv = HHM.Canvas(img, True, 2.25)
    y = 24
    for t, im in rows:
        cv.text(t, 16 / 2.25, (y + 24) / 2.25, 7.0, HHM.BONE)
        y += lab
        h, w = im.shape[:2]
        cv.img[y:y + h, (W - w) // 2:(W - w) // 2 + w] = im[..., :3]
        y += h + gap
    HB.save_png(os.path.join(OUT, name), cv.img)


def zoom(img, x0, y0, x1, y1, f):
    return np.repeat(np.repeat(img[y0:y1, x0:x1, :3], f, 0), f, 1)


def main():
    photo = HB.load_png(PHOTO)
    old_h = Atlas(os.path.join(REPO, "assets", "hud", "hud_parts.png"), os.path.join(REPO, "assets", "hud", "hud_parts.json"))
    new_h = Atlas(os.path.join(NEWROOT, "hud", "hud_parts_x2.png"), os.path.join(NEWROOT, "hud", "hud_parts_x2.json"))
    old_m = Atlas(os.path.join(REPO, "assets", "menu", "menu_parts.png"), os.path.join(REPO, "assets", "menu", "menu_parts.json"))
    new_m = Atlas(os.path.join(NEWROOT, "menu", "menu_parts_x2.png"), os.path.join(NEWROOT, "menu", "menu_parts_x2.json"))
    for tag, (oa, na, comp) in (("hud", (old_h, new_h, compose_hud)), ("menue", (old_m, new_m, compose_menu))):
        o, n = comp(oa, photo), comp(na, photo)
        up = lambda a, W, H: HB.nearest(a, H, W)  # noqa: E731
        rows = [("TODAY: 854 x 480 ATLAS, SHOWN AT 1708 x 960 (EACH PIXEL DOUBLED)", up(o, 1708, 960)),
                ("NEW: DOUBLE ATLAS AT 1708 x 960 (1:1)", n)]
        if tag == "hud":  # gleicher Bildschirmausschnitt: heute 4-fach, neu 2-fach
            zo = zoom(o, 686, 436, 854, 480, 4)
            zn = zoom(n, 686 * 2, 436 * 2, 854 * 2, 480 * 2, 2)
            rows.append(("DETAIL, SAME SCREEN SIZE: LEFT TODAY 4X, RIGHT NEW 2X", np.concatenate([zo, np.zeros((zo.shape[0], 28, 3), np.float32), zn], 1)))
        sheet(rows, f"{tag}-854.png", 1708)
        sheet([("TODAY: 854 x 480 ATLAS ON A 1920 x 1080 WINDOW (NEAREST, UNEVEN PIXELS 2.25X)", up(o, 1920, 1080)),
               ("NEW: DOUBLE ATLAS ON A 1920 x 1080 WINDOW (NEAREST, 1.124X)", up(n, 1920, 1080))], f"{tag}-1920.png", 1920)
    # Uebersicht des neuen HUD-Atlas auf dunklem Grund
    for nm, at in (("hud", new_h), ("menu", new_m)):
        a = at.img
        bg = np.zeros(a.shape[:2] + (3,), np.float32); bg[:] = c01((52, 48, 64))
        bg = bg * (1 - a[..., 3:4]) + a[..., :3] * a[..., 3:4]
        HB.save_png(os.path.join(OUT, f"teile-neu-{nm}.png"), bg)


main()
