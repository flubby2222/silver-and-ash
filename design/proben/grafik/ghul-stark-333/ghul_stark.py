"""Probestueck staerkerer Ghul (Issue #333): heutiger Ghul und drei Fassungen A/B/C, Probenblatt, Ansichten, Modelle.

Alles entsteht aus diesem Skript. Es nutzt die Bauteile von tools/models/ghoul.py (ohne dieses zu aendern: der Quelltext
wird eingelesen, der Aufruf von main() am Ende entfernt, der Rest ausgefuehrt) und verformt die fertigen Teile:
    H   heutiger Ghul (Gegenprobe: Textur gleicht assets/models/ghoul.png Pixel fuer Pixel)
    A   nur groesser (Faktor 1,3), Haut bleich (Palettenreihe Haut/Knochen, harte Stufen)
    B   wie A, dazu breitere Schultern, laengere Arme (nach vorn), Kopf 8 % kleiner gegenueber dem Rumpf
    C   wie B, dazu Buckel ueber den Schulterblaettern und ein Knochenkamm (neun Zacken) auf dem Ruecken
Nichts wird heruntergeladen. Das Spiel und assets/ werden nicht angefasst.

Aufruf (aus dem Repository-Wurzelverzeichnis; optional -- <Ausgabeordner> fuer einen zweiten Lauf):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/ghul-stark-333/ghul_stark.py
Ausgabe: blatt.png, ansichten-<H|A|B|C>.png, spiel-*.png, modelle/*.glb, texturen/*.png, masse.txt.
"""

import json
import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, G, FOG_COLOR, Part, rects_from_pixels, save_image, export_material, pass_material,  # noqa: E402
                 setup_render, camera, aim, render_passes, compose, save_png, load_png, write_light, stone_textures,
                 build_room, export_glb, to_game, pose, MATS)

OUT = HERE
if "--" in sys.argv:
    OUT = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
VARIANTS = ("H", "A", "B", "C")
LABEL = {"H": "HEUTE", "A": "A  größer + bleich", "B": "B  + Proportionen", "C": "C  + Buckel, Kamm"}
SCALE = {"H": 1.0, "A": 1.3, "B": 1.3, "C": 1.3}
GOLD = (214, 170, 64)

PAL = {r["name"]: np.array(r["farben"], float)
       for k in ("grund", "akzent")
       for r in json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))[k]}
HAUT, STOFF, BLUTROT = PAL["Haut/Knochen"], PAL["Stoff"], PAL["Blutrot"]
EYE_RED = BLUTROT[5]          # 200, 30, 40: mittlere Stufe der Ankuendigung (Urteil 22)

# --- Bauteile des heutigen Ghuls laden ---------------------------------------------------------------------------------

GHOUL_PATH = os.path.join(ROOT, "tools", "models", "ghoul.py")
_src = open(GHOUL_PATH, encoding="utf-8").read().rstrip()
assert _src.endswith("\nmain()"), "ghoul.py endet nicht mehr mit main()"
ns = {"__file__": GHOUL_PATH, "__name__": "ghoul_basis"}
exec(compile(_src[:-len("main()")], GHOUL_PATH, "exec"), ns)
TEX = ns["TEX"]
RECTS0, UV0 = dict(ns["RECTS"]), dict(ns["UV"])
# Fassung C braucht ein Feld fuer die Knochenzacken: Lumpen schrumpfen auf 16 x 16, daneben liegt "bone".
RECTS_C = dict(RECTS0)
RECTS_C["rags"] = (80, 0, 96, 16)
RECTS_C["bone"] = (96, 0, 112, 16)
UV_C = rects_from_pixels(RECTS_C, TEX)

NECK, HEAD_C = ns["NECK"], ns["HEAD_C"]
SHOULDER_L = V(0.19, 0.67, 0.16)

# --- Verformung der Teile ----------------------------------------------------------------------------------------------


def ss(x, a, b):
    t = min(max((x - a) / (b - a), 0.0), 1.0)
    return t * t * (3 - 2 * t)


class Shape:
    """Abbildung der Spiel-Achsen-Punkte des heutigen Ghuls auf die der Fassung (alle Teile stetig verformt)."""

    def __init__(self, variant):
        self.s = SCALE[variant]
        self.wide = variant in ("B", "C")
        self.hump = variant == "C"

    def torso(self, p):
        x, y, z = p
        if self.wide:      # Schulterguertel breiter, nur dort (nicht am Becken, nicht am Hals)
            w = ss(z, -0.04, 0.10) * (1 - ss(z, 0.17, 0.24)) * ss(y, 0.5, 0.62)
            x *= 1 + 0.25 * w
        if self.hump:      # Buckel ueber den Schulterblaettern
            y += 0.075 * math.exp(-(((z - 0.04) / 0.13) ** 2)) * ss(y, 0.50, 0.62)
        return V(x, y, z)

    def arm(self, p):
        """Arm um die (verformte) Schulter: nach vorn gestreckt, Handgelenk bleibt auf Hoehe 0,05."""
        sign = 1 if p.x >= 0 else -1
        sh = V(sign * SHOULDER_L.x, SHOULDER_L.y, SHOULDER_L.z)
        sh2 = self.torso(sh)
        sy = (sh2.y - 0.05) / (sh.y - 0.05)
        d = p - sh
        return V(sh2.x + d.x, sh2.y + d.y * sy, sh2.z + d.z * 1.22)

    def head(self, p):
        return self.torso(NECK) + (p - NECK) * 0.92

    def apply(self, name, p):
        if self.wide or self.hump:
            if name == "torso":
                p = self.torso(p)
            elif name.startswith(("upper_arm", "forearm")):
                p = self.arm(p)
            elif name in ("head", "jaw", "eye_l", "eye_r"):
                p = self.head(p)
        return p * self.s


def warp(part, shape):
    """Alle Ecken und den Drehpunkt des Teils durch die Verformung schicken (Spiel-Achsen, absolut)."""
    newpiv = shape.apply(part.name, part.pivot)
    for v in part.bm.verts:
        v.co = G(shape.apply(part.name, part.pivot + to_game(v.co)) - newpiv)
    part.pivot = newpiv


# --- Zusaetze ----------------------------------------------------------------------------------------------------------

SPINE = [(-0.23, 0.551), (-0.12, 0.611), (-0.02, 0.714), (0.07, 0.764), (0.15, 0.761)]   # (z, y) oben auf dem Grat


def add_crest(torso):
    """Knochenkamm: neun fuenfseitige Zacken (geschlossene Pyramiden, Fuss im Rumpf versenkt) auf dem Rueckengrat."""
    n = 9
    heights = (0.07, 0.09, 0.12, 0.15, 0.17, 0.16, 0.14, 0.11, 0.08)
    for i in range(n):
        f = i / (n - 1) * (len(SPINE) - 1)
        k = min(int(f), len(SPINE) - 2)
        a, b = SPINE[k], SPINE[k + 1]
        t = f - k
        z, y = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
        h = heights[i]
        torso.spike((0, y - 0.03, z), (0, y + h, z - 0.35 * h), 0.032, "bone", sides=5)


def eye_part(sign):
    """Rotes Augenkaestchen (nur fuer die Ankuendigung im Bild, nicht im Modell): Lage wie GhoulViews.cs."""
    tilt, roll = math.radians(14), math.radians(10)
    up = V(math.sin(roll), math.cos(roll) * math.cos(tilt), -math.sin(tilt) * math.cos(roll)).normalized()
    fwd = V(0, math.sin(tilt), math.cos(tilt))
    fwd = (fwd - up * fwd.dot(up)).normalized()
    side = up.cross(fwd).normalized()
    c = HEAD_C + up * -0.005 + side * (0.032 * sign) + fwd * 0.052
    hw, hh, hd = 0.024, 0.018, 0.012
    p = Part("eye_l" if sign < 0 else "eye_r", NECK, {"eye": (0.1, 0.1, 0.9, 0.9)}, parent="head")
    corners = {(a, b, d): c + side * (a * hw / 2) + up * (b * hh / 2) + fwd * (d * hd / 2)
               for a in (-1, 1) for b in (-1, 1) for d in (-1, 1)}
    quads = [[(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1)], [(-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)],
             [(-1, -1, -1), (-1, 1, -1), (-1, 1, 1), (-1, -1, 1)], [(1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1)],
             [(-1, -1, -1), (1, -1, -1), (1, -1, 1), (-1, -1, 1)], [(-1, 1, -1), (1, 1, -1), (1, 1, 1), (-1, 1, 1)]]
    p.convex([[corners[q] for q in quad] for quad in quads], "eye")
    return p


def build_parts(variant, with_eyes=False, prefix=""):
    ns["UV"] = UV_C if variant == "C" else UV0
    torso, head, jaw = ns["build_torso"](), ns["build_head"](), ns["build_jaw"]()
    if variant == "C":
        add_crest(torso)
    parts = [torso, head, jaw] + ns["build_arm"](1, 0.0) + ns["build_arm"](-1, 0.05) + ns["build_leg"](1) + ns["build_leg"](-1)
    if with_eyes:
        parts += [eye_part(-1), eye_part(1)]
    shape = Shape(variant)
    for p in parts:
        warp(p, shape)
        p.name = prefix + p.name
        p.parent = (prefix + p.parent) if p.parent else p.parent
    return parts


# --- Textur: Haut/Knochen-Reihe der Hauptpalette ----------------------------------------------------------------------


def lum(c):
    return c[..., 0] * 0.299 + c[..., 1] * 0.587 + c[..., 2] * 0.114


def to_ramp(img, ramp, boost=1.0, add=0.0):
    """Jeden Pixel auf die Stufe der Palettenreihe mit der naechsten Helligkeit legen (harte Stufen)."""
    y = np.clip(lum(img) * boost + add, 0, 255)
    idx = np.abs(y[..., None] - lum(ramp)[None, None, :]).argmin(-1)
    return ramp[idx]


def paint_bone(U, V_):
    """Knochenzacken: Fuss dunkel (Stufe 4), Mitte Stufe 6, Spitze Stufe 7, feine Rillen laengs."""
    h, w = U.shape
    ridge = ((np.floor(U * 10) % 2) == 0)
    out = np.where(V_[..., None] < 0.3, HAUT[4], np.where(V_[..., None] < 0.7, HAUT[6], HAUT[7]))
    return np.where(ridge[..., None] & (V_[..., None] > 0.15), np.where(V_[..., None] < 0.7, HAUT[5], HAUT[6]), out)


def pale_atlas(rects):
    """Gleicher Malvorgang wie ghoul.py (gleiche Maler), dann je Feld auf die Palette: Haut -> Haut/Knochen um zwei
    Stufen heller, Lumpen -> Stoff (Helligkeit gleich), Knochenzacken direkt aus der Reihe."""
    painters = dict(ns["PAINTERS"])
    painters["bone"] = paint_bone
    paint = 4
    atlas = np.zeros((TEX * paint, TEX * paint, 3))
    for name, (x0, y0, x1, y1) in rects.items():
        ph, pw = (y1 - y0) * paint, (x1 - x0) * paint
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        atlas[y0 * paint:y1 * paint, x0 * paint:x1 * paint] = painters[name](U, V_)
    small = atlas.reshape(TEX, paint, TEX, paint, 3).mean(axis=(1, 3))
    out = np.zeros_like(small)
    for name, (x0, y0, x1, y1) in rects.items():
        sub = small[y0:y1, x0:x1]
        if name == "rags":
            out[y0:y1, x0:x1] = to_ramp(sub, STOFF)
        elif name == "bone":
            out[y0:y1, x0:x1] = sub
        else:
            out[y0:y1, x0:x1] = to_ramp(sub, HAUT, boost=1.30, add=8)
    return out


# --- Blatt-Werkzeuge ---------------------------------------------------------------------------------------------------

def text(img, s, x, y, size=30, color=(235, 235, 235), bold=False):
    f = blf.load("C:/Windows/Fonts/" + ("arialbd.ttf" if bold else "arial.ttf"))
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    W, H = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((W, H))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "ghul_stark_txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(H, W, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + H), min(ww, x + W)
    m = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(m, np.array(color, float), img[y:ye, x:xe])


def blit(sheet, tile, x, y):
    sheet[y:y + tile.shape[0], x:x + tile.shape[1]] = tile


def nearest(img, f):
    return np.repeat(np.repeat(img, f, axis=0), f, axis=1)


def extent(objs):
    lo, hi = np.full(3, 1e9), np.full(3, -1e9)
    for o in objs:
        for c in o.bound_box:
            g = to_game(o.matrix_world @ V(*c))
            lo, hi = np.minimum(lo, (g.x, g.y, g.z)), np.maximum(hi, (g.x, g.y, g.z))
    return lo, hi


def render(visible, cam, loc, target, fov, size, mode):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim(cam, loc, target)
    return compose(render_passes("gs", visible), mode)[::-1]


def place(objs, angles, pos=(0, 0, 0)):
    prefix = objs[0].name.split("_")[0] + "_"
    pose(objs, {prefix + k: a for k, a in angles.items()}, root=Matrix.Translation(G(pos)))
    for o in objs:
        write_light(o)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    for d in ("modelle", "texturen"):
        os.makedirs(os.path.join(OUT, d), exist_ok=True)

    # 1. Texturen
    base_atlas = ns["paint_atlas"](RECTS0, ns["PAINTERS"], TEX, colors=32)
    im = bpy.data.images.load(os.path.join(ROOT, "assets", "models", "ghoul.png"))
    im.colorspace_settings.name = "Non-Color"
    heute_png = np.array(im.pixels[:]).reshape(TEX, TEX, 4)[..., :3] * 255
    bpy.data.images.remove(im)
    same = bool(np.abs(heute_png - base_atlas).max() < 1.0)
    atlas = {"H": base_atlas, "A": pale_atlas(RECTS0), "B": pale_atlas(RECTS0), "C": pale_atlas(RECTS_C)}
    images = {v: save_image("atlas_" + v, atlas[v], os.path.join(OUT, "texturen", f"ghul_{v}.png")) for v in VARIANTS}
    eye_img = save_image("eye", np.full((4, 4, 3), EYE_RED), os.path.join(OUT, "texturen", "augen_nur_bild.png"))

    # 2. Modelle (glb, wie im Spiel), Dreiecke und Masse
    stats = {}
    for v in VARIANTS:
        coll = bpy.data.collections.new("exp_" + v)
        bpy.context.scene.collection.children.link(coll)
        mat = export_material("gs_" + v, images[v])
        objs = [p.build(coll, mat) for p in build_parts(v)]
        pose(objs, {})
        export_glb(objs, os.path.join(OUT, "modelle", f"ghul_{v}.glb"))
        lo, hi = extent(objs)
        stats[v] = ([len(o.data.polygons) for o in objs], lo, hi)
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)
    with open(os.path.join(OUT, "masse.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write(f"Textur heute == assets/models/ghoul.png: {same}\n")
        for v in VARIANTS:
            t, lo, hi = stats[v]
            f.write(f"{v}: Dreiecke {sum(t)}, Faktor {SCALE[v]}, x {lo[0]:.3f}..{hi[0]:.3f}, y {lo[1]:.3f}..{hi[1]:.3f}, "
                    f"z {lo[2]:.3f}..{hi[2]:.3f}; je Teil {dict(zip(['torso','head','jaw','upper_arm_px','forearm_px','upper_arm_nx','forearm_nx','thigh_px','shin_px','thigh_nx','shin_nx'], t))}\n")

    # 3. Szenen zum Rendern: je Fassung ein Satz Objekte mit Durchgangs-Material (Augen nur fuer das Bild)
    pm = {v: pass_material("pm_" + v, images[v]) for v in VARIANTS}
    pm_eye = pass_material("pm_eye", eye_img)
    sets, eyes = {}, {}
    for v in VARIANTS:
        coll = bpy.data.collections.new("r_" + v)
        bpy.context.scene.collection.children.link(coll)
        built = [p.build(coll, pm_eye if "eye" in p.name else pm[v]) for p in build_parts(v, with_eyes=True, prefix=v + "_")]
        sets[v] = [o for o in built if "eye" not in o.name]
        eyes[v] = [o for o in built if "eye" in o.name]
    cam = camera()
    stone = stone_textures(tempfile.mkdtemp(prefix="gs_stein_"))
    room = build_room(stone)
    REST, WIND = {}, dict(ns["WIND_UP"])

    # 3a. Ansichten je Fassung (ohne Nebel): vorn, Seite, hinten, schraeg oben
    dirs = (("vorn", V(0, 0.15, 1)), ("Seite", V(1, 0.15, 0)), ("hinten", V(0, 0.15, -1)), ("schräg oben", V(0.6, 0.9, 0.7)))
    for v in VARIANTS:
        place(sets[v], REST)
        c = V(0, 0.55, 0.1)
        sh = np.zeros((40 + 440, 4 * 440, 3)) + FOG_COLOR
        text(sh, f"{v}  {LABEL[v]} - vorn, Seite, hinten, schräg oben (ohne Nebel)", 8, 4, 26, bold=True)
        for k, (nm, d) in enumerate(dirs):
            blit(sh, render(sets[v], cam, G(c + d.normalized() * 4.6), G(c), 30, (440, 440), "ansicht"), k * 440, 40)
            text(sh, nm, k * 440 + 6, 44, 26)
        save_png(sh[::-1], os.path.join(OUT, f"ansichten-{v}.png"))

    # 3b. Spielbilder 854 x 480, 70 Grad, Augen 1,6 m, Nebel und Koernung, im Gang
    def game(v, pos, cam_loc, tgt, angles, show_eyes=False):
        place(sets[v], angles, pos)
        vis = list(sets[v]) + room
        if show_eyes:
            place(eyes[v] + sets[v], angles, pos)
            vis += eyes[v]
        return render(vis, cam, G(cam_loc), G(tgt), 70, (854, 480), "spiel")

    tiles = {}
    for v in VARIANTS:
        front = game(v, (0, 0, 0), V(0, 1.6, 2.3), V(0, 0.6, 0), REST)
        obl = game(v, (0, 0, 0), V(1.5, 1.6, 1.8), V(0, 0.6, 0), REST)
        wind = game(v, (0, 0, 0), V(0, 1.6, 2.6), V(0, 0.7, 0), WIND, show_eyes=True)
        far = game(v, (0, 0, 0), V(0, 1.6, 10.0), V(0, 1.6, 0), REST)
        near4 = game(v, (0, 0, 0), V(0, 1.6, 4.0), V(0, 1.6, 0), REST)
        for nm, img in (("vorn-nah", front), ("schraeg-nah", obl), ("ausholen-nah", wind), ("10m", far), ("4m", near4)):
            tiles[(v, nm)] = img
            save_png(img[::-1], os.path.join(OUT, f"spiel-{v}-{nm}.png"))
    # alle vier nebeneinander im gleichen Bild
    xs = {"H": -1.8, "A": -0.6, "B": 0.6, "C": 1.8}
    lineups = {}
    for dist in (4.0, 10.0):
        vis = list(room)
        for v in VARIANTS:
            place(sets[v], REST, (xs[v], 0, 0))
            vis += sets[v]
        lineups[dist] = render(vis, cam, G(V(0, 1.6, dist)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
        save_png(lineups[dist][::-1], os.path.join(OUT, f"spiel-alle-{int(dist)}m.png"))

    # 4. Blatt
    CW, GAP, M = 430, 16, 24
    W = M * 2 + 4 * CW + 3 * GAP
    ROWH = 480 + 44
    far_h = 280 + 44
    lineup_h = 520 + 44 + 320 + 44
    Hh = 150 + 3 * ROWH + far_h + lineup_h + 330
    sheet = np.zeros((Hh, W, 3)) + FOG_COLOR
    text(sheet, "SILVER & ASH - stärkerer Ghul (Issue 333): heute und drei Fassungen", M, 14, 42, bold=True)
    text(sheet, "Alle Bilder im Spiel-Look (Gang, Laterne, Nebel, Körnung). Augen rot nur in der Ankündigung (Zeile 3), wie im Spiel per Code.", M, 70, 26)
    for k, v in enumerate(VARIANTS):
        x0 = M + k * (CW + GAP)
        t, lo, hi = stats[v]
        text(sheet, LABEL[v], x0, 106, 34, GOLD if v != "H" else (235, 235, 235), True)
        text(sheet, f"{sum(t)} Dreiecke, {hi[1]:.2f} m hoch, {hi[0] - lo[0]:.2f} m breit", x0, 142, 24)
    y = 190
    for nm, title, rows in (("vorn-nah", "von vorn, 2,3 m", (0, 480)), ("schraeg-nah", "schräg von vorn oben, 2,3 m", (0, 480)),
                            ("ausholen-nah", "Ankündigung (Ausholen) von vorn, 2,6 m", (0, 480))):
        text(sheet, title, M, y, 26, (170, 160, 180), True)
        for k, v in enumerate(VARIANTS):
            x0 = M + k * (CW + GAP)
            blit(sheet, tiles[(v, nm)][:, 214:644], x0, y + 40)
        y += ROWH
    text(sheet, "aus 10 m im Dunkel (Ausschnitt 4-fach, ohne Glättung): jede Fassung allein im Gang", M, y, 26, (170, 160, 180), True)
    for k, v in enumerate(VARIANTS):
        x0 = M + k * (CW + GAP)
        crop = tiles[(v, "10m")][248:318, 427 - 53:427 + 54]
        blit(sheet, nearest(crop, 4)[:, :CW], x0, y + 40)
    y += far_h
    text(sheet, "alle vier im selben Bild aus 10 m (von links: heute, A, B, C; Ausschnitt 4-fach)", M, y, 26, (170, 160, 180), True)
    blit(sheet, nearest(lineups[10.0][200:330, 250:604], 4), M, y + 40)
    y += 564
    text(sheet, "dasselbe aus 4 m (Ausschnitt 2-fach)", M, y, 26, (170, 160, 180), True)
    blit(sheet, nearest(lineups[4.0][200:360, 100:754], 2), M, y + 40)
    y += 364
    save_png(sheet[::-1], os.path.join(OUT, "blatt.png"))
    print("FERTIG", {v: sum(stats[v][0]) for v in VARIANTS}, "heute==assets:", same)


main()
