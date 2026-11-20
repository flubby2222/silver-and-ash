"""Teleporter fuer das Spiel (Issue #436, Entwurf C Hornkristall): zwei Zustaende, gesperrt und freigeschaltet.

Alles aus einem Skript: Geometrie, Textur (nur Farben der Hauptpalette), zwei .glb nach assets/models/ und die
Vorschaubilder samt Blatt nach design/proben/grafik/teleporter-436/. Nichts heruntergeladen.

Aufruf (aus dem Repository-Wurzelverzeichnis, ohne Argumente):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/teleporter.py

Ausgabe: assets/models/teleporter_locked.glb|png und teleporter_unlocked.glb|png. Beide .glb haben dieselben zwei Teile:
"body" (Horner, Sockel, Runen) und "crystal" (schwebender Kristall). Im Zustand "unlocked" ist die Kristallfarbe in der
Textur selbstleuchtend gemalt; das Spiel soll das Teil "crystal" ohne Laterne zeichnen (Hinweis der Leitung im Issue).
Koordinaten: Spiel-Achsen (x seitlich, y oben, z vorn), Meter, Ursprung Mitte des Sockels am Boden.
"""

import json
import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import blf  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, G, FOG_COLOR, Part, rects_from_pixels, save_image, export_material, pass_material,  # noqa: E402
                 setup_render, camera, aim, render_passes, compose, save_png, write_light, stone_textures,
                 build_room, export_glb, to_game, MATS)

MODELS = os.path.join(ROOT, "assets", "models")
OUT = os.path.join(ROOT, "design", "proben", "grafik", "teleporter-436")
STATES = ("locked", "unlocked")
LABEL = {"locked": "GESPERRT", "unlocked": "FREIGESCHALTET"}

PAL = {r["name"]: np.array(r["farben"], float)
       for k in ("grund", "akzent")
       for r in json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json")))[k]}
ST, EI, SF, HK, WK = PAL["Stein"], PAL["Eisen"], PAL["Stoff"], PAL["Haut/Knochen"], PAL["Werkstein"]
GOLD = PAL["Messing/Gold"][5]

# --- Textur-Atlas 64 x 64, 16 x 16 je Feld, nur Palettenfarben -------------------------------------------------------

FIELDS = {"stone": (0, 0), "werk": (16, 0), "dark": (32, 0), "iron": (48, 0),
          "glow": (0, 16), "rune": (16, 16), "cloth": (32, 16), "skin": (48, 16)}
UV = rects_from_pixels({k: (x, y, x + 16, y + 16) for k, (x, y) in FIELDS.items()}, 64)


def blocks(tones, mortar):
    out = np.zeros((16, 16, 3))
    for y in range(16):
        row = y // 4
        off = (row % 2) * 4
        for x in range(16):
            bx = (x + off) % 8
            out[y, x] = mortar if (y % 4 == 0 or bx == 0) else tones[(row * 3 + ((x + off) // 8) * 5 + 1) % len(tones)]
    return out


def field_glow(state):
    out = np.zeros((16, 16, 3))
    for y in range(16):
        for x in range(16):
            if state == "unlocked":
                out[y, x] = SF[5] if y < 5 else SF[6] if y < 10 else SF[7]
                if x in (7, 8):
                    out[y, x] = EI[7]
            else:
                out[y, x] = EI[2] if (y // 4) % 2 == 0 else EI[1]
    return out


def make_atlas(state):
    a = np.zeros((64, 64, 3)) + ST[1]

    def put(name, arr):
        x, y = FIELDS[name]
        a[y:y + 16, x:x + 16] = arr

    put("stone", blocks([ST[4], ST[5], ST[6], ST[7]], ST[2]))
    put("werk", blocks([WK[0], WK[1], WK[2], WK[3]], ST[11]))
    dark = np.zeros((16, 16, 3)) + ST[1]
    dark[(np.indices((16, 16)).sum(0) % 5) == 0] = ST[2]
    put("dark", dark)
    iron = np.zeros((16, 16, 3)) + EI[2]
    iron[(np.arange(16) % 4 < 2)] = EI[3]
    iron[:, 0] = EI[4]
    put("iron", iron)
    put("glow", field_glow(state))
    put("rune", np.zeros((16, 16, 3)) + (EI[7] if state == "unlocked" else EI[3]))
    put("cloth", np.zeros((16, 16, 3)) + SF[3])
    put("skin", np.zeros((16, 16, 3)) + HK[3])
    return a


# --- Bausteine -----------------------------------------------------------------------------------------------------

def oct_pts(r, y, rot=math.radians(22.5)):
    return [(r * math.cos(rot + k * math.pi / 4), y, r * math.sin(rot + k * math.pi / 4)) for k in range(8)]


def top_dark(n):
    return "dark" if n.y > 0.5 else "stone"


def dais(p):
    """Zwei Stufen: unten 0,12 m hoch (Radius 0,95), oben bis 0,20 m (Radius 0,78). Unter der Schritthoehe (0,4 m)."""
    p.prism(oct_pts(0.95, 0.0), (0, 0.12, 0), top_dark)
    p.prism(oct_pts(0.78, 0.08), (0, 0.12, 0), top_dark)


def pyramid_top(p, cx, cz, y, half_side, tip_y, rect):
    """Spitze auf einem quadratischen Schaft (Seite 2*half_side); Fuss 2 cm im Schaft."""
    p.spike((cx, y - 0.02, cz), (cx, tip_y, cz), half_side * math.sqrt(2), rect)


def octa(p, c, r, h, rect):
    cx, cy, cz = c
    e = [V(cx + r, cy, cz), V(cx, cy, cz + r), V(cx - r, cy, cz), V(cx, cy, cz - r)]
    top, bot = V(cx, cy + h, cz), V(cx, cy - h, cz)
    polys = []
    for k in range(4):
        polys += [[e[k], e[(k + 1) % 4], top], [e[k], e[(k + 1) % 4], bot]]
    p.convex(polys, rect)


def rune_slits(p, xs, ys, z, w=0.05, h=0.24, depth=0.03):
    """Schmale Runenschlitze, 5 mm in der Flaeche versenkt, 'depth' - 0,005 m davor. z = Lage der Flaeche (+ vorn)."""
    for x in xs:
        for y in ys:
            for s in (1, -1):
                a, b = s * (abs(z) - 0.005), s * (abs(z) - 0.005 + depth)
                p.box((x - w / 2, y, min(a, b)), (x + w / 2, y + h, max(a, b)), "rune")



# Kristall: Unterkante 1,75 m, ueber der Augenhoehe (1,6 m) eines Spielers auf dem Sockel; Radius 0,17 m haelt Abstand zu den Hoernern.
CRYSTAL_Y, CRYSTAL_R, CRYSTAL_H = 2.1, 0.17, 0.35


def build_model():
    """Zwei nach innen geneigte Horner halten einen schwebenden Kristall ueber dem Sockel."""
    body = Part("body", (0, 0, 0), UV)
    dais(body)
    for s in (-1, 1):
        a, b = V(s * 0.58, 0.15, 0), V(s * 0.30, 2.15, 0)
        d = (b - a).normalized()
        n = V(d.y, -d.x, 0) * s * s
        w = 0.11
        pts = [a + n * w, a - n * w, b - n * w, b + n * w]
        body.prism([(p.x, p.y, -0.11) for p in pts], (0, 0, 0.22), "werk")
        body.spike(b - d * 0.02, b + d * 0.5, 0.11 * math.sqrt(2), "werk")
        for y0 in (0.6, 1.0, 1.4):
            x = s * (0.58 - 0.28 * (y0 + 0.12 - 0.15) / 2.0)
            for z in (1, -1):
                lo, hi = z * 0.1, z * 0.13
                body.box((x - 0.025, y0, min(lo, hi)), (x + 0.025, y0 + 0.24, max(lo, hi)), "rune")
    body.prism(oct_pts(0.3, 0.19), (0, 0.024, 0), "rune")         # Zeichen unter dem Kristall
    crystal = Part("crystal", (0, CRYSTAL_Y, 0), UV)
    octa(crystal, (0, CRYSTAL_Y, 0), CRYSTAL_R, CRYSTAL_H, "glow")
    return [body, crystal]


def put_in_scene(parts, mat, tag):
    coll = bpy.data.collections.new(tag)
    bpy.context.scene.collection.children.link(coll)
    objs = [p.build(coll, mat, smooth=False) for p in parts]
    for o in objs:
        HOME[o.name] = o.matrix_world.copy()
    return objs


HOME = {}


def place(objs, pos=(0, 0, 0)):
    for o in objs:
        o.matrix_world = Matrix.Translation(G(pos)) @ HOME[o.name]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def tri_count(objs):
    return [len(o.data.polygons) for o in objs]


def extent(objs):
    lo, hi = np.full(3, 1e9), np.full(3, -1e9)
    for o in objs:
        for c in o.bound_box:
            g = to_game(o.matrix_world @ V(*c))
            lo, hi = np.minimum(lo, (g.x, g.y, g.z)), np.maximum(hi, (g.x, g.y, g.z))
    return lo, hi


# --- Blatt-Werkzeuge -------------------------------------------------------------------------------------------------

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
    tmp = os.path.join(tempfile.gettempdir(), "teleporter_txt.png")
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


def render(visible, cam, loc, target, fov, size, mode):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim(cam, loc, target)
    return compose(render_passes("tp", visible), mode)[::-1]


def blit(sheet, tile, x, y):
    sheet[y:y + tile.shape[0], x:x + tile.shape[1]] = tile



def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    os.makedirs(OUT, exist_ok=True)
    images = {s: save_image("teleporter_" + s, make_atlas(s), os.path.join(MODELS, f"teleporter_{s}.png")) for s in STATES}
    for s in STATES:
        objs = put_in_scene(build_model(), export_material("tp_" + s, images[s]), "exp_" + s)
        export_glb(objs, os.path.join(MODELS, f"teleporter_{s}.glb"))
        lo, hi = extent(objs)
        print(s, "Dreiecke (body, crystal):", tri_count(objs), "Summe", sum(tri_count(objs)),
              "x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f" % (lo[0], hi[0], lo[1], hi[1], lo[2], hi[2]))
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)

    pm = {s: pass_material("pm_" + s, images[s]) for s in STATES}
    sets = {s: put_in_scene(build_model(), pm[s], "r_" + s) for s in STATES}
    for objs in sets.values():
        place(objs)
    cam = camera()
    room = build_room(stone_textures(tempfile.mkdtemp(prefix="tp_stein_")))
    c = V(0, 1.35, 0)
    dirs = (("vorn", V(0, 0.08, 1)), ("Seite", V(1, 0.08, 0)), ("hinten", V(0, 0.08, -1)), ("schräg oben", V(0.6, 0.9, 0.7)))
    names = {"vorn": "vorn", "Seite": "seite", "hinten": "hinten", "schräg oben": "schraeg-oben"}
    tile = 440
    rows = 2 * (tile + 46)
    sheet = np.zeros((60 + rows + 46 + 2 * 250, 4 * tile, 3)) + FOG_COLOR
    text(sheet, "SILVER & ASH - Teleporter, Spielmodell (Issue 436, Entwurf C Hornkristall)", 10, 8, 34, bold=True)
    for r, s in enumerate(STATES):
        y0 = 60 + r * (tile + 46)
        text(sheet, LABEL[s] + " - vorn, Seite, hinten, schräg oben (ohne Nebel)", 10, y0, 26, bold=True)
        for k, (nm, d) in enumerate(dirs):
            vis = list(sets[s])
            t = render(vis, cam, G(c + d.normalized() * 6.5), G(c), 30, (tile, tile), "ansicht")
            blit(sheet, t, k * tile, y0 + 44)
            save_png(t[::-1], os.path.join(OUT, f"modell-{s}-{names[nm]}.png"))
    y0 = 60 + rows
    text(sheet, "Im Spiel (854 x 480, 70 Grad, Augen 1,6 m, Nebel, Körnung): aus 4 m, aus 10 m; unten auf dem Sockel stehend", 10, y0, 26, bold=True)
    spots = (("locked", 4, (0, 1.6, 4), (0, 1.6, 0)), ("locked", 10, (0, 1.6, 10), (0, 1.6, 0)),
             ("unlocked", 4, (0, 1.6, 4), (0, 1.6, 0)), ("unlocked", 10, (0, 1.6, 10), (0, 1.6, 0)),
             ("locked", "auf", (0, 1.6, 0.5), (0, 1.6, -3)), ("unlocked", "auf", (0, 1.6, 0.5), (0, 1.6, -3)))
    for k, (s, d, eye, tgt) in enumerate(spots):
        img = render(list(sets[s]) + room, cam, G(V(*eye)), G(V(*tgt)), 70, (854, 480), "spiel")
        tag = f"{d}m" if d != "auf" else "auf-sockel"
        save_png(img[::-1], os.path.join(OUT, f"spiel-{s}-{tag}.png"))
        x, y = (k % 4) * tile if k < 4 else (k - 4) * 2 * tile, y0 + 44 + (0 if k < 4 else 250)
        small = img[::2, ::2]
        blit(sheet, small, x, y)
        text(sheet, f"{LABEL[s]}, " + (f"{d} m" if d != "auf" else "auf dem Sockel, Blick nach vorn"), x + 4, y + 2, 20)
    save_png(sheet[::-1], os.path.join(OUT, "blatt-modell.png"))


main()
